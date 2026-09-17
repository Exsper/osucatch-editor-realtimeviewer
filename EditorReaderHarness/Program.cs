using System.Diagnostics;
using System.Runtime.InteropServices;
using Editor_Reader;
using osucatch_editor_realtimeviewer;

namespace EditorReaderHarness;

// 与主工程 app.Default 对应的本地设置替身（harness 不引用主工程）
internal static class Defaults
{
    public static int FullRead_Interval = 20;
    public static int LowFreqRead_Interval = 100;
}

internal static class Program
{
    private static EditorReader _reader = new();
    private static Process? _osu;

    private static long Us(long a, long b) => (b - a) * 1_000_000 / Stopwatch.Frequency;

    private static void WriteLine(string s = "") => Console.WriteLine(s);

    private static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        WriteLine("EditorReader 诊断 harness —— 只读，不会写入 osu! 进程");
        WriteLine("命令: a=附加osu并绑定editor  i=阶段级耗时  f=全量路径构成  s=压力测试  m=监控(模拟生产tick)");
        WriteLine("      v=读取原语对比  g=散列聚合对比  r=失效指针缓存  d=dump  x=重试修复测试  q=退出");
        WriteLine();

        Attach();

        // 支持 `EditorReaderHarness.exe a d f` 这种一次性执行，便于在后台任务里驱动
        if (args.Length > 0)
        {
            foreach (string arg in args) RunCommand(arg);
            return;
        }

