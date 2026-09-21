using Microsoft.Win32;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Objects;
using System.ComponentModel;
using System.Diagnostics;
using System.Timers;

namespace osucatch_editor_realtimeviewer
{
    public partial class Form1 : Form
    {
        public static string Path_Settings = "settings.txt";

        public EditorReaderHelper editorReaderHelper = new();

        public static DrawingHelper drawingHelper = new();

        public static BeatmapConverter lazerBeatmapConverter => new BeatmapConverter();
        public static BeatmapConverter stableBeatmapConverter => new BeatmapConverterOsuStable();

        // 后台流水线：_committed 是当前正在绘制的数据；重建在后台线程完成后才原子切换
        private CommittedState _committed = new CommittedState();
        private Task<CommittedState>? _rebuildTask;
        private int _rebuildTaskGeneration;
        private int _rebuildGeneration;
        private long _rebuildRetryTicks;

        // 模板谱面（只读参考）：三个菜单项都在构造函数里由 CreateTemplateMenu 创建，
        // 构造完成后必然非空，所以声明成不可空（null! 只是让编译器知道“稍后在构造函数里赋值”）
        private ToolStripMenuItem templateToolStripMenuItem = null!;
        private ToolStripMenuItem selectTemplateStripMenuItem = null!;
        private ToolStripMenuItem unloadTemplateStripMenuItem = null!;
        private TemplateBeatmapData? templateData;

        // 快捷开关条：可吸附在菜单栏下方（横排）或画布左 / 右侧（竖排），也可拖出为浮动小窗口
        private QuickToggleBar? quickToggleBar;
        private QuickToggleDockRow? quickToggleDockRow;
        private QuickToggleDocking? quickToggleDocking;
        private ToolStripMenuItem? quickToggleStripMenuItem;
        private ToolStripMenuItem? quickToggleDockSideToolStripMenuItem;
        private ToolStripMenuItem? quickToggleDockTopToolStripMenuItem;
        private ToolStripMenuItem? quickToggleDockLeftToolStripMenuItem;
        private ToolStripMenuItem? quickToggleDockRightToolStripMenuItem;

        #region 快捷开关条：开关标识与功能区

        /// <summary>快捷开关“固定预览时刻”的标识（勾选状态随其它开关一起持久化）。</summary>
        private const string FreezePreviewTimeKey = "FreezePreviewTime";

        // MOD 功能区（单选：NM / EZ / HR）
        private const string ModGroupKey = "Mod";
        private const string ModNoneKey = "Mod_None";
        private const string ModEasyKey = "Mod_EZ";
        private const string ModHardRockKey = "Mod_HR";

        /// <summary>MOD 功能区的三个开关，顺序与 <see cref="ModMode"/> 一致。</summary>
        private static readonly string[] ModKeys = { ModNoneKey, ModEasyKey, ModHardRockKey };

        // 果子标注功能区（单选：隐藏 / 距离-正常 / 距离-忽略SVM / 难度星数）
        private const string LabelGroupKey = "HitObjectLabel";
        private const string LabelHiddenKey = "Label_Hidden";
        private const string LabelDistanceKey = "Label_Distance";
        private const string LabelIgnoreSvmKey = "Label_IgnoreSVM";
        private const string LabelStarsKey = "Label_Stars";

        /// <summary>果子标注功能区的四个开关，顺序与 <see cref="QuickLabelMode"/> 一致。</summary>
        private static readonly string[] LabelKeys = { LabelHiddenKey, LabelDistanceKey, LabelIgnoreSvmKey, LabelStarsKey };

        // 拍线功能区（滑块 + 状态文字）
        private const string BarLineGroupKey = "BarLine";
        private const string BarLineValueKey = "BarLineMode";

        // 垂直缩放功能区（滑块 + 当前比例文字）
        private const string VerticalScaleGroupKey = "VerticalScale";
        private const string VerticalScaleValueKey = "VerticalScale";

        /// <summary>
        /// 垂直缩放滑块的取值范围与默认值，<b>以 0.1 为单位</b>：5 ~ 40 对应画面 Y 轴的 x0.5 ~ x4.0，默认 x1.0（= 10）。
        /// <para /><see cref="TrackBar"/> 只能取整数，所以滑块内部用“十分之一倍率”表示：
        /// 一个刻度 = 0.1，正好是需要的精度；显示时才换算成倍率。
        /// </para>
        /// </summary>
        private const int VerticalScaleMinTenths = 5;
        private const int VerticalScaleMaxTenths = 40;
        private const int VerticalScaleDefaultTenths = 10;

        /// <summary>滑块刻度间隔（同样以 0.1 为单位）：每 0.5 倍画一根刻度线。</summary>
        private const int VerticalScaleTickTenths = 5;

        /// <summary>一格滚轮（Ctrl + 滚轮）调整多少：1 = 0.1，与滑块步进一致。</summary>
        private const int VerticalScaleTenthsPerWheelNotch = 1;

        /// <summary>
        /// 当前 Y 轴缩放（以 0.1 为单位）。与滑块位置、<see cref="DrawingHelper.VerticalScale"/>
        /// 一起由 <see cref="ApplyVerticalScale"/> 统一维护，Ctrl + 滚轮也走同一条路径，
        /// 这样“滚轮改完滑块跟着动”天然成立（浮点倍率不适合拿来累加，故另存一份整数）。
        /// </summary>
        private int verticalScaleTenths = VerticalScaleDefaultTenths;

        /// <summary>快捷开关条上的下拉框 / 滑块所用的值标识。</summary>
        private const string FreezeValueKey = FreezePreviewTimeKey;

        /// <summary>
        /// 果子标注的快捷开关模式。只覆盖菜单里最常用的 4 种，
        /// 因此与 <see cref="HitObjectLabelType"/>（还含“比较行走速度 / 连击内果子数”）不是同一个枚举。
        /// </summary>
        private enum QuickLabelMode
        {
            Hidden = 0,
            Distance = 1,
            DistanceIgnoreSvm = 2,
            Stars = 3,
        }

        /// <summary>MOD 快捷开关模式。</summary>
        private enum ModMode
        {
            None = 0,
            Easy = 1,
            HardRock = 2,
        }

        /// <summary>已经创建过快捷开关条内容（避免语言切换重复添加控件）。</summary>
        private bool quickTogglesCreated;

        #endregion

        /// <summary>预览时刻是否被固定（“固定预览时刻”开关打开）：为 true 时预览画布不再跟随编辑器。</summary>
        private bool previewTimeFrozen;

        /// <summary>固定预览时刻后是否还没完成“钉住”（启动恢复固定状态、或还没读到过 editor 时刻）。</summary>
        private bool previewTimePinPending;

        /// <summary>是否已经从编辑器读到过时刻（用于避免把预览固定在未初始化的 0 上）。</summary>
        private bool editorTimeAvailable;

        /// <summary>
        /// 已提交（正在绘制）的解析/转换结果快照。
        /// </summary>
        private sealed class CommittedState
        {
            public BeatmapInfoCollection? Reader;
            public List<string>? ColourLines;
            public Beatmap? Beatmap;
            public IBeatmap? ConvertedBeatmap;
            public DrawingHelper Drawing = new();
            public int Mods = -1;
            public HitObjectLabelType LabelType = HitObjectLabelType.None;
            public bool ConverterIsStable;
        }

        /// <summary>
        /// 消费后台重建任务的结果：任务已完成且代次未过期时，把新数据原子地提交到绘制状态。
        /// </summary>
        private void ConsumeFinishedRebuild()
        {
            if (_rebuildTask == null || !_rebuildTask.IsCompleted) return;

            try
            {
                CommittedState? newState = _rebuildTask.GetAwaiter().GetResult();
                if (newState != null && _rebuildTaskGeneration == _rebuildGeneration)
                {
                    CommitState(newState);
                }
            }
            catch (Exception ex)
            {
                Log.ConsoleLog("Background rebuild failed.\r\n" + ex, Log.LogType.BeatmapBuilder, Log.LogLevel.Error);
                _rebuildRetryTicks = DateTime.Now.Ticks;
            }
            finally
            {
                _rebuildTask = null;
            }
        }

        private void CommitState(CommittedState newState)
        {
            _committed = newState;
            drawingHelper.ApplyBuildResult(newState.Drawing);
        }

        bool Need_Backup = false;
        Int64 LastDrawingTimeStamp = DateTime.Now.Ticks;
        int dpi = 96;
        float fontscale = 1;

        bool topmostCheck = false;
        bool lastTopmostApplied = false;
        bool lastMemoryOverThreshold = false;

        private SettingsForm? SettingsFormInstance = null;
        private BookmarkSettingsForm? BookmarkSettingsFormInstance = null;

        public static string Path_Img_Hitcircle = ResolveImagePath(@"img/fruit-apple.png");
        public static string Path_Img_Drop = ResolveImagePath(@"img/fruit-drop.png");
        public static string Path_Img_Banana = ResolveImagePath(@"img/fruit-bananas.png");

        /// <summary>
        /// 贴图路径按程序所在目录解析（exe 目录下的 img\），
        /// 避免在 Wine/osu-winello 下因工作目录被批处理切到 D:\（osu 目录）而按相对路径找不到贴图；
        /// 若程序目录下不存在该文件，则回退到原相对路径（即当前工作目录）。
        /// </summary>
        private static string ResolveImagePath(string relativePath)
        {
            string exeDirPath = Path.Combine(AppContext.BaseDirectory, relativePath);
            return File.Exists(exeDirPath) ? exeDirPath : relativePath;
        }

        public static bool NeedReapplySettings = false;
        public static bool NeedReapplyBookmarkStyles = false;

        public BookmarkManager bookmarkManager = new BookmarkManager();

        private static System.Timers.Timer backup_timer = new System.Timers.Timer(app.Default.Backup_Interval);
        private static System.Timers.Timer Memory_Monitor_Timer = new System.Timers.Timer(200);

        /// <summary>读取定时器；在 <see cref="Form1_Load"/> 里创建，之前的代码不应该碰它。</summary>
        private PeriodicTaskRunner runner = null!;
        private HealthMonitor? healthMonitor;

        public Form1()
        {
            Log.Breadcrumb("Form1: constructing...");
#if LEGACY_EDITOR_READER
            Log.Breadcrumb("EditorReader 实现: 旧版（EditorReaderLegacy，兼容性优先）");
#else
            Log.Breadcrumb("EditorReader 实现: 新版（EditorReader，含位宽/跨页/诊断改进）");
#endif
            InitializeComponent();

            // 模板菜单在构造函数里创建，确保语言资源能应用到它
            CreateTemplateMenu();

            // 快捷开关条（功能区内容在 CreateQuickToggles 里添加）
            CreateQuickToggleBar();
            CreateQuickToggles();
            ApplyQuickToggleLanguage();

            if (app.Default.Language_String != "")
            {
                defaultLanguageToolStripMenuItem.Checked = false;

                englishLanguageToolStripMenuItem.Checked = (app.Default.Language_String == "en-US");
                zhHansLanguageToolStripMenuItem.Checked = (app.Default.Language_String == "zh-Hans");
                Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(app.Default.Language_String);
                Form1.ApplyResources(this);
            }
            else
            {
                defaultLanguageToolStripMenuItem.Checked = true;
                englishLanguageToolStripMenuItem.Checked = false;
                zhHansLanguageToolStripMenuItem.Checked = false;
                // 跟随系统语言：模板菜单也要应用资源文本
                Form1.ApplyResources(this);
            }

            // ApplyResources 会按 resx 重设 Canvas 的 Location/Anchor（设置 Anchor 会清空 Dock），
            // 这里重新确认停靠布局：画布必须始终从菜单栏实际下沿开始，
            // 高 DPI（如 200% 缩放）下菜单变高时才不会被画布遮挡
            Canvas.Dock = DockStyle.Fill;
            Canvas.Margin = new Padding(0);

            // 窗口过窄时菜单项收进右侧溢出按钮（…），避免被直接裁剪；
            // 必须在 CreateTemplateMenu（插入模板菜单项）之后执行
            menuStrip1.CanOverflow = true;
            foreach (ToolStripItem item in menuStrip1.Items)
            {
                item.Overflow = ToolStripItemOverflow.AsNeeded;
            }

            if (app.Default.Window_X >= 0 && app.Default.Window_Y >= 0 &&
                IsPositionOnScreen(app.Default.Window_X, app.Default.Window_Y))
            {
                this.StartPosition = FormStartPosition.Manual;
                this.Location = new System.Drawing.Point(app.Default.Window_X, app.Default.Window_Y);
            }

            Log.Breadcrumb("Form1: constructed.");
        }

