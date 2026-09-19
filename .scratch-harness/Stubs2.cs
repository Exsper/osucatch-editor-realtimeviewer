// Minimal stand-ins for the diagnostic harness: only what the compiled-in product files touch.
using System.Windows.Forms;

namespace osucatch_editor_realtimeviewer
{
    internal sealed class AppStub
    {
        public bool BarLine_Show { get; set; }
        public int BarLine_Subdivide { get; set; }
        public int BarLine_Mode { get; set; }
        public bool QuickToggle_Visible { get; set; } = true;
        public bool QuickToggle_Floating { get; set; }
        public int QuickToggle_Float_X { get; set; } = -1;
        public int QuickToggle_Float_Y { get; set; } = -1;
        public string QuickToggle_States { get; set; } = "";
        public string QuickToggle_HiddenGroups { get; set; } = "";

        public void Save() { }
    }

    internal static class app
    {
        public static AppStub Default { get; } = new AppStub();
    }

    /// <summary>Placeholder for the logging helper QuickToggleDocking uses on save failures.</summary>
    internal static class Log
    {
        internal enum LogType { Program }
        internal enum LogLevel { Warning }

        internal static void ConsoleLog(string message, LogType type, LogLevel level) { }
    }
}