        while (true)
        {
            Console.Write("\n> ");
            string? line = Console.ReadLine();
            if (line == null) return;
            string cmd = line.Trim().ToLowerInvariant();
            if (cmd == "q") return;
            RunCommand(cmd);
        }
    }

    private static void RunCommand(string cmd)
    {
        switch (cmd)
        {
            case "a": Attach(); break;
            case "i": ItemizedTiming(); break;
            case "f": FullFetchBreakdown(); break;
            case "s": StressTest(); break;
            case "m": MonitorLoop(); break;
            case "v": ReadPrimitiveBench(); break;
            case "g": ScatterBench(); break;
            case "r": PointerCacheBench(); break;
            case "d": Dump(); break;
            case "o": ProbeOffsets(); break;
            case "p": PointerSizeRepro(); break;
            case "b": BatchReadBench(); break;
            case "z": FastPathBench(); break;
            case "e": EagerVsLazyLines(); break;
            case "k": ReadCostBreakdown(); break;
            case "x": RetryHealTest(); break;
            default: WriteLine("未知命令: " + cmd); break;
        }
    }

    // ------------------------------------------------------------------ 绑定

    private static bool Attach()
    {
        _osu = Mem.FindOsu();
        if (_osu == null)
        {
            WriteLine("!! 没找到 osu!.exe");
            return false;
        }

        // 用最小权限打开目标进程：osu! 常以管理员身份运行，.NET 的 Process.Handle
        // 默认申请权限过高会 "拒绝访问"，而 PROCESS_VM_READ 仍可读内存。
        if (_hProcess != IntPtr.Zero)
        {
            NativeClose(_hProcess);
            _hProcess = IntPtr.Zero;
        }

        _hProcess = Mem.OpenProcess(0x0010 /*VM_READ*/ | 0x1000 /*QUERY_LIMITED_INFORMATION*/, false, _osu.Id);
        if (_hProcess == IntPtr.Zero)
        {
            WriteLine("!! OpenProcess(VM_READ) 失败，err=" + Marshal.GetLastWin32Error());
            return false;
        }

        WriteLine($"osu! pid={_osu.Id} 目标位数={(Mem.IsTargetWow64FromHandle(_hProcess) ? "32 (WOW64)" : "64")}  标题={_osu.MainWindowTitle}");
        _reader.ForceHandle = _hProcess;
        _reader.SetProcess(_osu);
        try
        {
            _reader.SetEditor();
            _reader.SetHOM();
            _reader.ReadHOM();
            _reader.SetBeatmap();
            _reader.ReadBeatmap();
            _reader.SetControlPoints();
            // 顺便把物件列表指针也读出来（不读物件内容），后续 bench 才有数据
            try { _reader.SetObjects(); } catch (Exception ex) { WriteLine("SetObjects 失败（可能不在编辑器）: " + ex.Message); }
            WriteLine($"绑定成功: 控制点={_reader.numControlPoints} 物件={_reader.numObjects} 文件={_reader.Filename}");
            WriteLine($"路径={_reader.ContainingFolder}");
            WriteLine("(提示: 若此时不在编辑器里，请切到 osu! 编辑器后输入 a 重新绑定)");
            return true;
        }
        catch (Exception ex)
        {
            WriteLine("!! 绑定失败: " + ex.Message);
            return false;
        }
    }

    private static IntPtr _hProcess = IntPtr.Zero;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private static void NativeClose(IntPtr h) => CloseHandle(h);

    // ------------------------------------------------------------------ 1. 阶段级耗时

    private static void ItemizedTiming()
    {
        int n = _reader.numObjects;
        if (n <= 0) { WriteLine("没有物件数据，先绑定并确认在编辑器里"); return; }

        var (listHeader, dataArray, count, ptrSize) = _reader.GetObjectPointers();
        WriteLine($"物件数={count}, 目标指针宽度={ptrSize}, listHeader=0x{listHeader:X}, dataArray=0x{dataArray:X}");
        WriteLine("阶段取值：一次 FetchAll 中各阶段各执行 1 次；单项 = 单次调用耗时。\n");

        const int iter = 60;
        var hProcess = _hProcess;

        // 1) 列表头
        byte[] hdr = new byte[16];
        var r1 = Mem.Time(iter, () => Mem.Rpm(hProcess, listHeader, hdr, 16));
        WriteLine($"{"列表头读取(16B)",-34} 平均 {Mem.Fmt(r1.AvgUs),-9} p99 {Mem.Fmt(r1.P99Us),-9} /次 × 2 次/全量");

        // 2) 指针数组
        byte[] ptrs = new byte[4 * count + 64];
        var r2 = Mem.Time(iter, () => Mem.Rpm(hProcess, dataArray + 8, ptrs, 4 * count));
        WriteLine($"{"指针数组读取",-34} 平均 {Mem.Fmt(r2.AvgUs),-9} p99 {Mem.Fmt(r2.P99Us),-9} ({4 * count} B) × 1 次/全量");

        // 3) 逐物件结构体（每物件一次 336B 读取，生产实现即如此）
        byte[] one = new byte[336];
        long objPtr0 = Mem.ReadPointer(ptrs, 0, ptrSize);
        var r3 = Mem.Time(iter, () => Mem.Rpm(hProcess, (IntPtr)objPtr0, one, 336));
        WriteLine($"{"单个物件结构体读取(336B)",-34} 平均 {Mem.Fmt(r3.AvgUs),-9} p99 {Mem.Fmt(r3.P99Us),-9} × {count} 次/全量 → {Mem.Fmt(r3.AvgUs * count)}");

        // 4) 全物件结构体（同一缓冲循环，模拟 ReadObjects 的读取部分）
        byte[] big = new byte[336 * count];
        var r4 = Mem.Time(Math.Max(3, iter / 6), () =>
        {
            for (int i = 0; i < count; i++)
            {
                Mem.Rpm(hProcess, (IntPtr)Mem.ReadPointer(ptrs, 4 * i, ptrSize), big, 336 * 0 + 336);
            }
        });
        WriteLine($"{"全部物件结构体(单缓冲)",-34} 平均 {Mem.Fmt(r4.AvgUs),-9} p99 {Mem.Fmt(r4.P99Us),-9} × 1 次/全量");

        // 5) SampleFile 字符串（每次 2 次 RPM）
        Mem.Ensure(ref one, 336);
        // 取第一个物件，读 SampleFile 指针（偏移 84）
        Mem.Rpm(hProcess, (IntPtr)objPtr0, one, 336);
        long pStr = Mem.ReadPointer(one, 84, ptrSize);
        if (pStr != 0)
        {
            byte[] lenBuf = new byte[4];
            var r5 = Mem.Time(iter, () => Mem.Rpm(hProcess, (IntPtr)(pStr + 4), lenBuf, 4));
            WriteLine($"{"字符串长度读取(4B)",-34} 平均 {Mem.Fmt(r5.AvgUs),-9} p99 {Mem.Fmt(r5.P99Us),-9} × 物件数/全量");
        }

        WriteLine();
        WriteLine($"推算: 逐物件路径 syscall ≈ {count} (结构体) + {count}×slider(控制点列表) + {count}(字符串长度) + …");
        WriteLine($"      仅结构体一项就约 {Mem.Fmt(r3.AvgUs * count)}；真实全量请用 f 命令测量。");
    }

    // ------------------------------------------------------------------ 2. 全量路径构成

    private static void FullFetchBreakdown()
    {
        WriteLine("正在测量（每项取多次），请保持编辑器状态不变…");
        int warmup = 3;
        for (int i = 0; i < warmup; i++) _reader.FetchAll(false);

        const int iter = 25;
        // 单次全量读取的 RPM 次数/字节量
        Mem.ResetCounters();
        _reader.FetchAll(false);
        long rpmCalls = Mem.NumRpmCalls;
        long rpmBytes = Mem.NumBytesRead;

        double fetchAllMs = 0, readObjectsMs = 0;

        for (int i = 0; i < iter; i++)
        {
            long a = Stopwatch.GetTimestamp();
            _reader.FetchAll(false);
            long b = Stopwatch.GetTimestamp();
            fetchAllMs += Us(a, b) / 1000.0;
        }

        for (int i = 0; i < iter; i++)
        {
            _reader.SetObjects();
            long a = Stopwatch.GetTimestamp();
            _reader.ReadObjects(false);
            long b = Stopwatch.GetTimestamp();
            readObjectsMs += Us(a, b) / 1000.0;
        }

        // 只读指针数组的部分
        double setObjectsMs = 0;
        for (int i = 0; i < iter; i++)
        {
            long a = Stopwatch.GetTimestamp();
            _reader.SetObjects();
            long b = Stopwatch.GetTimestamp();
            setObjectsMs += Us(a, b) / 1000.0;
        }

        _reader.FetchAll(false);

        // 差异比较：主工程用 CheckDifference 逐物件比较字符串；这里用等价的逐字段比较估算同量级成本。
        _reader.FetchAll(false);
        var snapA = _reader.hitObjects.ToArray();
        var cpA = _reader.controlPoints.ToArray();
        _reader.FetchAll(false);
        var snapB = _reader.hitObjects.ToArray();
        var cpB = _reader.controlPoints.ToArray();

        double diffMs = 0;
        int diffCounted = 0;
        for (int i = 0; i < iter; i++)
        {
            long a = Stopwatch.GetTimestamp();
            diffCounted = 0;
            for (int k = 0; k < snapA.Length; k++) if (!HitObjectEquals(snapA[k], snapB[k])) diffCounted++;
            for (int k = 0; k < cpA.Length; k++) if (!ControlPointEquals(cpA[k], cpB[k])) diffCounted++;
            long b = Stopwatch.GetTimestamp();
            diffMs += Us(a, b) / 1000.0;
        }

        // 字符串行（主工程把这行缓存在 HitObjectLines，绘制时直接查表）
        double linesMs = 0;
        for (int i = 0; i < iter; i++)
        {
            long a = Stopwatch.GetTimestamp();
            for (int k = 0; k < snapA.Length; k++) snapA[k].ToString();
            long b = Stopwatch.GetTimestamp();
            linesMs += Us(a, b) / 1000.0;
        }

        // 校验成本（主工程在 BeatmapInfoCollection 构造函数里跑）
        double validateMs = 0;
        for (int i = 0; i < iter; i++)
        {
            long a = Stopwatch.GetTimestamp();
            Snapshot.Validate(_reader, out _, out _);
            long b = Stopwatch.GetTimestamp();
            validateMs += Us(a, b) / 1000.0;
        }

        WriteLine();
        WriteLine($"物件={_reader.numObjects} 控制点={_reader.numControlPoints}  迭代={iter} 次平均");
        WriteLine($"{"reader.FetchAll(false) 总计",-34} {fetchAllMs / iter,10:F2} ms   ← 真实全量读取（HOM/Beatmap/控制点/物件）");
        WriteLine($"{"  reader.SetObjects",-34} {setObjectsMs / iter,10:F2} ms   ← 列表头 + 指针数组（2 次 RPM）");
        WriteLine($"{"  reader.ReadObjects",-34} {readObjectsMs / iter,10:F2} ms   ← 逐物件读取（含 slider 子结构/字符串）");
        WriteLine($"{"逐物件 ToString() 建行",-34} {linesMs / iter,10:F2} ms   ← BeatmapInfoCollection 里的 HitObjectLines");
        WriteLine($"{"逐物件字段比较(≈CheckDiff)",-34} {diffMs / iter,10:F2} ms");
        WriteLine($"{"逐物件合法性校验",-34} {validateMs / iter,10:F2} ms");
        WriteLine();
        WriteLine($"RPM syscall 统计（单次 FetchAll）: {rpmCalls} 次 / {rpmBytes / 1024.0 / 1024.0:F2} MB");
        WriteLine($"物件平均读取字节: {(double)rpmBytes / Math.Max(1, _reader.numObjects):F0} B/物件");

        // 分配量：全量读取的 GC 压力（主工程按 20ms 跑，这里是每帧的垃圾量）
        int g0 = GC.CollectionCount(0);
        long before = GC.GetTotalAllocatedBytes(true);
        for (int i = 0; i < 20; i++) _reader.FetchAll(false);
        long after = GC.GetTotalAllocatedBytes(true);
        int g0After = GC.CollectionCount(0);
        double kbPerFetch = (after - before) / 20.0 / 1024;
        WriteLine($"每次 FetchAll 托管分配: {kbPerFetch:F1} KB   20 次触发 Gen0 GC {g0After - g0} 次");
        WriteLine($"（按 20ms 一次全量读取折算：{kbPerFetch * 50:F0} KB/s 垃圾）");

        // 现状（旧路径）vs 修复后（惰性字符串）的单次 tick 成本
        double oldPath = 0, newPath = 0;
        int n = _reader.hitObjects.Count;
        for (int i = 0; i < 10; i++)
        {
            long a = Stopwatch.GetTimestamp();
            _reader.FetchAll(false);
            var lines = _reader.hitObjects.Select((ho, idx) => (Line: ho.ToString(), ho.IsSelected, idx)).ToList();
            long b = Stopwatch.GetTimestamp();
            oldPath += Us(a, b) / 1000.0;
            GC.KeepAlive(lines);
        }

        for (int i = 0; i < 10; i++)
        {
            long a = Stopwatch.GetTimestamp();
            _reader.FetchAll(false);
            var lines = BuildSelectionRows(_reader.hitObjects);
            long b = Stopwatch.GetTimestamp();
            newPath += Us(a, b) / 1000.0;
            GC.KeepAlive(lines);
        }

        WriteLine();
        WriteLine($"单次 tick：旧路径（读取 + 逐条 ToString 建行） {oldPath / 10,8:F2} ms");
        WriteLine($"单次 tick：新路径（读取 + 只建 IsSelect 行）  {newPath / 10,8:F2} ms");
        double speedup = newPath > 0 ? oldPath / newPath : 0;
        WriteLine($"省下 {(oldPath - newPath) / 10:F2} ms/tick  →  吞吐提升 {speedup:F2}x");

        if (!Snapshot.TryValidateObjects(_reader, out string detail, out int rejects, out var reasons))
        {
            WriteLine($"\n!! 当前快照含非法物件 {rejects} 个，主工程会抛异常走退避。明细: {detail}");
            foreach (var kv in reasons) WriteLine($"     {Snapshot.ReasonName(kv.Key)}: {kv.Value}");
        }
    }

    private static bool HitObjectEquals(HitObject a, HitObject b)
    {
        return a.StartTime == b.StartTime && a.EndTime == b.EndTime && a.Type == b.Type && a.SoundType == b.SoundType &&
               a.SegmentCount == b.SegmentCount && a.X == b.X && a.Y == b.Y && a.BaseX == b.BaseX && a.BaseY == b.BaseY &&
               a.SpatialLength == b.SpatialLength && a.CurveType == b.CurveType && a.curveLength == b.curveLength &&
               a.SampleVolume == b.SampleVolume && a.SampleSet == b.SampleSet && a.SampleSetAdditions == b.SampleSetAdditions &&
               a.CustomSampleSet == b.CustomSampleSet && a.SampleFile == b.SampleFile && a.unifiedSoundAddition == b.unifiedSoundAddition;
    }

    private static bool ControlPointEquals(ControlPoint a, ControlPoint b)
    {
        return a.Offset == b.Offset && a.BeatLength == b.BeatLength && a.TimeSignature == b.TimeSignature &&
               a.SampleSet == b.SampleSet && a.CustomSamples == b.CustomSamples && a.Volume == b.Volume &&
               a.TimingChange == b.TimingChange && a.EffectFlags == b.EffectFlags;
    }

    /// <summary>复刻修复后的 SelectionLines 构建（只取 IsSelect，不拼字符串）。</summary>
    private static List<(bool IsSelect, int Index)> BuildSelectionRows(List<HitObject> hitObjects)
    {
        var rows = new List<(bool, int)>(hitObjects.Count);
        for (int i = 0; i < hitObjects.Count; i++) rows.Add((hitObjects[i].IsSelected, i));
        return rows;
    }

    // ------------------------------------------------------------------ 3. 压力测试（贴合"读取失败进退避"的痛点）

    private sealed class FailureTally
    {
        public long Attempts, Success;
        public long FailRpm;
        public long TotalRejectedObjects;
        public readonly Dictionary<int, long> RejectReasons = new();
        public readonly Dictionary<string, long> ReasonBuckets = new();
        public string? FirstFailure;
        public readonly List<double> Ms = new();

        public bool Bucket(string reason)
        {
            ReasonBuckets[reason] = ReasonBuckets.GetValueOrDefault(reason) + 1;
            FirstFailure ??= reason;
            return false;
        }
    }

    private static FailureTally RunStress(int milliseconds, int intervalMs, bool fetchFull)
    {
        var tally = new FailureTally();
        var sw = Stopwatch.StartNew();
        var sample = new Stopwatch();

        while (sw.ElapsedMilliseconds < milliseconds)
        {
            tally.Attempts++;
            sample.Restart();

            bool ok;
            try
            {
                _reader.FetchAll(fetchFull);
                string? why = Snapshot.Validate(_reader, out int rejects, out var reasons);
                ok = why == null;
                if (!ok)
                {
                    tally.Bucket(why);
                    foreach (var kv in reasons) tally.RejectReasons[kv.Key] = tally.RejectReasons.GetValueOrDefault(kv.Key) + kv.Value;
                    tally.TotalRejectedObjects += rejects;
                }
            }
            catch (Exception ex)
            {
                ok = false;
                tally.FailRpm++;
                tally.Bucket("RPM异常: " + ex.Message.Split('\n')[0]);
            }

            sample.Stop();
            tally.Ms.Add(sample.Elapsed.TotalMilliseconds);
            if (ok) tally.Success++;

            Thread.Sleep(intervalMs);
        }

        return tally;
    }

    private static void StressTest()
    {
        WriteLine("压力测试：模拟生产每 tick 的全量读取（FetchAll），统计失败率与失败原因。");
        WriteLine("三个阶段共约 35 秒，请按提示手动切换编辑器状态；每阶段结束会自动打印统计。");
        WriteLine("按回车立即开始…");
        Console.ReadLine();

        foreach ((string name, int ms) in new[] { ("安静（停在编辑器）", 10_000), ("编辑中（拖动/放置物件）", 15_000), ("播放中", 10_000) })
        {
            WriteLine($"\n--- {name} ({ms / 1000}s) ---");
            var t = RunStress(ms, Defaults.FullRead_Interval, false);
            PrintTally(t, ms);
        }
    }

    private static void PrintTally(FailureTally t, int ms)
    {
        double sum = 0, max = 0;
        foreach (double m in t.Ms) { sum += m; max = Math.Max(max, m); }
        double avg = t.Ms.Count > 0 ? sum / t.Ms.Count : 0;
        t.Ms.Sort();
        double p50 = t.Ms.Count > 0 ? t.Ms[t.Ms.Count / 2] : 0;
        double p99 = t.Ms.Count > 0 ? t.Ms[Math.Min(t.Ms.Count - 1, (int)(t.Ms.Count * 0.99))] : 0;

        long failures = t.Attempts - t.Success;
        WriteLine($"尝试={t.Attempts} ({t.Attempts * 1000.0 / ms:F1}/s)  失败={failures} ({100.0 * failures / Math.Max(1, t.Attempts):F1}%)  自愈判定用的失败物件总数={t.TotalRejectedObjects}");
        WriteLine($"FetchAll 耗时 avg={avg:F2}ms p50={p50:F2}ms p99={p99:F2}ms max={max:F2}ms");
        if (t.FailRpm > 0) WriteLine($"  RPM 抛异常: {t.FailRpm}");
        foreach (var kv in t.RejectReasons.OrderByDescending(k => k.Value))
        {
            WriteLine($"  非法物件({Snapshot.ReasonName(kv.Key)}): {kv.Value} 个");
        }
        foreach (var kv in t.ReasonBuckets.OrderByDescending(k => k.Value).Take(4))
        {
            WriteLine($"  失败原因: {kv.Key}  ×{kv.Value}");
        }
        if (t.FirstFailure != null) WriteLine($"  首次失败样本: {t.FirstFailure}");
    }

    // ------------------------------------------------------------------ 4. 监控（模拟生产 tick）

    private static void MonitorLoop()
    {
        WriteLine($"模拟生产 tick：interval={Defaults.FullRead_Interval}ms，FetchEditor 缓存 + 全量间隔判定。");
        WriteLine("输入持续时间（秒，回车=30）：");
        string? s = Console.ReadLine();
        int seconds = int.TryParse(s, out int v) && v > 0 ? v : 30;

        WriteLine("请现在手动切换编辑器状态（安静 / 拖动 / 播放 / 退出到选歌再回来），测试会自动统计…");

        long lastFullFetch = 0, lastEditorCheck = 0;
        bool editorOk = false;
        int failCount = 0;
        long ticks = 0, fullFetches = 0, nulls = 0, exceptions = 0, rejectObjects = 0;
        var reasons = new Dictionary<string, long>();
        var rejectReasons = new Dictionary<int, long>();
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < seconds * 1000L)
        {
            ticks++;
            try
            {
                if (!editorOk || sw.ElapsedMilliseconds - lastEditorCheck > Defaults.FullRead_Interval)
                {
                    lastEditorCheck = sw.ElapsedMilliseconds;
                    editorOk = _reader.EditorNeedsReload() == false;
                    if (!editorOk)
                    {
                        try { _reader.SetEditor(); editorOk = true; } catch { }
                    }
                }

                if (!editorOk) { Thread.Sleep(Defaults.FullRead_Interval); continue; }

                bool due = sw.ElapsedMilliseconds - lastFullFetch >= Defaults.FullRead_Interval;
                if (due)
                {
                    fullFetches++;
                    lastFullFetch = sw.ElapsedMilliseconds;
                    try
                    {
                        _reader.FetchAll(false);
                        string? why = Snapshot.Validate(_reader, out int rejects, out var rs);
                        if (why == null)
                        {
                            failCount = 0;
                        }
                        else
                        {
                            failCount++;
                            rejectObjects += rejects;
                            reasons[why] = reasons.GetValueOrDefault(why) + 1;
                            foreach (var kv in rs) rejectReasons[kv.Key] = rejectReasons.GetValueOrDefault(kv.Key) + kv.Value;
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions++;
                        failCount++;
                        reasons["RPM异常: " + ex.Message.Split('\n')[0]] = reasons.GetValueOrDefault("RPM异常: " + ex.Message.Split('\n')[0]) + 1;
                    }
                }
                else
                {
                    _reader.EditorTime();
                }
            }
            catch (Exception ex)
            {
                nulls++;
                reasons["外层异常: " + ex.GetType().Name] = reasons.GetValueOrDefault("外层异常: " + ex.GetType().Name) + 1;
            }

            Thread.Sleep(Defaults.FullRead_Interval);
        }

        WriteLine($"\ntick={ticks} 全量读取={fullFetches} 异常={exceptions} 其它错误={nulls} 失败物件总数={rejectObjects}");
        if (reasons.Count > 0)
        {
            WriteLine("失败原因分布:");
            foreach (var kv in reasons.OrderByDescending(k => k.Value).Take(12)) WriteLine($"  {kv.Value,6}  {kv.Key}");
        }
        else
        {
            WriteLine("没有出现读取失败。");
        }
        if (rejectReasons.Count > 0)
        {
            WriteLine("非法物件原因分布:");
            foreach (var kv in rejectReasons.OrderByDescending(k => k.Value)) WriteLine($"  {kv.Value,6}  {Snapshot.ReasonName(kv.Key)}");
        }
    }

    private static bool ValidateSnapshot(out string why)
    {
        why = Snapshot.Validate(_reader, out _, out _) ?? "";
        return why.Length == 0;
    }

    // ------------------------------------------------------------------ 5. 读取原语对比

    private static void ReadPrimitiveBench()
    {
        if (_osu == null) return;
        var h = _hProcess;
        var (_, dataArray, count, ptrSize) = _reader.GetObjectPointers();
        if (count <= 0) { WriteLine("先绑定 editor"); return; }

        byte[] ptrs = new byte[4 * count + 64];
        Mem.Rpm(h, dataArray + 8, ptrs, 4 * count);
        long target = Mem.ReadPointer(ptrs, 0, ptrSize);

        WriteLine($"目标地址 = 0x{target:X}（第一个物件）\n");
        WriteLine($"{"方法",-22}{"大小",-10}{"平均",-11}{"p99",-11}{"失败",-8}");
        foreach (int size in new[] { 4, 16, 336, 4096, 65536, 1048576 })
        {
            byte[] buf = new byte[size];
            int iter = size <= 4096 ? 20000 : 400;
            long failRpm = 0, failNt = 0;

            var a = Mem.Time(iter, () => { if (!Mem.Rpm(h, (IntPtr)target, buf, size)) failRpm++; });
            var b = Mem.Time(iter, () => { if (!Mem.NtRpm(h, (IntPtr)target, buf, size)) failNt++; });

            WriteLine($"{"ReadProcessMemory",-22}{size + "B",-10}{Mem.Fmt(a.AvgUs),-11}{Mem.Fmt(a.P99Us),-11}{failRpm,-8}");
            WriteLine($"{"NtReadVirtualMemory",-22}{size + "B",-10}{Mem.Fmt(b.AvgUs),-11}{Mem.Fmt(b.P99Us),-11}{failNt,-8}");
            WriteLine();
        }
    }

    // ------------------------------------------------------------------ 6. 散列聚合对比

    private static void ScatterBench()
    {
        if (_osu == null) return;
        var h = _hProcess;
        var (_, dataArray, count, ptrSize) = _reader.GetObjectPointers();
        if (count <= 0) { WriteLine("先绑定 editor"); return; }

        byte[] ptrs = new byte[4 * count + 64];
        Mem.Rpm(h, dataArray + 8, ptrs, 4 * count);
        var addrs = new long[count];
        for (int i = 0; i < count; i++) addrs[i] = Mem.ReadPointer(ptrs, 4 * i, ptrSize);

        // 地址连续性统计
        var sorted = (long[])addrs.Clone();
        Array.Sort(sorted);
        long[] gaps = new long[sorted.Length - 1];
        for (int i = 1; i < sorted.Length; i++) gaps[i - 1] = sorted[i] - sorted[i - 1];
        Array.Sort(gaps);
        WriteLine($"物件={count}  地址间隔: p25={gaps[gaps.Length / 4]} p50={gaps[gaps.Length / 2]} p90={gaps[(int)(gaps.Length * 0.9)]} max={gaps[^1]}");
        WriteLine($"物件总跨度={(sorted[^1] + 336 - sorted[0]) / 1024.0 / 1024.0:F2} MB\n");

        byte[] buf = new byte[8 * 1024 * 1024];
        var baseline = Mem.Time(20, () =>
        {
            for (int i = 0; i < count; i++) Mem.Rpm(h, (IntPtr)addrs[i], buf, 336);
        });
        WriteLine($"{"逐物件 1×RPM/个",-26}{"reads=" + count,-16}{"bytes=" + (count * 336 / 1024) + "KB",-18}avg={Mem.Fmt(baseline.AvgUs)}");

        foreach (long gap in new long[] { 0, 64, 512, 4096, 65536 })
        {
            long reads = 0, bytes = 0;
            var r = Mem.Time(10, () =>
            {
                var res = Mem.ScatterReadStructures(h, addrs, 336, gap, buf, 336);
                reads = res.Reads;
                bytes = res.Bytes;
            });
            WriteLine($"{"聚合 gap<=" + gap,-26}{"reads=" + reads,-16}{"bytes=" + (bytes / 1024) + "KB",-18}avg={Mem.Fmt(r.AvgUs)}  ({(double)count / Math.Max(1, reads):F1} 物件/read)");
        }

        WriteLine("\n注：聚合读取需要解析同一块缓冲里的多个物件，读到的字节数可能增加（gap 越大越浪费）。");
    }

    // ------------------------------------------------------------------ 7. 失效指针缓存

    private static void PointerCacheBench()
    {
        if (_osu == null) return;
        var h = _hProcess;
        var (_, dataArray, count, ptrSize) = _reader.GetObjectPointers();
        if (count <= 0) { WriteLine("先绑定 editor"); return; }

        byte[] a = new byte[4 * count + 64];
        byte[] b = new byte[4 * count + 64];

        WriteLine("快速路径成本对比（判断'物件列表是否变化'需要多少 syscall/时间）:\n");
        var r1 = Mem.Time(200, () => Mem.Rpm(h, dataArray + 8, a, 4 * count));
        WriteLine($"{"读整个指针数组",-34} 平均 {Mem.Fmt(r1.AvgUs),-10} ({4 * count} B, 1 次 RPM)");

        byte[] hdr = new byte[16];
        var r2 = Mem.Time(200, () => Mem.Rpm(h, dataArray, hdr, 16));
        WriteLine($"{"只读列表头(容量+长度)",-34} 平均 {Mem.Fmt(r2.AvgUs),-10} (16 B, 1 次 RPM)");

        // 缓存指针表：只比长度 + 抽样指针
        long cachedLen = count;
        var r3 = Mem.Time(200, () =>
        {
            Mem.Rpm(h, dataArray, hdr, 16);
            int len = BitConverter.ToInt32(hdr, 12);
            if (len != cachedLen) cachedLen = len;
        });
        WriteLine($"{"仅长度校验(判定是否变化)",-34} 平均 {Mem.Fmt(r3.AvgUs),-10} (16 B, 1 次 RPM)");

        // 长度 + 首尾指针校验
        var r4 = Mem.Time(200, () =>
        {
            Mem.Rpm(h, dataArray, hdr, 16);
            int len = BitConverter.ToInt32(hdr, 12);
            Mem.Rpm(h, dataArray + 8, a, 4);
            Mem.Rpm(h, (IntPtr)(dataArray.ToInt64() + 8 + 4L * (len - 1)), b, 4);
        });
        WriteLine($"{"长度+首尾指针校验",-34} 平均 {Mem.Fmt(r4.AvgUs),-10} (24 B, 3 次 RPM)");
    }

    // ------------------------------------------------------------------ 偏移探测

    /// <summary>
    /// 定位 HOM 内部的对象列表偏移：EditorReader 里写死的 72 在使用者这份 osu! 上可能已经变了。
    /// 判据：偏移处的指针指向一个 List 头（_items 非 0、_size 合理），且首元素指针指向的对象
    /// 结构里 StartTime/X/Y 落在合理范围。
    /// </summary>
    private static void ProbeOffsets()
    {
        if (_hProcess == IntPtr.Zero || _reader.HomAddress == IntPtr.Zero)
        {
            WriteLine("先执行 a 绑定 editor");
            return;
        }

        IntPtr hom = _reader.HomAddress;
        IntPtr pEditor = _reader.EditorAddress;
        WriteLine($"pEditor=0x{pEditor:X}  pHOM=0x{hom:X}  pBeatmap=0x{_reader.BeatmapAddress:X}  目标指针宽度=4\n");

        byte[] buf = new byte[512];
        Mem.Rpm(_hProcess, hom, buf, 512);

        // 编辑器对象 +112 是 Compose；HOM+72 / Compose+72 都是"列表"候选，逐个 dump 头部
        byte[] ebuf = new byte[256];
        Mem.Rpm(_hProcess, pEditor, ebuf, 256);
        uint compose = BitConverter.ToUInt32(ebuf, 112);
        WriteLine($"pCompose (pEditor+112) = 0x{compose:X8}");

        byte[] hdrDump = new byte[16];
        void DumpList(string label, long addr)
        {
            if (addr <= 0) { WriteLine($"  {label}: 空指针"); return; }
            if (!Mem.Rpm(_hProcess, (IntPtr)addr, hdrDump, 16)) { WriteLine($"  {label}: 0x{addr:X8} 读取失败"); return; }
            WriteLine($"  {label}: 0x{addr:X8}  [0]=0x{BitConverter.ToUInt32(hdrDump, 0):X8} [4]=0x{BitConverter.ToUInt32(hdrDump, 4):X8} " +
                      $"[8]=0x{BitConverter.ToUInt32(hdrDump, 8):X8} [12]={BitConverter.ToInt32(hdrDump, 12)}");
        }

        WriteLine("\n候选列表头 dump（[4]=_items 指针, [12]=_size）:");
        DumpList("HOM+56  (Bookmarks)", BitConverter.ToUInt32(buf, 56));
        DumpList("HOM+72  (主工程当作 Objects)", BitConverter.ToUInt32(buf, 72));
        DumpList("HOM+80  ", BitConverter.ToUInt32(buf, 80));
        DumpList("HOM+88  ", BitConverter.ToUInt32(buf, 88));
        DumpList("HOM+100 ", BitConverter.ToUInt32(buf, 100));
        DumpList("Compose+36", BitConverter.ToUInt32(ebuf, 112) + 36);

        byte[] cbuf = new byte[256];
        if (compose > 0x10000 && Mem.Rpm(_hProcess, (IntPtr)compose, cbuf, 256))
        {
            WriteLine("\nCompose 前 128 字节里像指针的值:");
            for (int off = 0; off < 128; off += 4)
            {
                uint v = BitConverter.ToUInt32(cbuf, off);
                if (v < 0x10000 || v > 0x7FFFFFFF) continue;
                WriteLine($"  Compose+{off,-4} = 0x{v:X8}");
            }
            DumpList("\nCompose+48 (Clipboard)", BitConverter.ToUInt32(cbuf, 48));
            DumpList("Compose+72 (Selected)", BitConverter.ToUInt32(cbuf, 72));
            DumpList("Compose+36", BitConverter.ToUInt32(cbuf, 36));
        }

        WriteLine("\nHOM 前 160 字节里像指针的值:");
        for (int off = 0; off < 160; off += 4)
        {
            uint v = BitConverter.ToUInt32(buf, off);
            if (v < 0x10000 || v > 0x7FFFFFFF) continue;
            WriteLine($"  +{off,-4} = 0x{v:X8}");
        }

        WriteLine("\n逐偏移尝试解析 List<HitObject>（寻找 偏移=72 之外的候选）:");
        byte[] hdr = new byte[16];
        byte[] probe = new byte[336];
        int bestOffset = -1;
        long bestScore = -1;

        for (int off = 0; off < 200; off += 4)
        {
            if (!Mem.Rpm(_hProcess, hom + off, hdr, 16)) continue;
            uint items = BitConverter.ToUInt32(hdr, 4);
            int size = BitConverter.ToInt32(hdr, 12);
            if (items < 0x10000 || items > 0x7FFFFFFF) continue;
            if (size <= 0 || size > 100000) continue;

            if (!Mem.Rpm(_hProcess, (IntPtr)items + 8, hdr, 16)) continue;
            uint first = BitConverter.ToUInt32(hdr, 0);
            if (first < 0x10000 || first > 0x7FFFFFFF) continue;

            if (!Mem.Rpm(_hProcess, (IntPtr)first, probe, 336)) continue;
            int startTime = BitConverter.ToInt32(probe, 16);
            int endTime = BitConverter.ToInt32(probe, 20);
            int type = BitConverter.ToInt32(probe, 24);
            float x = BitConverter.ToSingle(probe, 56);
            float y = BitConverter.ToSingle(probe, 60);
            float baseX = BitConverter.ToSingle(probe, 140);
            float baseY = BitConverter.ToSingle(probe, 144);

            bool plausible = type != 0 && (type & ~0xFF) == 0 && startTime >= -100000 && startTime < 10_000_000
                             && x >= -1000 && x <= 1000 && y >= -1000 && y <= 1000 && endTime >= startTime - 100000;
            long score = plausible ? 1000 - off : 0;

            WriteLine($"  偏移 {off,-3} items=0x{items:X8} size={size,-6} 首元素=0x{first:X8} " +
                      $"Start={startTime} End={endTime} Type={type} X={x:F1} Y={y:F1} Base=({baseX:F1},{baseY:F1}) {(plausible ? "  <== 可信" : "")}");

            if (score > bestScore)
            {
                bestScore = score;
                bestOffset = off;
            }
        }

        WriteLine($"\n结论: 主工程写死的偏移是 72；本机可信候选偏移 = {(bestScore > 0 ? bestOffset.ToString() : "无")}");
        if (bestScore <= 0)
        {
            WriteLine("（若物件的 Start/X/Y 全为 0，可能该谱面确实没有物件，或编辑器未加载完）");
        }
    }

    // ------------------------------------------------------------------ 指针宽度对照

    /// <summary>
    /// 对照实验：同样一批物件指针，按 4 字节 vs 8 字节解析，各读一遍并校验。
    /// 用于验证"主工程用 IntPtr.Size（viewer 自身位数）解析 32 位 osu! 指针"导致的读取失败。
    /// </summary>
    private static void PointerSizeRepro()
    {
        var (listHeader, dataArray, count, ptrSize) = _reader.GetObjectPointers();
        if (count <= 0) { WriteLine("没有物件，先 a 绑定"); return; }

        byte[] ptrs = new byte[4 * count + 64];
        Mem.Rpm(_hProcess, dataArray + 8, ptrs, 4 * count);

        const int TargetPtr = 4;   // 32 位 osu!：正确宽度
        const int WrongPtr = 8;    // viewer 汇编成 x64 时 IntPtr.Size 的值（错误）

        foreach ((string label, int size) in new[] { ("4 字节（正确，目标位数）", TargetPtr), ("8 字节（viewer 为 x64 时 IntPtr.Size）", WrongPtr) })
        {
            byte[] obj = new byte[336];
            int ok = 0, fail = 0, rejected = 0;
            string firstBad = "";
            for (int i = 0; i < count; i++)
            {
                long addr = size == 4 ? BitConverter.ToUInt32(ptrs, 4 * i) : (long)BitConverter.ToUInt64(ptrs, 4 * i);
                if (!Mem.Rpm(_hProcess, (IntPtr)addr, obj, 336)) { fail++; if (firstBad.Length == 0) firstBad = $"#{i} addr=0x{addr:X} 读取失败"; continue; }

                var ho = new HitObject
                {
                    X = BitConverter.ToSingle(obj, 56),
                    Y = BitConverter.ToSingle(obj, 60),
                    StartTime = BitConverter.ToInt32(obj, 16),
                    EndTime = BitConverter.ToInt32(obj, 20),
                    Type = BitConverter.ToInt32(obj, 24),
                    SegmentCount = BitConverter.ToInt32(obj, 32),
                    SampleVolume = BitConverter.ToInt32(obj, 108),
                    SampleSet = BitConverter.ToInt32(obj, 112),
                    SampleSetAdditions = BitConverter.ToInt32(obj, 116),
                };

                int r = Snapshot.RejectReason(ho);
                if (r >= 0) { rejected++; if (firstBad.Length == 0) firstBad = $"#{i} {Snapshot.ReasonName(r)} X={ho.X} Y={ho.Y} Type={ho.Type}"; }
                else ok++;
            }

            WriteLine($"{label,-34} 合法={ok,-5} 非法={rejected,-5} 读取失败={fail,-5}  {(firstBad.Length > 0 ? "首个问题: " + firstBad : "")}");
        }
    }

    /// <summary>
    /// 候选 B 完整对照：逐物件读取 vs 按"地址跨度聚合"批量读取。
    /// 两者解析出的字段必须完全一致（会做校验），再比耗时与 syscall 数。
    /// </summary>
    private static void BatchReadBench()
    {
        var (_, dataArray, count, _) = _reader.GetObjectPointers();
        if (count <= 0) { WriteLine("没有物件，先 a 绑定"); return; }
        if (count < 3) { WriteLine($"只有 {count} 个物件，不足以对比聚合读取，换张图"); return; }

        byte[] ptrs = new byte[4 * count + 64];
        Mem.Rpm(_hProcess, dataArray + 8, ptrs, 4 * count);
        var addrs = new long[count];
        for (int i = 0; i < count; i++) addrs[i] = BitConverter.ToUInt32(ptrs, 4 * i);

        const int StructSize = 336;
        var parsedBaseline = new HitObject[count];
        var parsedBatch = new HitObject[count];
        byte[] single = new byte[StructSize];

        // 基准：逐物件 1 次 RPM
        int baselineReads = 0;
        var tBase = Mem.Time(20, () =>
        {
            baselineReads = 0;
            for (int i = 0; i < count; i++)
            {
                if (Mem.Rpm(_hProcess, (IntPtr)addrs[i], single, StructSize)) baselineReads++;
                parsedBaseline[i] = Parse(single);
            }
        });

        WriteLine($"{"逐物件 1×RPM",-40} {count,7} reads  {count * StructSize / 1024.0,9:F0} KB  {Mem.Fmt(tBase.AvgUs),10}");
        WriteLine($"{"  （其中成功读取）",-40} {baselineReads,7}\n");

        foreach (int maxGap in new[] { 64, 1024, 4096, 16384, 65536 })
        {
            long reads = 0, bytes = 0;
            var t = Mem.Time(20, () =>
            {
                var res = BatchRead(addrs, StructSize, maxGap, parsedBatch);
                reads = res.Reads;
                bytes = res.Bytes;
            });

            int mismatch = 0;
            string firstMismatch = "";
            for (int i = 0; i < count; i++)
            {
                if (!SameObject(parsedBaseline[i], parsedBatch[i]))
                {
                    mismatch++;
                    if (firstMismatch.Length == 0)
                    {
                        firstMismatch = $"#{i} base(X={parsedBaseline[i].X},T={parsedBaseline[i].StartTime}) batch(X={parsedBatch[i].X},T={parsedBatch[i].StartTime})";
                    }
                }
            }

            WriteLine($"{"聚合读取 gap<=" + maxGap,-40} {reads,7} reads  {bytes / 1024.0,9:F0} KB  {Mem.Fmt(t.AvgUs),10}  " +
                      $"读放比 {(double)bytes / (count * StructSize):F1}x  字段不一致={mismatch}  {firstMismatch}");
        }

        WriteLine("\n注：'字段不一致' 应始终为 0，否则聚合方案不可用。");
    }

    private static HitObject Parse(byte[] b) => new()
    {
        SpatialLength = BitConverter.ToDouble(b, 8),
        StartTime = BitConverter.ToInt32(b, 16),
        EndTime = BitConverter.ToInt32(b, 20),
        Type = BitConverter.ToInt32(b, 24),
        SoundType = BitConverter.ToInt32(b, 28),
        SegmentCount = BitConverter.ToInt32(b, 32),
        X = BitConverter.ToSingle(b, 56),
        Y = BitConverter.ToSingle(b, 60),
        SampleVolume = BitConverter.ToInt32(b, 108),
        SampleSet = BitConverter.ToInt32(b, 112),
        SampleSetAdditions = BitConverter.ToInt32(b, 116),
        BaseX = BitConverter.ToSingle(b, 140),
        BaseY = BitConverter.ToSingle(b, 144),
    };

    private static bool SameObject(HitObject a, HitObject b)
        => a.StartTime == b.StartTime && a.EndTime == b.EndTime && a.Type == b.Type && a.SoundType == b.SoundType
           && a.SegmentCount == b.SegmentCount && a.X == b.X && a.Y == b.Y
           && a.BaseX == b.BaseX && a.BaseY == b.BaseY && a.SpatialLength == b.SpatialLength;

    /// <summary>
    /// 按地址跨度聚合读取：把排序后的物件地址按 maxGap 合并成若干"大块"，
    /// 每块一次 RPM 读进本地缓冲，再按偏移切片解析。返回实际 reads/bytes。
    /// </summary>
    private static (int Reads, long Bytes) BatchRead(long[] addrs, int structSize, int maxGap, HitObject[] output)
    {
        int n = addrs.Length;
        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        Array.Sort(order, (a, b) => addrs[a].CompareTo(addrs[b]));

        if (_batchBuffer.Length < structSize) _batchBuffer = new byte[structSize];

        int reads = 0;
        long bytes = 0;
        int k = 0;
        while (k < n)
        {
            long blockStart = addrs[order[k]];
            long blockEnd = blockStart + structSize;
            int j = k + 1;
            while (j < n)
            {
                long next = addrs[order[j]];
                if (next - blockEnd > maxGap) break;
                long end = next + structSize;
                if (end > blockEnd) blockEnd = end;
                j++;
            }

            long span = blockEnd - blockStart;
            if (span > 4L * 1024 * 1024)
            {
                // 太大就退回单物件读取，避免一次拉进巨量数据
                j = k + 1;
                span = structSize;
            }

            if (_batchBuffer.Length < span) _batchBuffer = new byte[span];
            if (Mem.Rpm(_hProcess, (IntPtr)blockStart, _batchBuffer, (int)span))
            {
                reads++;
                bytes += span;
                for (int t = k; t < j; t++)
                {
                    int off = (int)(addrs[order[t]] - blockStart);
                    var slice = new byte[structSize];
                    Buffer.BlockCopy(_batchBuffer, off, slice, 0, structSize);
                    output[order[t]] = Parse(slice);
                }
            }
            else
            {
                for (int t = k; t < j; t++) output[order[t]] = new HitObject();
            }

            k = j;
        }

        return (reads, bytes);
    }

    private static byte[] _batchBuffer = new byte[4096];

    // ------------------------------------------------------------------ 快速路径（指针表未变则跳过全量）

    /// <summary>
    /// 候选 C：把"物件指针表"缓存下来，每 tick 只做少量低成本读取判断表是否变化，
    /// 未变化则完全跳过全量重读，只刷新选中/时间。
    /// </summary>
    private static void FastPathBench()
    {
        var (_, dataArray, count, _) = _reader.GetObjectPointers();
        if (count <= 0) { WriteLine("没有物件，先 a 绑定"); return; }

        byte[] hdr = new byte[16];
        byte[] ptrs = new byte[4 * count + 64];
        byte[] one = new byte[4];

        WriteLine($"物件={count}\n");
        WriteLine("判断'物件列表是否变化'的几种成本：\n");

        var t1 = Mem.Time(500, () => Mem.Rpm(_hProcess, dataArray, hdr, 16));
        WriteLine($"{"A. 只读列表头(容量+长度)",-36} {Mem.Fmt(t1.AvgUs),10}   1 次 RPM / 16 B");

        int cachedLen = count;
        var t2 = Mem.Time(500, () =>
        {
            Mem.Rpm(_hProcess, dataArray, hdr, 16);
            if (BitConverter.ToInt32(hdr, 12) != cachedLen) cachedLen = BitConverter.ToInt32(hdr, 12);
        });
        WriteLine($"{"B. 长度比较（增删物件才会变）",-36} {Mem.Fmt(t2.AvgUs),10}   1 次 RPM / 16 B");

        uint cachedFirst = BitConverter.ToUInt32(ptrs, 0), cachedLast = BitConverter.ToUInt32(ptrs, 4 * (count - 1));
        Mem.Rpm(_hProcess, dataArray + 8, ptrs, 4 * count);
        cachedFirst = BitConverter.ToUInt32(ptrs, 0);
        cachedLast = BitConverter.ToUInt32(ptrs, 4 * (count - 1));

        var t3 = Mem.Time(500, () =>
        {
            Mem.Rpm(_hProcess, dataArray, hdr, 16);
            int len = BitConverter.ToInt32(hdr, 12);
            if (len == cachedLen)
            {
                Mem.Rpm(_hProcess, dataArray + 8, one, 4);
                if (BitConverter.ToUInt32(one, 0) == cachedFirst)
                {
                    Mem.Rpm(_hProcess, (IntPtr)(dataArray.ToInt64() + 8 + 4L * (len - 1)), one, 4);
                    if (BitConverter.ToUInt32(one, 0) == cachedLast) { /* 未变化 */ }
                }
            }
        });
        WriteLine($"{"C. 长度+首+尾指针比较",-36} {Mem.Fmt(t3.AvgUs),10}   3 次 RPM / 24 B");

        var t4 = Mem.Time(500, () => Mem.Rpm(_hProcess, dataArray + 8, ptrs, 4 * count));
        WriteLine($"{"D. 整张指针表逐一比较",-36} {Mem.Fmt(t4.AvgUs),10}   1 次 RPM / {4 * count} B");

        var t5 = Mem.Time(500, () => _reader.ReadObjects(false));
        WriteLine($"{"E. 全量重读物件（现状）",-36} {Mem.Fmt(t5.AvgUs),10}   ~{count} 次 RPM");

        WriteLine("\n结论参考：若 B/C 足够便宜，就能在'物件没变'的 tick 上只花 B/C 的成本；");
        WriteLine("只有真的增删/移动了物件，才需要 fall back 到 E。");
    }

    // ------------------------------------------------------------------ 字符串行的真实用途

    /// <summary>
    /// HitObjectLines（每条 ho.ToString()）只有"重建谱面"时才真正被消费；
    /// 绘制时用的是同一条数据的 IsSelect 标志。这里量一下"现状 vs 惰性"的差距。
    /// </summary>
    private static void EagerVsLazyLines()
    {
        if (_reader.numObjects <= 0) { WriteLine("没有物件，先 a 绑定"); return; }
        const int iter = 15;

        _reader.FetchAll(false);
        var objects = _reader.hitObjects;

        // 现状：每次全量读取都建字符串
        double eager = 0;
        for (int i = 0; i < iter; i++)
        {
            long a = Stopwatch.GetTimestamp();
            var lines = objects.Select((ho, idx) => new { Line = ho.ToString(), ho.IsSelected, idx }).ToList();
            long b = Stopwatch.GetTimestamp();
            eager += Us(a, b) / 1000.0;
            GC.KeepAlive(lines);
        }

        // 惰性：只用 IsSelect 标志，不建字符串
        double lazy = 0;
        for (int i = 0; i < iter; i++)
        {
            long a = Stopwatch.GetTimestamp();
            var lines = objects.Select((ho, idx) => new { Line = (string?)null, ho.IsSelected, idx }).ToList();
            long b = Stopwatch.GetTimestamp();
            lazy += Us(a, b) / 1000.0;
            GC.KeepAlive(lines);
        }

        WriteLine($"物件={objects.Count}  迭代={iter}");
        WriteLine($"{"现状：每条 ToString() 建行",-34} {eager / iter,8:F2} ms");
        WriteLine($"{"惰性：只取 IsSelect 标志",-34} {lazy / iter,8:F2} ms");
        WriteLine($"{"可省",-34} {(eager - lazy) / iter,8:F2} ms / 次全量读取");        WriteLine();
        WriteLine("（这一省法只有在'字符串确实只在重建谱面时被消费'时才成立——已确认：");
        WriteLine("  DrawingHelper 只用 SelectionLines[i].IsSelect，HitObjectLines 的字符串只在");
        WriteLine("  BuildNewBeatmapWithColorString 里被读取。）");
    }

    // ------------------------------------------------------------------ 剩余成本拆解

    /// <summary>
    /// ReadObjects 里还剩什么：syscall、共享缓冲、对象分配。
    /// 用同一批指针分别测"逐物件读进复用的单缓冲"和"读进新数组（现状）"。
    /// </summary>
    private static void ReadCostBreakdown()
    {
        var (_, dataArray, count, _) = _reader.GetObjectPointers();
        if (count <= 0) { WriteLine("没有物件，先 a 绑定"); return; }

        byte[] ptrs = new byte[4 * count + 64];
        Mem.Rpm(_hProcess, dataArray + 8, ptrs, 4 * count);
        var addrs = new int[count];
        for (int i = 0; i < count; i++) addrs[i] = (int)BitConverter.ToUInt32(ptrs, 4 * i);

        const int StructSize = 336;
        byte[] shared = new byte[StructSize];

        WriteLine($"物件={count}\n");

        var t1 = Mem.Time(15, () =>
        {
            for (int i = 0; i < count; i++) Mem.Rpm(_hProcess, (IntPtr)addrs[i], shared, StructSize);
        });
        WriteLine($"{"逐物件读进复用缓冲",-32} {Mem.Fmt(t1.AvgUs),10}   {count} 次 RPM，0 次 336B 分配");

        var t2 = Mem.Time(15, () =>
        {
            for (int i = 0; i < count; i++)
            {
                byte[] fresh = new byte[StructSize];
                Mem.Rpm(_hProcess, (IntPtr)addrs[i], fresh, StructSize);
                GC.KeepAlive(fresh);
            }
        });
        WriteLine($"{"逐物件读进新数组（现状）",-32} {Mem.Fmt(t2.AvgUs),10}   {count} 次 RPM，{count} 次 336B 分配");

        var t3 = Mem.Time(15, () =>
        {
            for (int i = 0; i < count; i++)
            {
                _ = new HitObject { X = 1, Y = 1, Type = 1 };
            }
        });
        WriteLine($"{"只 new HitObject（不读内存）",-32} {Mem.Fmt(t3.AvgUs),10}   {count} 次对象分配");

        var t4 = Mem.Time(15, () => _reader.ReadObjects(false));
        WriteLine($"{"reader.ReadObjects（现状全量）",-32} {Mem.Fmt(t4.AvgUs),10}   RPM + 分配 + DeStack");

        var t5 = Mem.Time(15, () => _reader.EditorTime());
        WriteLine($"{"reader.EditorTime（高频路径）",-32} {Mem.Fmt(t5.AvgUs),10}   1 次 RPM");
    }

    // ------------------------------------------------------------------ 8. 重试修复测试

    private static void RetryHealTest()
    {
        WriteLine("重试修复测试：模拟'读到非法快照后立刻重读'，看能否自愈以及重读代价。");
        WriteLine("请现在手动连续编辑物件（拖动/放置/删除）约 20 秒，测完按回车继续：");
        Console.ReadLine();

        long failures = 0, healed = 0, attempts = 0;
        var sw = Stopwatch.StartNew();
        var reasons = new Dictionary<string, long>();

        while (sw.ElapsedMilliseconds < 20_000)
        {
            attempts++;
            string why;
            try
            {
                _reader.FetchAll(false);
                if (ValidateSnapshot(out why)) { Thread.Sleep(Defaults.FullRead_Interval); continue; }
            }
            catch (Exception ex)
            {
                why = "RPM异常: " + ex.Message.Split('\n')[0];
            }

            failures++;
            reasons[why] = reasons.GetValueOrDefault(why) + 1;

            int retries = 1;
            for (; retries <= 3; retries++)
            {
                try
                {
                    _reader.FetchAll(false);
                    if (ValidateSnapshot(out _)) { healed++; break; }
                }
                catch { }
            }

            if (retries <= 3)
            {
                WriteLine($"  失败→第 {retries} 次重读成功  ({why})");
            }
            Thread.Sleep(Defaults.FullRead_Interval);
        }

        WriteLine($"\n尝试={attempts} 失败={failures} 重读后自愈={healed} ({(failures > 0 ? 100.0 * healed / failures : 0):F1}%)");
        foreach (var kv in reasons.OrderByDescending(k => k.Value).Take(10)) WriteLine($"  {kv.Value,5}  {kv.Key}");
    }

    // ------------------------------------------------------------------ 9. dump

    private static void Dump()
    {
        {
            // 诊断：核对 ReadHOM 实际读到的原始字节与主工程解析出的指针
            byte[] raw = new byte[96];
            Mem.Rpm(_hProcess, _reader.HomAddress, raw, 96);
            WriteLine($"pHOM=0x{_reader.HomAddress:X}  原始 +56=0x{BitConverter.ToUInt32(raw, 56):X8}  +72=0x{BitConverter.ToUInt32(raw, 72):X8}  +112(pCompose?)=0x{BitConverter.ToUInt32(raw, 112 < 96 ? 88 : 0):X8}");
            string hex = string.Join(" ", Enumerable.Range(0, 96 / 4).Select(i => BitConverter.ToUInt32(raw, i * 4).ToString("X8")));
            WriteLine("原始 dword[0..23]: " + hex);

            byte[] h16 = new byte[16];
            IntPtr pol = new IntPtr(BitConverter.ToUInt32(raw, 72));
            bool okList = Mem.Rpm(_hProcess, pol, h16, 16);
            WriteLine($"直接解析 HOM+72 = 0x{pol:X8}: ok={okList} _items=0x{BitConverter.ToUInt32(h16, 4):X8} _size={BitConverter.ToInt32(h16, 12)}");

            Mem.Rpm(_hProcess, _reader.EditorAddress + 112, h16, 4);
            uint pCompose = BitConverter.ToUInt32(h16, 0);
            Mem.Rpm(_hProcess, (IntPtr)pCompose, h16, 16);
            WriteLine($"pEditor+112 = 0x{pCompose:X8}, Compose+72 = 0x{BitConverter.ToUInt32(h16, 0):X8} (需再解引用)");
            Mem.Rpm(_hProcess, (IntPtr)(h16.Length >= 16 ? BitConverter.ToUInt32(h16, 0) : 0), h16, 16);
        }

        var (listHeader, dataArray, count, ptrSize) = _reader.GetObjectPointers();
        {
            byte[] h16 = new byte[16];
            bool ok = Mem.Rpm(_hProcess, listHeader, h16, 16);
            WriteLine($"直接读 listHeader=0x{listHeader:X} ok={ok} [4]=0x{BitConverter.ToUInt32(h16, 4):X8} [12]={BitConverter.ToInt32(h16, 12)}");
            _reader.SetObjects();
            var after = _reader.GetObjectPointers();
            WriteLine($"调用 SetObjects 后: header=0x{after.ListHeader:X} data=0x{after.DataArray:X} count={after.Count}");
        }
        WriteLine($"指针宽度={ptrSize}");
        WriteLine($"物件列表: header=0x{listHeader:X} data=0x{dataArray:X} count={count}");
        WriteLine($"控制点={_reader.numControlPoints} 书签={_reader.numBookmarks} 选中={_reader.numSelected}");
        WriteLine($"文件={_reader.Filename}");
        WriteLine($"BPM 相关: SliderMultiplier={_reader.SliderMultiplier} TickRate={_reader.SliderTickRate} CS={_reader.CircleSize} AR={_reader.ApproachRate} OD={_reader.OverallDifficulty} HP={_reader.HPDrainRate}");
        try
        {
            WriteLine($"EditorTime={_reader.EditorTime()}  objectRadius={_reader.objectRadius} stackOffset={_reader.stackOffset}");
        }
        catch (Exception ex) { WriteLine("EditorTime 读取失败: " + ex.Message); }
        WriteLine($"Diag 拒绝原因计数=[{string.Join(", ", _reader.DiagObjectRejectReasons)}]");
        if (_reader.DiagLastRejectSample != null) WriteLine("Diag 样本: " + _reader.DiagLastRejectSample);
        WriteLine("日志计数=" + Log.Count);
    }

}
