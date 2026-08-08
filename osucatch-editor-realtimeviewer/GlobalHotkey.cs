using System.Runtime.InteropServices;

namespace osucatch_editor_realtimeviewer
{
    public class GlobalHotkey
    {
        //如果函数执行成功，返回值不为0。
        //如果函数执行失败，返回值为0。要得到扩展错误信息，调用GetLastError。
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(
            IntPtr hWnd,                //要定义热键的窗口的句柄
            int id,                     //定义热键ID（不能与其它ID重复）           
            KeyModifiers fsModifiers,   //标识热键是否在按Alt、Ctrl、Shift、Windows等键时才会生效
            Keys vk                     //定义热键的内容
            );

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(
            IntPtr hWnd,                //要取消热键的窗口的句柄
            int id                      //要取消热键的ID
            );

        //定义了辅助键的名称（将数字转变为字符以便于记忆，也可去除此枚举而直接使用数值）
        [Flags()]
        public enum KeyModifiers
        {
            None = 0,
            Alt = 1,
            Ctrl = 2,
            Shift = 4,
            WindowsKey = 8
        }

        public static void UnRegisterGlobalHotKey(nint handle)
        {
            UnregisterHotKey(handle, 101);
            UnregisterHotKey(handle, 102);
            UnregisterHotKey(handle, 103);
            UnregisterHotKey(handle, 104);
            UnregisterHotKey(handle, 105);
            UnregisterHotKey(handle, 106);
            UnregisterHotKey(handle, 107);
            UnregisterHotKey(handle, 108);
        }

        public static void RegisterGlobalHotKey(nint handle)
        {
            // 注册热键
            bool success = RegisterHotKey(
                handle,
                101,
                KeyModifiers.Alt,
                Keys.D1
            );
            if (!success) MessageBox.Show("Register Alt+1 failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            success = RegisterHotKey(
                handle,
                102,
                KeyModifiers.Alt,
                Keys.D2
            );
            if (!success) MessageBox.Show("Register Alt+2 failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            success = RegisterHotKey(
                handle,
                103,
                KeyModifiers.Alt,
                Keys.D3
            );
            if (!success) MessageBox.Show("Register Alt+3 failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            success = RegisterHotKey(
                handle,
                104,
                KeyModifiers.Alt,
                Keys.D4
            );
            if (!success) MessageBox.Show("Register Alt+4 failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            success = RegisterHotKey(
                handle,
                105,
                KeyModifiers.Alt,
                Keys.D5
            );
            if (!success) MessageBox.Show("Register Alt+5 failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            success = RegisterHotKey(
                handle,
                106,
                KeyModifiers.Alt,
                Keys.D6
            );
            if (!success) MessageBox.Show("Register Alt+6 failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            success = RegisterHotKey(
                handle,
                107,
                KeyModifiers.Alt,
                Keys.D7
            );
            if (!success) MessageBox.Show("Register Alt+7 failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            success = RegisterHotKey(
                handle,
                108,
                KeyModifiers.Alt,
                Keys.D8
            );
            if (!success) MessageBox.Show("Register Alt+8 failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
