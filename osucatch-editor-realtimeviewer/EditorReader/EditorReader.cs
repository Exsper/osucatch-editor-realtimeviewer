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
    private const long MaxScanRegionSize = 256L * 1024 * 1024;

    /// <summary>
    /// 单次内存扫描的最长时间。Wine 下 ReadProcessMemory 可能对某些区域永久阻塞，
    /// 无法从超时点中断原生调用，因此把扫描放到独立线程，超时后放弃该线程并稍后重试。
    /// </summary>
    private const int ScanTimeoutMs = 30000;

    /// <summary>ReadProcessMemory 长时间不返回的区域：首次退避 60 秒，随后指数增长。</summary>
    private const int InitialRegionPenaltyMs = 60 * 1000;

    /// <summary>区域退避上限：保证"被拉黑的区域"最终一定会被重试。</summary>
    private const int MaxRegionPenaltyMs = 30 * 60 * 1000;

    /// <summary>某个区域的退避状态。</summary>
    private sealed class RegionPenalty
    {
        public int Count;
        public long NextRetryTimestamp;
    }

    /// <summary>
    /// ReadProcessMemory 长时间不返回过的区域（Wine 上确实存在）：按指数退避延后重试，
    /// 而不是永久跳过。永久拉黑会造成"该区域恰好含有 editor 对象时，本进程内永远无法重新绑定"，
    /// 用户只能不断重启 viewer —— 这正是 issue 中"重启 3 次以上 / 永久卡住"的来源之一。
    /// </summary>
    private readonly Dictionary<long, RegionPenalty> penalizedRegions = new();
    private readonly object penalizedRegionsLock = new();

    /// <summary>上次成功找到 editor 的区域起始地址：重绑时优先扫描它（快路径）。</summary>
    private long lastFoundRegionBase;

    /// <summary>上次扫描超时后继续扫描的起点，避免每次重试都从头开始。</summary>
    private int scanCursorIndex;

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

    private string ReadString(IntPtr pString)
    {
        if (pString == IntPtr.Zero)
        {
            return null;
        }

        SafeReadProcessMemory(process.Handle, pString + 4, buffer4, 4, ref bytesRead);
        int num = SafeBitConverterToInt32(buffer4, 0, "ReadString num");
        byte[] array = new byte[2 * num];
        SafeReadProcessMemory(process.Handle, pString + 8, array, 2 * num, ref bytesRead);
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

    private IntPtr FindEditorAddress()
    {
        Log.ConsoleLog("FindEditorAddress: start enumerating memory regions.", Log.LogType.EditorReader, Log.LogLevel.Info);

        Internals internals = new Internals();
        internals.MemInfo(process.Handle);
        int regionCount = internals.MemReg.Count;
        Log.ConsoleLog("FindEditorAddress: " + regionCount + " region(s) to scan.", Log.LogType.EditorReader, Log.LogLevel.Info);

        // 把扫描放到独立线程：若某个 ReadProcessMemory 在 Wine 下永久阻塞，
        // Join 超时后放弃该线程，把该区域延后重试（而不是永久跳过），而不是让整个程序卡死。
        // 扫描线程自带缓冲区（见 ScanForEditorAddress），超时被放弃后继续运行也不会与主线程抢共享字段。
        if (Interlocked.Increment(ref liveScanThreads) > MaxLiveScanThreads)
        {
            Interlocked.Decrement(ref liveScanThreads);
            throw new InvalidOperationException("Too many memory scans are still running; skipping this attempt.");
        }

        (IntPtr Address, long RegionBase, int RegionIndex) scanResult = (IntPtr.Zero, 0, -1);
        Exception? scanError = null;
        Thread scanThread = new Thread(() =>
        {
            try { scanResult = ScanForEditorAddress(internals); }
            catch (Exception ex) { scanError = ex; }
            finally { Interlocked.Decrement(ref liveScanThreads); }
        });
        scanThread.Start();

        if (!scanThread.Join(ScanTimeoutMs))
        {
            long stalled = Interlocked.Read(ref scanningRegionAddress);
            int stalledIndex = scanningRegionIndex;
            if (stalled != 0) PenalizeRegion(stalled);
            // 下一次从卡住区域之后继续扫（环形），保证多次重试能向前推进、最终覆盖整个地址空间
            if (regionCount > 0) scanCursorIndex = (stalledIndex + 1) % regionCount;
            Log.ConsoleLog("FindEditorAddress: scan aborted after " + ScanTimeoutMs + " ms, stalled at region " + stalledIndex + "/" + regionCount + " (address " + stalled + "). It will be retried with backoff.", Log.LogType.EditorReader, Log.LogLevel.Warning);
            throw new InvalidOperationException("Memory scan aborted: ReadProcessMemory did not return in time.");
        }

        if (scanError != null) throw scanError;

        if (scanResult.Address != IntPtr.Zero)
        {
            lastFoundRegionBase = scanResult.RegionBase;
            scanCursorIndex = scanResult.RegionIndex;
            ClearRegionPenalties();
        }
        else
        {
            // 完整扫过一遍没有结果：下次重新从 0 开始
            scanCursorIndex = 0;
        }

        return scanResult.Address;
    }

    /// <summary>
    /// 在目标进程里扫描编辑器签名。
    /// <para />扫描线程不共享任何实例缓冲区：超时被放弃的线程可能仍在读内存，
    /// 若与主线程共用 buffer/bytesRead 会读到互相覆盖的数据。
    /// </summary>
    private (IntPtr Address, long RegionBase, int RegionIndex) ScanForEditorAddress(Internals internals)
    {
        const int ReadChunkSize = 8 * 1024 * 1024;

        byte[] array = ToByteArray("230000001400000019000000eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee0C000000eeeeeeeeeeeeeeeeeeeeeeeeee00");
        int overlap = array.Length - 1;
        byte[] scanBuffer = null;
        byte[] probe16 = new byte[16];
        byte[] probe4 = new byte[4];
        IntPtr read = IntPtr.Zero;
        int skippedPenalized = 0;

        foreach (int i in GetScanOrder(internals))
        {
            Internals.MEMORY_BASIC_INFORMATION mEMORY_BASIC_INFORMATION = internals.MemReg[i];
            long regionBase = mEMORY_BASIC_INFORMATION.BaseAddress.ToInt64();
            long regionSize = mEMORY_BASIC_INFORMATION.RegionSize.ToInt64();

            if (IsRegionPenalized(regionBase))
            {
                skippedPenalized++;
                Log.ConsoleLog("FindEditorAddress: region " + i + "/" + internals.MemReg.Count + " (address " + regionBase + ") is in backoff, skipped for now.", Log.LogType.EditorReader, Log.LogLevel.Info);
                continue;
            }

            if (regionSize > MaxScanRegionSize)
            {
                Log.ConsoleLog("FindEditorAddress: skip region at " + mEMORY_BASIC_INFORMATION.BaseAddress + " (size " + regionSize + " bytes, > " + MaxScanRegionSize + ")", Log.LogType.EditorReader, Log.LogLevel.Warning);
                continue;
            }

            Log.ConsoleLog("FindEditorAddress: scanning region " + i + "/" + internals.MemReg.Count + " (size " + regionSize + " bytes) at " + mEMORY_BASIC_INFORMATION.BaseAddress, Log.LogType.EditorReader, Log.LogLevel.Debug);
            Interlocked.Exchange(ref scanningRegionAddress, regionBase);
            scanningRegionIndex = i;

            // 分块读取并扫描，避免一次性分配超大缓冲区；
            // 相邻块重叠 overlap 字节，防止签名跨块时漏检。
            long offset = 0;
            while (offset < regionSize)
            {
                int chunkSize = (int)Math.Min(ReadChunkSize, regionSize - offset);
                int readSize = chunkSize;
                if (offset + chunkSize < regionSize) readSize += overlap;

                EnsureBuffer(ref scanBuffer, readSize);
                if (!ReadProcessMemory(process.Handle, IntPtr.Add(mEMORY_BASIC_INFORMATION.BaseAddress, (int)offset), scanBuffer, readSize, ref read))
                {
                    offset += chunkSize;
                    continue;
                }

                int bytesToScan = (int)read;
                for (int j = 0; j <= bytesToScan - array.Length; j += 4)
                {
                    if (!PatternCheck(scanBuffer, array, j)) continue;

                    IntPtr candidate = new IntPtr(mEMORY_BASIC_INFORMATION.BaseAddress.ToInt64() + offset + j - 160);
                    if (!IsPlausibleEditor(candidate, probe16, probe4)) continue;

                    Log.ConsoleLog("FindEditorAddress: found at " + candidate, Log.LogType.EditorReader, Log.LogLevel.Debug);
                    return (candidate, regionBase, i);
                }

                offset += chunkSize;
                if ((long)read < readSize) break; // 区域剩余部分不可读，停止该区域
            }
        }

        if (skippedPenalized > 0)
        {
            Log.ConsoleLog("FindEditorAddress: " + skippedPenalized + " region(s) skipped due to backoff.", Log.LogType.EditorReader, Log.LogLevel.Warning);
        }

        Log.ConsoleLog("FindEditorAddress: no active editor found.", Log.LogType.EditorReader, Log.LogLevel.Warning);
        throw new InvalidOperationException("No active editor found.");
    }

    /// <summary>
    /// 扫描顺序：先试上次命中 editor 的区域（通常直接命中，省掉整轮全堆扫描），
    /// 再从上次超时点按环形顺序扫完整个列表，避免每次重试都从头扫、反复卡在同一个区域。
    /// </summary>
    private IEnumerable<int> GetScanOrder(Internals internals)
    {
        int regionCount = internals.MemReg.Count;
        int fastPathIndex = -1;

        if (lastFoundRegionBase != 0)
        {
            for (int i = 0; i < regionCount; i++)
            {
                if (internals.MemReg[i].BaseAddress.ToInt64() == lastFoundRegionBase)
                {
                    fastPathIndex = i;
                    yield return i;
                    break;
                }
            }
        }

        int start = (scanCursorIndex >= 0 && scanCursorIndex < regionCount) ? scanCursorIndex : 0;
        for (int k = 0; k < regionCount; k++)
        {
            int i = (start + k) % regionCount;
            if (i == fastPathIndex) continue;
            yield return i;
        }
    }

    private bool IsRegionPenalized(long regionBase)
    {
        lock (penalizedRegionsLock)
        {
            return penalizedRegions.TryGetValue(regionBase, out RegionPenalty? penalty) &&
                   Stopwatch.GetTimestamp() < penalty.NextRetryTimestamp;
        }
    }

    /// <summary>
    /// 把"读取超时"的区域延后重试（60 秒起，逐次翻倍，上限 30 分钟），而不是永久跳过。
    /// </summary>
    private void PenalizeRegion(long regionBase)
    {
        lock (penalizedRegionsLock)
        {
            if (!penalizedRegions.TryGetValue(regionBase, out RegionPenalty? penalty))
            {
                penalty = new RegionPenalty();
                penalizedRegions[regionBase] = penalty;
            }

            penalty.Count++;
            long delayMs = Math.Min((long)InitialRegionPenaltyMs << Math.Min(penalty.Count - 1, 10), MaxRegionPenaltyMs);
            penalty.NextRetryTimestamp = Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * (delayMs / 1000.0));
        }
    }

    private void ClearRegionPenalties()
    {
        lock (penalizedRegionsLock)
        {
            penalizedRegions.Clear();
        }
    }

    /// <summary>
    /// 候选编辑器的可信度校验：签名命中只说明内存里有这段字节。
    /// osu! 退出 test mode 后会重建编辑器对象，堆里可能残留同样能通过签名的死副本，
    /// 选错副本会造成之后持续读取失败。这里额外校验编辑器状态、HOM 与物件列表。
    /// <para />失败返回 false（不抛异常），让扫描继续找下一个候选。
    /// </summary>
    private bool IsPlausibleEditor(IntPtr pE, byte[] probe16, byte[] probe4)
    {
        if (pE == IntPtr.Zero) return false;

        IntPtr read = IntPtr.Zero;

        // 编辑器状态字段（与 EditorNeedsReload 的判定一致）
        if (!ReadProcessMemory(process.Handle, pE + 160, probe16, 16, ref read)) return false;
        if (BitConverter.ToInt32(probe16, 0) != 35 || BitConverter.ToInt32(probe16, 4) != 20 || BitConverter.ToInt32(probe16, 8) != 25) return false;

        // HOM 与物件列表：死副本通常指向已释放/清零的内存
        if (!ReadProcessMemory(process.Handle, pE + 28, probe4, 4, ref read)) return false;
        IntPtr pHom = ToIntPtr(probe4, 0);
        if (pHom == IntPtr.Zero) return false;

        if (!ReadProcessMemory(process.Handle, pHom + 72, probe4, 4, ref read)) return false;
        IntPtr pObjectsList = ToIntPtr(probe4, 0);
        if (pObjectsList == IntPtr.Zero) return false;

        if (!ReadProcessMemory(process.Handle, pObjectsList, probe16, 16, ref read)) return false;
        IntPtr pObjectsArray = ToIntPtr(probe16, 4);
        int count = BitConverter.ToInt32(probe16, 12);
        if (pObjectsArray == IntPtr.Zero || count < 0 || count > 1000000) return false;

        return true;
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
        ClearRegionPenalties();
        lastFoundRegionBase = 0;
        scanCursorIndex = 0;
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
        if (IntPtr.Size > 4)
        {
            return (IntPtr)BitConverter.ToUInt32(value, startIndex);
        }

        return (IntPtr)BitConverter.ToInt32(value, startIndex);
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
        if (!ReadProcessMemory(process.Handle, pEditor + 160, buffer16, 16, ref bytesRead) ||
            !ReadProcessMemory(process.Handle, pEditor + 208, buffer4, 4, ref bytesRead))
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
            SafeReadProcessMemory(process.Handle, pE + 28, buffer4, 4, ref bytesRead);
            IntPtr intPtr = ToIntPtr(buffer4, 0);
            if (intPtr == IntPtr.Zero)
            {
                return true;
            }
            SafeReadProcessMemory(process.Handle, intPtr + 72, buffer4, 4, ref bytesRead);
            IntPtr intPtr2 = ToIntPtr(buffer4, 0);
            return intPtr2 == IntPtr.Zero;
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
        SafeReadProcessMemory(process.Handle, pEditor + 176, buffer16, 16, ref bytesRead);
        return (BitConverter.ToInt32(buffer16, 8) + BitConverter.ToInt32(buffer16, 12)) / 2;
    }

    public void SetHOM()
    {
        SafeReadProcessMemory(process.Handle, pEditor + 28, buffer4, 4, ref bytesRead);
        pHOM = ToIntPtr(buffer4, 0);
        SafeReadProcessMemory(process.Handle, pEditor + 112, buffer4, 4, ref bytesRead);
        pCompose = ToIntPtr(buffer4, 0);
    }

    public void ReadHOM()
    {
        EnsureBuffer(ref buffer, 80);
        SafeReadProcessMemory(process.Handle, pHOM, buffer, 80, ref bytesRead);
        objectRadius = BitConverter.ToSingle(buffer, 24);
        stackOffset = BitConverter.ToSingle(buffer, 44);
        pBookmarksL = ToIntPtr(buffer, 56);
        pObjectsL = ToIntPtr(buffer, 72);
        EnsureBuffer(ref buffer, 256);
        SafeReadProcessMemory(process.Handle, pCompose, buffer, 256, ref bytesRead);
        pClipboardL = ToIntPtr(buffer, 48);
        pSelectedL = ToIntPtr(buffer, 72);
    }

    public void FetchBookmarks()
    {
        SafeReadProcessMemory(process.Handle, pBookmarksL, buffer16, 16, ref bytesRead);
        pBookmarksA = ToIntPtr(buffer16, 4);
        numBookmarks = SafeBitConverterToInt32(buffer16, 12, "numBookmarks");
        EnsureBuffer(ref buffer, 4 * numBookmarks);
        bookmarks = new int[numBookmarks];
        SafeReadProcessMemory(process.Handle, pBookmarksA + 8, buffer, 4 * numBookmarks, ref bytesRead);
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

        if (!ReadProcessMemory(process.Handle, pSelectedL, buffer16, 16, ref bytesRead))
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
        if (!ReadProcessMemory(process.Handle, pSelA + 8, pSelected, 4 * selCount, ref bytesRead))
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
        if (!ReadProcessMemory(process.Handle, pList, buffer16, 16, ref bytesRead))
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
        SafeReadProcessMemory(process.Handle, pHOM + 48, buffer4, 4, ref bytesRead);
        pBeatmap = ToIntPtr(buffer4, 0);
    }

    public void ReadBeatmap()
    {
        EnsureBuffer(ref buffer, 320);
        SafeReadProcessMemory(process.Handle, pBeatmap, buffer, 320, ref bytesRead);
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
        SafeReadProcessMemory(process.Handle, pBeatmap, buffer, 192, ref bytesRead);
        pControlPointsL = ToIntPtr(buffer, 176);
        SafeReadProcessMemory(process.Handle, pControlPointsL, buffer16, 16, ref bytesRead);
        pControlPointsA = ToIntPtr(buffer16, 4);
        numControlPoints = SafeBitConverterToInt32(buffer16, 12, "numControlPoints");
        EnsureBuffer(ref pControlPoints, 4 * numControlPoints);
        SafeReadProcessMemory(process.Handle, pControlPointsA + 8, pControlPoints, 4 * numControlPoints, ref bytesRead);
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
        SafeReadProcessMemory(process.Handle, pControlPoint, bufferCp, 48, ref bytesRead);
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
        SafeReadProcessMemory(process.Handle, pObjectsL, buffer16, 16, ref bytesRead);
        pObjectsA = ToIntPtr(buffer16, 4);
        numObjects = SafeBitConverterToInt32(buffer16, 12, "numObjects");
        EnsureBuffer(ref pObjects, 4 * numObjects);
        SafeReadProcessMemory(process.Handle, pObjectsA + 8, pObjects, 4 * numObjects, ref bytesRead);

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
        hitObjects = new List<HitObject>();
        for (int i = 0; i < numObjects; i++)
        {
            hitObjects.Add(ReadObject(ToIntPtr(pObjects, 4 * i), fetchHitSound));
        }
    }

    private HitObject ReadObject(IntPtr pObject, bool fetchHitSound)
    {
        SafeReadProcessMemory(process.Handle, pObject, bufferOb, 336, ref bytesRead);
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
        if (hitObject.IsSlider())
        {
            hitObject.curveLength = BitConverter.ToDouble(bufferOb, 148);
            hitObject.CurveType = BitConverter.ToInt32(bufferOb, 248);
            hitObject.unifiedSoundAddition = (fetchHitSound) ? BitConverter.ToBoolean(bufferOb, 286) : true;
            pPointsL = ToIntPtr(bufferOb, 196);
            pSTL = ToIntPtr(bufferOb, 224);
            pSSL = ToIntPtr(bufferOb, 228);
            pSSAL = ToIntPtr(bufferOb, 232);
            SafeReadProcessMemory(process.Handle, pPointsL, buffer16, 16, ref bytesRead);
            pTempA = ToIntPtr(buffer16, 4);
            numTemp = SafeBitConverterToInt32(buffer16, 12, "numTemp");
            EnsureBuffer(ref bTemp, 8 * numTemp);
            SafeReadProcessMemory(process.Handle, pTempA + 8, bTemp, 8 * numTemp, ref bytesRead);
            hitObject.sliderCurvePoints = new float[2 * numTemp];
            Buffer.BlockCopy(bTemp, 0, hitObject.sliderCurvePoints, 0, 8 * numTemp);
            if (!hitObject.unifiedSoundAddition)
            {
                SafeReadProcessMemory(process.Handle, pSTL, buffer16, 16, ref bytesRead);
                pTempA = ToIntPtr(buffer16, 4);
                numTemp = SafeBitConverterToInt32(buffer16, 12, "numTemp");
                EnsureBuffer(ref bTemp, 4 * numTemp);
                SafeReadProcessMemory(process.Handle, pTempA + 8, bTemp, 4 * numTemp, ref bytesRead);
                hitObject.SoundTypeList = new int[numTemp];
                Buffer.BlockCopy(bTemp, 0, hitObject.SoundTypeList, 0, 4 * numTemp);
                SafeReadProcessMemory(process.Handle, pSSL, buffer16, 16, ref bytesRead);
                pTempA = ToIntPtr(buffer16, 4);
                numTemp = SafeBitConverterToInt32(buffer16, 12, "numTemp");
                EnsureBuffer(ref bTemp, 4 * numTemp);
                SafeReadProcessMemory(process.Handle, pTempA + 8, bTemp, 4 * numTemp, ref bytesRead);
                hitObject.SampleSetList = new int[numTemp];
                Buffer.BlockCopy(bTemp, 0, hitObject.SampleSetList, 0, 4 * numTemp);
                SafeReadProcessMemory(process.Handle, pSSAL, buffer16, 16, ref bytesRead);
                pTempA = ToIntPtr(buffer16, 4);
                numTemp = SafeBitConverterToInt32(buffer16, 12, "numTemp");
                EnsureBuffer(ref bTemp, 4 * numTemp);
                SafeReadProcessMemory(process.Handle, pTempA + 8, bTemp, 4 * numTemp, ref bytesRead);
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
