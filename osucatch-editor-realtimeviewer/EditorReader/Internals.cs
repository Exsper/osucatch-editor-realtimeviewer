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


    [DllImport("kernel32.dll", SetLastError = true)]
    protected static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, int dwLength);

    public void MemInfo(IntPtr pHandle)
    {
        IntPtr lpAddress = IntPtr.Zero;
        // 安全上限：防止 Wine 下枚举异常时陷入死循环
        const int MaxQueryRegions = 100000;
        int queried = 0;
        while (true)
        {
            MEMORY_BASIC_INFORMATION lpBuffer = default(MEMORY_BASIC_INFORMATION);
            if (VirtualQueryEx(pHandle, lpAddress, out lpBuffer, Marshal.SizeOf(lpBuffer)) == 0 || lpBuffer.RegionSize.ToInt64() > int.MaxValue)
            {
                break;
            }

            if (lpBuffer.State == 4096 && lpBuffer.Protect == 4 && lpBuffer.Type == 131072)
            {
                MemReg.Add(lpBuffer);
            }

            IntPtr nextAddress = IntPtr.Add(lpBuffer.BaseAddress, lpBuffer.RegionSize.ToInt32());
            // 防止 Wine 下出现 RegionSize==0 / 地址不前进导致枚举死循环
            if (nextAddress.ToInt64() <= lpAddress.ToInt64()) break;
            lpAddress = nextAddress;

            if (++queried >= MaxQueryRegions) break;
        }

        MemReg.Sort((MEMORY_BASIC_INFORMATION a, MEMORY_BASIC_INFORMATION b) => ((int)a.RegionSize).CompareTo((int)b.RegionSize));
    }
}
