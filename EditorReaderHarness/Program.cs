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
            case "t": TickLoopSim(); break;
            case "c": ListEditorCandidates(); break;
            case "u": DiagnoseStuck(); break;
            case "w": ReplayFetchAll(); break;
            case "y": PointerSignCheck(); break;
            case "j": DiagnoseSegmentRead(); break;
            case "n": SwitchStress(); break;
            case "q2": SnapshotConsistency(); break;
            case "L": LoadWindowStress(); break;
            case "T": ProbeReadThreshold(); break;
            case "M": ErrorMonitor(); break;
            case "C": CompareCandidateChecks(); break;
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
    /// tick 循环模拟：复刻 EditorReaderHelper.FetchWithCache 的判定链，
    /// 对比"修复前（标题判断放在间隔判断之前）"与"修复后"在同样时长里各做了多少次全量读取。
    /// 只测判定链与读取成本，不涉及绘制/转换。
    /// </summary>
    private static void TickLoopSim()
    {
        if (_reader.numObjects <= 0) { WriteLine("没有物件，先 a 绑定"); return; }

        const int TickIntervalMs = 20;   // Drawing_Interval
        const int TicksToSimulate = 400;

        WriteLine($"物件={_reader.numObjects}  虚拟 tick 间隔=20ms  模拟 {TicksToSimulate} 个 tick");
        WriteLine("（同一段操作里各配置下的全量读取次数）\n");        foreach (int fullInterval in new[] { 20, 100, 500, 2000 })
        foreach (bool titleCheckFirst in new[] { true, false })
        {
            // 先建立一份缓存
            _reader.FetchAll(false);
            object? cachedCollection = new object();
            string cachedTitle = "x.osu@0";
            long virtualNow = 0;                  // 虚拟时钟：只用于判定，避免被真实读取耗时掩盖结论
            long lastEditorCheck = 0;
            long lastFullFetch = 0;
            long ticks = 0, fullFetches = 0;
            double realFetchMs = 0;

            while (ticks < TicksToSimulate)
            {
                ticks++;
                // 每次 tick 之间至少隔一个 Drawing_Interval：虚拟时钟必须先前进再判定，
                // 否则 lastFullFetch 会一直等于当前虚拟时间，间隔判断永远成立。
                virtualNow += TickIntervalMs;

                // FetchEditor：每 20ms 做一次真实校验
                if (virtualNow - lastEditorCheck >= TickIntervalMs)
                {
                    lastEditorCheck = virtualNow;
                }

                // beatmap_title 由 FetchEditor() 在每次校验时刷新、并在**全量读取成功后才**被记进 cachedTitle。
                // 同一个谱面期间标题文本不变，但它的"最新来源"总是比 cachedTitle 新 —— 用校验时间来模拟：
                // 只要"校验过但还没读"的 tick 存在，两者就不等。
                string beatmapTitle = "x.osu@" + lastEditorCheck;

                bool due;
                if (titleCheckFirst)
                {
                    due = cachedCollection == null || cachedTitle != beatmapTitle;
                    if (!due) due = virtualNow - lastFullFetch >= fullInterval;
                }
                else
                {
                    due = cachedCollection == null || virtualNow - lastFullFetch >= fullInterval;
                    if (!due) due = cachedTitle != beatmapTitle;
                }

                if (due)
                {
                    fullFetches++;
                    lastFullFetch = virtualNow;
                    long a = Stopwatch.GetTimestamp();
                    _reader.FetchAll(false);
                    long b = Stopwatch.GetTimestamp();
                    realFetchMs += Us(a, b) / 1000.0;
                    cachedTitle = beatmapTitle;      // 全量读取成功后才记下标题
                }
                else
                {
                    _reader.EditorTime();
                }
            }

            string label = titleCheckFirst ? "修复前" : "修复后";
            WriteLine($"读取间隔={fullInterval,5}ms  {label}  tick={ticks,-6} 全量读取={fullFetches,-6} 读取总耗时={realFetchMs,9:F0} ms");
        }

        WriteLine("\n结论：同一个谱面期间 beatmap_title 每次校验都被刷新，而 cachedTitle 只有全量读取成功才更新，");
        WriteLine("于是'标题判断放在间隔判断之前'会让间隔设置完全失效；修复后间隔判断先返回，标题只在间隔到时才参与。");
        WriteLine("另外注意：默认 FullRead_Interval=20ms 与 Drawing_Interval 相同时，间隔本身每个 tick 都会到期，");
        WriteLine("所以默认配置下修好这一项并不会减少读取次数 —— 它的价值在于把 LowFreqRead_Interval / 放大后的");
        WriteLine("间隔设置重新变成有效参数（修复前无论设多大都等于每 tick 全量读）。");

        WriteLine("\n（虚拟时钟：两次 tick 固定间隔 20ms，读取间隔 50ms。同一段操作里的全量读取次数对比。）");

        WriteLine("\n（同一段操作里各配置下的全量读取次数）\n");
    }

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

    // ------------------------------------------------------------------ 编辑器候选枚举

    /// <summary>
    /// 枚举进程里所有"看起来像 editor 对象"的候选（签名 + IsPlausibleEditor 那套字段校验），
    /// 用来诊断"从同一谱面集切难度后永久卡住"：堆里会残留死副本，选错就会一直读失败。
    /// </summary>
    private static void ListEditorCandidates()
    {
        if (_hProcess == IntPtr.Zero) { WriteLine("先 a 绑定"); return; }

        // 与主工程 ScanForEditorAddress 里的 ToByteArray("230000001400000019000000…") 完全一致：
        // 50 字节，0x0C 在索引 32，0xEE 是通配符
        byte[] pattern = new byte[]
        {
            0x23,0,0,0, 0x14,0,0,0, 0x19,0,0,0,
            0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,
            0xEE,0xEE,0xEE,0xEE,
            0x0C,0,0,0,
            0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,
            0x00
        };
        WriteLine($"模式长度自检: {pattern.Length} 字节 (应为 50)");

        // 先验证模式本身：在"已知能读到数据的编辑器地址 +160"处应该能匹配
        {
            byte[] known = new byte[pattern.Length];
            if (Mem.Rpm(_hProcess, _reader.EditorAddress + 160, known, known.Length))
            {
                bool match = PatternMatch(known, pattern, 0);
                WriteLine($"模式自检：当前绑定编辑器 0x{_reader.EditorAddress:X} +160 处 {(match ? "匹配" : "不匹配")}");
                WriteLine("  实际字节: " + string.Join(" ", known.Select(b => b.ToString("X2"))));
                WriteLine("  模式字节: " + string.Join(" ", pattern.Select(b => b == 0xEE ? "??" : b.ToString("X2"))));
                for (int i = 0; i < pattern.Length; i++)
                {
                    if (pattern[i] != 0xEE && pattern[i] != known[i])
                    {
                        WriteLine($"  第 {i} 字节不符: 模式=0x{pattern[i]:X2} 实际=0x{known[i]:X2}");
                    }
                }

                WriteLine();
            }
            else
            {
                WriteLine("模式自检：读取当前编辑器失败\n");
            }
        }

        WriteLine("正在枚举内存区域并扫描 editor 签名（每个区域一次读取，稍等）…");
        var regions = Mem.Regions(_hProcess);
        WriteLine($"已提交且可读的区域数: {regions.Count}\n");

        var found = new List<(long Candidate, long RegionBase, long RegionSize, string Verdict)>();
        long totalRead = 0;
        var sw = Stopwatch.StartNew();

        foreach (var r in regions)
        {
            long size = r.RegionSize.ToInt64();
            if (size <= 0 || size > 256L * 1024 * 1024) continue;

            byte[] buffer = new byte[size];
            if (!Mem.Rpm(_hProcess, r.BaseAddress, buffer, (int)size)) continue;
            totalRead += size;

            for (int j = 0; j + pattern.Length <= buffer.Length; j += 4)
            {
                // 只用模式里"确定"的字节做快速筛除（0xEE 是通配符，不能拿来筛）
                if (buffer[j] != 0x23 || buffer[j + 1] != 0 || buffer[j + 2] != 0 || buffer[j + 3] != 0) continue;
                if (buffer[j + 4] != 0x14 || buffer[j + 8] != 0x19 || buffer[j + 32] != 0x0C || buffer[j + 49] != 0x00) continue;
                if (!PatternMatch(buffer, pattern, j)) continue;

                long candidate = r.BaseAddress.ToInt64() + j - 160;
                found.Add((candidate, r.BaseAddress.ToInt64(), size, ""));
            }

            if (sw.ElapsedMilliseconds > 60000) { WriteLine("扫描超时，提前结束"); break; }
        }

        WriteLine($"扫描完成: {sw.ElapsedMilliseconds} ms, 读取 {totalRead / 1024.0 / 1024.0:F0} MB, 命中 {found.Count} 个候选\n");

        byte[] probe16 = new byte[16];
        byte[] probe4 = new byte[4];

        foreach (var (candidate, regionBase, regionSize, _) in found)
        {
            string verdict = DescribeEditorCandidate(candidate, probe16, probe4);
            bool isCurrent = candidate == _reader.EditorAddress.ToInt64();
            WriteLine($"  0x{candidate:X8}  region=0x{regionBase:X8}({regionSize / 1024}KB)  {(isCurrent ? "[当前绑定] " : "")}{verdict}");
        }

        WriteLine($"\n各候选的 pEditor+28(HOM) 指向的对象列表情况：");
        foreach (var (candidate, _, _, _) in found)
        {
            DumpCandidateChain(candidate, probe16, probe4);
        }
    }

    private static bool PatternMatch(byte[] buffer, byte[] pattern, int offset)
    {
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] != 0xEE && pattern[i] != buffer[offset + i]) return false;
        }
        return true;
    }

    /// <summary>复刻 IsPlausibleEditor 的每一步，并把失败在哪一步说清楚。</summary>
    private static string DescribeEditorCandidate(long candidate, byte[] probe16, byte[] probe4)
    {
        IntPtr pE = (IntPtr)candidate;
        if (!Mem.Rpm(_hProcess, pE + 160, probe16, 16))
        {
            return "字段读取失败 (pE+160)";
        }

        int f0 = BitConverter.ToInt32(probe16, 0), f4 = BitConverter.ToInt32(probe16, 4), f8 = BitConverter.ToInt32(probe16, 8);
        if (f0 != 35 || f4 != 20 || f8 != 25) return $"状态字段不符 ({f0},{f4},{f8})";

        if (!Mem.Rpm(_hProcess, pE + 28, probe4, 4)) return "pE+28 读取失败";
        IntPtr pHom = (IntPtr)BitConverter.ToUInt32(probe4, 0);
        if (pHom == IntPtr.Zero) return "HOM 为空";

        if (!Mem.Rpm(_hProcess, pHom + 72, probe4, 4)) return "HOM+72 读取失败";
        IntPtr pObjectsList = (IntPtr)BitConverter.ToUInt32(probe4, 0);
        if (pObjectsList == IntPtr.Zero) return "物件列表为空";

        if (!Mem.Rpm(_hProcess, pObjectsList, probe16, 16)) return "物件列表头读取失败";
        IntPtr pObjectsArray = (IntPtr)BitConverter.ToUInt32(probe16, 4);
        int count = BitConverter.ToInt32(probe16, 12);
        if (pObjectsArray == IntPtr.Zero || count < 0 || count > 1000000) return $"物件表非法 (items=0x{pObjectsArray:X}, size={count})";

        return $"通过校验 items=0x{pObjectsArray:X} count={count}";
    }

    /// <summary>逐个候选看它整条指针链读到的是什么谱面（HOM+48 是 Beatmap）。</summary>
    private static void DumpCandidateChain(long candidate, byte[] probe16, byte[] probe4)
    {
        IntPtr pE = (IntPtr)candidate;
        byte[] buffer = new byte[256];

        if (!Mem.Rpm(_hProcess, pE + 28, probe4, 4)) { WriteLine($"  0x{candidate:X8}: HOM 读取失败"); return; }
        IntPtr pHom = (IntPtr)BitConverter.ToUInt32(probe4, 0);
        if (pHom == IntPtr.Zero) { WriteLine($"  0x{candidate:X8}: HOM 为空"); return; }

        if (!Mem.Rpm(_hProcess, pHom + 48, probe4, 4)) { WriteLine($"  0x{candidate:X8}: HOM+48 读取失败"); return; }
        IntPtr pBeatmap = (IntPtr)BitConverter.ToUInt32(probe4, 0);
        if (pBeatmap == IntPtr.Zero) { WriteLine($"  0x{candidate:X8}: Beatmap 为空"); return; }

        // 物件列表
        Mem.Rpm(_hProcess, pHom + 72, probe4, 4);
        IntPtr pObjL = (IntPtr)BitConverter.ToUInt32(probe4, 0);
        int objCount = -1;
        if (pObjL != IntPtr.Zero && Mem.Rpm(_hProcess, pObjL, probe16, 16)) objCount = BitConverter.ToInt32(probe16, 12);

        // 控制点列表
        int cpCount = -1;
        if (Mem.Rpm(_hProcess, pBeatmap, buffer, 192))
        {
            IntPtr pCpL = (IntPtr)BitConverter.ToUInt32(buffer, 176);
            if (pCpL != IntPtr.Zero && Mem.Rpm(_hProcess, pCpL, probe16, 16)) cpCount = BitConverter.ToInt32(probe16, 12);
        }

        // 文件名
        string filename = "?";
        if (Mem.Rpm(_hProcess, pBeatmap, buffer, 320))
        {
            filename = ReadStringSafe(BitConverter.ToUInt32(buffer, 144));
        }

        // 编辑器状态字段
        int v0 = -1, v4 = -1, v8 = -1;
        if (Mem.Rpm(_hProcess, pE + 160, probe16, 16))
        {
            v0 = BitConverter.ToInt32(probe16, 0); v4 = BitConverter.ToInt32(probe16, 4); v8 = BitConverter.ToInt32(probe16, 8);
        }

        bool isCurrent = candidate == _reader.EditorAddress.ToInt64();
        WriteLine($"  0x{candidate:X8} {(isCurrent ? "[当前绑定]" : "          ")} 物件={objCount,-6} 控制点={cpCount,-4} 状态=({v0},{v4},{v8}) 文件={filename}");
    }

    private static string ReadStringSafe(uint pString)
    {
        if (pString == 0) return "(null)";
        byte[] len = new byte[4];
        if (!Mem.Rpm(_hProcess, (IntPtr)(pString + 4), len, 4)) return "(读取失败)";
        int n = BitConverter.ToInt32(len, 0);
        if (n <= 0 || n > 512) return "(长度异常 " + n + ")";
        byte[] buf = new byte[2 * n];
        if (!Mem.Rpm(_hProcess, (IntPtr)(pString + 8), buf, 2 * n)) return "(内容读取失败)";
        return new string(System.Text.Encoding.Unicode.GetChars(buf));
    }

    // ------------------------------------------------------------------ 卡住状态诊断

    /// <summary>
    /// 复刻主工程"读取失败 → 退避 → 重绑"的那条判定链，逐步打印每一环的结果，
    /// 用来定位"切难度后永久卡住"到底卡在哪一步。
    /// </summary>
    private static void DiagnoseStuck()
    {
        if (_hProcess == IntPtr.Zero) { WriteLine("先 a 绑定"); return; }
        if (_osu == null) return;

        WriteLine($"窗口标题(当前)     : {_osu.MainWindowTitle}");
        WriteLine($"缓存编辑器地址     : 0x{_reader.EditorAddress:X}");
        WriteLine($"缓存 HOM 地址      : 0x{_reader.HomAddress:X}");
        WriteLine();

        byte[] b16 = new byte[16];
        byte[] b4 = new byte[4];
        IntPtr pE = _reader.EditorAddress;

        bool ok160 = Mem.Rpm(_hProcess, pE + 160, b16, 16);
        bool ok208 = Mem.Rpm(_hProcess, pE + 208, b4, 4);

        WriteLine($"RPM(pE+160,16)     : {ok160}  -> ({BitConverter.ToInt32(b16, 0)}, {BitConverter.ToInt32(b16, 4)}, {BitConverter.ToInt32(b16, 8)})");
        WriteLine($"                    期望 (35, 20, 25)");
        WriteLine($"RPM(pE+208,4)      : {ok208}  -> 字节 = {string.Join(" ", b4.Select(b => b.ToString("X2")))}");
        if (ok208) WriteLine($"                    BitConverter.ToBoolean(b,1) = {BitConverter.ToBoolean(b4, 1)}   （true 即判定为需要重载）");
        WriteLine();

        if (ok160 && ok208)
        {
            bool flag = BitConverter.ToBoolean(b4, 1);
            bool fields = BitConverter.ToInt32(b16, 0) == 35 && BitConverter.ToInt32(b16, 4) == 20 && BitConverter.ToInt32(b16, 8) == 25;
            WriteLine($"EditorNeedsReload 的判定: 标志位={flag} 字段匹配={fields} => {(flag || !fields ? "需要重载" : "不需要重载")}");
        }

        WriteLine();
        WriteLine("FetchAll 逐步走一遍（定位到底哪一步读不到）：");
        {
            bool ok;
            ok = Mem.Rpm(_hProcess, pE + 28, b4, 4);
            IntPtr pHomNow = ok ? (IntPtr)BitConverter.ToUInt32(b4, 0) : IntPtr.Zero;
            WriteLine($"  1) pE+28 -> HOM       : ok={ok}  HOM=0x{pHomNow:X}   （缓存里是 0x{_reader.HomAddress:X}）");

            ok = Mem.Rpm(_hProcess, pE + 112, b4, 4);
            IntPtr pCompose = ok ? (IntPtr)BitConverter.ToUInt32(b4, 0) : IntPtr.Zero;
            WriteLine($"  2) pE+112 -> Compose  : ok={ok}  Compose=0x{pCompose:X}");

            byte[] hom = new byte[256];
            ok = Mem.Rpm(_hProcess, pHomNow, hom, 256);
            WriteLine($"  3) HOM 头部 256B      : ok={ok}");
            if (ok)
            {
                WriteLine($"     HOM+48  Beatmap    = 0x{BitConverter.ToUInt32(hom, 48):X}   (缓存 0x{_reader.BeatmapAddress:X})");
                WriteLine($"     HOM+56  Bookmarks  = 0x{BitConverter.ToUInt32(hom, 56):X}");
                WriteLine($"     HOM+72  Objects    = 0x{BitConverter.ToUInt32(hom, 72):X}");
                WriteLine($"     objectRadius={BitConverter.ToSingle(hom, 24)} stackOffset={BitConverter.ToSingle(hom, 44)}");
            }

            IntPtr pObjL = ok ? (IntPtr)BitConverter.ToUInt32(hom, 72) : IntPtr.Zero;
            ok = pObjL != IntPtr.Zero && Mem.Rpm(_hProcess, pObjL, b16, 16);
            int count = ok ? BitConverter.ToInt32(b16, 12) : -1;
            IntPtr pArr = ok ? (IntPtr)BitConverter.ToUInt32(b16, 4) : IntPtr.Zero;
            WriteLine($"  4) HOM+72 -> 物件列表 : ok={ok}  list=0x{pObjL:X} items=0x{pArr:X} count={count}");

            ok = count > 0 && Mem.Rpm(_hProcess, pArr + 8, b4, 4);
            IntPtr first = ok ? (IntPtr)BitConverter.ToUInt32(b4, 0) : IntPtr.Zero;
            WriteLine($"  5) 首个物件指针       : ok={ok}  p0=0x{first:X}");

            byte[] obj = new byte[336];
            ok = first != IntPtr.Zero && Mem.Rpm(_hProcess, first, obj, 336);
            WriteLine($"  6) 读取首个物件 336B  : ok={ok}");
            if (ok)
            {
                WriteLine($"     Start={BitConverter.ToInt32(obj, 16)} Type={BitConverter.ToInt32(obj, 24)} X={BitConverter.ToSingle(obj, 56)} Y={BitConverter.ToSingle(obj, 60)}");
            }
        }

        WriteLine();
        WriteLine("尝试读取一次全量数据（复刻 FetchAll）：");
        try
        {
            Mem.ResetCounters();
            long a = Stopwatch.GetTimestamp();
            _reader.FetchAll(false);
            long b = Stopwatch.GetTimestamp();
            WriteLine($"  FetchAll 成功: 物件={_reader.numObjects} 控制点={_reader.numControlPoints} 耗时={(b - a) * 1000.0 / Stopwatch.Frequency:F2} ms");
            string? why = Snapshot.Validate(_reader, out int rejects, out var reasons);
            WriteLine($"  校验: {(why == null ? "通过" : why)}");
            if (reasons.Count > 0)
            {
                foreach (var kv in reasons) WriteLine($"    {Snapshot.ReasonName(kv.Key)}: {kv.Value}");
            }
        }
        catch (Exception ex)
        {
            WriteLine($"  FetchAll 失败: {ex.Message}");
            if (_reader.DiagReadObjectFailure != null) WriteLine($"  失败位置: {_reader.DiagReadObjectFailure}");
        }

        WriteLine();
        WriteLine("pE+208 附近的字段语义探测（按 4 字节逐格打印 pE+192 .. pE+240）：");
        byte[] window = new byte[48];
        if (Mem.Rpm(_hProcess, pE + 192, window, 48))
        {
            for (int i = 0; i < 48; i += 4)
            {
                int asInt = BitConverter.ToInt32(window, i);
                float asFloat = BitConverter.ToSingle(window, i);
                WriteLine($"  pE+{192 + i,-4} = 0x{asInt:X8}  int={asInt,-12} float={asFloat}");
            }
        }
    }

    // ------------------------------------------------------------------ 复刻 FetchAll 读取序列

    /// <summary>
    /// 按主工程 FetchAll 的顺序把每一次 RPM 都重放一遍，精确指出第一个失败的调用。
    /// 主工程的读取散落在多个方法里，一旦抛异常就只剩一句 "ReadProcessMemory Error"，
    /// 这个重放是为了把失败的那一次读（地址、大小、属于哪个字段）暴露出来。
    /// </summary>
    private static void ReplayFetchAll()
    {
        if (_hProcess == IntPtr.Zero) { WriteLine("先 a 绑定"); return; }

        byte[] b4 = new byte[4];
        byte[] b16 = new byte[16];
        long fails = 0;
        int step = 0;

        void R(string what, IntPtr address, byte[] buf, int size)
        {
            step++;
            if (Mem.Rpm(_hProcess, address, buf, size)) return;
            fails++;
            if (fails <= 20)
            {
                WriteLine($"  [{step}] 失败 {what}: addr=0x{address:X} size={size}");
            }
        }

        int ptr(uint v) => (int)v;

        // ---- SetEditor 之后的部分：FetchHOM / FetchBeatmap / FetchControlPoints / FetchObjects
        R("pEditor+28 (HOM)", _reader.EditorAddress + 28, b4, 4);
        int pHom = BitConverter.ToInt32(b4, 0);
        R("pEditor+112 (Compose)", _reader.EditorAddress + 112, b4, 4);
        int pCompose = BitConverter.ToInt32(b4, 0);

        byte[] hom = new byte[256];
        R("HOM 头部 80", (IntPtr)pHom, hom, 80);
        int pBookmarksL = BitConverter.ToInt32(hom, 56);
        int pObjectsL = BitConverter.ToInt32(hom, 72);
        R("Compose 256", (IntPtr)pCompose, hom, 256);
        int pClipboardL = BitConverter.ToInt32(hom, 48);
        int pSelectedL = BitConverter.ToInt32(hom, 72);

        R("HOM+48 -> Beatmap", (IntPtr)(pHom + 48), b4, 4);
        int pBeatmap = BitConverter.ToInt32(b4, 0);

        byte[] bm = new byte[320];
        R("Beatmap 320", (IntPtr)pBeatmap, bm, 320);
        int pFolderStr = BitConverter.ToInt32(bm, 120);
        int pFileStr = BitConverter.ToInt32(bm, 144);

        void ReadStringDiag(string what, int pString)
        {
            if (pString == 0) { WriteLine($"  {what}: 空指针"); return; }
            R(what + " 长度", (IntPtr)(pString + 4), b4, 4);
            int n = BitConverter.ToInt32(b4, 0);
            if (n < 0 || n > 100000)
            {
                WriteLine($"  {what}: 长度异常 = {n}  (字符串指针 0x{pString:X})");
                return;
            }
            byte[] s = new byte[2 * Math.Max(n, 1)];
            R(what + " 内容", (IntPtr)(pString + 8), s, 2 * n);
        }

        ReadStringDiag("ContainingFolder", pFolderStr);
        ReadStringDiag("Filename", pFileStr);

        // 控制点
        R("Beatmap+176 -> 控制点列表", (IntPtr)(pBeatmap + 176), b4, 4);
        int pControlPointsL = BitConverter.ToInt32(b4, 0);
        R("控制点列表头", (IntPtr)pControlPointsL, b16, 16);
        int pControlPointsA = BitConverter.ToInt32(b16, 4);
        int numControlPoints = BitConverter.ToInt32(b16, 12);
        WriteLine($"  控制点: list=0x{pControlPointsL:X} items=0x{pControlPointsA:X} count={numControlPoints}");
        if (numControlPoints > 0 && numControlPoints < 1000000)
        {
            byte[] cps = new byte[4 * numControlPoints];
            R("控制点指针数组", (IntPtr)(pControlPointsA + 8), cps, 4 * numControlPoints);
            byte[] cp = new byte[48];
            for (int i = 0; i < numControlPoints; i++)
            {
                R($"控制点[{i}] 48B", (IntPtr)BitConverter.ToInt32(cps, 4 * i), cp, 48);
            }
        }

        // 物件
        R("物件列表头", (IntPtr)pObjectsL, b16, 16);
        int pObjectsA = BitConverter.ToInt32(b16, 4);
        int numObjects = BitConverter.ToInt32(b16, 12);
        WriteLine($"  物件: list=0x{pObjectsL:X} items=0x{pObjectsA:X} count={numObjects}");

        byte[] ptrs = new byte[4 * Math.Max(numObjects, 1)];
        R("物件指针数组", (IntPtr)(pObjectsA + 8), ptrs, 4 * numObjects);

        byte[] obj = new byte[336];
        int firstBadObject = -1;
        int badFieldStep = 0;
        long stringFails = 0;

        for (int i = 0; i < numObjects; i++)
        {
            int pObj = BitConverter.ToInt32(ptrs, 4 * i);
            step++;
            if (!Mem.Rpm(_hProcess, (IntPtr)pObj, obj, 336))
            {
                if (firstBadObject < 0) { firstBadObject = i; badFieldStep = step; }
                fails++;
                if (fails <= 20) WriteLine($"  [{step}] 失败 物件[{i}] 336B: addr=0x{pObj:X}");
                continue;
            }

            int type = BitConverter.ToInt32(obj, 24);
            string sampleFile = "";

            // SampleFile 字符串
            int pStr = BitConverter.ToInt32(obj, 84);
            if (pStr != 0)
            {
                step++;
                if (!Mem.Rpm(_hProcess, (IntPtr)(pStr + 4), b4, 4))
                {
                    stringFails++;
                    if (stringFails <= 10) WriteLine($"  [{step}] 失败 物件[{i}] SampleFile 长度: str=0x{pStr:X}");
                    continue;
                }
                int n = BitConverter.ToInt32(b4, 0);
                if (n < 0 || n > 100000)
                {
                    if (stringFails <= 10) WriteLine($"  [{step}] 物件[{i}] SampleFile 长度异常 = {n}");
                    stringFails++;
                    continue;
                }
                step++;
                byte[] s = new byte[2 * Math.Max(n, 1)];
                if (!Mem.Rpm(_hProcess, (IntPtr)(pStr + 8), s, 2 * n))
                {
                    stringFails++;
                    if (stringFails <= 10) WriteLine($"  [{step}] 失败 物件[{i}] SampleFile 内容: str=0x{pStr:X} len={n}");
                    continue;
                }
                sampleFile = new string(System.Text.Encoding.Unicode.GetChars(s, 0, 2 * n));
            }

            if ((type & 2) > 0)   // slider
            {
                int pPointsL = BitConverter.ToInt32(obj, 196);
                int pSTL = BitConverter.ToInt32(obj, 224);
                int pSSL = BitConverter.ToInt32(obj, 228);
                int pSSAL = BitConverter.ToInt32(obj, 232);

                R($"物件[{i}] slider 控制点列表头", (IntPtr)pPointsL, b16, 16);
                int pTempA = BitConverter.ToInt32(b16, 4);
                int numTemp = BitConverter.ToInt32(b16, 12);
                if (numTemp > 0 && numTemp < 1000000)
                {
                    byte[] curve = new byte[8 * numTemp];
                    R($"物件[{i}] slider 曲线点", (IntPtr)(pTempA + 8), curve, 8 * numTemp);
                }

                bool unified = BitConverter.ToBoolean(obj, 286);
                if (!unified)
                {
                    foreach ((string name, int list) in new[] { ("SoundTypeList", pSTL), ("SampleSetList", pSSL), ("SampleSetAdditionsList", pSSAL) })
                    {
                        R($"物件[{i}] {name} 列表头", (IntPtr)list, b16, 16);
                        int a = BitConverter.ToInt32(b16, 4);
                        int c = BitConverter.ToInt32(b16, 12);
                        if (c > 0 && c < 1000000)
                        {
                            byte[] tmp = new byte[4 * c];
                            R($"物件[{i}] {name} 内容", (IntPtr)(a + 8), tmp, 4 * c);
                        }
                    }
                }
            }
        }

        WriteLine();
        WriteLine($"重放结果: 共 {step} 次读取, 失败 {fails} 次");
        WriteLine($"  第一个读不到的物件下标 = {(firstBadObject < 0 ? "无" : firstBadObject.ToString())}（该次读取是第 {badFieldStep} 步）");
        WriteLine($"  SampleFile 相关失败 = {stringFails}");

        // 边界与内存映射状态：失败是不是"地址根本没映射"？
        WriteLine();
        WriteLine($"边界诊断入口: firstBadObject={firstBadObject} numObjects={numObjects} pObjectsA=0x{pObjectsA:X}");
        if (firstBadObject > 0 && firstBadObject < numObjects)
        {
            WriteLine("失败边界处的指针与映射状态：");
            for (int i = Math.Max(0, firstBadObject - 2); i < Math.Min(numObjects, firstBadObject + 2); i++)
            {
                long addr = (uint)BitConverter.ToInt32(ptrs, 4 * i);
                string state = Mem.DescribeRegion(_hProcess, (IntPtr)addr);
                bool readable = Mem.Rpm(_hProcess, (IntPtr)addr, obj, 336);
                WriteLine($"  物件[{i,6}] ptr=0x{addr:X8} 可读={readable,-6} {state}");
            }

            byte[] ptrs2 = new byte[4 * numObjects];
            bool ok2 = Mem.Rpm(_hProcess, (IntPtr)(pObjectsA + 8), ptrs2, 4 * numObjects);
            int changed = 0, firstChanged = -1;
            if (ok2)
            {
                for (int i = 0; i < numObjects; i++)
                {
                    if (ptrs[i] != ptrs2[i])
                    {
                        changed++;
                        if (firstChanged < 0) firstChanged = i;
                    }
                }
            }
            WriteLine($"\n两次读取指针数组的差异: 变化 {changed} 个（首个变化下标 {(firstChanged < 0 ? "无" : firstChanged.ToString())}）");
            WriteLine($"列表头二次读取: items=0x{Mem.ReadUInt32(_hProcess, pObjectsL + 4):X8} size={Mem.ReadInt32(_hProcess, pObjectsL + 12)}");

            // 把边界附近的内存区域全部列出来，看两个"可读"地址之间是不是夹了不可读的洞
            WriteLine();
            WriteLine("0x7F12F000..0x80014000 之间的内存区域：");
            foreach (string line in Mem.DescribeRegionsIn(_hProcess, 0x7F12F000, 0x80014000))
            {
                WriteLine("  " + line);
            }

            // 单页读取测试：失败是不是因为"跨页"，还是因为某段确实读不到
            WriteLine();
            WriteLine("逐地址 336B 读取测试：");
            foreach (long addr in new long[] { 0x7F12F550, 0x7F12FE00, 0x7F12FEC8, 0x7F12FF00, 0x7F12FFD0, 0x7F130000, 0x7F131000, 0x80011DDC })
            {
                bool r336 = Mem.Rpm(_hProcess, (IntPtr)addr, obj, 336);
                bool r1 = Mem.Rpm(_hProcess, (IntPtr)addr, b4, 4);
                WriteLine($"  0x{addr:X8}: 读336B={r336,-6} 读4B={r1}");
            }

            // 关键判断：失败物件是真的没数据，还是"数据存在但读取被最后几个字节卡住"？
            WriteLine();
            WriteLine("失败物件的内容可用性测试（能读多少就读多少）：");
            foreach (int i in new[] { firstBadObject, firstBadObject + 1, firstBadObject + 500, numObjects - 1 })
            {
                long addr = (uint)BitConverter.ToInt32(ptrs, 4 * i);
                var reads = Mem.ReadAsMuchAsPossible(_hProcess, (IntPtr)addr, 336);
                string desc = reads.Bytes > 0
                    ? $"读到 {reads.Bytes}B: Start={BitConverter.ToInt32(reads.Data, 16)} Type={BitConverter.ToInt32(reads.Data, 24)} X={BitConverter.ToSingle(reads.Data, 56):F1} Y={BitConverter.ToSingle(reads.Data, 60):F1}"
                    : "一个字节都读不到";
                WriteLine($"  物件[{i,6}] ptr=0x{addr:X8}  {desc}");
            }
        }
    }

    // ------------------------------------------------------------------ 指针符号扩展验证

    /// <summary>
    /// 直接对比"有符号读指针"与"无符号读指针"：对地址 >= 0x80000000 的物件，
    /// 有符号读会在 64 位宿主上符号扩展成 0xFFFFFFFF8xxxxxxx，RPM 立刻失败。
    /// </summary>
    private static void PointerSignCheck()
    {
        var (_, dataArray, count, _) = _reader.GetObjectPointers();
        if (count <= 0) { WriteLine("没有物件，先 a 绑定"); return; }

        byte[] ptrs = new byte[4 * count + 64];
        Mem.Rpm(_hProcess, dataArray + 8, ptrs, 4 * count);

        int below = 0, above = 0;
        int signedFailBelow = 0, unsignedFailBelow = 0;
        int signedFailAbove = 0;
        string firstAboveSample = "";

        byte[] obj = new byte[336];
        for (int i = 0; i < count; i++)
        {
            uint raw = BitConverter.ToUInt32(ptrs, 4 * i);

            // 有符号读（修复前的行为）
            int signedVal = BitConverter.ToInt32(ptrs, 4 * i);
            IntPtr signedPtr = (IntPtr)signedVal;                 // x64 上会符号扩展
            // 无符号读（修复后的行为）
            IntPtr unsignedPtr = (IntPtr)(long)raw;

            bool above2G = raw >= 0x80000000;
            if (above2G)
            {
                above++;
                if (signedPtr == unsignedPtr)
                {
                    signedFailAbove++;   // 出乎意料：应当不同
                }
                if (firstAboveSample.Length == 0)
                {
                    firstAboveSample = $"#{i} raw=0x{raw:X8} 有符号=0x{signedPtr.ToInt64():X16} 无符号=0x{unsignedPtr.ToInt64():X16}";
                }
            }
            else
            {
                below++;
                if (!Mem.Rpm(_hProcess, signedPtr, obj, 336)) signedFailBelow++;
                if (!Mem.Rpm(_hProcess, unsignedPtr, obj, 336)) unsignedFailBelow++;
            }
        }

        WriteLine($"物件总数 = {count}");
        WriteLine($"  地址 <  0x80000000 的物件: {below}（有符号/无符号读结果相同，失败 有符号={signedFailBelow} 无符号={unsignedFailBelow}）");
        WriteLine($"  地址 >= 0x80000000 的物件: {above}");
        if (firstAboveSample.Length > 0)
        {
            WriteLine($"  首个高位地址样本: {firstAboveSample}");
            WriteLine($"  其中有符号与无符号相同的数量 = {signedFailAbove}（应当为 0，否则说明符号扩展没发生）");
        }

        // 对高位地址做实际读取对比
        WriteLine();
        WriteLine("对高位地址物件的实际读取对比：");
        int shown = 0;
        for (int i = 0; i < count && shown < 5; i++)
        {
            uint raw = BitConverter.ToUInt32(ptrs, 4 * i);
            if (raw < 0x80000000) continue;
            shown++;
            IntPtr signedPtr = (IntPtr)BitConverter.ToInt32(ptrs, 4 * i);
            IntPtr unsignedPtr = (IntPtr)(long)raw;
            bool a = Mem.Rpm(_hProcess, signedPtr, obj, 336);
            bool b = Mem.Rpm(_hProcess, unsignedPtr, obj, 336);
            WriteLine($"  物件[{i,6}] raw=0x{raw:X8}  有符号读={a,-6} 无符号读={b}");
        }
    }

    // ------------------------------------------------------------------ 分段读取诊断

    /// <summary>
    /// 对当前失败的那个物件地址，手工重放"整块读 → 按页切分逐段读"的过程，
    /// 看每一段到底能不能读、差多少字节。
    /// </summary>
    private static void DiagnoseSegmentRead()
    {
        var (_, dataArray, count, _) = _reader.GetObjectPointers();
        if (count <= 0) { WriteLine("没有物件，先 a 绑定"); return; }
        if (dataArray == IntPtr.Zero) { WriteLine("物件数组为空"); return; }

        try { _reader.FetchAll(false); WriteLine("FetchAll 成功，当前没有失败物件"); return; }
        catch { }

        string? diag = _reader.DiagReadObjectFailure;
        WriteLine($"DiagReadObjectFailure = {diag ?? "(无)"}");

        int index = -1;
        if (diag != null)
        {
            var m = System.Text.RegularExpressions.Regex.Match(diag, @"index=(\d+)");
            if (m.Success) index = int.Parse(m.Groups[1].Value);
        }
        if (index < 0) { WriteLine("无法解析失败下标"); return; }

        byte[] ptrs = new byte[4 * count + 64];
        Mem.Rpm(_hProcess, dataArray + 8, ptrs, 4 * count);

        WriteLine();
        for (int i = Math.Max(0, index - 2); i <= Math.Min(count - 1, index + 2); i++)
        {
            uint p = BitConverter.ToUInt32(ptrs, 4 * i);
            byte[] b = new byte[336];
            bool full = Mem.Rpm(_hProcess, (IntPtr)(long)p, b, 336);
            var partial = Mem.ReadAsMuchAsPossible(_hProcess, (IntPtr)(long)p, 336);
            WriteLine($"  物件[{i,6}] ptr=0x{p:X8} 整块336B={full,-6} 最多可读={partial.Bytes}B");
            WriteLine($"      {Mem.DescribeRegion(_hProcess, (IntPtr)(long)p)}");
        }

        uint bad = BitConverter.ToUInt32(ptrs, 4 * index);
        WriteLine();
        WriteLine($"失败物件 0x{bad:X8} 的逐块可读性（每块 16B，只打印每 64B 一次或不ok的）：");
        for (int off = 0; off < 336; off += 16)
        {
            byte[] b = new byte[16];
            bool ok = Mem.Rpm(_hProcess, (IntPtr)(long)(bad + off), b, 16);
            if (!ok || off % 64 == 0)
            {
                WriteLine($"  +{off,4}..{off + 16,4}: {(ok ? "ok" : "读取失败")}  地址=0x{bad + off:X8}");
            }
        }

        WriteLine();
        WriteLine("解析所需的最大偏移 = 286（unifiedSoundAddition），即需要 287 字节");
        var p2 = Mem.ReadAsMuchAsPossible(_hProcess, (IntPtr)(long)bad, 336);
        WriteLine($"该物件实际可读 {p2.Bytes} 字节 => 是否够 287: {p2.Bytes >= 287}");
        if (p2.Bytes >= 160)
        {
            WriteLine($"  已读到的字段: Start={BitConverter.ToInt32(p2.Data, 16)} Type={BitConverter.ToInt32(p2.Data, 24)} X={BitConverter.ToSingle(p2.Data, 56):F1} Y={BitConverter.ToSingle(p2.Data, 60):F1}");
        }
    }

    // ------------------------------------------------------------------ 切难度压力测试

    /// <summary>
    /// 专门针对"切换同一谱面集的不同难度"：持续全量读取并记录每次失败的下标/地址，
    /// 同时打印谱面是否发生了变化，用于定位剩下那 ~1/12 的失败。
    /// </summary>
    private static void SwitchStress()
    {
        WriteLine("切难度压力测试：每 50ms 全量读取一次，记录谱面变化与失败。");
        WriteLine("输入持续秒数（回车=90）：");
        string? s = Console.ReadLine();
        int seconds = int.TryParse(s, out int v) && v > 0 ? v : 90;
        WriteLine($"开始，请现在反复切换同类谱面集的不同难度（{seconds} 秒）…\n");

        int lastObjects = -1;
        string lastFile = "";
        long fetches = 0, failures = 0, switches = 0, healed = 0, persistent = 0;
        var failureReasons = new Dictionary<string, long>();
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < seconds * 1000L)
        {
            fetches++;
            try
            {
                _reader.FetchAll(false);

                if (_reader.numObjects != lastObjects || _reader.Filename != lastFile)
                {
                    switches++;
                    WriteLine($"  [{(sw.ElapsedMilliseconds / 1000.0):F1}s] 谱面变化: 物件 {lastObjects} -> {_reader.numObjects}  文件={_reader.Filename}");
                    lastObjects = _reader.numObjects;
                    lastFile = _reader.Filename;
                }
            }
            catch (Exception ex)
            {
                failures++;
                string detail = _reader.DiagReadObjectFailure ?? ex.Message;
                failureReasons[detail] = failureReasons.GetValueOrDefault(detail) + 1;
                WriteLine($"  [{(sw.ElapsedMilliseconds / 1000.0):F1}s] !! 读取失败: {detail}");

                // 立刻重试：区分"瞬时撕裂（重读即可）"与"真的读不到"
                bool ok = false;
                for (int r = 0; r < 3 && !ok; r++)
                {
                    try { _reader.FetchAll(false); ok = true; }
                    catch { }
                }
                if (ok) healed++; else persistent++;
                WriteLine($"      立即重试 => {(ok ? "成功（瞬时）" : "仍然失败（持续）")}");

                if (!ok)
                {
                    // 持续失败：把失败物件的映射状态、可读字节数都打出来
                    WriteLine("      --- 持续失败详细诊断 ---");
                    var (_, dataArray, count, _) = _reader.GetObjectPointers();
                    int idx = -1;
                    var m = System.Text.RegularExpressions.Regex.Match(detail, @"index=(\d+)");
                    if (m.Success) idx = int.Parse(m.Groups[1].Value);

                    if (dataArray != IntPtr.Zero && idx >= 0 && idx < count)
                    {
                        byte[] ptrs = new byte[4 * count + 64];
                        if (Mem.Rpm(_hProcess, dataArray + 8, ptrs, 4 * count))
                        {
                            for (int i = Math.Max(0, idx - 1); i <= Math.Min(count - 1, idx + 1); i++)
                            {
                                uint p = BitConverter.ToUInt32(ptrs, 4 * i);
                                var partial = Mem.ReadAsMuchAsPossible(_hProcess, (IntPtr)(long)p, 336);
                                // 用 Type 判断这个物件需要多少字节
                                int need = 148;
                                if (partial.Bytes >= 28)
                                {
                                    int type = BitConverter.ToInt32(partial.Data, 24);
                                    if ((type & 2) > 0) need = 287;
                                }
                                WriteLine($"      物件[{i,6}] ptr=0x{p:X8} 可读={partial.Bytes}B 需要={need}B 够用={(partial.Bytes >= need)}");
                                WriteLine($"         {Mem.DescribeRegion(_hProcess, (IntPtr)(long)p)}");
                            }
                        }
                    }

                    // 三次重试分别失败在哪
                    WriteLine("      连续 3 次重试的失败位置:");
                    for (int r = 0; r < 3; r++)
                    {
                        try { _reader.FetchAll(false); WriteLine($"        第{r + 1}次: 成功"); }
                        catch (Exception ex2) { WriteLine($"        第{r + 1}次: {(_reader.DiagReadObjectFailure ?? ex2.Message)}"); }
                    }
                }
            }

            Thread.Sleep(50);
        }

        WriteLine($"\n总计: 读取 {fetches} 次, 谱面变化 {switches} 次, 失败 {failures} 次");
        WriteLine($"  失败后立即重试即可恢复(瞬时): {healed}");
        WriteLine($"  失败后重试仍失败(持续)    : {persistent}");
        if (failureReasons.Count > 0)
        {
            WriteLine("失败明细:");
            foreach (var kv in failureReasons.OrderByDescending(k => k.Value)) WriteLine($"  ×{kv.Value}  {kv.Key}");
        }
        else
        {
            WriteLine("没有失败。");
        }
    }

    // ------------------------------------------------------------------ 快照一致性（seqlock）测试

    /// <summary>
    /// 验证"边加载边读"导致的撕裂：列表头读到的 _size / _items，与随后读到的指针数组
    /// 可能不属于同一时刻（编辑器正在往里添加物件）。做法是读完指针数组后再读一次列表头，
    /// 看两次头是否一致；不一致就说明这一份快照是撕裂的。
    /// </summary>
    private static void SnapshotConsistency()
    {
        var (listHeader, _, _, _) = _reader.GetObjectPointers();
        if (listHeader == IntPtr.Zero) { WriteLine("先 a 绑定"); return; }

        WriteLine("快照一致性测试：每次读取前后各读一次列表头，统计不一致（撕裂）的比例。");
        WriteLine("输入持续秒数（回车=60）：");
        string? s = Console.ReadLine();
        int seconds = int.TryParse(s, out int v) && v > 0 ? v : 60;
        WriteLine($"开始，请现在反复切换难度 / 让编辑器加载谱面（{seconds} 秒）…\n");

        byte[] h1 = new byte[16];
        byte[] h2 = new byte[16];
        byte[] ptrs = new byte[4 * 200000];

        long reads = 0, torn = 0, tornAndFailed = 0, tornAndOk = 0;
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < seconds * 1000L)
        {
            reads++;

            if (!Mem.Rpm(_hProcess, listHeader, h1, 16)) { Thread.Sleep(20); continue; }
            uint items1 = BitConverter.ToUInt32(h1, 4);
            int size1 = BitConverter.ToInt32(h1, 12);
            if (items1 == 0 || size1 <= 0 || size1 > 200000) { Thread.Sleep(20); continue; }

            bool arrOk = Mem.Rpm(_hProcess, (IntPtr)(long)(items1 + 8), ptrs, 4 * size1);

            bool hdr2Ok = Mem.Rpm(_hProcess, listHeader, h2, 16);
            uint items2 = BitConverter.ToUInt32(h2, 4);
            int size2 = BitConverter.ToInt32(h2, 12);

            bool isTorn = !hdr2Ok || items1 != items2 || size1 != size2;

            if (isTorn)
            {
                torn++;
                bool anyFail = !arrOk;
                if (!anyFail)
                {
                    byte[] one = new byte[336];
                    for (int i = Math.Max(0, size1 - 3); i < size1; i++)
                    {
                        if (!Mem.Rpm(_hProcess, (IntPtr)(long)BitConverter.ToUInt32(ptrs, 4 * i), one, 336)) { anyFail = true; break; }
                    }
                }
                if (anyFail) tornAndFailed++; else tornAndOk++;
            }

            Thread.Sleep(20);
        }

        WriteLine($"\n总计读取 {reads} 次，其中快照撕裂 {torn} 次 ({100.0 * torn / Math.Max(1, reads):F1}%)");
        WriteLine($"  撕裂且末尾物件读不到: {tornAndFailed}");
        WriteLine($"  撕裂但恰好还能读  : {tornAndOk}");
        WriteLine("\n若撕裂比例明显高于实际失败率，说明'撕裂 → 抛异常 → 退避'是对的，");
        WriteLine("但可以通过'读前后各校验一次列表头'把撕裂的快照直接丢弃，而不是让它去撞异常。");
    }

    // ------------------------------------------------------------------ 加载窗口自动压测

    /// <summary>
    /// 只在"检测到谱面正在加载/切换"时才高强度读取，从而稳定命中撕裂窗口。
    /// 检测方式：物件数变化，或前后两次读取的数不一致。命中后立刻连续读取 200 次，
    /// 记录每次失败并判断"重读一次是否恢复"。
    /// </summary>
    private static void LoadWindowStress()
    {
        WriteLine("加载窗口自动压测：检测到谱面变化后立刻连读 200 次，专门抓撕裂窗口。");
        WriteLine("输入持续秒数（回车=120）：");
        string? s = Console.ReadLine();
        int seconds = int.TryParse(s, out int v) && v > 0 ? v : 120;
        WriteLine("无需你配合——你只要照常切难度就行。也可以什么都不做。\n");

        int lastObjects = -1;
        string lastFile = "";
        long loads = 0, bursts = 0, burstReads = 0, fails = 0, healed = 0, persistent = 0;
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < seconds * 1000L)
        {
            try
            {
                _reader.FetchAll(false);
                if (_reader.numObjects != lastObjects || _reader.Filename != lastFile)
                {
                    WriteLine($"  [{(sw.ElapsedMilliseconds / 1000.0):F1}s] 谱面变化: {lastObjects} -> {_reader.numObjects}  {_reader.Filename}");
                    lastObjects = _reader.numObjects;
                    lastFile = _reader.Filename;
                    loads++;

                    // 命中变化：立刻连打，把加载窗口里的撕裂都抓出来
                    bursts++;
                    for (int k = 0; k < 200; k++)
                    {
                        burstReads++;
                        try { _reader.FetchAll(false); }
                        catch (Exception ex)
                        {
                            fails++;
                            string detail = _reader.DiagReadObjectFailure ?? ex.Message;
                            // 判断是否瞬时：立刻重试
                            bool ok = false;
                            for (int r = 0; r < 3 && !ok; r++)
                            {
                                try { _reader.FetchAll(false); ok = true; } catch { }
                            }
                            if (ok) healed++; else
                            {
                                // "立即重试也失败"不等于"永久读不到"：可能几次都落在同一个重分配窗口里。
                                // 隔 1 秒再确认，才能区分真·持续失败。
                                Thread.Sleep(1000);
                                bool stillBad = false;
                                try { _reader.FetchAll(false); } catch { stillBad = true; }

                                if (stillBad)
                                {
                                    persistent++;
                                    WriteLine($"      !! 真·持续失败: {detail}");
                                    WriteLine($"         {Mem.DescribeRegion(_hProcess, _reader.EditorAddress)}");
                                }
                                else
                                {
                                    healed++;
                                    if (fails <= 8) WriteLine($"      [burst{k}] 瞬时（1 秒后恢复）: {detail}");
                                }
                            }

                            if (fails <= 8 && ok) WriteLine($"      [burst{k}] 瞬时: {detail}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLine($"  [{(sw.ElapsedMilliseconds / 1000.0):F1}s] 常规读取失败: {_reader.DiagReadObjectFailure ?? ex.Message}");
            }

            Thread.Sleep(200);
        }

        WriteLine($"\n总计: 谱面变化 {loads} 次, 其中触发连打 {bursts} 次, 连打共读 {burstReads} 次");
        WriteLine($"  失败 {fails} 次 ({(burstReads > 0 ? 100.0 * fails / burstReads : 0):F2}%)");
        WriteLine($"  瞬时(重读即恢复): {healed}");
        WriteLine($"  持续(重读仍失败): {persistent}");
    }

    // ------------------------------------------------------------------ 读取长度阈值探测

    /// <summary>
    /// 对一个"读不到"的地址，用递增的长度反复读，找出从多少字节开始失败。
    /// 这能区分"整页不可读" / "跨页不可读" / "只有尾部几条字节不可读"。
    /// </summary>
    private static void ProbeReadThreshold()
    {
        WriteLine("输入要探测的地址（十六进制，可带 0x 前缀）：");
        string? input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input)) { WriteLine("未输入"); return; }
        input = input.Trim();
        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) input = input.Substring(2);
        if (input.Length > 8) input = input.Substring(input.Length - 8);   // 只取低 32 位

        if (!long.TryParse(input, System.Globalization.NumberStyles.HexNumber, null, out long addr))
        {
            WriteLine("地址解析失败");
            return;
        }

        IntPtr p = (IntPtr)addr;
        WriteLine($"\n目标地址 = 0x{addr:X8}");
        WriteLine(Mem.DescribeRegion(_hProcess, p));
        WriteLine($"页 = 0x{addr & ~0xFFFL:X8}，页内偏移 = 0x{addr & 0xFFF:X}，距页尾 {0x1000 - (addr & 0xFFF)} 字节\n");

        WriteLine("长度阈值（从哪个长度开始失败）：");
        byte[] buf = new byte[4096];
        foreach (int size in new[] { 4, 16, 28, 32, 64, 128, 148, 192, 256, 272, 287, 288, 320, 336, 352, 512, 1024, 2048, 4096 })
        {
            bool ok = Mem.Rpm(_hProcess, p, buf, size);
            WriteLine($"  {size,5} B  {(ok ? "ok" : "失败")}");
        }

        WriteLine("\n逐 16 字节分段（前 400 字节）：");
        for (int off = 0; off < 400; off += 16)
        {
            byte[] b = new byte[16];
            bool ok = Mem.Rpm(_hProcess, (IntPtr)(addr + off), b, 16);
            WriteLine($"  +{off,4} (0x{addr + off:X8}): {(ok ? "ok" : "失败")}");
        }
    }

    // ------------------------------------------------------------------ 长时间错误监控

    /// <summary>
    /// 长时间错误监控：每 50ms 走一次完整的"编辑器校验 + 全量读取"链路，
    /// 复刻生产里的重试策略（快照级失败原地重试 1 次，重试仍失败才计入），
    /// 每 10 秒打印一次汇总，所有失败明细写入日志文件。
    /// 用于"用户实际操作（进退编辑器/切难度/改谱/新建/保存）时有没有错误"。
    /// </summary>
    private static void ErrorMonitor()
    {
        WriteLine("长时间错误监控。输入持续秒数（回车=1800）：");
        string? s = Console.ReadLine();
        int seconds = int.TryParse(s, out int v) && v > 0 ? v : 1800;

        WriteLine($"开始监控 {seconds} 秒。请现在开始你的操作序列…\n");

        long ticks = 0, fullReads = 0, transient = 0, hardFailures = 0;
        long editorChecks = 0, editorNotReady = 0, rebinds = 0;
        long lastObjects = -1;
        string lastFile = "";
        long mapSwitches = 0;
        var reasonCounts = new Dictionary<string, long>();

        var sw = Stopwatch.StartNew();
        long nextReportAt = 10_000;
        string logPath = Path.Combine(Path.GetTempPath(), "editorreader-monitor.log");
        try { File.WriteAllText(logPath, "EditorReader 监控日志 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n"); } catch { }
        void Log(string line)
        {
            try { File.AppendAllText(logPath, line + "\r\n"); } catch { }
        }

        while (sw.ElapsedMilliseconds < seconds * 1000L)
        {
            ticks++;

            // ---- 编辑器校验（等价于 FetchEditor 的廉价路径）
            try
            {
                editorChecks++;
                if (_reader.EditorNeedsReload())
                {
                    rebinds++;
                    Log($"[{sw.ElapsedMilliseconds / 1000.0:F1}s] 需要重绑（退出编辑器 / 换谱面 / 地址失效）");
                    try
                    {
                        _reader.ResetEditor();
                        _reader.FetchEditor();
                    }
                    catch (Exception ex)
                    {
                        editorNotReady++;
                        Log($"[{sw.ElapsedMilliseconds / 1000.0:F1}s] 重绑失败(可能不在编辑器): {ex.Message}");
                        Thread.Sleep(50);
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                editorNotReady++;
                Log($"[{sw.ElapsedMilliseconds / 1000.0:F1}s] 编辑器校验异常: {ex.Message}");
                Thread.Sleep(50);
                continue;
            }

            // ---- 全量读取（含生产策略：快照级失败重试 1 次）
            try
            {
                fullReads++;
                try
                {
                    _reader.FetchAll(false);
                }
                catch (Exception first)
                {
                    try
                    {
                        _reader.FetchAll(false);
                        transient++;
                        Log($"[{sw.ElapsedMilliseconds / 1000.0:F1}s] 瞬时失败(重试恢复): {_reader.DiagReadObjectFailure ?? first.Message}");
                    }
                    catch (Exception second)
                    {
                        hardFailures++;
                        string detail = _reader.DiagReadObjectFailure ?? second.Message;
                        reasonCounts[detail] = reasonCounts.GetValueOrDefault(detail) + 1;
                        Log($"[{sw.ElapsedMilliseconds / 1000.0:F1}s] !! 硬失败(重试仍失败): {detail}");
                    }
                }

                if (_reader.numObjects != lastObjects || _reader.Filename != lastFile)
                {
                    mapSwitches++;
                    Log($"[{sw.ElapsedMilliseconds / 1000.0:F1}s] 谱面变化: {lastObjects} -> {_reader.numObjects}  {_reader.Filename}  控制点={_reader.numControlPoints}");
                    lastObjects = _reader.numObjects;
                    lastFile = _reader.Filename;
                }
            }
            catch (Exception ex)
            {
                hardFailures++;
                reasonCounts["外层: " + ex.Message] = reasonCounts.GetValueOrDefault("外层: " + ex.Message) + 1;
                Log($"[{sw.ElapsedMilliseconds / 1000.0:F1}s] !! 外层异常: {ex}");
            }

            // ---- 周期汇总
            if (sw.ElapsedMilliseconds >= nextReportAt)
            {
                nextReportAt += 10_000;
                string line = $"[{sw.ElapsedMilliseconds / 1000.0,6:F0}s] tick={ticks} 全量={fullReads} 瞬时={transient} 硬失败={hardFailures} " +
                              $"重绑={rebinds} 未就绪={editorNotReady} 谱面变化={mapSwitches} 当前物件={_reader.numObjects}";
                WriteLine(line);
                Log(line);
            }

            Thread.Sleep(50);
        }

        WriteLine("\n===== 监控结束 =====");
        WriteLine($"tick={ticks}  全量读取={fullReads}  编辑器校验={editorChecks}");
        WriteLine($"瞬时失败(重试即恢复)={transient}");
        WriteLine($"硬失败(重试仍失败)  ={hardFailures}");
        WriteLine($"编辑器重绑={rebinds}  绑定未就绪={editorNotReady}  谱面变化={mapSwitches}");
        if (reasonCounts.Count > 0)
        {
            WriteLine("硬失败原因:");
            foreach (var kv in reasonCounts.OrderByDescending(k => k.Value).Take(20)) WriteLine($"  ×{kv.Value}  {kv.Key}");
        }
        else
        {
            WriteLine("没有硬失败。");
        }
        WriteLine($"完整日志: {logPath}");
    }

    // ------------------------------------------------------------------ 候选判据对照（旧 vs 新）

    /// <summary>
    /// 对每个编辑器候选分别用"旧判据"和"新判据"跑一遍，看谁能通过。
    /// 旧判据 = 签名 + 状态字段 + HOM/物件列表指针非空（原 IsPlausibleEditor / EditorMissingObjects）。
    /// 新判据 = 再跟着指针实际采样读一次物件（ObjectsReadableFromChain）。
    /// 死副本应当表现为"旧判据通过、新判据拒绝"。
    /// </summary>
    private static void CompareCandidateChecks()
    {
        if (_hProcess == IntPtr.Zero) { WriteLine("先 a 绑定"); return; }

        byte[] pattern = new byte[]
        {
            0x23,0,0,0, 0x14,0,0,0, 0x19,0,0,0,
            0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,
            0xEE,0xEE,0xEE,0xEE,
            0x0C,0,0,0,
            0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,0xEE,
            0x00
        };

        WriteLine("扫描全部编辑器候选…");
        var regions = Mem.Regions(_hProcess);
        var candidates = new List<long>();
        foreach (var r in regions)
        {
            long size = r.RegionSize.ToInt64();
            if (size <= 0 || size > 256L * 1024 * 1024) continue;
            byte[] buffer = new byte[size];
            if (!Mem.Rpm(_hProcess, r.BaseAddress, buffer, (int)size)) continue;
            for (int j = 0; j + pattern.Length <= buffer.Length; j += 4)
            {
                if (buffer[j] != 0x23 || buffer[j + 4] != 0x14 || buffer[j + 8] != 0x19 || buffer[j + 32] != 0x0C) continue;
                if (!PatternMatch(buffer, pattern, j)) continue;
                candidates.Add(r.BaseAddress.ToInt64() + j - 160);
            }
        }

        WriteLine($"命中 {candidates.Count} 个候选。判据对照：\n");
        WriteLine($"{"候选地址",-14}{"状态字段",-14}{"旧判据",-8}{"新判据",-8}{"物件数",-8}{"采样可读",-10}说明");
        WriteLine(new string('-', 104));

        byte[] b16 = new byte[16];
        byte[] b4 = new byte[4];

        foreach (long c in candidates)
        {
            IntPtr pE = (IntPtr)c;

            string fields = "读取失败";
            if (Mem.Rpm(_hProcess, pE + 160, b16, 16))
            {
                fields = $"({BitConverter.ToInt32(b16, 0)},{BitConverter.ToInt32(b16, 4)},{BitConverter.ToInt32(b16, 8)})";
            }

            // 旧判据：HOM 非空 且 物件列表指针非空
            bool oldOk = false;
            IntPtr pHom = IntPtr.Zero;
            if (Mem.Rpm(_hProcess, pE + 28, b4, 4))
            {
                pHom = (IntPtr)(long)BitConverter.ToUInt32(b4, 0);
                if (pHom != IntPtr.Zero && Mem.Rpm(_hProcess, (IntPtr)(pHom.ToInt64() + 72), b4, 4))
                {
                    oldOk = BitConverter.ToUInt32(b4, 0) != 0;
                }
            }

            // 新判据：再跟着指针采样读物件
            int count = -1, readable = 0;
            bool newOk = false;
            if (oldOk)
            {
                Mem.Rpm(_hProcess, (IntPtr)(pHom.ToInt64() + 72), b4, 4);
                IntPtr pList = (IntPtr)(long)BitConverter.ToUInt32(b4, 0);
                if (Mem.Rpm(_hProcess, pList, b16, 16))
                {
                    IntPtr pArr = (IntPtr)(long)BitConverter.ToUInt32(b16, 4);
                    count = BitConverter.ToInt32(b16, 12);
                    if (count > 0 && pArr != IntPtr.Zero)
                    {
                        foreach (int idx in new[] { 0, count / 2, count - 1 })
                        {
                            if (!Mem.Rpm(_hProcess, (IntPtr)(pArr.ToInt64() + 8 + 4 * idx), b4, 4)) continue;
                            IntPtr pObj = (IntPtr)(long)BitConverter.ToUInt32(b4, 0);
                            if (pObj == IntPtr.Zero) continue;
                            if (Mem.Rpm(_hProcess, pObj, b4, 4)) readable++;
                        }
                    }
                    else if (count == 0)
                    {
                        readable = -2;   // 空列表
                    }
                }
                newOk = count == 0 || readable > 0;
            }

            bool isCurrent = c == _reader.EditorAddress.ToInt64();
            string note = isCurrent ? "[当前绑定] " : "";
            if (oldOk && !newOk) note += "<== 死副本：旧判据放过、新判据拒绝";
            else if (!oldOk) note += "（旧判据就拒绝了）";

            WriteLine($"{("0x" + c.ToString("X8")),-16}{fields,-14}{(oldOk ? "通过" : "拒绝"),-8}{(newOk ? "通过" : "拒绝"),-8}{count,-8}{readable,-10}{note}");
        }

        WriteLine("\n旧判据只要求 HOM 与物件列表指针非空；新判据还要求采样到的物件真能读出来。");
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
