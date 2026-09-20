// Copyright (c) 2019 Karoo13. Licensed under https://github.com/Karoo13/EditorReader/blob/master/LICENSE
// See the LICENCE file in the EditorReader folder for full licence text.
// https://github.com/Karoo13/EditorReader
// Decompiled with ICSharpCode.Decompiler 8.1.1.7464

using osucatch_editor_realtimeviewer;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Editor_Reader;

public class EditorReader
{
    public bool autoDeStack = true;

    public bool autoRound;

    private byte[] buffer;

    private byte[] buffer4 = new byte[4];

    private byte[] buffer16 = new byte[16];

    private byte[] bufferCp = new byte[48];

    private byte[] bufferOb = new byte[336];

    private IntPtr bytesRead;

    private Process process;

    /// <summary>
    /// 目标进程句柄。默认走 <see cref="Process.Handle"/>；
    /// 若宿主已用 <c>OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_LIMITED_INFORMATION)</c> 打开句柄，
    /// 可写入 <see cref="ForceHandle"/> 以避免 .NET 默认权限不足（osu! 以管理员身份运行时会出现）。
    /// </summary>
    private IntPtr TargetHandle => forceHandle != IntPtr.Zero ? forceHandle : process.Handle;

    private IntPtr forceHandle;

    /// <summary>
    /// 由宿主提供的目标进程句柄（例如只有 VM_READ 权限的句柄）。
    /// 设为非 0 后所有读取都使用它；宿主负责在换绑进程时替换/关闭句柄。
    /// </summary>
    public IntPtr ForceHandle
    {
        get => forceHandle;
        set => forceHandle = value;
    }

    private IntPtr pEditor;

    private IntPtr pCompose;

    private IntPtr pHOM;

    private IntPtr pBeatmap;

    public float objectRadius;

    public float stackOffset;

    private IntPtr pBookmarksL;

    private IntPtr pBookmarksA;

    public int numBookmarks;

    public int[] bookmarks;

    public string ContainingFolder;

    public string Filename;

    public float HPDrainRate;

    public float CircleSize;

    public float OverallDifficulty;

    public float ApproachRate;

    public double SliderMultiplier;

    public double SliderTickRate;

    public int BeatmapVersion;

    public int PreviewTime;

    public float StackLeniency;

    public float TimelineZoom;

    private IntPtr pControlPointsL;

    private IntPtr pControlPointsA;

    public int numControlPoints;

    private byte[] pControlPoints;

    public List<ControlPoint> controlPoints;

    private IntPtr pObjectsL;

    private IntPtr pObjectsA;

    public int numObjects;

    private byte[] pObjects;

    /// <summary>
    /// 主物件列表指针 -> 主物件下标，用于轻量地把选中物件列表映射回 SourceIndex。
    /// 每次 SetObjects 时重建。
    /// </summary>
    private Dictionary<IntPtr, int>? masterIndexByPointer;

    public List<HitObject> hitObjects;

    /// <summary>
    /// 目标进程的指针宽度。绝不能再用 <see cref="IntPtr.Size"/>：
    /// 那是 viewer 自己的位数，而 osu! 是 32 位进程。viewer 汇编成 AnyCPU/x64 时
    /// <c>IntPtr.Size == 8</c>，会把每 8 字节才读一次指针，从第二个物件起全部错位
    /// ——表现为"物件读出来全是垃圾值 / 数量为 0"，而不是报错。
    /// 因此 32 位目标必须按 4 字节指针读取。
    /// </summary>
    private const int TargetPointerSize = 4;

    /// <summary>
    /// 诊断用：最近一次 <see cref="ReadObjects"/> 的失败分类计数。
    /// 仅用于性能/稳定性测量（EditorReaderHarness），不参与正常读取逻辑。
    /// </summary>
    public readonly long[] DiagObjectRejectReasons = new long[8];

    /// <summary>诊断用：最近一次 <see cref="ReadObjects"/> 读取的对象数。</summary>
    public int DiagLastInvalidObjectIndex = -1;

    /// <summary>诊断用：最近一次 <see cref="ReadObjects"/> 中被拒绝的对象样本（物件字符串 + 原因）。</summary>
    public string? DiagLastRejectSample;

    /// <summary>诊断用：最近一次 <see cref="ReadObjects"/> 抛异常时的物件下标与地址。</summary>
    public string? DiagReadObjectFailure;

    /// <summary>诊断用：最近一次 <see cref="ReadObjects"/> 跳过的空指针条数（编辑器重建列表的瞬间）。</summary>
    public int DiagNullObjectPointers;

    // ---- 扫描诊断（EditorReaderHarness 读取；正常运行时只是几个赋值） ----

    /// <summary>诊断用：最近一次编辑器扫描共检查了多少个区域。</summary>
    public long DiagScanRegionsScanned;
    /// <summary>诊断用：最近一次扫描中被跳过的区域数（退避 + 超大）。</summary>
    public long DiagScanRegionsSkipped;
    /// <summary>诊断用：扫描中成功读取的内存块数。</summary>
    public long DiagScanChunksOk;
    /// <summary>诊断用：扫描中读取失败的内存块数。</summary>
    public long DiagScanChunksFailed;
    /// <summary>诊断用：签名命中次数（命中后还要过候选校验）。</summary>
    public int DiagScanSignatureHits;
    /// <summary>诊断用：首个被拒绝候选的地址与原因。</summary>
    public string? DiagScanFirstReject;
    /// <summary>诊断用：首个读取失败的内存块信息。</summary>
    public string? DiagScanFirstReadFailure;

    /// <summary>诊断用：最近一次 MemInfo 的枚举过程（Internals）。</summary>
    private Internals? lastScanInternals;

    /// <summary>诊断用：最近一次区域枚举查询了多少个区域。</summary>
    public int DiagMemInfoQueried => lastScanInternals?.DiagQueried ?? 0;
    /// <summary>诊断用：最近一次区域枚举命中过滤条件的区域数。</summary>
    public int DiagMemInfoMatched => lastScanInternals?.DiagMatched ?? 0;
    /// <summary>诊断用：最近一次区域枚举的结束原因。</summary>
    public string DiagMemInfoStopReason => lastScanInternals?.DiagStopReason ?? "(未执行)";
    /// <summary>诊断用：区域枚举是否启用了宽松过滤。</summary>
    public bool DiagMemInfoRelaxed => lastScanInternals?.DiagRelaxedPass ?? false;
    /// <summary>诊断用：最近一次区域枚举失败时的 Win32 错误码。</summary>
    public int DiagMemInfoLastError => lastScanInternals?.DiagLastError ?? 0;
    /// <summary>诊断用：最近一次枚举到的前若干个区域的原始字段。</summary>
    public IReadOnlyList<string> DiagMemInfoFirstRegions => lastScanInternals?.DiagFirstRegions ?? (IReadOnlyList<string>)Array.Empty<string>();

    /// <summary>诊断用：命中过滤条件的所有区域 (地址, RegionSize)，按大小升序。</summary>
    public IReadOnlyList<(long Base, long Size)> DiagMatchedRegions
        => lastScanInternals?.DiagMatchedRegions ?? (IReadOnlyList<(long, long)>)Array.Empty<(long, long)>();

    /// <summary>诊断用：某次扫描中被"大小超限"跳过的区域明细。</summary>
    public readonly List<string> DiagSkippedTooLarge = new();

    /// <summary>诊断用：枚举到但从未进入扫描循环的区域（索引与地址），用于定位"漏扫"。</summary>
    public readonly List<string> DiagNeverScannedRegions = new();

    /// <summary>诊断用：本次扫描待扫描的区域总数（MemReg 数量）。</summary>
    public int DiagScanOrderCount;

    /// <summary>诊断用：实际进入扫描循环的区域数（按集合统计，与计数器互相印证）。</summary>
    public int DiagScanDidCount;