        private string Select_Osu_Path()
        {
            FolderBrowserDialog folder = new FolderBrowserDialog();
            folder.ShowNewFolderButton = false;
            folder.RootFolder = Environment.SpecialFolder.MyComputer;
            folder.Description = "Select osu! Folder";
            DialogResult path = folder.ShowDialog();
            if (path == DialogResult.OK)
            {
                //check if osu!.exe is present
                if (!File.Exists(System.IO.Path.Combine(folder.SelectedPath, "osu!.exe")))
                {
                    MessageBox.Show("No osu!.exe in this folder!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return Select_Osu_Path();
                }
            }
            return folder.SelectedPath;
        }

        public static string GetOsuPath()
        {
            using (RegistryKey? osureg = Registry.ClassesRoot.OpenSubKey("osu\\DefaultIcon"))
            {
                if (osureg != null)
                {
                    string? osukey = osureg.GetValue(null)?.ToString();
                    if (osukey == null) return "";
                    string osupath = osukey.Remove(0, 1);
                    osupath = osupath.Remove(osupath.Length - 11);
                    return osupath;
                }
                else
                {
                    Log.ConsoleLog("Could not find osu path from registry.", Log.LogType.Program, Log.LogLevel.Warning);
                    return "";
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Log.Breadcrumb("Form1_Load: begin.");
            healthMonitor = new HealthMonitor(this);

            // -----------------------reading settings-----------------------
            // show log console
            if (app.Default.Show_Console) Program.ShowConsole();

            // window size
            // 尺寸上限为所有屏幕的并集：防止上次最大化/异常值被存成普通尺寸后，
            // 下次启动窗口铺满桌面且无法缩放（Wine 下尤为明显）
            System.Drawing.Rectangle virtualScreen = SystemInformation.VirtualScreen;
            this.Width = Math.Clamp(app.Default.Window_Width, 200, virtualScreen.Width);
            this.Height = Math.Clamp(app.Default.Window_Height, 200, virtualScreen.Height);
            SizeChanged += Form1_SizeChanged;

            if (app.Default.Window_Maximized) this.WindowState = FormWindowState.Maximized;

            topmostCheck = app.Default.Auto_Topmost;
            TopWhenEditorFocusToolStripMenuItem.Checked = topmostCheck;

            cubicFittingCurveToolStripMenuItem.Checked = app.Default.Show_CubicFittingCurve;

            ReapplyBookmarkStyles();

            // 快捷开关栏：恢复功能区显示状态、开关状态与吸附位置 / 浮动状态（浮窗要等主窗口显示出来后再弹出）
            quickToggleBar?.ApplyHiddenGroups(app.Default.QuickToggle_HiddenGroups);
            quickToggleBar?.ApplyCheckedStates(app.Default.QuickToggle_States);
            // ApplyCheckedStates 不触发事件，这里手动把各开关的内部状态同步过来
            SetPreviewTimeFrozen(quickToggleBar?.IsChecked(FreezePreviewTimeKey) ?? false);
            SyncModToggleFromMenu();
            SyncLabelToggleFromMenu();
            ApplyBarLineMode(BarLineSettings.CurrentMode, persist: false);
            // 垂直缩放不持久化：每次启动都把滑块与画面恢复成默认的 x1.0
            ApplyVerticalScale(VerticalScaleDefaultTenths);
            // Ctrl + 滚轮缩放 Y 轴：滚轮消息发给“拥有键盘焦点的窗口”（焦点可能在画布上），
            // 用消息过滤器在消息分发前拦下，不依赖 WinForms 的滚轮冒泡行为
            wheelFilter = new VerticalScaleWheelFilter(this);
            Application.AddMessageFilter(wheelFilter);
            FormClosed += (sender, e) =>
            {
                if (wheelFilter != null) Application.RemoveMessageFilter(wheelFilter);
            };
            quickToggleDocking?.ApplyStartupState(
                app.Default.QuickToggle_Visible,
                app.Default.QuickToggle_Floating,
                QuickToggleDockSideHelper.ParseDockSide(app.Default.QuickToggle_DockSide));
            if (quickToggleStripMenuItem != null) quickToggleStripMenuItem.Checked = app.Default.QuickToggle_Visible;
            SyncQuickToggleDockSideMenu();

            // osu path
            if (app.Default.osu_path == "")
            {
                string osu_path = "";
                osu_path = GetOsuPath();
                if (osu_path == "")
                {
                    osu_path = Select_Osu_Path();
                }
                app.Default.osu_path = osu_path;
                app.Default.Save();
            }

            // converter
            if (app.Default.Use_Stable_Converter)
            {
                lazerConverterToolStripMenuItem.Checked = false;
                stableConverterToolStripMenuItem.Checked = true;
            }
            else
            {
                lazerConverterToolStripMenuItem.Checked = true;
                stableConverterToolStripMenuItem.Checked = false;
            }

            // contain screens count
            drawingHelper.ScreensContain = app.Default.ScreensContain;
            Canvas.screensContain = app.Default.ScreensContain;
            Canvas.UseBatchRendering = app.Default.Use_Batch_Rendering;
            ToolStripMenuItem[] screensMenuItems = {
                Screens1ToolStripMenuItem,
                Screens2ToolStripMenuItem,
                Screens3ToolStripMenuItem,
                Screens4ToolStripMenuItem,
                Screens5ToolStripMenuItem,
                Screens6ToolStripMenuItem,
                Screens7ToolStripMenuItem,
                Screens8ToolStripMenuItem,
            };
            for (int i = 0; i < screensMenuItems.Length; i++)
            {
                if (i == app.Default.ScreensContain - 1) screensMenuItems[i].Checked = true;
                else screensMenuItems[i].Checked = false;
            }
            // --------------------------------------------------------------

            Log.Breadcrumb("Form1_Load: settings applied.");

            // ----------------------------get dpi---------------------------
            Graphics graphics = this.CreateGraphics();
            dpi = (Int32)graphics.DpiX;
            Log.ConsoleLog("DPI: " + dpi, Log.LogType.Program, Log.LogLevel.Info);
            fontscale = 96f / dpi;
            Log.ConsoleLog("Text Scale x" + fontscale.ToString("F2"), Log.LogType.Program, Log.LogLevel.Info);
            Canvas.fontScale = fontscale;
            graphics.Dispose();
            // --------------------------------------------------------------

            // canvas init
            Log.Breadcrumb("Form1_Load: initializing canvas (OpenGL)...");
            this.Canvas.Init();
            Log.Breadcrumb("Form1_Load: canvas initialized.");

            // reader timer
            runner = new PeriodicTaskRunner(app.Default.Drawing_Interval, app.Default.Idle_Interval, reader_timer_Work);
            runner.Start();

            // backup timer
            backup_timer.Elapsed += backup_timer_Tick;
            backup_timer.Start();

            // memory monitor timer
            Memory_Monitor_Timer.Elapsed += Memory_Monitor;
            Memory_Monitor_Timer.Start();

            Log.Breadcrumb("Form1_Load: timers started.");


            // RegisterHotKey
            if (app.Default.Bookmark_RegisterHotKey)
            {
                GlobalHotkey.RegisterGlobalHotKey(this.Handle);
            }

            Log.Breadcrumb("Form1_Load: done.");
        }

        private void Memory_Monitor(object? sender, EventArgs e)
        {
            long memorySize = System.GC.GetTotalMemory(false);
            long requiredMemory = 1024 * 1024 * 1000; // 1G

            bool overThreshold = memorySize > requiredMemory;
            if (overThreshold != lastMemoryOverThreshold)
            {
                lastMemoryOverThreshold = overThreshold;
                Log.ConsoleLog("Total Memory: " + (1.0 * memorySize / 1024 / 1024).ToString("F3") + "MB", Log.LogType.Program, overThreshold ? Log.LogLevel.Warning : Log.LogLevel.Debug);
            }

            CheckTopmost();
        }

        private void CheckTopmost()
        {
            if (this == null || this.IsDisposed || this.Disposing)
            {
                return;
            }

            bool shouldTopmost = topmostCheck && (SettingsFormInstance == null) && (BookmarkSettingsFormInstance == null)
                && ProcessFocus.IsEditorForeground(editorReaderHelper.OsuProcessId);

            // 状态没变化就不 Invoke，避免每 200ms 无条件设置 TopMost
            if (shouldTopmost == lastTopmostApplied) return;

            lastTopmostApplied = shouldTopmost;
            Invoke(new MethodInvoker(delegate ()
            {
                if (this != null && !this.IsDisposed && !this.Disposing) this.TopMost = shouldTopmost;
                // 浮动小窗口跟随主窗口置顶，编辑器在前台时开关也能一直看见
                quickToggleDocking?.SyncTopMost(shouldTopmost);
            }));
        }

        private void ReapplySettings()
        {
            Invoke(new MethodInvoker(delegate ()
            {
                System.Drawing.Rectangle virtualScreen = SystemInformation.VirtualScreen;
                this.Width = Math.Clamp(app.Default.Window_Width, 200, virtualScreen.Width);
                this.Height = Math.Clamp(app.Default.Window_Height, 200, virtualScreen.Height);

            }));
            runner.SetInterval(app.Default.Drawing_Interval, app.Default.Idle_Interval);

            // 设置窗口改了拍线模式：把快捷开关栏上的滑块/状态文字同步过来
            Invoke(new MethodInvoker(delegate ()
            {
                ApplyBarLineMode(BarLineSettings.CurrentMode, persist: false);
            }));

            if (app.Default.Backup_Enabled)
            {
                backup_timer.Interval = app.Default.Backup_Interval;
            }
            if (app.Default.FilterNearbyHitObjects)
            {
                backupToolStripMenuItem.Enabled = false;
            }
            else
            {
                backupToolStripMenuItem.Enabled = true;
            }
        }

        private void ReapplyBookmarkStyles()
        {
            Invoke(new MethodInvoker(delegate ()
            {
                string setdel = "Set/Del ";
                if (Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "zh")
                {
                    setdel = "标记/删除 ";
                }

                bookmarkSetStripMenuItem_1.Text = setdel + ((app.Default.Bookmark_Comment_1 != "") ? app.Default.Bookmark_Comment_1 : "Type 1");
                bookmarkSetStripMenuItem_2.Text = setdel + ((app.Default.Bookmark_Comment_2 != "") ? app.Default.Bookmark_Comment_2 : "Type 2");
                bookmarkSetStripMenuItem_3.Text = setdel + ((app.Default.Bookmark_Comment_3 != "") ? app.Default.Bookmark_Comment_3 : "Type 3");
                bookmarkSetStripMenuItem_4.Text = setdel + ((app.Default.Bookmark_Comment_4 != "") ? app.Default.Bookmark_Comment_4 : "Type 4");
                bookmarkSetStripMenuItem_5.Text = setdel + ((app.Default.Bookmark_Comment_5 != "") ? app.Default.Bookmark_Comment_5 : "Type 5");
                bookmarkSetStripMenuItem_6.Text = setdel + ((app.Default.Bookmark_Comment_6 != "") ? app.Default.Bookmark_Comment_6 : "Type 6");
                bookmarkSetStripMenuItem_7.Text = setdel + ((app.Default.Bookmark_Comment_7 != "") ? app.Default.Bookmark_Comment_7 : "Type 7");
                bookmarkSetStripMenuItem_8.Text = setdel + ((app.Default.Bookmark_Comment_8 != "") ? app.Default.Bookmark_Comment_8 : "Type 8");

            }));

            if (bookmarkManager.Bookmarks.Count > 0 && bookmarkManager.BeatmapFilename != "")
            {
                if (app.Default.Bookmark_AutoLoadSave)
                {
                    // 自动更新书签
                    string filepath = Path.Combine(app.Default.Bookmark_FolderPath, bookmarkManager.BeatmapFolder, bookmarkManager.BeatmapFilename) + ".bps";
                    BookmarkPlus.SaveBookmarksToFile(filepath, bookmarkManager.Bookmarks);
                }
            }
        }

        private bool FetchOsuProcess()
        {

            if (!editorReaderHelper.FetchProcess())
            {
                // 还没有任何可绘制数据时也要让状态栏说明原因（有数据时由 DrawLastKnownFrame 一并更新）
                if (_committed.Reader == null) SetStatusText("Osu!.exe is not running");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 检查编辑器绑定。
        /// <para />返回 false 时调用方应继续用上一份有效数据绘制（详见 <see cref="DrawLastKnownFrame"/>），
        /// 而不是清空数据并抛异常中断这一帧——那正是"错误刷屏 + 画面冻结"的原因。
        /// </summary>
        private bool FetchEditor()
        {
            if (!editorReaderHelper.FetchEditor())
            {
                if (_committed.Reader == null) SetStatusText(UnavailableStatusText());
                return false;
            }
            return true;
        }

        /// <summary>
        /// 读取不可用时的状态栏文本：区分"编辑器没打开"（正常空闲）、"正在重绑"与"读取失败"。
        /// </summary>
        private string UnavailableStatusText()
        {
            if (editorReaderHelper.IsRebinding) return "Re-binding editor...";
            if (editorReaderHelper.LastEditorState == EditorReaderHelper.EditorState.Active) return "Editor read failed, retrying";
            return StatusTextFor(editorReaderHelper.LastEditorState);
        }

        /// <summary>
        /// 状态栏文本：区分"编辑器没打开"（正常空闲）与"读取失败"（故障）。
        /// </summary>
        private static string StatusTextFor(EditorReaderHelper.EditorState state)
        {
            return state switch
            {
                EditorReaderHelper.EditorState.Active => "Drawing",
                EditorReaderHelper.EditorState.NotInEditor => "Editor is not running",
                EditorReaderHelper.EditorState.Failed => "Editor read failed, retrying",
                _ => "Connecting to editor",
            };
        }

        private void SetStatusText(string text)
        {
            try
            {
                if (IsDisposed || Disposing) return;
                Invoke(new MethodInvoker(delegate ()
                {
                    if (IsDisposed || Disposing) return;
                    if (StateToolStripStatusLabel.Text != text) StateToolStripStatusLabel.Text = text;
                }));
            }
            catch (Exception ex)
            {
                // 窗口正在关闭时 Invoke 会失败，这里不影响读取循环
                Log.ConsoleLog("Set status text failed.\r\n" + ex, Log.LogType.Program, Log.LogLevel.Debug);
            }
        }

        /// <summary>
        /// 用上一份有效数据绘制一帧：读取失败/编辑器暂时不可用时保持画面，只更新状态栏。
        /// 没有任何有效数据时什么都不做（等待第一次成功读取）。
        /// </summary>
        private void DrawLastKnownFrame(string statusText)
        {
            if (_committed.Reader == null) return;

            RequestDraw(null, null, statusText);
        }

        /// <summary>
        /// 向 UI 线程提交一帧绘制。
        /// <para /><paramref name="editorTime"/> 为 null 时保持当前播放头不变：
        /// 读取失败时沿用上一份数据绘制，不应把播放头退回旧值。
        /// </summary>
        private void RequestDraw(int? editorTime, string? title, string statusText)
        {
            try
            {
                if (editorTime.HasValue) ApplyEditorTime(editorTime.Value);

                Invoke(new MethodInvoker(delegate ()
                {
                    if (IsDisposed || Disposing) return;
                    if (title != null && this.Text != title) this.Text = title;
                    if (StateToolStripStatusLabel.Text != statusText) StateToolStripStatusLabel.Text = statusText;
                    this.Canvas.Canvas_Paint(null, null);
                }));
                Log.ConsoleLog("Draw a frame successful.", Log.LogType.Drawing, Log.LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Log.ConsoleLog("Draw a frame failed.\r\n" + ex, Log.LogType.Drawing, Log.LogLevel.Debug);
            }

            if (DateTime.Now.Ticks > LastDrawingTimeStamp) LastDrawingTimeStamp = DateTime.Now.Ticks;
        }

        /// <summary>
        /// 在后台线程执行完整解析链路：构建 beatmap → 转换 → 装载绘图对象。
        /// 物件处理顺序与原来的同步实现完全一致，因此转换阶段的随机数序列不变。
        /// </summary>
        private CommittedState BuildNewState(
            BeatmapInfoCollection thisReader,
            CommittedState committed,
            string filepath,
            int mods,
            HitObjectLabelType labelType,
            bool converterIsStable,
            DifferenceType differenceType)
        {
            var newState = new CommittedState
            {
                Reader = thisReader,
                Mods = mods,
                LabelType = labelType,
                ConverterIsStable = converterIsStable,
                ColourLines = committed.ColourLines,
            };

            Log.ConsoleLog("Start build new beatmap.", Log.LogType.BeatmapBuilder, Log.LogLevel.Debug);
            Beatmap? beatmap;
            if (differenceType == DifferenceType.DifferentFile)
            {
                // fetch colors and beatmap version because editor reader doesn't fetch it.
                try
                {
                    beatmap = BeatmapBuilder.BuildNewBeatmapWithFilePath(thisReader, filepath, out var colourLines);
                    newState.ColourLines = colourLines;
                }
                catch (Exception ex)
                {
                    Log.ConsoleLog("Build new beatmap from beatmap file failed.\r\n" + ex, Log.LogType.BeatmapBuilder, Log.LogLevel.Error);
                    throw;
                }
            }
            else if (differenceType == DifferenceType.DifferentObjects)
            {
                // when osu! finished loading beatmap, beatmap version will automatically be updated to v14
                // so if you edit the map, we can assume it uses v14 format
                try
                {
                    thisReader.BeatmapVersion = 14;
                    beatmap = BeatmapBuilder.BuildNewBeatmapWithColorString(thisReader, newState.ColourLines);
                }
                catch (Exception ex)
                {
                    Log.ConsoleLog("Build new beatmap from reader failed.\r\n" + ex, Log.LogType.BeatmapBuilder, Log.LogLevel.Error);
                    throw;
                }
            }
            else
            {
                beatmap = committed.Beatmap;
            }
            if (beatmap == null) throw new Exception("Build beatmap error.");
            newState.Beatmap = beatmap;
            Log.ConsoleLog("Build new beatmap successfully.", Log.LogType.BeatmapBuilder, Log.LogLevel.Debug);

            // convert beatmap to catch（按物件顺序消费随机数，保持与原来一致的时序）
            IBeatmap? convertedBeatmap;
            if (differenceType != DifferenceType.None || committed.ConvertedBeatmap == null || mods != committed.Mods || converterIsStable != committed.ConverterIsStable)
            {
                convertedBeatmap = converterIsStable
                    ? stableBeatmapConverter.GetConvertedBeatmap(beatmap, mods)
                    : lazerBeatmapConverter.GetConvertedBeatmap(beatmap, mods);
            }
            else
            {
                convertedBeatmap = committed.ConvertedBeatmap;
            }
            if (convertedBeatmap == null) throw new Exception("Convert beatmap error.");
            newState.ConvertedBeatmap = convertedBeatmap;

            // prepare drawing objects
            Log.ConsoleLog("Try building drawing objects.", Log.LogType.BeatmapConverter, Log.LogLevel.Debug);
            var stagingDrawing = new DrawingHelper();
            stagingDrawing.LabelType = labelType;
            stagingDrawing.LoadBeatmap(convertedBeatmap, mods);
            newState.Drawing = stagingDrawing;
            Log.ConsoleLog("Build drawing objects successfully.", Log.LogType.BeatmapConverter, Log.LogLevel.Debug);

            return newState;
        }

        private int GetMods()
        {
            int mods = 0;
            if (hRToolStripMenuItem.Checked) mods = (1 << 4);
            else if (eZToolStripMenuItem.Checked) mods = (1 << 1);
            return mods;
        }

        private HitObjectLabelType GetHitObjectLabelType()
        {
            HitObjectLabelType labelType = HitObjectLabelType.None;
            if (hideToolStripMenuItem.Checked) labelType = HitObjectLabelType.None;
            else if (sameWithEditorToolStripMenuItem.Checked) labelType = HitObjectLabelType.Distance_SameWithEditor;
            else if (noSliderVelocityMultiplierToolStripMenuItem.Checked) labelType = HitObjectLabelType.Distance_NoSliderVelocityMultiplier;
            else if (compareWithWalkSpeedToolStripMenuItem.Checked) labelType = HitObjectLabelType.Distance_CompareWithWalkSpeed;
            else if (difficultyStarsToolStripMenuItem.Checked) labelType = HitObjectLabelType.Difficulty_Stars;
            else if (fruitCountInComboToolStripMenuItem.Checked) labelType = HitObjectLabelType.FruitCountInCombo;
            else labelType = HitObjectLabelType.None;
            return labelType;
        }

        private void BackupBeatmap(BeatmapInfoCollection thisReader, string filepath)
        {
            try
            {
                Log.ConsoleLog("Start backup.", Log.LogType.Backup, Log.LogLevel.Info);
                string backupFilePath = Path.Combine(app.Default.Backup_Folder, DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss ") + thisReader.Filename);
                string? directoryPath = Path.GetDirectoryName(backupFilePath);
                if (directoryPath == null)
                {
                    Log.ConsoleLog("Backup failed. Path is invalid: " + backupFilePath, Log.LogType.Backup, Log.LogLevel.Error);
                }
                else
                {
                    Directory.CreateDirectory(directoryPath);
                    Log.ConsoleLog("Create new beatmap.", Log.LogType.Backup, Log.LogLevel.Info);
                    string newBeatmap = BeatmapBuilder.BuildNewBeatmapFileFromFilepath(filepath, thisReader);
                    File.WriteAllText(backupFilePath, newBeatmap);
                    Need_Backup = false;
                    Log.ConsoleLog("Backup successfully.", Log.LogType.Backup, Log.LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                Log.ConsoleLog("Backup failed.\r\n" + ex.ToString(), Log.LogType.Backup, Log.LogLevel.Error);
                Need_Backup = false;
            }
        }

        private async Task reader_timer_Work(CancellationToken cancellationToken)
        {
            // 让出当前线程：避免第一次执行时（runner.Start 调用）把读取/解析
            // 同步跑在 UI 线程上，导致 Form1_Load 卡住、窗口启动时未响应
            await Task.Yield();

            // Step0. check settings change
            if (NeedReapplySettings)
            {
                ReapplySettings();
                NeedReapplySettings = false;
            }
            if (NeedReapplyBookmarkStyles)
            {
                ReapplyBookmarkStyles();
                NeedReapplyBookmarkStyles = false;
            }

            try
            {
                // 后台重建任务与读取是否成功无关：先消费掉，避免读取中断期间重建结果一直不生效
                ConsumeFinishedRebuild();

                // 后台重绑（可能长达 30 秒的内存扫描）期间不碰 EditorReader，只继续绘制上一份数据
                if (editorReaderHelper.IsRebinding)
                {
                    DrawLastKnownFrame(UnavailableStatusText());
                    return;
                }

                // Step1. fetch osu! process
                if (!FetchOsuProcess())
                {
                    DrawLastKnownFrame("Osu!.exe is not running");
                    return;
                }


                // Step2. fetch editor
                if (!FetchEditor())
                {
                    // 编辑器不可用（test mode/选歌/读取失败）不是致命错误：
                    // 保留上一份有效数据继续绘制，只更新状态栏
                    DrawLastKnownFrame(UnavailableStatusText());
                    return;
                }


                // Step3. fetch all
                BeatmapInfoCollection? thisReader;
                bool blockBackup = false;
                if (app.Default.FilterNearbyHitObjects)
                {
                    blockBackup = true;

                    double partialLoadingHalfTimeSpan = 10 * 1000;
                    if (eZToolStripMenuItem.Checked) partialLoadingHalfTimeSpan *= 1.5;
                    if (hRToolStripMenuItem.Checked) partialLoadingHalfTimeSpan /= 1.5;
                    thisReader = editorReaderHelper.FetchAll(partialLoadingHalfTimeSpan);
                }
                else thisReader = editorReaderHelper.FetchAll();
                if (thisReader == null)
                {
                    // 读取失败：helper 内部已按退避重试/触发后台重绑，这里同样不中断绘制
                    DrawLastKnownFrame(UnavailableStatusText());
                    return;
                }

                // Step4. 判断是否需要启动新重建

                int mods = GetMods();
                HitObjectLabelType labelType = GetHitObjectLabelType();
                bool converterIsStable = app.Default.Use_Stable_Converter;

                // 选中态不参与重建判定：绘制时通过 SelectionLines 实时查询，避免点击/框选触发全量解析
                DifferenceType differenceType;
                if (thisReader.IsFreshFetch || _committed.Reader == null ||
                    mods != _committed.Mods || labelType != _committed.LabelType || converterIsStable != _committed.ConverterIsStable)
                {
                    differenceType = thisReader.CheckDifference(_committed.Reader, false);
                }
                else
                {
                    // 高频路径：数据与上次全量读取完全相同，无需逐物件比较
                    differenceType = DifferenceType.None;
                }
                drawingHelper.SelectionLines = thisReader.SelectionLines;

                // Step5. Build osu file Path
                string filepath = "";
                try
                {
                    filepath = Path.Combine(app.Default.osu_path, "Songs", thisReader.ContainingFolder, thisReader.Filename);
                }
                catch (Exception ex)
                {
                    Log.ConsoleLog("Path is invalid.\r\n" + ex.ToString(), Log.LogType.EditorReader, Log.LogLevel.Error);
                    Log.ConsoleLog("ContainingFolder: " + thisReader.ContainingFolder, Log.LogType.EditorReader, Log.LogLevel.Error);
                    Log.ConsoleLog("Filename: " + thisReader.Filename, Log.LogType.EditorReader, Log.LogLevel.Error);
                    _committed.Reader = null;
                    throw new Exception("Build Filepath error.");
                }


                // Step6. 需要重建时，把解析/转换/装载丢到后台线程，绘制循环继续用旧数据跑
                bool needRebuild = differenceType != DifferenceType.None
                    || _committed.ConvertedBeatmap == null
                    || mods != _committed.Mods
                    || labelType != _committed.LabelType
                    || converterIsStable != _committed.ConverterIsStable;

                if (needRebuild && _rebuildTask == null && DateTime.Now.Ticks - _rebuildRetryTicks > TimeSpan.FromMilliseconds(500).Ticks)
                {
                    // 重建才需要每行 .osu 文本（16000 物件约 33ms）：在这里一次性生成，
                    // 而不是每次全量读取都生成。生成后的表归 thisReader 所有，读取循环不再碰它。
                    thisReader.EnsureHitObjectLines();

                    int generation = _rebuildGeneration;
                    CommittedState committedSnapshot = _committed;
                    _rebuildTask = Task.Run(() => BuildNewState(thisReader, committedSnapshot, filepath, mods, labelType, converterIsStable, differenceType), cancellationToken);
                    _rebuildTaskGeneration = generation;
                }


                // Step7. Backup
                if (!blockBackup & Need_Backup)
                {
                    if (editorReaderHelper.Is_Editor_Running && _committed.Beatmap != null)
                    {
                        BackupBeatmap(thisReader, filepath);
                    }
                }


                // Step8. drop outdated data (really need it?)
                if (DateTime.Now.Ticks <= LastDrawingTimeStamp)
                {
                    Log.ConsoleLog("Drop an outdated data.", Log.LogType.Program, Log.LogLevel.Warning);
                    throw new Exception("Timing error.");
                }


                // set bookmarkplus
                if (bookmarkManager.IsBeatmapChanged(thisReader.ContainingFolder, thisReader.Filename))
                {
                    drawingHelper.Bookmarks = bookmarkManager.Bookmarks;
                }


                // Step11. drawing（标题/状态栏/绘制合成一次跨线程调用，减少每 tick 的 Invoke 次数）
                Log.ConsoleLog("Start drawing.", Log.LogType.Drawing, Log.LogLevel.Debug);

                string title = editorReaderHelper.beatmap_title;
                if (drawingHelper.LabelType == HitObjectLabelType.Difficulty_Stars && !app.Default.FilterNearbyHitObjects && _committed.ConvertedBeatmap != null)
                    title = "Stars: " + _committed.ConvertedBeatmap.BeatmapInfo.StarRating.ToString("0.00") + "*";

                RequestDraw(thisReader.EditorTime, title, "Drawing");

            }

            catch (OperationCanceledException)
            {
                // 任务被取消
                this.Invoke((MethodInvoker)delegate
                {
                    StateToolStripStatusLabel.Text = "Idle";
                });
                throw;
            }

        }


        private async void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            healthMonitor?.Dispose();
            healthMonitor = null;

            // 保存开关状态与停靠 / 浮动位置，并释放浮窗（释放时会把开关条放回停靠行）
            quickToggleDocking?.SaveState();
            quickToggleDocking?.Dispose();
            quickToggleDocking = null;

            if (app.Default.Bookmark_RegisterHotKey)
            {
                GlobalHotkey.UnRegisterGlobalHotKey(this.Handle);
            }

            await runner.StopAsync();
            backup_timer.Stop();
            backup_timer.Dispose();
            Memory_Monitor_Timer.Stop();
            Memory_Monitor_Timer.Dispose();

            app.Default.Window_X = this.Location.X;
            app.Default.Window_Y = this.Location.Y;
            app.Default.Window_Maximized = (this.WindowState == FormWindowState.Maximized);
            app.Default.Save();
        }

        private void noneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            noneToolStripMenuItem.Checked = true;
            hRToolStripMenuItem.Checked = false;
            eZToolStripMenuItem.Checked = false;
        }

        private void hRToolStripMenuItem_Click(object sender, EventArgs e)
        {
            noneToolStripMenuItem.Checked = false;
            hRToolStripMenuItem.Checked = true;
            eZToolStripMenuItem.Checked = false;
        }

        private void eZToolStripMenuItem_Click(object sender, EventArgs e)
        {
            noneToolStripMenuItem.Checked = false;
            hRToolStripMenuItem.Checked = false;
            eZToolStripMenuItem.Checked = true;
        }

        private void githubToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo(@"https://github.com/Exsper/osucatch-editor-realtimeviewer") { UseShellExecute = true });
        }

        private void Form1_SizeChanged(object? sender, EventArgs e)
        {
            // 最大化/最小化过程中的尺寸不能写入设置：否则会把全屏尺寸存成普通窗口尺寸，
            // 下次启动时窗口铺满桌面且（Wine 下）无法缩放
            if (this.WindowState != FormWindowState.Normal) return;

            System.Drawing.Rectangle virtualScreen = SystemInformation.VirtualScreen;
            app.Default.Window_Width = Math.Clamp(this.Width, 200, virtualScreen.Width);
            app.Default.Window_Height = Math.Clamp(this.Height, 200, virtualScreen.Height);
            app.Default.Save();
        }

        /// <summary>
        /// 判断保存的窗口位置是否仍在某个屏幕内（防止窗口被恢复到屏幕外导致无法拖动/缩放）。
        /// </summary>
        private static bool IsPositionOnScreen(int x, int y)
        {
            foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
            {
                if (screen.Bounds.Contains(x, y) || screen.Bounds.Contains(x + 80, y + 40))
                {
                    return true;
                }
            }
            return false;
        }

        private void openSettingsFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SettingsFormInstance != null && !SettingsFormInstance.IsDisposed)
                SettingsFormInstance.Activate();
            else
            {
                SettingsFormInstance = new SettingsForm();
                Form1 host = this;
                SettingsFormInstance.SettingsApplied += () => host.ReapplySettings();
                SettingsFormInstance.FormClosed += (s, args) => { SettingsFormInstance = null; }; // 关闭时重置变量
                SettingsFormInstance.ShowDialog();
            }
        }

        private void bookmarkSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (BookmarkSettingsFormInstance != null && !BookmarkSettingsFormInstance.IsDisposed)
                BookmarkSettingsFormInstance.Activate();
            else
            {
                BookmarkSettingsFormInstance = new BookmarkSettingsForm();
                BookmarkSettingsFormInstance.FormClosed += (s, args) => { BookmarkSettingsFormInstance = null; }; // 关闭时重置变量
                BookmarkSettingsFormInstance.ShowDialog();
            }
        }

        private void backup_timer_Tick(object? source, ElapsedEventArgs? e)
        {
            if (app.Default.Backup_Enabled) Need_Backup = true;
        }

        private void hideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            hideToolStripMenuItem.Checked = true;
            sameWithEditorToolStripMenuItem.Checked = false;
            noSliderVelocityMultiplierToolStripMenuItem.Checked = false;
            compareWithWalkSpeedToolStripMenuItem.Checked = false;
            difficultyStarsToolStripMenuItem.Checked = false;
            fruitCountInComboToolStripMenuItem.Checked = false;
        }

        private void sameWithEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            hideToolStripMenuItem.Checked = false;
            sameWithEditorToolStripMenuItem.Checked = true;
            noSliderVelocityMultiplierToolStripMenuItem.Checked = false;
            compareWithWalkSpeedToolStripMenuItem.Checked = false;
            difficultyStarsToolStripMenuItem.Checked = false;
            fruitCountInComboToolStripMenuItem.Checked = false;
        }

