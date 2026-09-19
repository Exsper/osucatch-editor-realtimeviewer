// Copyright (c) 2019 Karoo13. Licensed under https://github.com/Karoo13/EditorReader/blob/master/LICENSE
// See the LICENCE file in the EditorReader folder for full licence text.
// https://github.com/Karoo13/EditorReader
// Decompiled with ICSharpCode.Decompiler 8.1.1.7464

using System.Runtime.InteropServices;

namespace Editor_Reader;

internal class Internals
{
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

    public List<MEMORY_BASIC_INFORMATION> MemReg { get; set; } = new List<MEMORY_BASIC_INFORMATION>();

    // ---- 诊断：MemInfo 的枚举过程。0 个区域被扫到时，这几个数字能直接指出卡在哪一步 ----

    /// <summary>诊断用：枚举循环实际查询成功（VirtualQueryEx 返回非 0）的次数。</summary>
    public int DiagQueried;

    /// <summary>诊断用：命中状态/保护/类型过滤的区域数。</summary>
    public int DiagMatched;

    /// <summary>诊断用：循环结束原因。</summary>
    public string DiagStopReason = "(未执行)";

    /// <summary>诊断用：前若干个区域的原始字段，格式 地址/大小/State/Protect/Type。</summary>
    public readonly List<string> DiagFirstRegions = new();

    /// <summary>
    /// 诊断用：所有命中区域的 (地址, RegionSize) 明细，供上层判断"哪些被跳过、RegionSize 是否异常"。
    /// </summary>
    public readonly List<(long Base, long Size)> DiagMatchedRegions = new();

    /// <summary>诊断用：VirtualQueryEx 返回 0 时的 Win32 错误码。</summary>
    public int DiagLastError;

    /// <summary>诊断用：是否启用了宽松过滤（严格过滤命中 0 个时）。</summary>
    public bool DiagRelaxedPass;

    [DllImport("kernel32.dll", SetLastError = true)]
    protected static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, int dwLength);

    public void MemInfo(IntPtr pHandle)
    {
        MemReg.Clear();
        DiagQueried = 0;
        DiagMatched = 0;
        DiagFirstRegions.Clear();
        DiagStopReason = "(未执行)";

        IntPtr lpAddress = IntPtr.Zero;
        // 安全上限：防止 Wine 下枚举异常时陷入死循环
        const int MaxQueryRegions = 100000;
        int queried = 0;
        int structSize = Marshal.SizeOf<MEMORY_BASIC_INFORMATION>();
        var allRegions = new List<MEMORY_BASIC_INFORMATION>();

        while (true)
        {
            MEMORY_BASIC_INFORMATION lpBuffer = default(MEMORY_BASIC_INFORMATION);
            int ret = VirtualQueryEx(pHandle, lpAddress, out lpBuffer, structSize);
            if (ret == 0)
            {
                DiagStopReason = $"VirtualQueryEx 返回 0（已查询 {queried} 个区域，地址 0x{lpAddress.ToInt64():X}）";
                DiagLastError = Marshal.GetLastWin32Error();
                break;
            }

            queried++;
            DiagQueried = queried;
            allRegions.Add(lpBuffer);
            if (DiagFirstRegions.Count < 12)
            {
                DiagFirstRegions.Add($"0x{lpBuffer.BaseAddress.ToInt64():X8} size=0x{lpBuffer.RegionSize.ToInt64():X} " +
                                     $"State={lpBuffer.State} Protect={lpBuffer.Protect} Type={lpBuffer.Type}");
            }

            // 注意：这里不能因为 RegionSize 大就 break。Wine 报告的某些区域可能很大，
            // 早期版本用 "> int.MaxValue 就 break" 会让整个枚举提前结束、MemReg 为空，
            // 于是扫描器一个区域都没扫就报 "No active editor found"。
            if (lpBuffer.State == 4096 && lpBuffer.Protect == 4 && lpBuffer.Type == 131072)
            {
                MemReg.Add(lpBuffer);
                DiagMatched++;
                DiagMatchedRegions.Add((lpBuffer.BaseAddress.ToInt64(), lpBuffer.RegionSize.ToInt64()));
            }

            long size = lpBuffer.RegionSize.ToInt64();
            if (size <= 0)
            {
                DiagStopReason = $"RegionSize={size}（异常），停止枚举（已查询 {queried} 个区域）";
                break;
            }

            long next = lpBuffer.BaseAddress.ToInt64() + size;
            // 防止 Wine 下地址不前进导致枚举死循环
            if (next <= lpAddress.ToInt64())
            {
                DiagStopReason = $"地址未前进（base=0x{lpBuffer.BaseAddress.ToInt64():X} size=0x{size:X}），停止枚举";
                break;
            }

            lpAddress = (IntPtr)next;

            if (queried >= MaxQueryRegions)
            {
                DiagStopReason = $"达到查询上限 {MaxQueryRegions}";
                break;
            }
        }

        if (DiagStopReason == "(未执行)") DiagStopReason = "正常结束";

        int strictMatched = MemReg.Count;

        // 严格过滤（COMMIT + PAGE_READWRITE + MEM_PRIVATE）一个都没命中时，说明该平台
        // 报告的页面属性与真实 Windows 不同（Wine 常见）。这时退回"语义等价"的宽松条件：
        // 已提交 且 可读（含只读 / 写时复制 / 可执行读写）。这不会改变正常 Windows 的行为
        // （那里严格条件总能命中），只在异常环境里给出一次机会。
        if (strictMatched == 0)
        {
            DiagRelaxedPass = true;
            foreach (var mbi in allRegions)
            {
                if (mbi.State != 0x1000) continue;

                // 只排除明确不可访问的：PAGE_NOACCESS(0x01) / PAGE_GUARD(0x100)
                if (mbi.Protect == 0x01 || (mbi.Protect & 0x100) != 0) continue;

                long sz = mbi.RegionSize.ToInt64();
                if (sz <= 0 || sz > 512L * 1024 * 1024) continue;

                MemReg.Add(mbi);
            }

            DiagStopReason += $"；严格过滤命中 0 个，已启用宽松过滤（可读已提交页），得到 {MemReg.Count} 个区域";
        }

        MemReg.Sort((MEMORY_BASIC_INFORMATION a, MEMORY_BASIC_INFORMATION b) => ((int)a.RegionSize).CompareTo((int)b.RegionSize));
    }
}
