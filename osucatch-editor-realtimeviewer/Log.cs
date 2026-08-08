using System.Text;

namespace osucatch_editor_realtimeviewer
{
    public static class Log
    {
        public enum LogType
        {
            Default,
            Program,
            EditorReader,
            BeatmapBuilder,
            BeatmapConverter,
            Drawing,
            Backup,
            Timer,
            Bookmark,
        }

        public enum LogLevel { Debug, Info, Warning, Error }

        /// <summary>
        /// 会话日志与崩溃日志的统一存放目录：
        /// %LocalAppData%\OsuCatch-Editor-RealtimeViewer\logs
        /// 与 user.config 同根目录，程序目录只读时也能写入。
        /// </summary>
        public static string LogDirectory { get; } = ResolveLogDirectory();

        /// <summary>内存中保留的最近日志条数，崩溃时随报告一起写出。</summary>
        private const int RecentBufferCapacity = 2000;

        /// <summary>单日日志文件超过该大小后只保留末尾一段。</summary>
        private const long MaxSessionLogBytes = 5 * 1024 * 1024;

        private static readonly object syncRoot = new();
        private static readonly Queue<string> recentLines = new(RecentBufferCapacity);
        private static readonly object sessionFileSync = new();
        private static bool sessionDirectoryReady;

        private static string ResolveLogDirectory()
        {
            try
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OsuCatch-Editor-RealtimeViewer",
                    "logs");
            }
            catch
            {
                return Path.Combine(Path.GetTempPath(), "OsuCatch-Editor-RealtimeViewer", "logs");
            }
        }

        public static void ConsoleLog(string msg, LogType logType = LogType.Default, LogLevel logLevel = LogLevel.Info)
        {
            if (logType == LogType.Program && !app.Default.Log_Program) return;
            if (logType == LogType.EditorReader && !app.Default.Log_EditorReader) return;
            if (logType == LogType.BeatmapBuilder && !app.Default.Log_BeatmapBuilder) return;
            if (logType == LogType.BeatmapConverter && !app.Default.Log_BeatmapConverter) return;
            if (logType == LogType.Drawing && !app.Default.Log_Drawing) return;
            if (logType == LogType.Backup && !app.Default.Log_Backup) return;
            if (logType == LogType.Timer && !app.Default.Log_Timer) return;
            if (logType == LogType.Bookmark && !app.Default.Log_Bookmark) return;

            if (app.Default.Log_Level > (int)logLevel) return;

            Append(FormatLine(logLevel, logType, msg));
        }

        /// <summary>
        /// 生命周期/崩溃事件日志：不受日志类型开关与级别限制，
        /// 始终写入内存缓冲和当日日志文件，用于在崩溃时还原程序走到哪一步。
        /// </summary>
        public static void Breadcrumb(string msg)
        {
            Append("[LIFE] [" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + msg);
        }

        /// <summary>取最近若干条日志（按时间正序），供崩溃报告使用。</summary>
        public static string GetRecentLines(int maxLines)
        {
            lock (syncRoot)
            {
                if (recentLines.Count == 0) return "(no recent log)";
                string[] lines = recentLines.ToArray();
                int start = Math.Max(0, lines.Length - maxLines);
                return string.Join(Environment.NewLine, lines, start, lines.Length - start);
            }
        }

        private static string FormatLine(LogLevel logLevel, LogType logType, string msg)
        {
            return "[" + logLevel + "] [" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] [" + logType + "] " + msg;
        }

        private static void Append(string line)
        {
            lock (syncRoot)
            {
                recentLines.Enqueue(line);
                while (recentLines.Count > RecentBufferCapacity) recentLines.Dequeue();
            }

            AppendToSessionFile(line);

            bool showConsole = false;
            try { showConsole = app.Default.Show_Console; }
            catch { }

            if (showConsole)
            {
                try { Console.WriteLine(line); }
                catch { /* 控制台不可用时忽略 */ }
            }
        }

        private static void AppendToSessionFile(string line)
        {
            try
            {
                string path = Path.Combine(LogDirectory, "log_" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                lock (sessionFileSync)
                {
                    if (!sessionDirectoryReady)
                    {
                        Directory.CreateDirectory(LogDirectory);
                        sessionDirectoryReady = true;
                    }

                    FileInfo info = new FileInfo(path);
                    if (info.Exists && info.Length > MaxSessionLogBytes)
                    {
                        KeepTail(path, MaxSessionLogBytes / 2);
                    }

                    using (FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (StreamWriter writer = new StreamWriter(fs, new UTF8Encoding(false)))
                    {
                        writer.Write(line);
                        writer.Write(Environment.NewLine);
                        writer.Flush();
                    }
                }
            }
            catch
            {
                // 日志写入失败绝不能影响主程序
            }
        }

        /// <summary>日志文件过大时只保留末尾一段（对齐到行），避免日志无限增长。</summary>
        private static void KeepTail(string path, long keepBytes)
        {
            byte[] tail;
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fs.Seek(-keepBytes, SeekOrigin.End);
                tail = new byte[fs.Length - fs.Position];
                int total = 0;
                while (total < tail.Length)
                {
                    int read = fs.Read(tail, total, tail.Length - total);
                    if (read == 0) break;
                    total += read;
                }
                if (total < tail.Length) Array.Resize(ref tail, total);
            }

            int start = 0;
            while (start < tail.Length && tail[start] != (byte)'\n') start++;
            if (start >= tail.Length) return;

            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                fs.Write(tail, start, tail.Length - start);
            }
        }
    }
}
