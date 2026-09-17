namespace osucatch_editor_realtimeviewer
{
    /// <summary>
    /// 主工程 Log 的替身：EditorReader 的内部错误日志走到这里。
    /// 默认只累计计数，避免在计时循环里往控制台写 IO 把数字带偏。
    /// </summary>
    public static class Log
    {
        public enum LogType { Default, Program, EditorReader, BeatmapBuilder, BeatmapConverter, Drawing, Backup, Timer, Bookmark }

        public enum LogLevel { Debug, Info, Warning, Error }

        public static long Count;

        public static bool Echo;

        public static readonly List<string> Recent = new();

        public static void ConsoleLog(string msg, LogType logType = LogType.Default, LogLevel logLevel = LogLevel.Info)
        {
            Interlocked.Increment(ref Count);
            lock (Recent)
            {
                if (Recent.Count < 200)
                {
                    Recent.Add("[" + logLevel + "][" + logType + "] " + msg);
                }
            }

            if (Echo)
            {
                Console.WriteLine("    [osu-reader] " + logLevel + ": " + msg);
            }
        }

        public static void Breadcrumb(string msg) => ConsoleLog(msg, LogType.Program, LogLevel.Debug);
    }
}
