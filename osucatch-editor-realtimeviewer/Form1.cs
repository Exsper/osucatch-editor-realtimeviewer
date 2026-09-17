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

        // 模板谱面（只读参考）
        private ToolStripMenuItem? templateToolStripMenuItem;
        private ToolStripMenuItem? selectTemplateStripMenuItem;
        private ToolStripMenuItem? unloadTemplateStripMenuItem;
        private TemplateBeatmapData? templateData;

        // 快捷开关条：可吸附在菜单栏下方的工具栏行里，也可拖出为浮动小窗口
        private QuickToggleBar? quickToggleBar;
        private QuickToggleDockRow? quickToggleDockRow;
        private QuickToggleDocking? quickToggleDocking;
        private ToolStripMenuItem? quickToggleStripMenuItem;

        /// <summary>快捷开关“固定预览时刻”的标识（勾选状态随其它开关一起持久化）。</summary>
        private const string FreezePreviewTimeKey = "FreezePreviewTime";

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

        private PeriodicTaskRunner runner;
        private HealthMonitor? healthMonitor;

        public Form1()
        {
            Log.Breadcrumb("Form1: constructing...");
            InitializeComponent();

            // 模板菜单在构造函数里创建，确保语言资源能应用到它
            CreateTemplateMenu();

            // 快捷开关条（开关内容在 CreateQuickToggles 里添加）
            CreateQuickToggleBar();
            CreateQuickToggles();

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

            // 快捷开关栏：恢复开关状态与停靠 / 浮动状态（浮窗要等主窗口显示出来后再弹出）
            quickToggleBar?.ApplyCheckedStates(app.Default.QuickToggle_States);
            // ApplyCheckedStates 不触发事件，这里手动把“固定预览时刻”的内部状态同步过来
            SetPreviewTimeFrozen(quickToggleBar?.IsChecked(FreezePreviewTimeKey) ?? false);
            quickToggleDocking?.ApplyStartupState(app.Default.QuickToggle_Visible, app.Default.QuickToggle_Floating);
            if (quickToggleStripMenuItem != null) quickToggleStripMenuItem.Checked = app.Default.QuickToggle_Visible;

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
                resources.ApplyResources(item, item.Name);
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

        private async void selectTemplateStripMenuItem_Click(object sender, EventArgs e)
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

        private void unloadTemplateStripMenuItem_Click(object sender, EventArgs e)
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
            if (unloadTemplateStripMenuItem == null) return;

            string baseText = unloadTemplateStripMenuItem.Text;
            int suffixIndex = baseText.LastIndexOf(" (");
            if (suffixIndex > 0) baseText = baseText.Substring(0, suffixIndex);

            unloadTemplateStripMenuItem.Text = (templateData != null) ? baseText + " (" + templateData.Filename + ")" : baseText;
        }

        /// <summary>
        /// 创建快捷开关条（菜单栏正下方的工具栏行）：吸附在工具栏与拖出为浮动小窗口之间切换。
        /// </summary>
        private void CreateQuickToggleBar()
        {
            quickToggleBar = new QuickToggleBar();
            // 先于停靠管理器订阅：停靠管理器在自己的 ToggleChanged 处理里统一保存设置，
            // 这里先更新运行状态，保证同一次保存里带上的都是最新状态
            quickToggleBar.ToggleChanged += quickToggleBar_ToggleChanged;

            quickToggleDockRow = new QuickToggleDockRow();
            quickToggleDocking = new QuickToggleDocking(this, quickToggleBar, quickToggleDockRow, menuStrip1);

            QuickToggleDocking docking = quickToggleDocking;
            docking.StateChanged += (sender, e) =>
            {
                if (quickToggleStripMenuItem != null) quickToggleStripMenuItem.Checked = docking.BarVisible;
            };

            // 停靠行要排在菜单栏之后、画布之前参与停靠布局：
            // WinForms 按控件集合的倒序布局停靠控件（集合末尾的控件最先被安排），
            // 把行插到菜单栏原来的位置上，它才会正好占据菜单栏下方的一行，
            // 而画布（Dock=Fill）在其下方填满剩余空间；否则行会盖在画布上而不是挤开画布。
            Controls.Add(quickToggleDockRow);
            Controls.SetChildIndex(quickToggleDockRow, Controls.GetChildIndex(menuStrip1));

            CreateQuickToggleMenu();
        }

        /// <summary>
        /// 创建快捷开关条上的即时开关。开关内容后续继续在这里添加。
        /// </summary>
        private void CreateQuickToggles()
        {
            if (quickToggleBar == null) return;

            (string text, string toolTip) = FreezePreviewTimeText(previewTimeFrozen);
            quickToggleBar.AddToggle(FreezePreviewTimeKey, text, false);
            quickToggleBar.SetToggleText(FreezePreviewTimeKey, text, toolTip);
        }

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

        private void quickToggleBar_ToggleChanged(object? sender, QuickToggleChangedEventArgs e)
        {
            if (e.Key == FreezePreviewTimeKey) SetPreviewTimeFrozen(e.IsChecked);
        }

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

            // 按钮文本/提示跟随状态：⏸ 表示点击后固定，▶ 表示点击后恢复跟随
            (string text, string toolTip) = FreezePreviewTimeText(frozen);
            quickToggleBar?.SetToggleText(FreezePreviewTimeKey, text, toolTip);

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
        /// 程序内创建“快捷开关栏”菜单项（显示 / 隐藏整行）。
        /// </summary>
        private void CreateQuickToggleMenu()
        {
            quickToggleStripMenuItem = new ToolStripMenuItem
            {
                Name = "quickToggleStripMenuItem",
                Checked = true,
            };
            quickToggleStripMenuItem.Click += quickToggleStripMenuItem_Click;

            // 归入 Viewer 菜单的显示选项分组（分隔线之前的最后一项）
            int insertIndex = viewerToolStripMenuItem.DropDownItems.IndexOf(toolStripSeparator1);
            if (insertIndex < 0) insertIndex = viewerToolStripMenuItem.DropDownItems.Count;
            viewerToolStripMenuItem.DropDownItems.Insert(insertIndex, quickToggleStripMenuItem);
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

        /// <summary>语言切换后刷新快捷开关条上的固定文本。</summary>
        private void ApplyQuickToggleLanguage()
        {
            quickToggleDocking?.ApplyLanguage();

            (string text, string toolTip) = FreezePreviewTimeText(previewTimeFrozen);
            quickToggleBar?.SetToggleText(FreezePreviewTimeKey, text, toolTip);
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
            if (bookmarkManager.BeatmapFolder == null || bookmarkManager.BeatmapFilename == null)
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
