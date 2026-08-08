using System.Runtime.InteropServices;

namespace osucatch_editor_realtimeviewer
{
    public static class ProcessFocus
    {
        // Windows API：获取当前焦点窗口的句柄
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        // Windows API：获取窗口的进程 ID
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        private static int lastCursorX = int.MinValue;
        private static int lastCursorY = int.MinValue;

        /// <summary>
        /// 判断前台窗口是否属于指定进程。
        /// 只做两次 P/Invoke + 进程 ID 比较，不再每次创建 Process 对象读取模块信息，
        /// 也避免了 MainModule 访问权限/进程退出竞态导致的异常。
        /// </summary>
        public static bool IsEditorForeground(int? osuProcessId)
        {
            if (osuProcessId == null) return false;

            // 获取当前焦点窗口的句柄
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero) return false;

            // 获取焦点窗口的进程 ID
            if (GetWindowThreadProcessId(foregroundWindow, out uint processId) == 0) return false;

            return processId == osuProcessId.Value;
        }

        /// <summary>
        /// 鼠标是否正在移动（通过轮询光标位置变化判断）。
        /// 编辑器操作通常是"悬停到目标位置再点击"，鼠标移动期间需要实时刷新预览。
        /// </summary>
        public static bool IsMouseMoving()
        {
            if (!GetCursorPos(out POINT pt)) return false;

            bool moving = pt.X != lastCursorX || pt.Y != lastCursorY;
            lastCursorX = pt.X;
            lastCursorY = pt.Y;
            return moving;
        }
    }
}
