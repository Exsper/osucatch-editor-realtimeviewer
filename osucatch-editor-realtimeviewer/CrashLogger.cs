using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace osucatch_editor_realtimeviewer
{
    /// <summary>
    /// 崩溃日志：程序出现未处理异常（或原生代码崩溃）时，
    /// 在 %LocalAppData%\OsuCatch-Editor-RealtimeViewer\logs 下生成 crash_*.log，
    /// 并始终维护一份 crash_latest.log 指向最近一次崩溃。
    /// </summary>
    public static class CrashLogger
    {
        /// <summary>最多保留多少份崩溃报告（本地日志体积控制）。</summary>
        private const int MaxCrashLogs = 2;

        private static readonly object crashWriteSync = new();
        private static int crashSerial;

        // ---------------- 托管异常崩溃报告 ----------------

        /// <summary>写崩溃报告，返回日志文件路径；失败返回 null。</summary>
        public static string? WriteCrashReport(string reason, Exception? exception)
        {
            try
            {
                int serial = Interlocked.Increment(ref crashSerial);
                string fileName = "crash_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + serial + ".log";
                string path = Path.Combine(Log.LogDirectory, fileName);

                Directory.CreateDirectory(Log.LogDirectory);
                string report = BuildReport(reason, exception);

                lock (crashWriteSync)
                {
                    File.WriteAllText(path, report, new UTF8Encoding(true));
                    File.WriteAllText(Path.Combine(Log.LogDirectory, "crash_latest.log"), report, new UTF8Encoding(true));
                }

                PruneOldCrashLogs();
                return path;
            }
            catch
            {
                return null; // 崩溃处理本身不能再抛异常
            }
        }

        /// <summary>尽量弹窗告知用户日志位置；弹窗失败时静默。</summary>
        public static void TryShowDialog(string? logPath, Exception? exception)
        {
            try
            {
                string message =
                    "程序发生未处理的异常，即将退出。\r\n\r\n" +
                    (exception == null ? "" : "异常: " + exception.GetType().Name + "\r\n" + exception.Message + "\r\n\r\n") +
                    "崩溃日志已保存到:\r\n" + (logPath ?? "(日志写入失败)") +
                    "\r\n\r\n请将该日志文件发送给开发者以便定位问题。";
                MessageBox.Show(message, "OsuCatch Editor RealtimeViewer - 崩溃", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
            }
        }

        private static string BuildReport(string reason, Exception? exception)
        {
            var sb = new StringBuilder();
            sb.AppendLine("================================================================");
            sb.AppendLine(" OsuCatch Editor RealtimeViewer - 崩溃日志 / Crash Report");
            sb.AppendLine("================================================================");
            sb.AppendLine("Time          : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.AppendLine("Reason        : " + reason);
            sb.AppendLine("Application   : " + GetExecutableName());
            sb.AppendLine("Version       : " + GetVersion());
            sb.AppendLine("Process ID    : " + Environment.ProcessId);
            sb.AppendLine("Thread ID     : " + Environment.CurrentManagedThreadId);
            sb.AppendLine("OS            : " + RuntimeInformation.OSDescription + " (" + (Environment.Is64BitProcess ? "x64" : "x86") + ")");
            sb.AppendLine(".NET Runtime  : " + RuntimeInformation.FrameworkDescription);
            sb.AppendLine("Command line  : " + Environment.CommandLine);
            sb.AppendLine("Settings file : " + FixedPathUserSettingsProvider.SettingsFilePath);

            AppendSettings(sb);

            if (exception != null)
            {
                int index = 0;
                for (Exception? current = exception; current != null && index < 20; current = current.InnerException, index++)
                {
                    sb.AppendLine();
                    sb.AppendLine("---- Exception " + index + " ----");
                    sb.AppendLine("Type       : " + current.GetType().FullName);
                    sb.AppendLine("Message    : " + current.Message);
                    sb.AppendLine("Source     : " + current.Source);
                    sb.AppendLine("TargetSite : " + current.TargetSite);
                    sb.AppendLine("Stack trace:");
                    sb.AppendLine(current.StackTrace ?? "(none)");
                }
            }

            sb.AppendLine();
            sb.AppendLine("---- Recent log (last lines) ----");
            sb.AppendLine(Log.GetRecentLines(2000));

            return sb.ToString();
        }

        private static void AppendSettings(StringBuilder sb)
        {
            try
            {
                sb.AppendLine();
                sb.AppendLine("---- Settings ----");
                foreach (System.Configuration.SettingsProperty property in app.Default.Properties)
                {
                    try
                    {
                        object? value = app.Default[property.Name];
                        sb.AppendLine(property.Name + " = " + (value?.ToString() ?? "<null>"));
                    }
                    catch
                    {
                        sb.AppendLine(property.Name + " = <read error>");
                    }
                }
            }
            catch
            {
                sb.AppendLine("(settings unavailable)");
            }
        }

        private static string GetExecutableName()
        {
            try
            {
                return Path.GetFileName(Environment.ProcessPath ?? AppDomain.CurrentDomain.FriendlyName);
            }
            catch
            {
                return AppDomain.CurrentDomain.FriendlyName;
            }
        }

        private static string GetVersion()
        {
            try
            {
                Assembly? assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                return assembly?.GetName().Version?.ToString() ?? "(unknown)";
            }
            catch
            {
                return "(unknown)";
            }
        }

        private static void PruneOldCrashLogs()
        {
            try
            {
                string[] files = Directory.GetFiles(Log.LogDirectory, "crash_*.log");
                if (files.Length <= MaxCrashLogs) return;
                foreach (string file in files.OrderByDescending(f => File.GetLastWriteTime(f)).Skip(MaxCrashLogs))
                {
                    try { File.Delete(file); }
                    catch { }
                }
            }
            catch
            {
            }
        }
    }
}
