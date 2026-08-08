using System.Runtime.InteropServices;

namespace osucatch_editor_realtimeviewer
{
    internal static class Program
    {
        private static int crashHandled;

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        public static void ShowConsole()
        {
            AllocConsole();

            // AllocConsole 之后必须重新绑定 Console 输出流：
            // .NET 会缓存首次访问 Console 时的标准句柄，而启动日志（Breadcrumb）
            // 会在 AllocConsole 之前第一次访问 Console，导致之后的 WriteLine
            // 全部写到旧的空句柄上，控制台窗口虽然出现却没有输出。
            try
            {
                var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                Console.SetOut(stdout);

                var stderr = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
                Console.SetError(stderr);
            }
            catch
            {
                // 重绑定失败不影响主程序，最多丢失控制台输出
            }
        }

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            InstallCrashHandlers();

            try
            {
                // To customize application configuration such as set high DPI settings or default font,
                // see https://aka.ms/applicationconfiguration.
                ApplicationConfiguration.Initialize();
                Log.Breadcrumb("Main: configuration initialized.");

                Log.Breadcrumb("Main: creating main form...");
                Form1 form = new Form1();
                Log.Breadcrumb("Main: main form created.");

                Log.Breadcrumb("Main: entering message loop.");
                Application.Run(form);
                Log.Breadcrumb("Main: message loop exited normally.");
            }
            catch (Exception ex)
            {
                HandleCrash("Main entry point", ex, exit: true);
            }
        }

        /// <summary>
        /// 在启动最早期注册全局异常处理，保证任何未处理异常都能留下日志。
        /// </summary>
        private static void InstallCrashHandlers()
        {
            Log.Breadcrumb("Application starting. .NET " + Environment.Version + " " + (Environment.Is64BitProcess ? "x64" : "x86") + ".");

            // 任意线程上未处理的异常都会终止进程，先写日志再让进程退出
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                HandleCrash(args.IsTerminating ? "Unhandled exception (terminating)" : "Unhandled exception", args.ExceptionObject as Exception, exit: false);

            // UI 线程异常
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, args) =>
                HandleCrash("Unhandled UI thread exception", args.Exception, exit: true);

            // Task 未观察异常不会直接崩溃，记录一条即可
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Log.Breadcrumb("Unobserved task exception: " + args.Exception?.GetType().Name + ": " + args.Exception?.Message);
                args.SetObserved();
            };

            Log.Breadcrumb("Crash handlers installed.");
        }

        private static void HandleCrash(string reason, Exception? exception, bool exit)
        {
            // 多个处理器可能先后触发，只处理第一个，避免重复写日志/弹窗
            if (Interlocked.Exchange(ref crashHandled, 1) != 0)
            {
                Log.Breadcrumb("Ignoring additional crash event: " + reason);
                return;
            }

            Log.Breadcrumb("CRASH: " + reason + (exception == null ? "" : " (" + exception.GetType().Name + ")"));
            string? logPath = CrashLogger.WriteCrashReport(reason, exception);
            Log.Breadcrumb("Crash report written: " + (logPath ?? "(failed)"));
            CrashLogger.TryShowDialog(logPath, exception);

            if (exit)
            {
                try { Environment.Exit(1); }
                catch { }
            }
        }

    }
}