    /// <summary>诊断用：MemReg 里的区域总数。</summary>
    public int DiagMemRegCount;

    /// <summary>诊断用：本次扫描顺序中前若干个区域索引。</summary>
    public readonly List<int> DiagScanOrderHead = new();

    /// <summary>诊断用：本次扫描顺序中最后若干个区域索引。</summary>
    public readonly List<int> DiagScanOrderTail = new();

    /// <summary>
    /// 目标是否跑在 Wine 下。Wine 的 ReadProcessMemory 对"跨页 / 边界"的处理与真实 Windows
    /// 有差异，同一个读取可能间歇性失败。这种环境下的读取失败不应该被当成"编辑器失效"。
    /// </summary>
    public bool IsWineTarget { get; set; } = DetectWine();

    private static bool DetectWine()
    {
        try
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WINEPREFIX"))
                   || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WINEDLLOVERRIDES"));
        }
        catch
        {
            return false;
        }
    }

    private IntPtr pClipboardL;

    private IntPtr pClipboardA;

    public int numClipboard;

    private byte[] pClipboard;

    public List<HitObject> clipboardObjects;

    private IntPtr pSelectedL;

    private IntPtr pSelectedA;

    public int numSelected;

    private byte[] pSelected;

    public List<HitObject> selectedObjects;

    /// <summary>
    /// 单个内存区域允许扫描的最大字节数：超过则跳过并记录警告。
    /// Wine/osu-winello 下 VirtualQueryEx 可能报告超大已提交区域，全量扫描会让程序长时间
    /// 停在 "Try fetch editor"；编辑器签名通常位于较小的堆区域中。
    /// </summary>
    private const long MaxScanRegionSize = 512L * 1024 * 1024;

    /// <summary>
    /// 单次内存扫描的最长时间。某些环境下 ReadProcessMemory 可能对个别区域永久阻塞，
    /// 无法从超时点中断原生调用，因此把扫描放到独立线程，超时后放弃该线程、下次重试。
    /// </summary>
    private const int ScanTimeoutMs = 30000;

    /// <summary>上次成功找到 editor 的区域起始地址：重绑时优先扫描它（快路径）。</summary>
    private long lastFoundRegionBase;

    /// <summary>
    /// 仍在运行的扫描线程数（含超时被放弃、但可能还阻塞在 ReadProcessMemory 里的线程）。
    /// 超时的线程无法被中断，这里限制并发数，避免反复重试时线程无限堆积。
    /// </summary>
    private int liveScanThreads;
    private const int MaxLiveScanThreads = 3;

    /// <summary>当前扫描线程正在读取的区域起始地址（供超时看门狗定位卡点）。</summary>
    private long scanningRegionAddress;

    /// <summary>当前扫描线程正在读取的区域序号（供超时看门狗定位卡点）。</summary>
    private volatile int scanningRegionIndex;

    private IntPtr pHoveredObject;

    public HitObject hoveredObject;

    private IntPtr pSliderPlacement;

    public HitObject sliderPlacement;

    private IntPtr pPointsL;

    private IntPtr pSTL;

    private IntPtr pSSL;

    private IntPtr pSSAL;

    private IntPtr pTempA;

    private int numTemp;

    private byte[] bTemp;

    [DllImport("kernel32.dll", SetLastError = true)]
    protected static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, ref IntPtr lpNumberOfBytesRead);

    private static bool SafeReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, ref IntPtr lpNumberOfBytesRead)
    {
        bool result = ReadProcessMemory(hProcess, lpBaseAddress, lpBuffer, dwSize, ref lpNumberOfBytesRead);
        if (!result)
        {
            Log.ConsoleLog("ReadProcessMemory failed: lpBaseAddress: " + lpBaseAddress + ", lpBuffer's length: " + lpBuffer.Length, Log.LogType.EditorReader, Log.LogLevel.Error);
            throw new Exception("ReadProcessMemory Error. Cancelled reading.");
        }
        return result;
    }

    private static int SafeBitConverterToInt32(byte[] value, int startIndex, string varName = "")
    {
        const int MAX = 1000000;
        int result = BitConverter.ToInt32(value, startIndex);
        if (result < 0 || result > MAX)
        {
            Log.ConsoleLog("BitConverterToInt32 error: " + varName + "=" + result, Log.LogType.EditorReader, Log.LogLevel.Error);
            throw new Exception("ReadProcessMemory Error. Reading cancelled.");
        }
        return result;
    }

    private static void EnsureBuffer(ref byte[] buf, int size)
    {
        if (buf == null || buf.Length < size)
        {
            buf = new byte[size];
        }
    }

    /// <summary>目标进程的页大小（读失败时按页对齐切分重试用）。</summary>
    private const int TargetPageSize = 4096;

    /// <summary>
    /// 分段读取：先整块读，失败则按页边界切分后逐段读。
    /// <para /><b>为什么需要它</b>：osu! 的物件结构体跨页时，后一页可能已被回收
    /// （编辑器增删物件会让堆在页边界上增长/收缩，压在页尾的那个物件就会有一段读不到）。
    /// 单次 RPM 只要有任何一页不可读就整体失败，于是那个物件永远读不出来 ——
    /// 全量读取每 tick 都抛异常，客户端就永久卡在"重试中"。
    /// 分段读取能把这些"大部分仍可读"的物件救回来：实测 0xB981FEF0 这个物件
    /// 336 字节里只有前 256 字节可读，而普通物件真正用到的字段全在前 148 字节内。
    /// </summary>
    /// <returns>true 表示至少读到了前 <paramref name="minRequired"/> 字节。</returns>
    private bool SafeReadSegmented(IntPtr address, byte[] buf, int size, int minRequired)
    {
        // 1) 常规情况：一次读成功
        if (ReadProcessMemory(TargetHandle, address, buf, size, ref bytesRead))
        {
            return true;
        }

        // 2) 失败：按页边界切分，逐段读进临时缓冲再拼起来
        EnsureBuffer(ref buf, size);
        byte[] tmp = new byte[Math.Min(TargetPageSize, size)];
        int offset = 0;

        while (offset < size)
        {
            long here = address.ToInt64() + offset;
            long pageEnd = ((here / TargetPageSize) + 1) * TargetPageSize;
            int chunk = (int)Math.Min(pageEnd - here, size - offset);
            if (chunk <= 0) break;

            if (!ReadProcessMemory(TargetHandle, IntPtr.Add(address, offset), tmp, chunk, ref bytesRead))
            {
                if (offset >= minRequired) break;   // 需要的字段已经读到了，后面的字节用不上
                Log.ConsoleLog("SafeReadSegmented: unreadable at " + (address.ToInt64() + offset) +
                               " (need " + minRequired + " bytes, got " + offset + ")", Log.LogType.EditorReader, Log.LogLevel.Error);
                throw new Exception("ReadProcessMemory Error. Cancelled reading.");
            }

            Buffer.BlockCopy(tmp, 0, buf, offset, chunk);
            offset += chunk;
        }

        return offset >= minRequired;
    }

    private string ReadString(IntPtr pString)
    {
        if (pString == IntPtr.Zero)
        {
            return null;
        }

        SafeReadProcessMemory(TargetHandle, pString + 4, buffer4, 4, ref bytesRead);
        int num = SafeBitConverterToInt32(buffer4, 0, "ReadString num");
        byte[] array = new byte[2 * num];
        SafeReadProcessMemory(TargetHandle, pString + 8, array, 2 * num, ref bytesRead);
        char[] array2 = new char[num];
        Buffer.BlockCopy(array, 0, array2, 0, 2 * num);
        return new string(array2);
    }

    private static byte[] ToByteArray(string hexString)
    {
        byte[] array = new byte[hexString.Length / 2];
        for (int i = 0; i < hexString.Length - 1; i += 2)
        {
            array[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
        }

        return array;
    }

    private static bool PatternCheck(byte[] searchBuffer, byte[] arrPattern, int nOffset)
    {
        if (nOffset + arrPattern.Length > searchBuffer.Length)
        {
            return false;
        }

        for (int i = 0; i < arrPattern.Length; i++)
        {
            if (arrPattern[i] != 238 && arrPattern[i] != searchBuffer[nOffset + i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 诊断用：用**主扫描自己的代码路径**（同一模式串、同一 IsPlausibleEditor、同一分块逻辑）
    /// 只扫一个指定区域。用于回答"主扫描为什么没在某个区域里找到签名"：
    /// 如果这里能命中，说明区域本身没问题，是扫描顺序/覆盖的问题；
    /// 如果这里也不命中，说明主路径读取该区域的方式（分块/地址推进）有问题。
    /// </summary>
    /// <returns>找到的候选地址，未找到返回 IntPtr.Zero；并输出过程信息。</returns>
    public string ProbeRegionWithMainPath(long regionBase, long regionSize)
    {
        const int ReadChunkSize = 8 * 1024 * 1024;
        byte[] array = ToByteArray("230000001400000019000000eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee0C000000eeeeeeeeeeeeeeeeeeeeeeeeee00");
        int overlap = array.Length - 1;
        byte[] scanBuffer = null;
        byte[] probe16 = new byte[16];
        byte[] probe4 = new byte[4];
        IntPtr read = IntPtr.Zero;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"  区域 0x{regionBase:X8} size=0x{regionSize:X} ({regionSize / 1024.0 / 1024.0:F1} MB), 模式={array.Length}字节");
        if (regionSize > MaxScanRegionSize)
        {
            sb.AppendLine($"  !! 区域超过 MaxScanRegionSize({MaxScanRegionSize}) 会被主扫描直接跳过");
            return sb.ToString();
        }

        long offset = 0;
        int chunks = 0, failed = 0, hits = 0, plausReject = 0;
        IntPtr found = IntPtr.Zero;
        string? rejectReason = null;
        while (offset < regionSize)
        {
            int chunkSize = (int)Math.Min(ReadChunkSize, regionSize - offset);
            int readSize = chunkSize;
            if (offset + chunkSize < regionSize) readSize += overlap;

            EnsureBuffer(ref scanBuffer, readSize);
            if (!ReadProcessMemory(TargetHandle, IntPtr.Add((IntPtr)regionBase, (int)offset), scanBuffer, readSize, ref read))
            {
                failed++;
                sb.AppendLine($"  块 offset=0x{offset:X} size={readSize} 读取失败");
                offset += chunkSize;
                continue;
            }

            chunks++;
            int bytesToScan = (int)read;
            for (int j = 0; j <= bytesToScan - array.Length; j += 4)
            {
                if (!PatternCheck(scanBuffer, array, j)) continue;
                hits++;
                IntPtr candidate = new IntPtr(regionBase + offset + j - 160);
                if (!IsPlausibleEditor(candidate, probe16, probe4, out string why))
                {
                    plausReject++;
                    rejectReason ??= $"候选 0x{candidate.ToInt64():X} 被拒: {why}";
                    continue;
                }
                found = candidate;
                sb.AppendLine($"  !! 命中并通过校验: pEditor=0x{candidate.ToInt64():X} (偏移 0x{offset + j:X})");
            }

            if (bytesToScan < readSize) break;
            offset += chunkSize;
        }

        sb.AppendLine($"  分块: 成功={chunks} 失败={failed}; 签名命中={hits}; 候选被拒={plausReject}");
        if (rejectReason != null) sb.AppendLine("  " + rejectReason);
        if (found == IntPtr.Zero && hits == 0)
        {
            sb.AppendLine("  => 主路径在这个区域里没有找到签名");
        }
        return sb.ToString();
    }

    /// <summary>
    /// 查找编辑器对象地址。
    /// <para /><b>实现要点（Wine 兼容）</b>：这里刻意采用**最简单的顺序扫描**：
    /// 按区域逐个读、逐个找签名，找到就返回。不再使用"环形扫描游标 + 区域退避 +
    /// 跨线程元组回传"那套机制 —— 那套机制在 Wine 上会让扫描从中间开始并漏掉前面的区域
    /// （实测：166 个候选区域只扫了 152 个，恰好漏掉含编辑器对象的那个）。
    /// 顺序扫描的等价实现已在 Wine 上独立验证：同一区域、同一模式串、同一候选校验，一次命中。
    /// <para />超时保护保留：扫描仍在线程里执行，卡住就放弃本次尝试，但下次仍然从第 0 个区域开始。
    /// </summary>
    private IntPtr FindEditorAddress()
    {
        Log.ConsoleLog("FindEditorAddress: start enumerating memory regions.", Log.LogType.EditorReader, Log.LogLevel.Info);

        Internals internals = new Internals();
        internals.MemInfo(TargetHandle);
        int regionCount = internals.MemReg.Count;
        lastScanInternals = internals;

        Log.ConsoleLog("FindEditorAddress: " + regionCount + " region(s) to scan. " +
                       "枚举: 查询=" + internals.DiagQueried + " 命中过滤=" + internals.DiagMatched +
                       " 结束原因=" + internals.DiagStopReason, Log.LogType.EditorReader, Log.LogLevel.Info);

        if (regionCount == 0)
        {
            string detail = "区域枚举结果为空：查询=" + internals.DiagQueried +
                            " 命中过滤=" + internals.DiagMatched +
                            " 结束原因=" + internals.DiagStopReason +
                            " VirtualQueryEx错误码=" + internals.DiagLastError;
            Log.ConsoleLog("FindEditorAddress: " + detail, Log.LogType.EditorReader, Log.LogLevel.Error);
            throw new InvalidOperationException("内存区域枚举失败（0 个可扫描区域）。" + detail);
        }

        if (Interlocked.Increment(ref liveScanThreads) > MaxLiveScanThreads)
        {
            Interlocked.Decrement(ref liveScanThreads);
            throw new InvalidOperationException("Too many memory scans are still running; skipping this attempt.");
        }

        // 扫描结果用独立的 volatile 字段回传：跨线程读元组在放弃线程时可能读到半写状态
        lastEditorCandidate = IntPtr.Zero;
        Exception? scanError = null;
        Thread scanThread = new Thread(() =>
        {
            try { lastEditorCandidate = ScanForEditorAddress(internals); }
            catch (Exception ex) { scanError = ex; }
            finally { Interlocked.Decrement(ref liveScanThreads); }
        });
        scanThread.Start();

        if (!scanThread.Join(ScanTimeoutMs))
        {
            long stalled = Interlocked.Read(ref scanningRegionAddress);
            Log.ConsoleLog("FindEditorAddress: scan aborted after " + ScanTimeoutMs + " ms, stalled near 0x" + stalled.ToString("X") +
                           " (" + DiagScanRegionsScanned + "/" + regionCount + " regions done). Next attempt restarts from region 0.",
                           Log.LogType.EditorReader, Log.LogLevel.Warning);
            throw new InvalidOperationException("Memory scan aborted: ReadProcessMemory did not return in time.");
        }

        if (scanError != null) throw scanError;

        // 记住命中的区域：下一次重绑时该区域会被优先扫描（实测冷扫描 694ms -> 热扫描 63ms）
        if (lastEditorCandidate != IntPtr.Zero)
        {
            lastFoundRegionBase = scanningRegionAddress;
        }

        return lastEditorCandidate;
    }

    /// <summary>扫描线程找到的候选地址（volatile，避免跨线程元组回传的半写状态）。</summary>
    private volatile IntPtr lastEditorCandidate;

    /// <summary>
    /// 在目标进程里扫描编辑器签名。
    /// <para />扫描线程不共享任何实例缓冲区：超时被放弃的线程可能仍在读内存，
    /// 若与主线程共用 buffer/bytesRead 会读到互相覆盖的数据。
    /// </summary>
    /// <summary>
    /// 顺序扫描编辑器签名。
    /// <para /><b>为什么是顺序扫描</b>：等价实现（同样的模式串、同样的 IsPlausibleEditor、
    /// 同样的分块读取）已在 Wine 上独立复扫验证——对含签名的那 9.3MB 区域一次命中并通过校验。
    /// 而原先那套"环形游标 + 区域退避 + 跨线程元组回传"在 Wine 上会让扫描从列表中间起步、
    /// 只覆盖前 152/166 个区域，恰好漏掉含编辑器对象的那个区域，于是永远报 No active editor found。
    /// <para />因此这里去掉那些机制：每次尝试都从第 0 个区域开始，逐个读完。
    /// 上次命中的区域会被提到最前面（省时间），但不影响覆盖范围。
    /// 超时由调用方 FindEditorAddress 控制。
    /// </summary>
    private IntPtr ScanForEditorAddress(Internals internals)
    {
        const int ReadChunkSize = 8 * 1024 * 1024;

        byte[] array = ToByteArray("230000001400000019000000eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee0C000000eeeeeeeeeeeeeeeeeeeeeeeeee00");
        int overlap = array.Length - 1;
        byte[] scanBuffer = null;
        byte[] probe16 = new byte[16];
        byte[] probe4 = new byte[4];
        IntPtr read = IntPtr.Zero;
        int total = internals.MemReg.Count;

        // 诊断：全部从同一处开始计数
        DiagScanRegionsScanned = 0;
        DiagScanRegionsSkipped = 0;
        DiagScanChunksOk = 0;
        DiagScanChunksFailed = 0;
        DiagScanSignatureHits = 0;
        DiagScanFirstReject = null;
        DiagScanFirstReadFailure = null;
        DiagSkippedTooLarge.Clear();
        DiagNeverScannedRegions.Clear();
        DiagScanOrderCount = total;
        DiagScanDidCount = 0;
        DiagMemRegCount = total;

        // 扫描顺序：上次命中的区域优先，其余按原顺序（不改变覆盖范围）
        var order = new List<int>(total);
        long preferBase = lastFoundRegionBase;
        int preferIndex = -1;
        if (preferBase != 0)
        {
            for (int i = 0; i < total; i++)
            {
                if (internals.MemReg[i].BaseAddress.ToInt64() == preferBase) { preferIndex = i; break; }
            }
        }
        if (preferIndex >= 0) order.Add(preferIndex);
        for (int i = 0; i < total; i++)
        {
            if (i != preferIndex) order.Add(i);
        }

        List<int> skippedIndices = new();

        foreach (int i in order)
        {
            Internals.MEMORY_BASIC_INFORMATION mbi = internals.MemReg[i];
            long regionBase = mbi.BaseAddress.ToInt64();
            long regionSize = mbi.RegionSize.ToInt64();

            if (regionSize > MaxScanRegionSize)
            {
                DiagScanRegionsSkipped++;
                DiagSkippedTooLarge.Add($"0x{regionBase:X8} size=0x{regionSize:X} ({regionSize / 1024 / 1024} MB)");
                Log.ConsoleLog("FindEditorAddress: skip region 0x" + regionBase.ToString("X") + " (size " + regionSize + " > " + MaxScanRegionSize + ")", Log.LogType.EditorReader, Log.LogLevel.Warning);
                continue;
            }

            DiagScanRegionsScanned++;
            DiagScanDidCount = (int)DiagScanRegionsScanned;
            Interlocked.Exchange(ref scanningRegionAddress, regionBase);

            long offset = 0;
            while (offset < regionSize)
            {
                int chunkSize = (int)Math.Min(ReadChunkSize, regionSize - offset);
                int readSize = chunkSize;
                if (offset + chunkSize < regionSize) readSize += overlap;

                EnsureBuffer(ref scanBuffer, readSize);
                if (!ReadProcessMemory(TargetHandle, IntPtr.Add(mbi.BaseAddress, (int)offset), scanBuffer, readSize, ref read))
                {
                    DiagScanChunksFailed++;
                    if (DiagScanFirstReadFailure == null)
                    {
                        DiagScanFirstReadFailure = $"region=0x{regionBase:X} offset={offset} size={readSize} (regionSize={regionSize})";
                    }
                    offset += chunkSize;
                    continue;
                }

                DiagScanChunksOk++;
                int bytesToScan = (int)read;
                for (int j = 0; j <= bytesToScan - array.Length; j += 4)
                {
                    if (!PatternCheck(scanBuffer, array, j)) continue;

                    DiagScanSignatureHits++;
                    IntPtr candidate = new IntPtr(regionBase + offset + j - 160);
                    string reject;
                    if (!IsPlausibleEditor(candidate, probe16, probe4, out reject))
                    {
                        DiagScanFirstReject ??= $"candidate=0x{candidate.ToInt64():X} {reject}";
                        continue;
                    }

                    scanningRegionIndex = i;
                    Log.ConsoleLog("FindEditorAddress: found at 0x" + candidate.ToInt64().ToString("X") +
                                   " (region " + i + "/" + total + ")", Log.LogType.EditorReader, Log.LogLevel.Info);
                    return candidate;
                }

                offset += chunkSize;
                if ((long)read < readSize) break; // 区域剩余部分不可读，停止该区域
            }
        }

        Log.ConsoleLog("FindEditorAddress: 扫描统计: 区域=" + DiagScanRegionsScanned + "/" + total +
                       " 超大跳过=" + DiagScanRegionsSkipped +
                       " 块成功=" + DiagScanChunksOk + " 块失败=" + DiagScanChunksFailed +
                       " 签名命中=" + DiagScanSignatureHits +
                       " 首个候选被拒=" + (DiagScanFirstReject ?? "无") +
                       " 首个读失败=" + (DiagScanFirstReadFailure ?? "无"), Log.LogType.EditorReader, Log.LogLevel.Warning);

        Log.ConsoleLog("FindEditorAddress: no active editor found.", Log.LogType.EditorReader, Log.LogLevel.Warning);
        throw new InvalidOperationException("No active editor found.");
    }

    /// <summary>
    /// 候选编辑器的可信度校验：签名命中只说明内存里有这段字节。
    /// osu! 退出 test mode 后会重建编辑器对象，堆里可能残留同样能通过签名的死副本，
    /// 选错副本会造成之后持续读取失败。这里额外校验编辑器状态、HOM 与物件列表。
    /// <para />失败返回 false（不抛异常），让扫描继续找下一个候选。
    /// </summary>
    private bool IsPlausibleEditor(IntPtr pE, byte[] probe16, byte[] probe4)
        => IsPlausibleEditor(pE, probe16, probe4, out _);

    /// <summary>带原因输出的版本：扫描失败时能说清"候选为什么被拒"。</summary>
    private bool IsPlausibleEditor(IntPtr pE, byte[] probe16, byte[] probe4, out string reject)
    {
        reject = "";
        if (pE == IntPtr.Zero) { reject = "候选地址为 0"; return false; }

        IntPtr read = IntPtr.Zero;

        // 编辑器状态字段（与 EditorNeedsReload 的判定一致）
        if (!ReadProcessMemory(TargetHandle, pE + 160, probe16, 16, ref read)) { reject = "pE+160 读取失败"; return false; }
        int f0 = BitConverter.ToInt32(probe16, 0), f4 = BitConverter.ToInt32(probe16, 4), f8 = BitConverter.ToInt32(probe16, 8);
        if (f0 != 35 || f4 != 20 || f8 != 25) { reject = $"状态字段不符 ({f0},{f4},{f8})"; return false; }

        // HOM 与物件列表：死副本通常指向已释放/清零的内存
        if (!ReadProcessMemory(TargetHandle, pE + 28, probe4, 4, ref read)) { reject = "pE+28 读取失败"; return false; }
        IntPtr pHom = ToIntPtr(probe4, 0);
        if (pHom == IntPtr.Zero) { reject = "HOM 为空"; return false; }

        if (!ReadProcessMemory(TargetHandle, pHom + 72, probe4, 4, ref read)) { reject = $"HOM(0x{pHom.ToInt64():X})+72 读取失败"; return false; }
        IntPtr pObjectsList = ToIntPtr(probe4, 0);
        if (pObjectsList == IntPtr.Zero) { reject = "物件列表为空"; return false; }

        if (!ReadProcessMemory(TargetHandle, pObjectsList, probe16, 16, ref read)) { reject = $"物件列表(0x{pObjectsList.ToInt64():X})头读取失败"; return false; }
        IntPtr pObjectsArray = ToIntPtr(probe16, 4);
        int count = BitConverter.ToInt32(probe16, 12);
        if (pObjectsArray == IntPtr.Zero || count < 0 || count > 1000000)
        {
            reject = $"物件表非法 items=0x{pObjectsArray.ToInt64():X} count={count}";
            return false;
        }

        // 上面几步只证明了"指针链结构上说得通"。死副本的堆块在被复用前内容不会被清零，
        // 所以一整条指针链都可能仍然自洽 —— 必须再实际跟着指针读一次物件才算数。
        if (!ObjectsReadableFromChain(pHom, probe16, probe4))
        {
            reject = $"指针链可读性检查未通过 (count={count})";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 从 HOM 出发跟着指针链检查"物件列表里的物件真的能读"。
    /// <para />这是区分"活着的编辑器"与"堆里残留的死副本"的关键一步：
    /// 死副本的指针链能通过结构校验，但它指向的物件已经被释放，
    /// 于是候选校验/重载判断都会误判，之后每次全量读取都抛异常 ——
    /// 表现就是"退出再进入编辑器后长时间一直读取失败、重绑也救不回来"。
    /// </summary>
    /// <returns>true 表示物件列表可读且至少有一个物件能读出来。</returns>
    private bool ObjectsReadableFromChain(IntPtr pHom, byte[] probe16, byte[] probe4)
    {
        IntPtr read = IntPtr.Zero;

        if (!ReadProcessMemory(TargetHandle, pHom + 72, probe4, 4, ref read)) return false;
        IntPtr pObjectsList = ToIntPtr(probe4, 0);
        if (pObjectsList == IntPtr.Zero) return false;

        if (!ReadProcessMemory(TargetHandle, pObjectsList, probe16, 16, ref read)) return false;
        IntPtr pObjectsArray = ToIntPtr(probe16, 4);
        int count = BitConverter.ToInt32(probe16, 12);
        if (pObjectsArray == IntPtr.Zero || count < 0 || count > 1000000) return false;

        // 编辑器里总有物件可读；numObjects==0 只可能出现在"列表正在被重建"的瞬间，
        // 不作为判定依据（那种瞬态由上层重试覆盖）。
        if (count == 0) return true;

        // 采样首、中、尾三个物件：跟着指针读 4 字节即可确认整条链没被复用
        int[] samples = { 0, count / 2, count - 1 };
        int readable = 0;
        foreach (int index in samples)
        {
            if (!ReadProcessMemory(TargetHandle, pObjectsArray + 8 + TargetPointerSize * index, probe4, 4, ref read)) continue;
            IntPtr pObject = ToIntPtr(probe4, 0);
            if (pObject == IntPtr.Zero) continue;
            if (ReadProcessMemory(TargetHandle, pObject, probe4, 4, ref read)) readable++;
        }

        return readable > 0;
    }

    /// <summary>
    /// 绑定 osu! 进程。切换到新进程时清空扫描状态：地址空间已完全不同，
    /// 旧的退避表/命中区域/扫描游标都失去意义。
    /// </summary>
    public void SetProcess(Process forceProcess = null)
    {
        if (forceProcess != null)
        {
            this.process = forceProcess;
            ResetScanState();
            return;
        }

        Process[] processesByName = Process.GetProcessesByName("osu!");
        foreach (Process process in processesByName)
        {
            if (process.MainModule.ModuleName == "osu!.exe" && process.MainModule.FileVersionInfo.ProductName == "osu!")
            {
                this.process = process;
                ResetScanState();
                return;
            }
        }

        throw new InvalidOperationException("No process for osu!.exe found.");
    }

    private void ResetScanState()
    {
        lastFoundRegionBase = 0;
        Interlocked.Exchange(ref scanningRegionAddress, 0);
    }

    public bool ProcessNeedsReload()
    {
        if (process != null)
        {
            return process.HasExited;
        }

        return true;
    }

    /// <summary>
    /// 当前连接的 osu! 进程 ID（用于前台窗口判断，避免每次创建 Process 对象）。
    /// </summary>
    public int? OsuProcessId => process?.Id;

    public string ProcessTitle()
    {
        if (ProcessNeedsReload())
        {
            return "";
        }

        process.Refresh();
        return process.MainWindowTitle;
    }

    private IntPtr ToIntPtr(byte[] value, int startIndex)
    {
        // 目标进程（osu!）是 32 位：必须按 4 字节读取，不能跟随 viewer 自身的 IntPtr.Size。
        // <para /><b>必须按无符号读</b>：指针最高位为 1（地址 >= 0x80000000，编辑器堆长大后会用到）
        // 时，ToInt32 得到负数，在 x64 宿主上转 IntPtr 会符号扩展成 0xFFFFFFFF8xxxxxxx，
        // ReadProcessMemory 立刻失败 —— 表现就是"编辑一会儿之后某个物件开始永久读不到，
        // 整个全量读取每 tick 抛异常，客户端卡在重试中"。ToUInt32 零扩展，得到正确的 0x8xxxxxxx。
        if (TargetPointerSize > 4)
        {
            return (IntPtr)(long)BitConverter.ToUInt64(value, startIndex);
        }

        return (IntPtr)(long)BitConverter.ToUInt32(value, startIndex);
    }

    public void SetEditor()
    {
        pEditor = FindEditorAddress();
    }

    /// <summary>
    /// 清空已缓存的编辑器地址，强制下一次检查时重新扫描内存。
    /// 同时清掉扫描退避表/命中区域/游标：手动重绑时应当从干净状态重新开始。
    /// </summary>
    public void ResetEditor()
    {
        pEditor = IntPtr.Zero;
        ResetScanState();
    }

    public bool EditorNeedsReload()
    {
        if (ProcessNeedsReload())
        {
            return true;
        }
        if (pEditor == IntPtr.Zero)
        {
            return true;
        }

        // 读取失败时必须视为需要重载，不能依赖上次成功读取残留的 buffer 值做判断
        // （osu! 从 test mode 退出重建 editor 后，旧 pEditor 可能已失效，ReadProcessMemory 失败但 buffer 仍是旧签名）
        if (!ReadProcessMemory(TargetHandle, pEditor + 160, buffer16, 16, ref bytesRead) ||
            !ReadProcessMemory(TargetHandle, pEditor + 208, buffer4, 4, ref bytesRead))
        {
            return true;
        }

        if (BitConverter.ToBoolean(buffer4, 1) || BitConverter.ToInt32(buffer16, 0) != 35 || BitConverter.ToInt32(buffer16, 4) != 20 || BitConverter.ToInt32(buffer16, 8) != 25)
        {
            return true;
        }

        return EditorMissingObjects(pEditor);
    }

    private bool EditorMissingObjects(IntPtr pE)
    {
        try
        {
            SafeReadProcessMemory(TargetHandle, pE + 28, buffer4, 4, ref bytesRead);
            IntPtr pHom = ToIntPtr(buffer4, 0);
            if (pHom == IntPtr.Zero)
            {
                return true;
            }

            SafeReadProcessMemory(TargetHandle, pHom + 72, buffer4, 4, ref bytesRead);
            IntPtr pObjectsList = ToIntPtr(buffer4, 0);
            if (pObjectsList == IntPtr.Zero)
            {
                return true;
            }

            // 列表为空是合法状态（新建难度 / 清空谱面后列表会有若干秒是空的），
            // 此时 List<T> 的 _items 可能是 null，绝不能据此判定"需要重载"。
            // 否则每 tick 都会触发一次后台重扫 + 重新绑定，日志刷满
            // "Editor needs Reload."，而内存读取始终停摆 —— 新建谱面后的死循环就是这么来的。
            if (!ReadProcessMemory(TargetHandle, pObjectsList, buffer16, 16, ref bytesRead))
            {
                return true;
            }
            int count = BitConverter.ToInt32(buffer16, 12);
            if (count < 0 || count > 1000000)
            {
                return true;
            }
            if (count == 0)
            {
                return false;
            }

            // 光看指针非空不够：退出/重进编辑器后，旧编辑器对象常常整条指针链都还自洽，
            // 但链上的物件已经被释放。实际跟指针读一次物件，才能识别出这种死副本。
            // Wine 下不这么做：那里 ReadProcessMemory 会间歇性失败，一旦据此判定"需要重载"，
            // 就会每 tick 触发一次后台重扫 + 重新绑定，日志刷满 "Editor needs Reload."，
            // 而读取始终起不来（读取层面的偶发失败由上层重试/退避负责）。
            if (IsWineTarget)
            {
                return false;
            }

            return !ObjectsReadableFromChain(pHom, buffer16, buffer4);
        }
        catch
        {
            // 读取失败视为"对象缺失"：EditorNeedsReload 会据此返回 true（需要重载），
            // FindEditorAddress 扫描中则跳过该候选继续扫描，避免假目标中断整次扫描
            return true;
        }
    }

    public int EditorTime()
    {
        SafeReadProcessMemory(TargetHandle, pEditor + 176, buffer16, 16, ref bytesRead);
        return (BitConverter.ToInt32(buffer16, 8) + BitConverter.ToInt32(buffer16, 12)) / 2;
    }

    public void SetHOM()
    {
        SafeReadProcessMemory(TargetHandle, pEditor + 28, buffer4, 4, ref bytesRead);
        pHOM = ToIntPtr(buffer4, 0);
        SafeReadProcessMemory(TargetHandle, pEditor + 112, buffer4, 4, ref bytesRead);
        pCompose = ToIntPtr(buffer4, 0);
    }

    public void ReadHOM()
    {
        // HOM：一次读 80 字节（objectRadius/stackOffset/书签列表/物件列表都在前 76 字节内）
        EnsureBuffer(ref buffer, 80);
        SafeReadProcessMemory(TargetHandle, pHOM, buffer, 80, ref bytesRead);
        objectRadius = BitConverter.ToSingle(buffer, 24);
        stackOffset = BitConverter.ToSingle(buffer, 44);
        pBookmarksL = ToIntPtr(buffer, 56);
        pObjectsL = ToIntPtr(buffer, 72);

        // Compose：一次读 80 字节覆盖两个列表指针（原实现分两次读 80 + 256）。
        // 同一次调用里少发几次 RPM，在 Wine 下更稳（Wine 的 ReadProcessMemory 行为与真实
        // Windows 有差异，多段读取更容易踩到边界）。
        EnsureBuffer(ref buffer, 80);
        SafeReadProcessMemory(TargetHandle, pCompose, buffer, 80, ref bytesRead);
        pClipboardL = ToIntPtr(buffer, 48);
        pSelectedL = ToIntPtr(buffer, 72);
    }

    public void FetchBookmarks()
    {
        SafeReadProcessMemory(TargetHandle, pBookmarksL, buffer16, 16, ref bytesRead);
        pBookmarksA = ToIntPtr(buffer16, 4);
        numBookmarks = SafeBitConverterToInt32(buffer16, 12, "numBookmarks");
        EnsureBuffer(ref buffer, 4 * numBookmarks);
        bookmarks = new int[numBookmarks];
        SafeReadProcessMemory(TargetHandle, pBookmarksA + 8, buffer, 4 * numBookmarks, ref bytesRead);
        Buffer.BlockCopy(buffer, 0, bookmarks, 0, 4 * numBookmarks);
    }

    /// <summary>
    /// 轻量读取当前物件/控制点/书签数量，用于快速判断 beatmap 是否发生变化。
    /// 不会抛异常；指针无效或读取失败时返回 false。
    /// </summary>
    public bool TryReadCounts(out int numObjects, out int numControlPoints, out int numBookmarks)
    {
        numObjects = numControlPoints = numBookmarks = -1;
        if (process == null || pObjectsL == IntPtr.Zero || pControlPointsL == IntPtr.Zero || pBookmarksL == IntPtr.Zero)
        {
            return false;
        }

        bool ok = true;
        ok &= ReadListCount(pObjectsL, out numObjects);
        ok &= ReadListCount(pControlPointsL, out numControlPoints);
        ok &= ReadListCount(pBookmarksL, out numBookmarks);
        return ok;
    }

    /// <summary>
    /// 轻量读取当前选中物件在主物件列表中的下标（0..numObjects-1，顺序不保证与主列表一致）。
    /// 高频 tick 时使用，避免等待 150ms 一次的全量读取。
    /// 不会抛异常；指针无效、读取失败或主物件指针表未就绪时返回 false（调用方沿用旧状态）。
    /// </summary>
    public bool TryReadSelectedIndices(out int[] selectedIndices)
    {
        selectedIndices = Array.Empty<int>();
        if (process == null || pSelectedL == IntPtr.Zero || masterIndexByPointer == null)
        {
            return false;
        }

        if (!ReadProcessMemory(TargetHandle, pSelectedL, buffer16, 16, ref bytesRead))
        {
            return false;
        }

        IntPtr pSelA = ToIntPtr(buffer16, 4);
        int selCount = BitConverter.ToInt32(buffer16, 12);
        if (selCount < 0 || selCount > 1000000)
        {
            return false;
        }

        if (selCount == 0)
        {
            return true;
        }

        if (pSelA == IntPtr.Zero)
        {
            return false;
        }

        EnsureBuffer(ref pSelected, 4 * selCount);
        if (!ReadProcessMemory(TargetHandle, pSelA + 8, pSelected, 4 * selCount, ref bytesRead))
        {
            return false;
        }

        int[] result = new int[selCount];
        int found = 0;
        for (int i = 0; i < selCount; i++)
        {
            IntPtr objPtr = ToIntPtr(pSelected, 4 * i);
            if (masterIndexByPointer.TryGetValue(objPtr, out int index))
            {
                result[found++] = index;
            }
        }

        if (found != selCount)
        {
            Array.Resize(ref result, found);
        }

        selectedIndices = result;
        return true;
    }

    private bool ReadListCount(IntPtr pList, out int count)
    {
        count = -1;
        if (!ReadProcessMemory(TargetHandle, pList, buffer16, 16, ref bytesRead))
        {
            return false;
        }

        int value = BitConverter.ToInt32(buffer16, 12);
        if (value < 0 || value > 1000000)
        {
            return false;
        }

        count = value;
        return true;
    }

    public void SetBeatmap()
    {
        SafeReadProcessMemory(TargetHandle, pHOM + 48, buffer4, 4, ref bytesRead);
        pBeatmap = ToIntPtr(buffer4, 0);
    }

    public void ReadBeatmap()
    {
        EnsureBuffer(ref buffer, 320);
        SafeReadProcessMemory(TargetHandle, pBeatmap, buffer, 320, ref bytesRead);
        SliderMultiplier = BitConverter.ToDouble(buffer, 8);
        SliderTickRate = BitConverter.ToDouble(buffer, 16);
        ApproachRate = BitConverter.ToSingle(buffer, 44);
        CircleSize = BitConverter.ToSingle(buffer, 48);
        HPDrainRate = BitConverter.ToSingle(buffer, 52);
        OverallDifficulty = BitConverter.ToSingle(buffer, 56);
        ContainingFolder = ReadString(ToIntPtr(buffer, 120));
        Filename = ReadString(ToIntPtr(buffer, 144));
        BeatmapVersion = BitConverter.ToInt32(buffer, 216);
        PreviewTime = BitConverter.ToInt32(buffer, 288);
        StackLeniency = BitConverter.ToSingle(buffer, 296);
        TimelineZoom = BitConverter.ToSingle(buffer, 304);
    }

    public void SetControlPoints()
    {
        EnsureBuffer(ref buffer, 192);
        SafeReadProcessMemory(TargetHandle, pBeatmap, buffer, 192, ref bytesRead);
        pControlPointsL = ToIntPtr(buffer, 176);
        SafeReadProcessMemory(TargetHandle, pControlPointsL, buffer16, 16, ref bytesRead);
        pControlPointsA = ToIntPtr(buffer16, 4);
        numControlPoints = SafeBitConverterToInt32(buffer16, 12, "numControlPoints");
        EnsureBuffer(ref pControlPoints, 4 * numControlPoints);
        SafeReadProcessMemory(TargetHandle, pControlPointsA + 8, pControlPoints, 4 * numControlPoints, ref bytesRead);
    }

    public void ReadControlPoints()
    {
        controlPoints = new List<ControlPoint>();
        for (int i = 0; i < numControlPoints; i++)
        {
            controlPoints.Add(ReadControlPoint(ToIntPtr(pControlPoints, 4 * i)));
        }
    }

    private ControlPoint ReadControlPoint(IntPtr pControlPoint)
    {
        SafeReadProcessMemory(TargetHandle, pControlPoint, bufferCp, 48, ref bytesRead);
        return new ControlPoint
        {
            BeatLength = BitConverter.ToDouble(bufferCp, 4),
            Offset = BitConverter.ToDouble(bufferCp, 12),
            CustomSamples = BitConverter.ToInt32(bufferCp, 20),
            SampleSet = BitConverter.ToInt32(bufferCp, 24),
            TimeSignature = BitConverter.ToInt32(bufferCp, 28),
            Volume = BitConverter.ToInt32(bufferCp, 32),
            EffectFlags = BitConverter.ToInt32(bufferCp, 36),
            TimingChange = BitConverter.ToBoolean(bufferCp, 40)
        };
    }

    public void SetObjects()
    {
        SafeReadProcessMemory(TargetHandle, pObjectsL, buffer16, 16, ref bytesRead);
        pObjectsA = ToIntPtr(buffer16, 4);
        numObjects = SafeBitConverterToInt32(buffer16, 12, "numObjects");
        EnsureBuffer(ref pObjects, 4 * numObjects);
        SafeReadProcessMemory(TargetHandle, pObjectsA + 8, pObjects, 4 * numObjects, ref bytesRead);

        // 建立指针 -> 主物件下标映射，供高频选中读取使用
        if (masterIndexByPointer == null || masterIndexByPointer.Count != numObjects)
        {
            masterIndexByPointer = new Dictionary<IntPtr, int>(numObjects);
        }
        else
        {
            masterIndexByPointer.Clear();
        }
        for (int i = 0; i < numObjects; i++)
        {
            masterIndexByPointer[ToIntPtr(pObjects, 4 * i)] = i;
        }
    }

    public void ReadObjects(bool fetchHitSound = true)
    {
        var hitObjects = new List<HitObject>();
        DiagReadObjectFailure = null;
        int nullPointers = 0;
        for (int i = 0; i < numObjects; i++)
        {
            IntPtr pObject = ToIntPtr(pObjects, 4 * i);
            if (pObject == IntPtr.Zero)
            {
                // 编辑器重建物件列表的瞬间，指针数组里会出现空槽（实测 index=0 ptr=0x0）。
                // 那不是"读到垃圾"，只是这一条还没写进去，跳过即可；
                // 若当成错误抛出，整个全量读取就会失败，客户端开始退避/重绑。
                nullPointers++;
                continue;
            }

            try
            {
                hitObjects.Add(ReadObject(pObject, fetchHitSound));
            }
            catch (Exception ex)
            {
                // 把"读到第几个物件、地址是多少"记下来：否则只剩一句笼统的 RPM 错误无从定位
                DiagReadObjectFailure = $"index={i} ptr=0x{pObject.ToInt64():X} numObjects={numObjects} : {ex.Message}";
                Log.ConsoleLog("ReadObjects failed: " + DiagReadObjectFailure, Log.LogType.EditorReader, Log.LogLevel.Error);
                throw;
            }
        }

        DiagNullObjectPointers = nullPointers;
        if (nullPointers > 0)
        {
            Log.ConsoleLog($"ReadObjects: skipped {nullPointers} null pointer(s) out of {numObjects}.",
                Log.LogType.EditorReader, Log.LogLevel.Warning);
        }

        // 诊断：统计哪些字段越界（EditorReaderHarness 使用，正常运行时开销为 0）
        for (int i = 0; i < hitObjects.Count; i++)
        {
            int reason = RejectReason(hitObjects[i]);
            if (reason >= 0)
            {
                DiagObjectRejectReasons[reason]++;
                DiagLastInvalidObjectIndex = i;
                DiagLastRejectSample = "reason=" + reason + " index=" + i + " ptr=" + ToIntPtr(pObjects, 4 * i) + " " + hitObjects[i];
            }
        }

        this.hitObjects = hitObjects;
    }

    /// <summary>
    /// 与 <c>BeatmapInfoCollection</c> 的合法性校验保持一致的分类：
    /// 0=序号,1=X,2=Y,3=SegmentCount,4=Type,5=SampleSet,6=SampleSetAdditions,7=SampleVolume；-1 表示合法。
    /// </summary>
    private static int RejectReason(HitObject ho)
    {
        if (ho.X > 1000 || ho.X < -1000) return 1;
        if (ho.Y > 1000 || ho.Y < -1000) return 2;
        if (ho.SegmentCount > 9000) return 3;
        if (ho.Type == 0) return 4;
        if (ho.SampleSet > 1000) return 5;
        if (ho.SampleSetAdditions > 1000) return 6;
        if (ho.SampleVolume > 1000) return 7;
        return -1;
    }

    /// <summary>
    /// 诊断用：物件列表的三个关键指针，用于在 harness 里复现/对比读取路径。
    /// </summary>
    public (IntPtr ListHeader, IntPtr DataArray, int Count, int PointerSize) GetObjectPointers()
        => (pObjectsL, pObjectsA, numObjects, TargetPointerSize);

    /// <summary>诊断用：当前缓存的编辑器对象地址（不触发重新扫描）。</summary>
    public IntPtr EditorAddress => pEditor;

    /// <summary>诊断用：当前缓存的 HOM 地址。</summary>
    public IntPtr HomAddress => pHOM;

    /// <summary>诊断用：当前缓存的 Beatmap 地址。</summary>
    public IntPtr BeatmapAddress => pBeatmap;

    /// <summary>
    /// 读取物件结构体时"至少要读到多少字节"。
    /// <para />普通物件（圆/滑条头）解析用到的最大偏移是 144（BaseY）+4 = 148；
    /// 滑条额外用到 286（unifiedSoundAddition）+1 = 287，以及 196/224/228/232 处的子列表指针。
    /// <para />为什么必须区分：编辑器增删物件会让堆在页边界上收缩，压在页尾的物件
    /// 可能只有前 256 字节可读（实测 0xB981FEF0 就是这种）。若一律要求 287 字节，
    /// 这类物件会永久读取失败，一个物件就能让整个全量读取每 tick 抛异常、客户端卡死。
    /// </summary>
    private const int ObjectRequiredBytesBase = 148;
    private const int ObjectRequiredBytesSlider = 287;

    /// <summary>先用它把 Type（偏移 24）读出来，据此决定这个物件要读到多少字节。</summary>
    private const int ObjectTypeProbeBytes = 32;

    private HitObject ReadObject(IntPtr pObject, bool fetchHitSound)
    {
        int required;

        // 快路径：整块读成功就说明 336 字节全可读，直接判类型，省掉一次探测读取
        //（每秒约 1.6 万次物件读取，多一次 RPM 就是多 1.6ms/帧）。
        if (ReadProcessMemory(TargetHandle, pObject, bufferOb, 336, ref bytesRead))
        {
            required = (BitConverter.ToInt32(bufferOb, 24) & 2) > 0 ? ObjectRequiredBytesSlider : ObjectRequiredBytesBase;
        }
        else
        {
            // 慢路径：物件跨页且后一页被回收。先读结构体头部拿 Type，据此决定需要多少字节。
            byte[] head = new byte[ObjectTypeProbeBytes];
            if (!SafeReadSegmented(pObject, head, ObjectTypeProbeBytes, sizeof(int)))
            {
                throw new Exception("ReadProcessMemory Error. Cancelled reading.");
            }

            required = (BitConverter.ToInt32(head, 24) & 2) > 0 ? ObjectRequiredBytesSlider : ObjectRequiredBytesBase;
            if (!SafeReadSegmented(pObject, bufferOb, 336, required))
            {
                throw new Exception("ReadProcessMemory Error. Cancelled reading.");
            }
        }

        bool isSlider = required == ObjectRequiredBytesSlider;
        HitObject hitObject = new HitObject();
        hitObject.SpatialLength = BitConverter.ToDouble(bufferOb, 8);
        hitObject.StartTime = BitConverter.ToInt32(bufferOb, 16);
        hitObject.EndTime = BitConverter.ToInt32(bufferOb, 20);
        hitObject.Type = BitConverter.ToInt32(bufferOb, 24);
        hitObject.SoundType = BitConverter.ToInt32(bufferOb, 28);
        hitObject.SegmentCount = BitConverter.ToInt32(bufferOb, 32);
        hitObject.X = BitConverter.ToSingle(bufferOb, 56);
        hitObject.Y = BitConverter.ToSingle(bufferOb, 60);
        hitObject.SampleFile = ReadString(ToIntPtr(bufferOb, 84));
        hitObject.Type |= (BitConverter.ToInt32(bufferOb, 96) & 7) << 4;
        hitObject.SampleVolume = BitConverter.ToInt32(bufferOb, 108);
        hitObject.SampleSet = BitConverter.ToInt32(bufferOb, 112);
        hitObject.SampleSetAdditions = BitConverter.ToInt32(bufferOb, 116);
        hitObject.CustomSampleSet = BitConverter.ToInt32(bufferOb, 120);
        hitObject.IsSelected = BitConverter.ToBoolean(bufferOb, 133);
        hitObject.BaseX = BitConverter.ToSingle(bufferOb, 140);
        hitObject.BaseY = BitConverter.ToSingle(bufferOb, 144);
        if (isSlider)
        {
            hitObject.curveLength = BitConverter.ToDouble(bufferOb, 148);
            hitObject.CurveType = BitConverter.ToInt32(bufferOb, 248);
            hitObject.unifiedSoundAddition = (fetchHitSound) ? BitConverter.ToBoolean(bufferOb, 286) : true;
            pPointsL = ToIntPtr(bufferOb, 196);
            pSTL = ToIntPtr(bufferOb, 224);
            pSSL = ToIntPtr(bufferOb, 228);
            pSSAL = ToIntPtr(bufferOb, 232);
            SafeReadProcessMemory(TargetHandle, pPointsL, buffer16, 16, ref bytesRead);
            pTempA = ToIntPtr(buffer16, 4);
            numTemp = SafeBitConverterToInt32(buffer16, 12, "numTemp");
            EnsureBuffer(ref bTemp, 8 * numTemp);
            SafeReadProcessMemory(TargetHandle, pTempA + 8, bTemp, 8 * numTemp, ref bytesRead);
            hitObject.sliderCurvePoints = new float[2 * numTemp];
            Buffer.BlockCopy(bTemp, 0, hitObject.sliderCurvePoints, 0, 8 * numTemp);
            if (!hitObject.unifiedSoundAddition)
            {
                SafeReadProcessMemory(TargetHandle, pSTL, buffer16, 16, ref bytesRead);
                pTempA = ToIntPtr(buffer16, 4);
                numTemp = SafeBitConverterToInt32(buffer16, 12, "numTemp");
                EnsureBuffer(ref bTemp, 4 * numTemp);
                SafeReadProcessMemory(TargetHandle, pTempA + 8, bTemp, 4 * numTemp, ref bytesRead);
                hitObject.SoundTypeList = new int[numTemp];
                Buffer.BlockCopy(bTemp, 0, hitObject.SoundTypeList, 0, 4 * numTemp);
                SafeReadProcessMemory(TargetHandle, pSSL, buffer16, 16, ref bytesRead);
                pTempA = ToIntPtr(buffer16, 4);
                numTemp = SafeBitConverterToInt32(buffer16, 12, "numTemp");
                EnsureBuffer(ref bTemp, 4 * numTemp);
                SafeReadProcessMemory(TargetHandle, pTempA + 8, bTemp, 4 * numTemp, ref bytesRead);
                hitObject.SampleSetList = new int[numTemp];
                Buffer.BlockCopy(bTemp, 0, hitObject.SampleSetList, 0, 4 * numTemp);
                SafeReadProcessMemory(TargetHandle, pSSAL, buffer16, 16, ref bytesRead);
                pTempA = ToIntPtr(buffer16, 4);
                numTemp = SafeBitConverterToInt32(buffer16, 12, "numTemp");
                EnsureBuffer(ref bTemp, 4 * numTemp);
                SafeReadProcessMemory(TargetHandle, pTempA + 8, bTemp, 4 * numTemp, ref bytesRead);
                hitObject.SampleSetAdditionsList = new int[numTemp];
                Buffer.BlockCopy(bTemp, 0, hitObject.SampleSetAdditionsList, 0, 4 * numTemp);
            }
        }

        if (autoDeStack)
        {
            hitObject.DeStack();
        }

        if (autoRound)
        {
            hitObject.Round();
        }

        return hitObject;
    }

    public void FetchEditor()
    {
        if (ProcessNeedsReload())
        {
            SetProcess();
        }

        SetEditor();
    }

    public void FetchHOM()
    {
        if (EditorNeedsReload())
        {
            FetchEditor();
        }

        SetHOM();
        ReadHOM();
    }

    public void FetchBeatmap()
    {
        SetBeatmap();
        ReadBeatmap();
    }

    public void FetchControlPoints()
    {
        SetControlPoints();
        ReadControlPoints();
    }

    public void FetchObjects(bool fetchHitSound = true)
    {
        SetObjects();
        ReadObjects(fetchHitSound);
    }

    public void FetchAll(bool fetchFull = true)
    {
        FetchHOM();
        FetchBeatmap();
        FetchControlPoints();
        FetchObjects(fetchFull);
        if (fetchFull) FetchBookmarks();
    }
}