        private void noSliderVelocityMultiplierToolStripMenuItem_Click(object sender, EventArgs e)
        {
            hideToolStripMenuItem.Checked = false;
            sameWithEditorToolStripMenuItem.Checked = false;
            noSliderVelocityMultiplierToolStripMenuItem.Checked = true;
            compareWithWalkSpeedToolStripMenuItem.Checked = false;
            difficultyStarsToolStripMenuItem.Checked = false;
            fruitCountInComboToolStripMenuItem.Checked = false;
        }

        private void compareWithWalkSpeedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            hideToolStripMenuItem.Checked = false;
            sameWithEditorToolStripMenuItem.Checked = false;
            noSliderVelocityMultiplierToolStripMenuItem.Checked = false;
            compareWithWalkSpeedToolStripMenuItem.Checked = true;
            difficultyStarsToolStripMenuItem.Checked = false;
            fruitCountInComboToolStripMenuItem.Checked = false;
        }

        private void difficultyStarsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            hideToolStripMenuItem.Checked = false;
            sameWithEditorToolStripMenuItem.Checked = false;
            noSliderVelocityMultiplierToolStripMenuItem.Checked = false;
            compareWithWalkSpeedToolStripMenuItem.Checked = false;
            difficultyStarsToolStripMenuItem.Checked = true;
            fruitCountInComboToolStripMenuItem.Checked = false;
        }

        private void fruitCountInComboToolStripMenuItem_Click(object sender, EventArgs e)
        {
            hideToolStripMenuItem.Checked = false;
            sameWithEditorToolStripMenuItem.Checked = false;
            noSliderVelocityMultiplierToolStripMenuItem.Checked = false;
            compareWithWalkSpeedToolStripMenuItem.Checked = false;
            difficultyStarsToolStripMenuItem.Checked = false;
            fruitCountInComboToolStripMenuItem.Checked = true;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void Screens1ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Screens1ToolStripMenuItem.Checked = true;
            Screens2ToolStripMenuItem.Checked = false;
            Screens3ToolStripMenuItem.Checked = false;
            Screens4ToolStripMenuItem.Checked = false;
            Screens5ToolStripMenuItem.Checked = false;
            Screens6ToolStripMenuItem.Checked = false;
            Screens7ToolStripMenuItem.Checked = false;
            Screens8ToolStripMenuItem.Checked = false;

            app.Default.ScreensContain = 1;
            app.Default.Save();
            Canvas.screensContain = 1;
            drawingHelper.ScreensContain = 1;
            this.Canvas.ScreensContainChanged();
        }

        private void Screens2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Screens1ToolStripMenuItem.Checked = false;
            Screens2ToolStripMenuItem.Checked = true;
            Screens3ToolStripMenuItem.Checked = false;
            Screens4ToolStripMenuItem.Checked = false;
            Screens5ToolStripMenuItem.Checked = false;
            Screens6ToolStripMenuItem.Checked = false;
            Screens7ToolStripMenuItem.Checked = false;
            Screens8ToolStripMenuItem.Checked = false;

            app.Default.ScreensContain = 2;
            app.Default.Save();
            Canvas.screensContain = 2;
            drawingHelper.ScreensContain = 2;
            this.Canvas.ScreensContainChanged();
        }

        private void Screens3ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Screens1ToolStripMenuItem.Checked = false;
            Screens2ToolStripMenuItem.Checked = false;
            Screens3ToolStripMenuItem.Checked = true;
            Screens4ToolStripMenuItem.Checked = false;
            Screens5ToolStripMenuItem.Checked = false;
            Screens6ToolStripMenuItem.Checked = false;
            Screens7ToolStripMenuItem.Checked = false;
            Screens8ToolStripMenuItem.Checked = false;

            app.Default.ScreensContain = 3;
            app.Default.Save();
            Canvas.screensContain = 3;
            drawingHelper.ScreensContain = 3;
            this.Canvas.ScreensContainChanged();
        }

        private void Screens4ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Screens1ToolStripMenuItem.Checked = false;
            Screens2ToolStripMenuItem.Checked = false;
            Screens3ToolStripMenuItem.Checked = false;
            Screens4ToolStripMenuItem.Checked = true;
            Screens5ToolStripMenuItem.Checked = false;
            Screens6ToolStripMenuItem.Checked = false;
            Screens7ToolStripMenuItem.Checked = false;
            Screens8ToolStripMenuItem.Checked = false;

            app.Default.ScreensContain = 4;
            app.Default.Save();
            Canvas.screensContain = 4;
            drawingHelper.ScreensContain = 4;
            this.Canvas.ScreensContainChanged();
        }

        private void Screens5ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Screens1ToolStripMenuItem.Checked = false;
            Screens2ToolStripMenuItem.Checked = false;
            Screens3ToolStripMenuItem.Checked = false;
            Screens4ToolStripMenuItem.Checked = false;
            Screens5ToolStripMenuItem.Checked = true;
            Screens6ToolStripMenuItem.Checked = false;
            Screens7ToolStripMenuItem.Checked = false;
            Screens8ToolStripMenuItem.Checked = false;

            app.Default.ScreensContain = 5;
            app.Default.Save();
            Canvas.screensContain = 5;
            drawingHelper.ScreensContain = 5;
            this.Canvas.ScreensContainChanged();
        }

        private void Screens6ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Screens1ToolStripMenuItem.Checked = false;
            Screens2ToolStripMenuItem.Checked = false;
            Screens3ToolStripMenuItem.Checked = false;
            Screens4ToolStripMenuItem.Checked = false;
            Screens5ToolStripMenuItem.Checked = false;
            Screens6ToolStripMenuItem.Checked = true;
            Screens7ToolStripMenuItem.Checked = false;
            Screens8ToolStripMenuItem.Checked = false;

            app.Default.ScreensContain = 6;
            app.Default.Save();
            Canvas.screensContain = 6;
            drawingHelper.ScreensContain = 6;
            this.Canvas.ScreensContainChanged();
        }

        private void Screens7ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Screens1ToolStripMenuItem.Checked = false;
            Screens2ToolStripMenuItem.Checked = false;
            Screens3ToolStripMenuItem.Checked = false;
            Screens4ToolStripMenuItem.Checked = false;
            Screens5ToolStripMenuItem.Checked = false;
            Screens6ToolStripMenuItem.Checked = false;
            Screens7ToolStripMenuItem.Checked = true;
            Screens8ToolStripMenuItem.Checked = false;

            app.Default.ScreensContain = 7;
            app.Default.Save();
            Canvas.screensContain = 7;
            drawingHelper.ScreensContain = 7;
            this.Canvas.ScreensContainChanged();
        }

        private void Screens8ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Screens1ToolStripMenuItem.Checked = false;
            Screens2ToolStripMenuItem.Checked = false;
            Screens3ToolStripMenuItem.Checked = false;
            Screens4ToolStripMenuItem.Checked = false;
            Screens5ToolStripMenuItem.Checked = false;
            Screens6ToolStripMenuItem.Checked = false;
            Screens7ToolStripMenuItem.Checked = false;
            Screens8ToolStripMenuItem.Checked = true;

            app.Default.ScreensContain = 8;
            app.Default.Save();
            Canvas.screensContain = 8;
            drawingHelper.ScreensContain = 8;
            this.Canvas.ScreensContainChanged();
        }

        private void backupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Need_Backup = true;
        }

        public static void ApplyResources(Form form)
        {
            ComponentResourceManager rm = new System.ComponentModel.ComponentResourceManager(form.GetType());

            // $this 资源里含有设计时的 ClientSize/Location，语言切换时不应重置用户调整过的窗口布局，
            // 因此在应用前后保存并恢复窗体的大小和位置（Text 等语言资源仍会正常应用）
            Size formSize = form.Size;
            Point formLocation = form.Location;

            rm.ApplyResources(form, "$this");
            AppLang(form, rm);

            form.Size = formSize;
            form.Location = formLocation;

            // 模板菜单文本跟随语言切换（并保留已加载模板的文件名后缀）
            if (form is Form1 form1)
            {
                form1.RestoreTemplateMenuText();
                form1.ApplyQuickToggleLanguage();
            }
        }

        private static void AppLang(ToolStripMenuItem item, System.ComponentModel.ComponentResourceManager resources)
        {
            if (item is ToolStripMenuItem)
            {
                resources.ApplyResources(item, item.Name ?? "");
                ToolStripMenuItem tsmi = (ToolStripMenuItem)item;
                if (tsmi.DropDownItems.Count > 0)
                {
                    foreach (var c in tsmi.DropDownItems)
                    {
                        if (c is ToolStripMenuItem) AppLang((ToolStripMenuItem)c, resources);
                    }
                }
            }
        }

        private static void AppLang(Control control, System.ComponentModel.ComponentResourceManager resources)
        {
            if (control is MenuStrip)
            {
                resources.ApplyResources(control, control.Name);
                MenuStrip ms = (MenuStrip)control;
                if (ms.Items.Count > 0)
                {
                    foreach (ToolStripMenuItem c in ms.Items)
                    {
                        AppLang(c, resources);
                    }
                }
            }

            foreach (Control c in control.Controls)
            {
                resources.ApplyResources(c, c.Name);
                AppLang(c, resources);
            }
        }

        private void englishLanguageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            defaultLanguageToolStripMenuItem.Checked = false;
            englishLanguageToolStripMenuItem.Checked = true;
            zhHansLanguageToolStripMenuItem.Checked = false;
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
            Form1.ApplyResources(this);

            app.Default.Language_String = "en-US";
            ReapplyBookmarkStyles();
            app.Default.Save();
        }

        private void zhHansLanguageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            defaultLanguageToolStripMenuItem.Checked = false;
            englishLanguageToolStripMenuItem.Checked = false;
            zhHansLanguageToolStripMenuItem.Checked = true;
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("zh-Hans");
            Form1.ApplyResources(this);

            app.Default.Language_String = "zh-Hans";
            ReapplyBookmarkStyles();
            app.Default.Save();
        }

        private void defaultLanguageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            defaultLanguageToolStripMenuItem.Checked = true;
            englishLanguageToolStripMenuItem.Checked = false;
            zhHansLanguageToolStripMenuItem.Checked = false;
            Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.CurrentCulture;
            Form1.ApplyResources(this);

            app.Default.Language_String = "";
            ReapplyBookmarkStyles();
            app.Default.Save();
        }

        private async void forceResetStripMenuItem_Click(object sender, EventArgs e)
        {
            Log.Breadcrumb("Manual reset requested.");
            _committed = new CommittedState();
            _rebuildGeneration++;
            _rebuildTask = null;
            _rebuildRetryTicks = 0;

            // 强制丢弃进程/编辑器绑定与全部缓存，并立即重新查找 osu! 进程、重扫编辑器地址。
            // 此前只是 editorReaderHelper = new()，而 EditorReader 是静态实例，
            // 编辑器地址/扫描退避表都还在，卡住时按 Reset 等于没按。
            editorReaderHelper.ForceRebind(resetRebindBackoff: true);

            await runner.StopAsync();
            // 恢复正常的绘制间隔：此前用 Idle_Interval 重建 runner，
            // 于是 Reset 之后预览会变成每 3 秒才刷新一次
            runner = new PeriodicTaskRunner(app.Default.Drawing_Interval, app.Default.Idle_Interval, reader_timer_Work);
            runner.Start();
        }

        private void restartProgramStripMenuItem_Click(object sender, EventArgs e)
        {
            // 获取当前应用程序的可执行文件路径
            string applicationPath = Application.ExecutablePath;

            // 启动一个新的进程来运行当前应用程序
            ProcessStartInfo processStartInfo = new ProcessStartInfo(applicationPath);
            Process.Start(processStartInfo);

            // 关闭当前应用程序
            Application.Exit();
        }

        private async void selectTemplateStripMenuItem_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "选择模板谱面";
                openFileDialog.Filter = "osu Beatmap (*.osu)|*.osu";

                // 默认文件夹为当前读取的 osu 文件所在文件夹
                string defaultFolder = "";
                if (_committed.Reader != null && _committed.Reader.ContainingFolder != "")
                {
                    defaultFolder = Path.Combine(app.Default.osu_path, "Songs", _committed.Reader.ContainingFolder);
                    if (!Directory.Exists(defaultFolder)) defaultFolder = "";
                }
                if (defaultFolder == "") defaultFolder = Path.Combine(app.Default.osu_path, "Songs");
                if (Directory.Exists(defaultFolder)) openFileDialog.InitialDirectory = defaultFolder;

                if (openFileDialog.ShowDialog() != DialogResult.OK) return;

                string filePath = openFileDialog.FileName;
                selectTemplateStripMenuItem.Enabled = false;
                try
                {
                    TemplateBeatmapData? data = await Task.Run(() => LoadTemplate(filePath));
                    if (data == null)
                    {
                        MessageBox.Show("模板加载失败，文件可能不是有效的 osu 谱面。", "模板", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    templateData = data;
                    drawingHelper.Template = data;
                    unloadTemplateStripMenuItem.Enabled = true;
                    RestoreTemplateMenuText();
                    Log.ConsoleLog("Template loaded: " + filePath, Log.LogType.Program, Log.LogLevel.Info);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("模板加载失败：\r\n" + ex.Message, "模板", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    selectTemplateStripMenuItem.Enabled = true;
                }
            }
        }

        private void unloadTemplateStripMenuItem_Click(object? sender, EventArgs e)
        {
            templateData = null;
            drawingHelper.Template = null;
            unloadTemplateStripMenuItem.Enabled = false;
            RestoreTemplateMenuText();
            Log.ConsoleLog("Template unloaded.", Log.LogType.Program, Log.LogLevel.Info);
        }

        /// <summary>
        /// 程序内创建模板菜单（设在构造函数中，早于语言资源的应用）。
        /// </summary>
        private void CreateTemplateMenu()
        {
            templateToolStripMenuItem = new ToolStripMenuItem();
            templateToolStripMenuItem.Name = "templateToolStripMenuItem";
            templateToolStripMenuItem.Text = "模板";
            selectTemplateStripMenuItem = new ToolStripMenuItem();
            selectTemplateStripMenuItem.Name = "selectTemplateStripMenuItem";
            selectTemplateStripMenuItem.Text = "选择模板谱面...";
            selectTemplateStripMenuItem.Click += selectTemplateStripMenuItem_Click;
            unloadTemplateStripMenuItem = new ToolStripMenuItem();
            unloadTemplateStripMenuItem.Name = "unloadTemplateStripMenuItem";
            unloadTemplateStripMenuItem.Text = "卸载模板";
            unloadTemplateStripMenuItem.Enabled = false;
            unloadTemplateStripMenuItem.Click += unloadTemplateStripMenuItem_Click;
            templateToolStripMenuItem.DropDownItems.Add(selectTemplateStripMenuItem);
            templateToolStripMenuItem.DropDownItems.Add(unloadTemplateStripMenuItem);
            menuStrip1.Items.Insert(1, templateToolStripMenuItem);
        }

        /// <summary>
        /// 在语言资源应用后恢复“卸载模板”菜单文本：
        /// 去掉可能已附加的文件名后缀，若已加载模板则按当前语言文本补回后缀。
        /// </summary>
        private void RestoreTemplateMenuText()
        {
            string baseText = unloadTemplateStripMenuItem.Text ?? "";
            int suffixIndex = baseText.LastIndexOf(" (");
            if (suffixIndex > 0) baseText = baseText.Substring(0, suffixIndex);

            unloadTemplateStripMenuItem.Text = (templateData != null) ? baseText + " (" + templateData.Filename + ")" : baseText;
        }

        /// <summary>
        /// 创建快捷开关条：可吸附在菜单栏下方（横排）或画布左 / 右侧（竖排），也可以拖出为浮动小窗口。
        /// </summary>
        private void CreateQuickToggleBar()
        {
            quickToggleBar = new QuickToggleBar();
            // 先于停靠管理器订阅：停靠管理器在自己的 ToggleChanged 处理里统一保存设置，
            // 这里先更新运行状态，保证同一次保存里带上的都是最新状态
            quickToggleBar.ToggleChanged += quickToggleBar_ToggleChanged;
            quickToggleBar.GroupChanged += quickToggleBar_GroupChanged;

            quickToggleDockRow = new QuickToggleDockRow();
            quickToggleDocking = new QuickToggleDocking(this, quickToggleBar, quickToggleDockRow, menuStrip1, statusStrip1);

            QuickToggleDocking docking = quickToggleDocking;
            docking.StateChanged += (sender, e) =>
            {
                if (quickToggleStripMenuItem != null) quickToggleStripMenuItem.Checked = docking.BarVisible;
                SyncQuickToggleDockSideMenu();
            };

            // 停靠行要参与主窗口的停靠布局。它在控件集合里的位置决定它落在哪：
            // 顶部吸附时排在画布之后、菜单栏之前（正好占据菜单栏下方的一行，把画布挤下去），
            // 左 / 右侧吸附时排在最前面（正好夹在菜单栏与状态栏之间的左 / 右边）。
            // 具体位置由停靠管理器按吸附位置安排（见 QuickToggleDocking.PlaceDockRow）。
            Controls.Add(quickToggleDockRow);
            docking.ApplyDockSide();

            CreateQuickToggleMenu();
        }

        /// <summary>
        /// 创建快捷开关条上的内容：最左边是独立的“固定预览时刻”，其后是各个功能区
        /// （MOD / 果子标注 / 拍线）。
        /// 只在首次调用时建控件；之后（语言切换）只刷新文本，避免重复添加。
        /// </summary>
        private void CreateQuickToggles()
        {
            if (quickToggleBar == null || quickTogglesCreated) return;
            quickTogglesCreated = true;

            bool chinese = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "zh";

            // ---- 固定预览时刻：不属于任何功能区，固定排在条的最左边、始终显示 ----
            (string freezeText, string freezeToolTip) = FreezePreviewTimeText(previewTimeFrozen);
            quickToggleBar.AddStandaloneToggle(FreezePreviewTimeKey, freezeText, false);
            quickToggleBar.SetToggleText(FreezePreviewTimeKey, freezeText, freezeToolTip);
            // 条上这个按钮只显示图标，右键菜单里给它一个和功能区一样的“是否显示”勾选项
            quickToggleBar.AddStandaloneToggleToMenu(FreezePreviewTimeKey, FreezePreviewTimeMenuText());

            // ---- MOD：三个按钮单选，当前 MOD 对应的按钮保持按下（NM/EZ/HR 一看便知，不加标题） ----
            QuickToggleBar.QuickToggleGroup modGroup = quickToggleBar.AddGroup(ModGroupKey, "");
            for (int i = 0; i < ModKeys.Length; i++)
            {
                modGroup.AddToggle(ModKeys[i], ModToggleText((ModMode)i), (ModMode)i == ModMode.None);
            }

            // ---- 果子标注：四个按钮单选，对应菜单栏里最常用的四种标注。
            //      每个模式都有对应图标，因此整组不加标题、按钮只显示图标（名称见按钮提示）；
            //      条上没标题，但右键菜单里得有个看得懂的名字，见 LabelGroupMenuText。 ----
            QuickToggleBar.QuickToggleGroup labelGroup = quickToggleBar.AddGroup(LabelGroupKey, "", LabelGroupMenuText());
            for (int i = 0; i < LabelKeys.Length; i++)
            {
                labelGroup.AddToggle(LabelKeys[i], LabelToggleText((QuickLabelMode)i), (QuickLabelMode)i == QuickLabelMode.Hidden);
            }

            // ---- 拍线：滑块选择密度 + 当前模式文字 ----
            QuickToggleBar.QuickToggleGroup barLineGroup = quickToggleBar.AddGroup(BarLineGroupKey, chinese ? "拍线" : "Bar Lines");
            barLineGroup.AddSlider(
                BarLineValueKey,
                0,
                BarLineSettings.ModeCount - 1,
                (int)BarLineSettings.CurrentMode,
                1);
            barLineGroup.AddLabel(BarLineValueKey + "_Text", BarLineStatusText());

            // ---- 垂直缩放：滑块调节 Y 轴拉伸 / 压缩（X 轴不动），旁边显示当前比例。
            //      值只作用于本次运行，不写入设置文件，因此每次启动都是默认的 x1.0。 ----
            QuickToggleBar.QuickToggleGroup scaleGroup = quickToggleBar.AddGroup(
                VerticalScaleGroupKey,
                chinese ? "Y缩放" : "Y Scale");
            scaleGroup.AddSlider(
                VerticalScaleValueKey,
                VerticalScaleMinTenths,
                VerticalScaleMaxTenths,
                VerticalScaleDefaultTenths,
                VerticalScaleTickTenths);
            scaleGroup.AddLabel(VerticalScaleValueKey + "_Text", VerticalScaleStatusText());
            // 条上只放得下“x1.0”，Ctrl+滚轮这个快捷方式写进悬停提示
            quickToggleBar.SetGroupLabelToolTip(VerticalScaleGroupKey, VerticalScaleValueKey + "_Text", VerticalScaleHintText());

            ApplyQuickToggleIcons();
        }

        #region 快捷开关：按钮图标

        /// <summary>图标目录：与贴图一样按程序所在目录解析（icons\ 就在 exe 旁边）。</summary>
        private const string IconsFolder = "icons";

        /// <summary>“固定预览时刻”两个状态的图标文件名（相对 icons\）。</summary>
        private const string FreezeIconFile = "QuickLabelMode_Switch2Freeze.png";

        private const string FollowIconFile = "QuickLabelMode_Switch2Following.png";

        /// <summary>
        /// 果子标注四个按钮的图标文件名，顺序与 <see cref="LabelKeys"/> / <see cref="QuickLabelMode"/> 一致。
        /// </summary>
        private static readonly string[] LabelIconFiles =
        {
            "QuickLabelMode_Hide.png",
            "QuickLabelMode_Distance.png",
            "QuickLabelMode_DistanceIgnoreSvm.png",
            "QuickLabelMode_Stars.png",
        };

        /// <summary>取 icons\ 目录下某个图标文件的完整路径（按 exe 目录解析，找不到时回退相对路径）。</summary>
        private static string ResolveIconPath(string fileName)
            => ResolveImagePath(Path.Combine(IconsFolder, fileName));

        /// <summary>果子标注模式对应的图标文件路径。</summary>
        private static string LabelModeIconPath(QuickLabelMode mode)
        {
            int index = (int)mode;
            return (index >= 0 && index < LabelIconFiles.Length) ? ResolveIconPath(LabelIconFiles[index]) : "";
        }

        /// <summary>
        /// 给按钮装上图标（“固定预览时刻” + 果子标注四个按钮）。
        /// <para />图标缺失时会退化成显示文字——所以即使 icons\ 没随程序一起发布，
        /// 按钮也不会变成认不出来的空白图标。
        /// </para>
        /// </summary>
        private void ApplyQuickToggleIcons()
        {
            if (quickToggleBar == null) return;

            // 图标缺失时退化为文字显示：先按文字算好标签与提示，再尝试装图标
            (string followText, string followToolTip) = FreezePreviewTimeText(false);
            (string freezeText, string freezeToolTip) = FreezePreviewTimeText(true);

            bool freeze = previewTimeFrozen;
            quickToggleBar.SetToggleText(
                FreezePreviewTimeKey,
                freeze ? followText : freezeText,
                freeze ? followToolTip : freezeToolTip);
            quickToggleBar.SetToggleImage(
                FreezePreviewTimeKey,
                ResolveIconPath(freeze ? FollowIconFile : FreezeIconFile));

            for (int i = 0; i < LabelKeys.Length; i++)
            {
                quickToggleBar.SetToggleImage(LabelKeys[i], LabelModeIconPath((QuickLabelMode)i));
            }

            // 整组只显示图标（果子标注这个标题已经按需求去掉，图标本身就能说明模式）
            quickToggleBar.SetGroupTogglesDisplayStyle(LabelGroupKey, ToolStripItemDisplayStyle.Image);
        }

        #endregion

        #region 快捷开关：设置与执行

        /// <summary>
        /// “固定预览时刻”开关在当前语言下的按钮文本与提示。
        /// 按钮文本按“点击后会做什么”显示（与右端“浮动/吸附”按钮一致）：
        /// 跟随中显示 ⏸️（点击固定），已固定显示 ▶️（点击恢复跟随）。
        /// </summary>
        private static (string Text, string ToolTip) FreezePreviewTimeText(bool frozen)
        {
            bool chinese = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "zh";
            string text = frozen ? "▶️" : "⏸️";
            string toolTip = frozen
                ? (chinese ? "恢复跟随 editor 时刻" : "Resume following the editor time")
                : (chinese
                    ? "固定预览时刻：预览停在当前 editor 时刻（距离辅助线仍跟随 editor 实时时刻）"
                    : "Freeze the preview at the current editor time (the distance helper still follows the editor)");
            return (text, toolTip);
        }

        /// <summary>
        /// 右键菜单里“跟随模式”勾选项的文字：和 Mod / HitObjectLabel / 拍线 一样，
        /// 勾选表示这个 ⏸️ / ▶️ 按钮在快捷开关栏上显示。
        /// <para />文字必须单独给：条上这个按钮只显示图标，没法拿它的 Text 当菜单项。
        /// </para>
        /// </summary>
        private static string FreezePreviewTimeMenuText()
        {
            bool chinese = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "zh";
            return chinese ? "跟随模式" : "Follow mode";
        }

        /// <summary>MOD 快捷按钮在当前语言下的文本（三种 MOD 名称各语言都相同）。</summary>
        private static string ModToggleText(ModMode mode)
        {
            return mode switch
            {
                ModMode.Easy => "EZ",
                ModMode.HardRock => "HR",
                _ => "NM",
            };
        }

        private static string ModToggleToolTip(ModMode mode)
        {
            bool chinese = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "zh";
            return mode switch
            {
                ModMode.Easy => chinese ? "切换到 EZ（Easy）" : "Switch to EZ (Easy)",
                ModMode.HardRock => chinese ? "切换到 HR（HardRock）" : "Switch to HR (HardRock)",
                _ => chinese ? "切换到 NM（NoMod）" : "Switch to NM (NoMod)",
            };
        }

        /// <summary>
        /// 右键菜单里“果子标注”功能区的名称。
        /// <para />这个功能区在条上只显示四个图标、故意不带标题，
        /// 若不显式给名字，菜单里的勾选项就会显示内部标识 <c>HitObjectLabel</c>——看不出是什么。
        /// 文案与菜单栏的“果子标注 / Fruit Labels”保持一致（不带快捷键标记，菜单项是代码创建的）。
        /// </para>
        /// </summary>
        private static string LabelGroupMenuText()
        {
            bool chinese = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "zh";
            return chinese ? "果子标注" : "Fruit Labels";
        }

        /// <summary>果子标注快捷按钮在当前语言下的文本。</summary>
        private static string LabelToggleText(QuickLabelMode mode)
        {
            bool chinese = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "zh";
            return mode switch
            {
                QuickLabelMode.Distance => chinese ? "距离-正常" : "Dist",
                QuickLabelMode.DistanceIgnoreSvm => chinese ? "距离-忽略SVM" : "Dist-IgnoreSV",
                QuickLabelMode.Stars => chinese ? "难度星数" : "Stars",
                _ => chinese ? "隐藏" : "Hide",
            };
        }

        private static string LabelToggleToolTip(QuickLabelMode mode)
        {
            bool chinese = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "zh";
            return mode switch
            {
                QuickLabelMode.Distance => chinese ? "距离标注：与编辑器相同" : "Distance label: same as editor",
                QuickLabelMode.DistanceIgnoreSvm => chinese ? "距离标注：忽略滑条速度倍率" : "Distance label: ignore slider velocity multiplier",
                QuickLabelMode.Stars => chinese ? "难度标注：标注物件的难度星数" : "Difficulty label: star difficulty of the object",
                _ => chinese ? "不显示果子标注" : "Hide fruit labels",
            };
        }

        /// <summary>
        /// 功能区里显示当前拍线模式的文字：只写档位（如“每2拍”），不再带“拍线：”前缀。
        /// 前缀对横排只是占地方，竖排（吸附在左 / 右侧）时更会把整列撑宽，而所在功能区本身就叫“拍线”。
        /// </summary>
        private static string BarLineStatusText() => BarLineSettings.GetOptionName(BarLineSettings.CurrentMode);

        /// <summary>
        /// 设置画面 Y 轴（时间轴）的缩放比例：滑块值以 0.1 为单位，这里换算成倍率后立即生效
        /// （范围 x0.5 ~ x4.0，步进 0.1）。
        /// <para />只影响绘制（<see cref="DrawingHelper.VerticalScale"/>），<b>不写入设置文件</b>，
        /// 所以重启后回到默认的 x1.0；画面 X 轴与各物件的显示大小都不受影响。
        /// </para>
        /// </summary>
        private void ApplyVerticalScale(int tenths)
        {
            int clamped = Math.Clamp(tenths, VerticalScaleMinTenths, VerticalScaleMaxTenths);
            verticalScaleTenths = clamped;
            drawingHelper.VerticalScale = clamped / 10f;

            quickToggleBar?.SetGroupSliderValue(VerticalScaleGroupKey, VerticalScaleValueKey, clamped);
            quickToggleBar?.SetGroupLabelText(VerticalScaleGroupKey, VerticalScaleValueKey + "_Text", VerticalScaleStatusText());
        }

        /// <summary>
        /// 垂直缩放功能区里显示的当前比例文字（形如 <c>x1.0</c>）。
        /// 滑块步进 0.1，所以一位小数恰好能反映出每一档的变化。
        /// </summary>
        private static string VerticalScaleStatusText() => "x" + drawingHelper.VerticalScale.ToString("0.0");

        /// <summary>
        /// Y 缩放比例文字的悬停提示：条上只显示“x1.0”，这里把 Ctrl + 滚轮的快捷操作一并说明。
        /// </summary>
        private static string VerticalScaleHintText()
        {
            bool chinese = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "zh";
            return chinese
                ? "Y 轴缩放比例（在 viewer 窗口内按住 Ctrl 滚滚轮也能调，每格 0.1）"
                : "Y axis scale (hold Ctrl and scroll inside the viewer window; 0.1 per notch)";
        }

        #region Y 轴缩放：Ctrl + 滚轮

        /// <summary>滚轮消息（WM_MOUSEWHEEL）；wParam 高 16 位是带符号的滚动量。</summary>
        private const int WM_MOUSEWHEEL = 0x020A;

        /// <summary>Ctrl + 滚轮的消息过滤器实例（随窗体关闭一并注销）。</summary>
        private VerticalScaleWheelFilter? wheelFilter;

        /// <summary>
        /// Ctrl + 滚轮调整 Y 轴缩放的消息过滤器。
        /// <para /><b>为什么用消息过滤器而不是重写 <c>OnMouseWheel</c></b>：滚轮消息由系统发给
        /// “当前拥有键盘焦点的窗口”。焦点可能落在画布上（<see cref="Canvas"/> 是 GLControl，
        /// 有自己独立的窗口句柄），也可能落在主窗口本身；过滤器在消息进入控件之前就能看到，
        /// 因此不必依赖“未处理的滚轮消息会被 WinForms 冒泡给父控件”这个实现细节。
        /// </para>
        /// <para />回调里的三道闸（前台、光标在窗口内、按住 Ctrl）都在 <see cref="TryZoomVerticalScaleByWheel"/>。
        /// </para>
        /// </summary>
        private sealed class VerticalScaleWheelFilter : IMessageFilter
        {
            private readonly Form1 owner;

            internal VerticalScaleWheelFilter(Form1 owner) => this.owner = owner;

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != WM_MOUSEWHEEL) return false;

                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                return owner.TryZoomVerticalScaleByWheel(delta);
            }
        }

        /// <summary>
        /// 处理一次滚轮消息：只有“viewer 在前台 + 光标在 viewer 窗口内 + 按住 Ctrl”时才调整 Y 轴缩放。
        /// <para />向上滚 = 放大（把时间轴拉长），一格 0.1，与滑块步进一致；缩放基准仍是判定线，
        /// 与拖滑块完全等价（滚轮只是另一个入口）。
        /// </para>
        /// </summary>
        /// <returns>true 表示这条滚轮消息已被消费，不再交给画布 / 快捷开关栏等控件。</returns>
        private bool TryZoomVerticalScaleByWheel(int wheelDelta)
        {
            // 正在关闭时不再动控件（消息过滤器要等 FormClosed 才注销，期间可能还有滚轮消息在途）
            if (IsDisposed || Disposing) return false;

            // ① 本窗口必须在前台：焦点在窗口内的任意子控件上才算。
            //    “viewer 不在前台”包含两类情况，这里一并挡掉：
            //    - 焦点在别的程序（或本程序的浮窗 / 设置窗口）上；
            //    - Windows 的“悬停时滚动非活动窗口”把滚轮直接送到悬停窗口，但焦点并不在我们的窗口上。
            if (!ContainsFocus) return false;

            // ② 光标必须落在 viewer 窗口内（Ctrl+滚轮甩到窗口外时不该改缩放）
            if (!Bounds.Contains(Cursor.Position)) return false;

            // ③ 必须按住 Ctrl：没按 Ctrl 时把滚轮让给其它控件（菜单栏溢出滚动、下拉框等）
            if ((ModifierKeys & Keys.Control) != Keys.Control) return false;

            int notches = wheelDelta / SystemInformation.MouseWheelScrollDelta;
            // 高精度滚轮 / 触摸板可能只报小于一格（120）的增量：按方向算作一格，避免完全没反应
            if (notches == 0) notches = Math.Sign(wheelDelta);
            if (notches == 0) return false;

            ApplyVerticalScale(verticalScaleTenths + notches * VerticalScaleTenthsPerWheelNotch);
            return true;
        }

        #endregion





        private void quickToggleBar_ToggleChanged(object? sender, QuickToggleChangedEventArgs e)
        {
            if (e.Key == FreezePreviewTimeKey)
            {
                SetPreviewTimeFrozen(e.IsChecked);
                return;
            }

            int modIndex = Array.IndexOf(ModKeys, e.Key);
            if (modIndex >= 0)
            {
                // 单选：同一次点击里 GroupChanged 与 ToggleChanged 都会到，这里只负责执行切换
                ApplyModMode((ModMode)modIndex);
                return;
            }

            int labelIndex = Array.IndexOf(LabelKeys, e.Key);
            if (labelIndex >= 0)
            {
                ApplyHitObjectLabelMode((QuickLabelMode)labelIndex);
            }
        }

        private void quickToggleBar_GroupChanged(object? sender, QuickToggleGroupChangedEventArgs e)
        {
            if (e.Key == VerticalScaleValueKey)
            {
                ApplyVerticalScale(e.Value);
                return;
            }

            if (e.Key != BarLineValueKey) return;

            ApplyBarLineMode(BarLineSettings.Clamp(e.Value), persist: true);
        }

        /// <summary>
        /// 切换 MOD：同步菜单栏勾选（物件重建由绘制循环按菜单状态自行触发），
        /// 并把快捷按钮设为单选状态。
        /// </summary>
        private void ApplyModMode(ModMode mode)
        {
            noneToolStripMenuItem.Checked = mode == ModMode.None;
            eZToolStripMenuItem.Checked = mode == ModMode.Easy;
            hRToolStripMenuItem.Checked = mode == ModMode.HardRock;
            SetQuickToggleSelection(ModKeys, (int)mode);
        }

        /// <summary>
        /// 切换果子标注模式：同步菜单栏勾选并请求重建（标签文本在重建时才计算），
        /// 同时把快捷按钮设为单选状态。
        /// </summary>
        private void ApplyHitObjectLabelMode(QuickLabelMode mode)
        {
            hideToolStripMenuItem.Checked = mode == QuickLabelMode.Hidden;
            sameWithEditorToolStripMenuItem.Checked = mode == QuickLabelMode.Distance;
            noSliderVelocityMultiplierToolStripMenuItem.Checked = mode == QuickLabelMode.DistanceIgnoreSvm;
            compareWithWalkSpeedToolStripMenuItem.Checked = false;
            difficultyStarsToolStripMenuItem.Checked = mode == QuickLabelMode.Stars;
            fruitCountInComboToolStripMenuItem.Checked = false;

            SetQuickToggleSelection(LabelKeys, (int)mode);
        }

        /// <summary>
        /// 把一组快捷开关按钮设为“只有选中的那个按下”（单选式功能区）。
        /// <para />先悄悄改内部状态（不触发事件），再逐个反射到按钮上；被选中的那个放在最后设置，
        /// 它的 CheckedChanged 会触发一次 <see cref="quickToggleBar_ToggleChanged"/>——
        /// 那次调用看到的已经是最终状态，因此不会再来回切换。
        /// </para>
        /// </summary>
        private void SetQuickToggleSelection(string[] keys, int selectedIndex)
        {
            if (quickToggleBar == null || selectedIndex < 0 || selectedIndex >= keys.Length) return;

            quickToggleBar.SetCheckedSilently(keys, keys[selectedIndex]);
            for (int i = 0; i < keys.Length; i++)
            {
                if (i == selectedIndex) continue;
                quickToggleBar.SetChecked(keys[i], false);
            }
            quickToggleBar.SetChecked(keys[selectedIndex], true);
        }

        /// <summary>
        /// 设置拍线模式（快捷开关滑块与设置菜单下拉框共用），并同步状态文字。
        /// </summary>
        /// <param name="persist">是否立刻写入设置（启动时恢复不需要重复落盘）。</param>
        private void ApplyBarLineMode(BarLineMode mode, bool persist)
        {
            BarLineMode clamped = BarLineSettings.Clamp((int)mode);
            BarLineSettings.CurrentMode = clamped;

            quickToggleBar?.SetGroupSliderValue(BarLineGroupKey, BarLineValueKey, (int)clamped);
            quickToggleBar?.SetGroupLabelText(BarLineGroupKey, BarLineValueKey + "_Text", BarLineStatusText());

            if (!persist) return;
            try
            {
                app.Default.Save();
            }
            catch (Exception ex)
            {
                Log.ConsoleLog("Save bar line mode failed.\r\n" + ex, Log.LogType.Program, Log.LogLevel.Warning);
            }
        }

        /// <summary>按当前菜单状态把 MOD 快捷按钮同步过来（启动时恢复用，不触发重建）。</summary>
        private void SyncModToggleFromMenu()
        {
            if (hRToolStripMenuItem.Checked) SetQuickToggleSelection(ModKeys, (int)ModMode.HardRock);
            else if (eZToolStripMenuItem.Checked) SetQuickToggleSelection(ModKeys, (int)ModMode.Easy);
            else SetQuickToggleSelection(ModKeys, (int)ModMode.None);
        }

        /// <summary>按当前菜单状态把果子标注快捷按钮同步过来（启动时恢复用，不触发重建）。</summary>
        private void SyncLabelToggleFromMenu()
        {
            QuickLabelMode mode = QuickLabelMode.Hidden;
            if (sameWithEditorToolStripMenuItem.Checked) mode = QuickLabelMode.Distance;
            else if (noSliderVelocityMultiplierToolStripMenuItem.Checked) mode = QuickLabelMode.DistanceIgnoreSvm;
            else if (difficultyStarsToolStripMenuItem.Checked) mode = QuickLabelMode.Stars;
            SetQuickToggleSelection(LabelKeys, (int)mode);
        }

        #endregion

        /// <summary>
        /// 切换“固定预览时刻”：打开后预览画布钉在当前 editor 时刻不再跟随，关闭后恢复跟随。
        /// <para />只影响预览时刻（<see cref="DrawingHelper.CurrentTime"/>）；
        /// 距离辅助线用的是始终跟随编辑器的 <see cref="DrawingHelper.EditorTime"/>，不受影响。
        /// </summary>
        private void SetPreviewTimeFrozen(bool frozen)
        {
            previewTimeFrozen = frozen;
            previewTimePinPending = frozen;
            // 判定线据此改为跟随 editor 时刻在画面上的位置
            drawingHelper.FixedPreviewTime = frozen;

            // 按钮图标/提示跟随状态：冰冻图标表示点击后固定，播放图标表示点击后恢复跟随
            ApplyQuickToggleIcons();

            if (!frozen)
            {
                Log.ConsoleLog("Preview time follows editor again.", Log.LogType.Drawing, Log.LogLevel.Info);
                return;
            }

            if (editorTimeAvailable)
            {
                // 已经读到过 editor 时刻：立刻钉住（即使随后还有一帧在路上，时刻也只差一帧，约 20ms）
                previewTimePinPending = false;
                drawingHelper.CurrentTime = drawingHelper.EditorTime;
                Log.ConsoleLog("Preview time frozen at " + drawingHelper.EditorTime.ToString("F0") + " ms.", Log.LogType.Drawing, Log.LogLevel.Info);
            }
            else
            {
                // 还没读到过 editor 时刻（例如启动时恢复固定状态）：等第一帧读到时刻时再钉住，避免固定到 0
                Log.ConsoleLog("Preview time freeze is waiting for the first editor time.", Log.LogType.Drawing, Log.LogLevel.Info);
            }
        }

        /// <summary>
        /// 用一帧读到的 editor 时刻更新绘制时间：
        /// <see cref="DrawingHelper.EditorTime"/> 始终跟随编辑器；
        /// 预览时刻只在正常模式（未固定）或刚打开固定模式（需要钉住）时更新。
        /// <para />固定模式下预览时刻不连续跟随，而是在 editor 位置（判定线）越出画面时才整页翻页，
        /// 见 <see cref="DrawingHelper.PageToKeepEditorVisible"/>。
        /// </summary>
        private void ApplyEditorTime(int editorTime)
        {
            drawingHelper.EditorTime = editorTime;
            editorTimeAvailable = true;

            if (!previewTimeFrozen || previewTimePinPending)
            {
                // 正常跟随；或刚打开固定模式：把预览钉在这一刻
                previewTimePinPending = false;
                drawingHelper.CurrentTime = editorTime;
                return;
            }

            if (drawingHelper.PageToKeepEditorVisible(Canvas.VisibleTopY, Canvas.VisibleBottomY))
            {
                Log.ConsoleLog("Preview time paged to keep editor position visible.", Log.LogType.Drawing, Log.LogLevel.Info);
            }
        }

        /// <summary>
        /// 程序内创建“快捷开关栏”菜单项（显示 / 隐藏整行）与“快捷开关栏位置”子菜单
        /// （顶部横排 / 左侧竖排 / 右侧竖排），文本由语言资源按控件名应用。
        /// </summary>
        private void CreateQuickToggleMenu()
        {
            quickToggleStripMenuItem = new ToolStripMenuItem
            {
                Name = "quickToggleStripMenuItem",
                Checked = true,
            };
            quickToggleStripMenuItem.Click += quickToggleStripMenuItem_Click;

            quickToggleDockTopToolStripMenuItem = CreateQuickToggleDockSideMenuItem(
                "quickToggleDockTopToolStripMenuItem", QuickToggleDockSide.Top);
            quickToggleDockLeftToolStripMenuItem = CreateQuickToggleDockSideMenuItem(
                "quickToggleDockLeftToolStripMenuItem", QuickToggleDockSide.Left);
            quickToggleDockRightToolStripMenuItem = CreateQuickToggleDockSideMenuItem(
                "quickToggleDockRightToolStripMenuItem", QuickToggleDockSide.Right);

            quickToggleDockSideToolStripMenuItem = new ToolStripMenuItem
            {
                Name = "quickToggleDockSideToolStripMenuItem",
            };
            quickToggleDockSideToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                quickToggleDockTopToolStripMenuItem,
                quickToggleDockLeftToolStripMenuItem,
                quickToggleDockRightToolStripMenuItem,
            });

            // 归入 Viewer 菜单的显示选项分组（分隔线之前的最后一项）
            int insertIndex = viewerToolStripMenuItem.DropDownItems.IndexOf(toolStripSeparator1);
            if (insertIndex < 0) insertIndex = viewerToolStripMenuItem.DropDownItems.Count;
            viewerToolStripMenuItem.DropDownItems.Insert(insertIndex, quickToggleStripMenuItem);
            viewerToolStripMenuItem.DropDownItems.Insert(insertIndex + 1, quickToggleDockSideToolStripMenuItem);

            SyncQuickToggleDockSideMenu();
        }

        private ToolStripMenuItem CreateQuickToggleDockSideMenuItem(string name, QuickToggleDockSide side)
        {
            ToolStripMenuItem item = new()
            {
                Name = name,
                CheckOnClick = false,
            };
            item.Click += (sender, e) => SetQuickToggleDockSide(side);
            return item;
        }

        /// <summary>选择吸附位置：立即吸附过去（正在浮动时一并吸附回主窗口）并保存。</summary>
        private void SetQuickToggleDockSide(QuickToggleDockSide side)
        {
            if (quickToggleDocking == null) return;

            quickToggleDocking.DockTo(side);
            SyncQuickToggleDockSideMenu();
            app.Default.Save();
        }

        /// <summary>把当前吸附位置同步到 Viewer 菜单里的三个勾选项（单选）。</summary>
        private void SyncQuickToggleDockSideMenu()
        {
            if (quickToggleDocking == null) return;

            QuickToggleDockSide side = quickToggleDocking.DockSide;
            if (quickToggleDockTopToolStripMenuItem != null) quickToggleDockTopToolStripMenuItem.Checked = side == QuickToggleDockSide.Top;
            if (quickToggleDockLeftToolStripMenuItem != null) quickToggleDockLeftToolStripMenuItem.Checked = side == QuickToggleDockSide.Left;
            if (quickToggleDockRightToolStripMenuItem != null) quickToggleDockRightToolStripMenuItem.Checked = side == QuickToggleDockSide.Right;
        }

        private void quickToggleStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (quickToggleDocking == null) return;

            bool visible = !quickToggleDocking.BarVisible;
            quickToggleDocking.BarVisible = visible;
            if (quickToggleStripMenuItem != null) quickToggleStripMenuItem.Checked = visible;
            quickToggleDocking.SaveState();
            app.Default.Save();
        }

        /// <summary>语言切换后刷新快捷开关条上的文本（功能区标题、按钮、状态文字）。</summary>
        private void ApplyQuickToggleLanguage()
        {
            quickToggleDocking?.ApplyLanguage();
            if (quickToggleBar == null) return;

            bool chinese = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "zh";

            (string text, string toolTip) = FreezePreviewTimeText(previewTimeFrozen);
            quickToggleBar.SetToggleText(FreezePreviewTimeKey, text, toolTip);
            quickToggleBar.SetStandaloneToggleMenuText(FreezePreviewTimeKey, FreezePreviewTimeMenuText());

            // 功能区标题（AddGroup 对已存在的功能区只更新标题）
            quickToggleBar.AddGroup(ModGroupKey, "");
            quickToggleBar.AddGroup(LabelGroupKey, "", LabelGroupMenuText());
            quickToggleBar.AddGroup(BarLineGroupKey, chinese ? "拍线" : "Bar Lines");
            quickToggleBar.AddGroup(VerticalScaleGroupKey, chinese ? "Y缩放" : "Y Scale");

            for (int i = 0; i < ModKeys.Length; i++)
            {
                quickToggleBar.SetToggleText(ModKeys[i], ModToggleText((ModMode)i), ModToggleToolTip((ModMode)i));
            }
            for (int i = 0; i < LabelKeys.Length; i++)
            {
                quickToggleBar.SetToggleText(LabelKeys[i], LabelToggleText((QuickLabelMode)i), LabelToggleToolTip((QuickLabelMode)i));
            }

            // 提示文字按语言刷新后再重装图标（图标只显示时，文字只作为按钮提示）
            ApplyQuickToggleIcons();

            quickToggleBar.SetGroupLabelText(BarLineGroupKey, BarLineValueKey + "_Text", BarLineStatusText());
            quickToggleBar.SetGroupLabelText(VerticalScaleGroupKey, VerticalScaleValueKey + "_Text", VerticalScaleStatusText());
            quickToggleBar.SetGroupLabelToolTip(VerticalScaleGroupKey, VerticalScaleValueKey + "_Text", VerticalScaleHintText());
        }

        /// <summary>
        /// 解析模板 .osu 文件并转换为可接物件（在后台线程执行）。
        /// 只读使用，不影响主谱面的解析/转换时序。
        /// </summary>
        private static TemplateBeatmapData? LoadTemplate(string path)
        {
            Beatmap? beatmap = BeatmapBuilder.BuildNewBeatmapFromBeatmapFile(path);
            if (beatmap == null) return null;

            BeatmapConverter converter = app.Default.Use_Stable_Converter ? new BeatmapConverterOsuStable() : new BeatmapConverter();
            IBeatmap? converted = converter.GetConvertedBeatmap(beatmap, 0);
            if (converted == null) return null;

            List<PalpableCatchHitObject> objects = converter.GetPalpableObjects(converted, 0);
            if (objects.Count <= 0) return null;

            float circleDiameter = (float)(108.848 - converted.Difficulty.CircleSize * 8.9646);
            int approachTime = (int)((converted.Difficulty.ApproachRate < 5)
                ? 1800 - converted.Difficulty.ApproachRate * 120
                : 1200 - (converted.Difficulty.ApproachRate - 5) * 150);

            return new TemplateBeatmapData
            {
                FilePath = path,
                Filename = Path.GetFileName(path),
                Objects = objects,
                CircleDiameter = circleDiameter,
                ApproachTime = approachTime,
            };
        }

        private void TopWhenEditorFocusToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (topmostCheck)
            {
                topmostCheck = false;
                TopWhenEditorFocusToolStripMenuItem.Checked = false;
            }
            else
            {
                topmostCheck = true;
                TopWhenEditorFocusToolStripMenuItem.Checked = true;
            }
            app.Default.Auto_Topmost = topmostCheck;
            app.Default.Save();
        }

        private void loadOnlyBookmarkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Select Bookmark file";
                openFileDialog.Filter = "BookmarkPlus File (*.bps)|*.bps";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    List<Bookmark> bookmarks = BookmarkPlus.loadBookmarksFromFile(filePath, false);
                    bookmarkManager.Bookmarks = bookmarks;
                    drawingHelper.Bookmarks = bookmarks;
                    if (bookmarks.Count <= 0) MessageBox.Show("There are no Bookmarks in this file.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    else MessageBox.Show("Loaded " + bookmarks.Count + " Bookmark(s).", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void loadFullBookmarkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Select Bookmark file";
                openFileDialog.Filter = "BookmarkPlus File (*.bps)|*.bps";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    List<Bookmark> bookmarks = BookmarkPlus.loadBookmarksFromFile(filePath, true);
                    bookmarkManager = new BookmarkManager(bookmarks);
                    drawingHelper.Bookmarks = bookmarks;
                    if (bookmarks.Count <= 0) MessageBox.Show("There are no Bookmarks in this file.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    else MessageBox.Show("Loaded " + bookmarks.Count + " Bookmark(s).", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void saveBookmarkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (bookmarkManager.BeatmapFolder == "" || bookmarkManager.BeatmapFilename == "")
            {
                MessageBox.Show("Editor is not running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 创建SaveFileDialog实例
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Title = "Save Bookmark";
            saveFileDialog.Filter = "BookmarkPlus File (*.bps)|*.bps"; // 文件类型过滤器
            saveFileDialog.FileName = editorReaderHelper.beatmap_title;
            saveFileDialog.DefaultExt = "bps"; // 默认扩展名

            // 显示对话框并获取结果
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                // 获取选中的文件路径
                string filePath = saveFileDialog.FileName;
                // 写入文件
                if (BookmarkPlus.SaveBookmarksToFile(filePath, bookmarkManager.Bookmarks))
                    MessageBox.Show("Bookmarks saved to " + filePath, "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void SetDelBookmark(int styleId)
        {
            if (string.IsNullOrEmpty(bookmarkManager.BeatmapFolder) || string.IsNullOrEmpty(bookmarkManager.BeatmapFilename))
            {
                MessageBox.Show("Editor is not running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            double currentTime = drawingHelper.CurrentTime;
            bookmarkManager.Add_Del_Bookmark(new Bookmark { StyleId = styleId, Time = currentTime });
            if (app.Default.Bookmark_AutoLoadSave)
            {
                string filepath = Path.Combine(app.Default.Bookmark_FolderPath, bookmarkManager.BeatmapFolder, bookmarkManager.BeatmapFilename) + ".bps";
                BookmarkPlus.SaveBookmarksToFile(filepath, bookmarkManager.Bookmarks);
            }
        }

        private void bookmarkSetStripMenuItem_1_Click(object sender, EventArgs e)
        {
            SetDelBookmark(1);
        }

        private void bookmarkSetStripMenuItem_2_Click(object sender, EventArgs e)
        {
            SetDelBookmark(2);
        }

        private void bookmarkSetStripMenuItem_3_Click(object sender, EventArgs e)
        {
            SetDelBookmark(3);
        }

        private void bookmarkSetStripMenuItem_4_Click(object sender, EventArgs e)
        {
            SetDelBookmark(4);
        }

        private void bookmarkSetStripMenuItem_5_Click(object sender, EventArgs e)
        {
            SetDelBookmark(5);
        }

        private void bookmarkSetStripMenuItem_6_Click(object sender, EventArgs e)
        {
            SetDelBookmark(6);
        }

        private void bookmarkSetStripMenuItem_7_Click(object sender, EventArgs e)
        {
            SetDelBookmark(7);
        }

        private void bookmarkSetStripMenuItem_8_Click(object sender, EventArgs e)
        {
            SetDelBookmark(8);
        }

        private const int WM_HOTKEY = 0x0312;
        // 处理Windows消息
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == 101) SetDelBookmark(1);
                else if (id == 102) SetDelBookmark(2);
                else if (id == 103) SetDelBookmark(3);
                else if (id == 104) SetDelBookmark(4);
                else if (id == 105) SetDelBookmark(5);
                else if (id == 106) SetDelBookmark(6);
                else if (id == 107) SetDelBookmark(7);
                else if (id == 108) SetDelBookmark(8);
            }
            base.WndProc(ref m);
        }

        private void ClearBookmarkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bookmarkManager.Bookmarks.Clear();
        }

        private void cubicFittingCurveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (app.Default.Show_CubicFittingCurve)
            {
                app.Default.Show_CubicFittingCurve = false;
                cubicFittingCurveToolStripMenuItem.Checked = false;
            }
            else
            {
                app.Default.Show_CubicFittingCurve = true;
                cubicFittingCurveToolStripMenuItem.Checked = true;
            }
            app.Default.Save();
        }

        private void lazerConverterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            app.Default.Use_Stable_Converter = false;
            app.Default.Save();
            lazerConverterToolStripMenuItem.Checked = true;
            stableConverterToolStripMenuItem.Checked = false;
        }

        private void stableConverterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            app.Default.Use_Stable_Converter = true;
            app.Default.Save();
            lazerConverterToolStripMenuItem.Checked = false;
            stableConverterToolStripMenuItem.Checked = true;
        }

        private void generateConversionMappingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_committed.ConvertedBeatmap != null)
            {
                string conversionMapping = ((BeatmapConverterOsuStable)stableBeatmapConverter).BuildConversionMapping(_committed.ConvertedBeatmap, _committed.Mods);
                StreamWriter writer = new("expected-conversion.json");
                writer.Write(conversionMapping);
                writer.Close();
            }
        }

        public void GenerateConversionMapping(string path, int mods)
        {
            Beatmap? beatmap = BeatmapBuilder.BuildNewBeatmapFromBeatmapFile(path);
            if (beatmap == null)
            {
                return;
            }
            IBeatmap convertedBeatmap = stableBeatmapConverter.GetConvertedBeatmap(beatmap, mods);
            string conversionMapping = ((BeatmapConverterOsuStable)stableBeatmapConverter).BuildConversionMapping(convertedBeatmap, mods);
            StreamWriter writer = new(Path.GetFileNameWithoutExtension(path) + ".json");
            writer.Write(conversionMapping);
            writer.Close();
        }

    }



}
