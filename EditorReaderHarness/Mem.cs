using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EditorReaderHarness;

/// <summary>
/// 跨进程读取原语集合：所有"更好的内存读取方法"的候选实现都放在这里，
/// 方便用同一套计时框架横向对比。
/// </summary>
public static class Mem
{
    // ---------------------------------------------------------------- native

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

    [DllImport("ntdll.dll")]
    private static extern int NtReadVirtualMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, IntPtr dwSize, out IntPtr lpNumberOfBytesRead);

    [DllImport("ntdll.dll")]
    private static extern int NtReadVirtualMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, IntPtr dwSize, out IntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, int dwLength);

    public static long NumRpmCalls;
    public static long NumBytesRead;

    public static void ResetCounters()
    {
        NumRpmCalls = 0;
        NumBytesRead = 0;
    }

    // ---------------------------------------------------------------- 现有的逐次读取方式

    /// <summary>与主工程 <c>EditorReader.SafeReadProcessMemory</c> 完全一致（kernel32 ReadProcessMemory）。</summary>
    public static bool Rpm(IntPtr hProcess, IntPtr address, byte[] buffer, int size)
    {
        NumRpmCalls++;
        NumBytesRead += size;
        return ReadProcessMemory(hProcess, address, buffer, size, out _);
    }

    public static bool Rpm(IntPtr hProcess, IntPtr address, IntPtr buffer, int size)
    {
        NumRpmCalls++;
        NumBytesRead += size;
        return ReadProcessMemory(hProcess, address, buffer, size, out _);
    }

    // ---------------------------------------------------------------- 候选 A：ntdll 直连

    /// <summary>绕过 kernel32 包装，直接调用 NtReadVirtualMemory。</summary>
    public static bool NtRpm(IntPtr hProcess, IntPtr address, byte[] buffer, int size)
    {
        NumRpmCalls++;
        NumBytesRead += size;
        int status = NtReadVirtualMemory(hProcess, address, buffer, (IntPtr)size, out IntPtr read);
        return status >= 0 && read.ToInt64() == size;
    }

    public static bool NtRpm(IntPtr hProcess, IntPtr address, IntPtr buffer, int size)
    {
        NumRpmCalls++;
        NumBytesRead += size;
        int status = NtReadVirtualMemory(hProcess, address, buffer, (IntPtr)size, out IntPtr read);
        return status >= 0 && read.ToInt64() == size;
    }

    // ---------------------------------------------------------------- 候选 B：散列聚合（scatter-gather）

    /// <summary>
    /// 把一批地址合并成尽量少的"大块读取"：相邻地址间隔小于 maxGap 时就并进同一块。
    /// maxGap=0 表示要求地址严格连续，maxGap 越大合并越激进（阅读量也越大）。
    /// 返回：(读取次数, 覆盖字节数, 命中物件的读取次数)。
    /// </summary>
    public static (int Reads, long Bytes, int HitObjects) ScatterReadStructures(
        IntPtr hProcess, IReadOnlyList<long> addresses, int structSize, long maxGap, byte[] scratch, int stride)
    {
        int reads = 0;
        long bytes = 0;
        var order = new int[addresses.Count];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        Array.Sort(order, (a, b) => addresses[a].CompareTo(addresses[b]));

        int k = 0;
        while (k < order.Length)
        {
            long blockStart = addresses[order[k]];
            long blockEnd = blockStart + structSize;
            int j = k + 1;
            while (j < order.Length)
            {
                long next = addresses[order[j]];
                if (next - blockEnd > maxGap) break;
                if (next + structSize > blockEnd) blockEnd = next + structSize;
                j++;
            }

            long span = blockEnd - blockStart;
            // 单次读取上限（避免一个稀疏区域把整个地址空间拉进来）
            if (span > 1L * 1024 * 1024)
            {
                j = k + 1;
                blockEnd = blockStart + structSize;
                span = structSize;
            }

            int readSize = (int)span;
            if (scratch != null && readSize <= scratch.Length && span > 0)
            {
                Rpm(hProcess, (IntPtr)blockStart, scratch, readSize);
                bytes += readSize;
                reads++;
            }

            k = j;
        }

        return (reads, bytes, addresses.Count);
    }

    // ---------------------------------------------------------------- 候选 C：整区映射

    [DllImport("ntdll.dll")]
    private static extern int NtMapViewOfSection(IntPtr SectionHandle, IntPtr ProcessHandle, ref IntPtr BaseAddress,
        IntPtr ZeroBits, IntPtr CommitSize, IntPtr SectionOffset, ref IntPtr ViewSize, int InheritDisposition, uint AllocationType, uint Win32Protect);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("ntdll.dll")]
    private static extern int NtCreateSection(out IntPtr SectionHandle, uint DesiredAccess, IntPtr ObjectAttributes,
        ref long MaximumSize, uint SectionPageProtection, uint AllocationAttributes, IntPtr FileHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

    /// <summary>
    /// 用 NtCreateSection(SEC_COMMIT) + NtMapViewOfSection 把目标进程的一段提交内存直接映射进本进程
    /// 地址空间，之后轮询就是本地读（零 syscall）。失败返回 IntPtr.Zero 并给出 NTSTATUS。
    /// </summary>
    public static IntPtr TryMapRegion(IntPtr hProcess, long baseAddress, long size, out IntPtr sectionHandle, out long mappedSize, out int createStatus, out int mapStatus)
    {
        sectionHandle = IntPtr.Zero;
        mappedSize = 0;
        createStatus = 0;
        mapStatus = 0;

        long maxSize = size;
        createStatus = NtCreateSection(out IntPtr section, 0x0004 /*SECTION_MAP_READ*/, IntPtr.Zero,
            ref maxSize, 0x02 /*PAGE_READONLY*/, 0x08000000 /*SEC_COMMIT*/, IntPtr.Zero);
        if (createStatus < 0) return IntPtr.Zero;

        IntPtr viewBase = IntPtr.Zero;
        IntPtr viewSize = IntPtr.Zero;
        mapStatus = NtMapViewOfSection(section, (IntPtr)(-1) /*NtCurrentProcess*/, ref viewBase, IntPtr.Zero, IntPtr.Zero,
            IntPtr.Zero, ref viewSize, 1 /*ViewUnmap*/, 0, 0x02 /*PAGE_READONLY*/);
        if (mapStatus < 0)
        {
            CloseHandle(section);
            return IntPtr.Zero;
        }

        sectionHandle = section;
        mappedSize = viewSize.ToInt64();
        return viewBase;
    }

    public static void UnmapRegion(IntPtr viewBase, IntPtr sectionHandle)
    {
        if (viewBase != IntPtr.Zero) UnmapViewOfFile(viewBase);
        if (sectionHandle != IntPtr.Zero) CloseHandle(sectionHandle);
    }

    // ---------------------------------------------------------------- 区域枚举

    public static List<MEMORY_BASIC_INFORMATION> Regions(IntPtr hProcess)
    {
        var list = new List<MEMORY_BASIC_INFORMATION>();
        IntPtr address = IntPtr.Zero;
        int guard = 0;
        while (guard++ < 200000)
        {
            if (VirtualQueryEx(hProcess, address, out MEMORY_BASIC_INFORMATION mbi, Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
            {
                break;
            }
            if (mbi.State == 0x1000 && mbi.Protect == 0x04 && mbi.Type == 0x20000)
            {
                list.Add(mbi);
            }

            long next = mbi.BaseAddress.ToInt64() + mbi.RegionSize.ToInt64();
            if (next <= address.ToInt64()) break;
            address = (IntPtr)next;
        }

        return list;
    }

    // ---------------------------------------------------------------- 进程连接

    public static Process? FindOsu()
    {
        foreach (Process p in Process.GetProcessesByName("osu!"))
        {
            try
            {
                if (p.MainModule?.ModuleName == "osu!.exe") return p;
            }
            catch
            {
                // 32 位进程 / 权限问题：忽略
            }
        }

        return null;
    }

    public static bool IsTargetWow64(Process p)
    {
        try
        {
            return IsWow64Process(p.Handle, out bool wow64) && wow64;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>用已有句柄判断目标位数（句柄权限只需 QUERY_LIMITED_INFORMATION）。</summary>
    public static bool IsTargetWow64FromHandle(IntPtr hProcess)
        => IsWow64Process(hProcess, out bool wow64) && wow64;

    public static int PtrSize(Process p) => IsTargetWow64(p) ? 4 : 8;

    public static long ReadPointer(byte[] buffer, int offset, int ptrSize)
        => ptrSize == 4 ? BitConverter.ToUInt32(buffer, offset) : (long)BitConverter.ToUInt64(buffer, offset);

    /// <summary>计时助手：跑 n 次取统计。</summary>
    public static (double TotalMs, double AvgUs, double P50Us, double P99Us, double MaxUs) Time(int iterations, Action body)
    {
        var samples = new double[iterations];
        var sw = new Stopwatch();
        for (int i = 0; i < iterations; i++)
        {
            sw.Restart();
            body();
            sw.Stop();
            samples[i] = sw.Elapsed.TotalMilliseconds;
        }

        Array.Sort(samples);
        double total = 0;
        foreach (double s in samples) total += s;
        return (total, total * 1000.0 / iterations, samples[iterations / 2] * 1000.0,
                samples[Math.Min(iterations - 1, (int)(iterations * 0.99))] * 1000.0, samples[iterations - 1] * 1000.0);
    }

    public static string Fmt(double us) => us < 1000 ? us.ToString("F1") + "us" : (us / 1000).ToString("F2") + "ms";

    public static void Ensure(ref byte[] buffer, int size)
    {
        if (buffer == null || buffer.Length < size) buffer = new byte[size];
    }

    /// <summary>把 EditorReader 里那种十六进制签名串转成字节数组（0xEE 视为通配符，由 PatternMatch 处理）。</summary>
    public static byte[] HexToBytes(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    /// <summary>与 EditorReader.PatternCheck 一致：模式里的 0xEE 是通配符。</summary>
    public static bool PatternMatch(byte[] buffer, byte[] pattern, int offset)
    {
        if (offset < 0 || offset + pattern.Length > buffer.Length) return false;
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] != 0xEE && pattern[i] != buffer[offset + i]) return false;
        }
        return true;
    }

    /// <summary>查询某个地址所在的内存区域，用于判断"读不到"是因为地址没映射还是权限不足。</summary>
    public static string DescribeRegion(IntPtr hProcess, IntPtr address)
    {
        if (VirtualQueryEx(hProcess, address, out MEMORY_BASIC_INFORMATION mbi, Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
        {
            return "VirtualQueryEx 失败 err=" + Marshal.GetLastWin32Error();
        }

        string state = mbi.State switch { 0x1000 => "MEM_COMMIT", 0x2000 => "MEM_RESERVE", 0x10000 => "MEM_FREE", _ => "0x" + mbi.State.ToString("X") };
        string protect = mbi.Protect switch
        {
            0x01 => "PAGE_NOACCESS",
            0x02 => "PAGE_READONLY",
            0x04 => "PAGE_READWRITE",
            0x10 => "PAGE_EXECUTE",
            0x20 => "PAGE_EXECUTE_READ",
            0x40 => "PAGE_EXECUTE_READWRITE",
            0x00 => "(none)",
            _ => "0x" + mbi.Protect.ToString("X")
        };
        string type = mbi.Type switch { 0x20000 => "MEM_PRIVATE", 0x40000 => "MEM_MAPPED", 0x1000000 => "MEM_IMAGE", _ => "0x" + mbi.Type.ToString("X") };

        return $"{state} {protect} {type} 区域=[0x{mbi.BaseAddress.ToInt64():X8}, +{mbi.RegionSize.ToInt64()})  查询地址偏移={address.ToInt64() - mbi.BaseAddress.ToInt64()}";
    }

    public static uint ReadUInt32(IntPtr hProcess, long address)
    {
        byte[] b = new byte[4];
        return Rpm(hProcess, (IntPtr)address, b, 4) ? BitConverter.ToUInt32(b, 0) : 0;
    }

    public struct RegionStats
    {
        public int Commit, Reserve, Free, Readable, Filtered;
    }

    /// <summary>
    /// 枚举全部内存区域并统计各类状态，用于判断 Wine 下 VirtualQueryEx 的报告是否
    /// 与真实 Windows 不同（主工程只接受 COMMIT + PAGE_READWRITE + MEM_PRIVATE）。
    /// </summary>
    public static List<MEMORY_BASIC_INFORMATION> EnumerateAllRegions(IntPtr hProcess, out RegionStats stats)
    {
        stats = default;
        var list = new List<MEMORY_BASIC_INFORMATION>();
        IntPtr address = IntPtr.Zero;
        int guard = 0;

        while (guard++ < 200000)
        {
            if (VirtualQueryEx(hProcess, address, out MEMORY_BASIC_INFORMATION mbi, Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
            {
                break;
            }

            list.Add(mbi);

            switch (mbi.State)
            {
                case 0x1000: stats.Commit++; break;
                case 0x2000: stats.Reserve++; break;
                case 0x10000: stats.Free++; break;
            }

            bool readable = mbi.Protect is 0x02 or 0x04 or 0x20 or 0x40;
            if (readable) stats.Readable++;
            if (mbi.State == 0x1000 && mbi.Protect == 0x04 && mbi.Type == 0x20000) stats.Filtered++;

            long next = mbi.BaseAddress.ToInt64() + mbi.RegionSize.ToInt64();
            if (next <= address.ToInt64()) break;
            address = (IntPtr)next;
        }

        return list;
    }

    /// <summary>枚举 [from, to) 之间的所有内存区域并描述它们。</summary>
    public static List<string> DescribeRegionsIn(IntPtr hProcess, long from, long to)
    {
        var result = new List<string>();
        long address = from;
        int guard = 0;
        while (address < to && guard++ < 500)
        {
            if (VirtualQueryEx(hProcess, (IntPtr)address, out MEMORY_BASIC_INFORMATION mbi, Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
            {
                result.Add($"0x{address:X8}: VirtualQueryEx 失败 err={Marshal.GetLastWin32Error()}");
                break;
            }

            string state = mbi.State switch { 0x1000 => "COMMIT", 0x2000 => "RESERVE", 0x10000 => "FREE", _ => "0x" + mbi.State.ToString("X") };
            string protect = mbi.Protect switch
            {
                0x01 => "NOACCESS",
                0x02 => "READONLY",
                0x04 => "READWRITE",
                0x10 => "EXECUTE",
                0x20 => "EXECUTE_READ",
                0x40 => "EXECUTE_READWRITE",
                0x00 => "-",
                _ => "0x" + mbi.Protect.ToString("X")
            };
            string type = mbi.Type switch { 0x20000 => "PRIVATE", 0x40000 => "MAPPED", 0x1000000 => "IMAGE", _ => "-" };

            long size = mbi.RegionSize.ToInt64();
            result.Add($"[0x{mbi.BaseAddress.ToInt64():X8}, +0x{size:X}) {state} {protect} {type}");

            long next = mbi.BaseAddress.ToInt64() + size;
            if (next <= address) break;
            address = next;
        }
        return result;
    }

    public static int ReadInt32(IntPtr hProcess, long address)
    {
        byte[] b = new byte[4];
        return Rpm(hProcess, (IntPtr)address, b, 4) ? BitConverter.ToInt32(b, 0) : int.MinValue;
    }

    /// <summary>
    /// 从地址开始尽量往后读，遇到不可读就停。用于判断"整块读失败"是因为数据不存在，
    /// 还是因为尾部越过了已提交区域（例如对象正好压在页边界上）。
    /// </summary>
    public static (int Bytes, byte[] Data) ReadAsMuchAsPossible(IntPtr hProcess, IntPtr address, int size)
    {
        byte[] data = new byte[size];
        int got = 0;
        int step = 64;
        while (got < size)
        {
            int want = Math.Min(step, size - got);
            byte[] chunk = new byte[want];
            if (!Rpm(hProcess, address + got, chunk, want)) break;
            Buffer.BlockCopy(chunk, 0, data, got, want);
            got += want;
        }
        return (got, data);
    }

    /// <summary>指针解析宽度覆盖（诊断用：模拟旧的 IntPtr.Size 行为）。</summary>
    public static int? PointerSizeOverride;
}
