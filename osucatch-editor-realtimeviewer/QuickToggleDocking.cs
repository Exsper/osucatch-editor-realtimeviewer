namespace osucatch_editor_realtimeviewer
{
    /// <summary>
    /// 快捷开关条的停靠行：吸附时占据菜单栏正下方的一行；浮动或隐藏时高度归零，
    /// 配合 <see cref="Control.Visible"/> = false 完全不占用画布空间。
    /// </summary>
    internal sealed class QuickToggleDockRow : Panel
    {
        private QuickToggleBar? bar;

        internal QuickToggleDockRow()
        {
            Name = "quickToggleDockRow";
            Dock = DockStyle.Top;
            AutoSize = false;
            Height = 0;
            Margin = Padding.Empty;
            Padding = Padding.Empty;
            TabStop = false;
        }

        internal bool HasBar => bar != null && bar.Parent == this;

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (e.Control is QuickToggleBar added) bar = added;
            PerformLayout();
        }

        protected override void OnControlRemoved(ControlEventArgs e)
        {
            base.OnControlRemoved(e);
            if (e.Control != bar) return;
            bar = null;
            if (Height != 0) Height = 0;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (bar == null || bar.Parent != this) return;

            // 行高由条的首选高度决定；条本身不自动调整大小，宽度始终铺满窗口。
            // 首选高度必须按“实际可用宽度”问条要：条用 Flow 布局换行排布，传一个很宽的宽度
            // 只会得到“单行”的高度，行会被压成一行高，第二行往后的控件全被裁掉看不见。
            int width = Math.Max(Width, 1);
            int height = Math.Max(bar.GetPreferredSize(new Size(width, 0)).Height, 1);
            bar.Bounds = new Rectangle(0, 0, width, height);
            if (Height != height) Height = height;
        }
    }

    /// <summary>浮动小窗口：浮动状态下承载快捷开关条。</summary>
    internal sealed class QuickToggleFloatForm : Form
    {
        private readonly QuickToggleBar bar;

        internal QuickToggleFloatForm(QuickToggleBar bar)
        {
            this.bar = bar;

            Name = "quickToggleFloatForm";
            Text = bar.BarTitle;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.Manual;
            // 主窗口按 DPI 自行缩放，这里不再自动缩放，避免高 DPI 下被放大两次
            AutoScaleMode = AutoScaleMode.None;
            Controls.Add(bar);
            Refit();
        }

        /// <summary>让窗口贴合开关条尺寸（开关数量变化时也会调用）。</summary>
        internal void Refit()
        {
            Size size = bar.PreferredFloatSize();
            if (ClientSize != size) ClientSize = size;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            bar.Bounds = new Rectangle(Point.Empty, ClientSize);
        }
    }

    /// <summary>拖动浮窗时提示吸附目标位置的无边框小窗（不抢焦点）。</summary>
    internal sealed class QuickToggleDropHintForm : Form
    {
        internal QuickToggleDropHintForm()
        {
            Name = "quickToggleDropHintForm";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = SystemColors.Highlight;
            Opacity = 0.55;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
        }

        // 拖动过程中弹出提示窗不应打断鼠标交互
        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return cp;
            }
        }
    }

    /// <summary>
    /// 快捷开关条的停靠 / 浮动管理器：在“菜单栏下方的停靠行”和“浮动小窗口”之间搬运开关条，
    /// 处理拖出、拖回时的吸附判定，并把停靠状态与开关状态持久化到用户设置。
    /// </summary>
    internal sealed class QuickToggleDocking : IDisposable
    {
        /// <summary>吸附判定距离（屏幕像素）：拖到停靠行 / 菜单栏附近这么多像素内松手即吸附回去。</summary>
        private const int SnapDistance = 32;

        /// <summary>吸附提示条高度（屏幕像素）。</summary>
        private const int HintHeight = 6;

        private readonly Form owner;
        private readonly QuickToggleBar bar;
        private readonly QuickToggleDockRow dockRow;
        private readonly Control? snapAnchor;
        private readonly QuickToggleFloatForm floatForm;
        private readonly QuickToggleDropHintForm hintForm;

        private bool barVisible = true;
        private bool floating;
        private bool dragging;
        private bool disposed;
        private bool suppressReDock;

        private Point dragOriginCursor;
        private Point dragOriginFormLocation;

        internal QuickToggleDocking(Form owner, QuickToggleBar bar, QuickToggleDockRow dockRow, Control? snapAnchor)
        {
            this.owner = owner;
            this.bar = bar;
            this.dockRow = dockRow;
            this.snapAnchor = snapAnchor;

            floatForm = new QuickToggleFloatForm(bar);
            hintForm = new QuickToggleDropHintForm();

            // 浮窗 / 提示窗都归属主窗口：主窗口置顶时一起置顶，主窗口关闭时一起关闭
            floatForm.Owner = owner;
            floatForm.FormClosing += FloatForm_FormClosing;
            hintForm.Owner = owner;

            bar.DragStarted += Bar_DragStarted;
            bar.DragMoved += Bar_DragMoved;
            bar.DragEnded += Bar_DragEnded;
            bar.FloatingToggleRequested += Bar_FloatingToggleRequested;
            bar.HideRequested += Bar_HideRequested;
            bar.ContentChanged += Bar_ContentChanged;
            bar.ToggleChanged += Bar_ToggleChanged;
            bar.GroupsVisibilityChanged += Bar_GroupsVisibilityChanged;

            dockRow.Controls.Add(bar);
        }

        /// <summary>停靠状态（停靠 / 浮动 / 显示 / 隐藏）发生变化时触发。</summary>
        internal event EventHandler? StateChanged;

        /// <summary>当前是否浮动为小窗口。</summary>
        internal bool IsFloating => floating;

        /// <summary>快捷开关栏是否显示（隐藏时停靠行与浮窗都不占屏幕空间）。</summary>
        internal bool BarVisible
        {
            get => barVisible;
            set
            {
                if (disposed || barVisible == value) return;
                barVisible = value;
                ApplyVisibility();
                SaveState();
                RaiseStateChanged();
            }
        }

        /// <summary>
        /// 按用户设置恢复启动状态。<paramref name="floatMode"/> 为 true 时等主窗口显示出来之后再弹出浮窗，
        /// 避免启动过程中浮窗出现在错误的位置。
        /// </summary>
        internal void ApplyStartupState(bool visible, bool floatMode)
        {
            if (disposed) return;

            barVisible = visible;
            if (!visible || !floatMode)
            {
                DockCore();
                return;
            }

            owner.BeginInvoke(new Action(() =>
            {
                if (disposed) return;
                FloatCore(SavedFloatLocation());
            }));
        }

        /// <summary>吸附回工具栏。</summary>
        internal void Dock()
        {
            if (disposed) return;
            DockCore();
            SaveState();
        }

        /// <summary>浮动为小窗口；<paramref name="location"/> 为空时沿用上次保存的位置。</summary>
        internal void Float(Point? location = null)
        {
            if (disposed) return;
            FloatCore(location ?? SavedFloatLocation());
            SaveState();
        }

        /// <summary>在停靠与浮动之间切换。</summary>
        internal void ToggleFloating()
        {
            if (floating) Dock();
            else Float();
        }

        /// <summary>主窗口置顶状态变化时同步浮窗，保证浮窗与预览窗口一起显示在编辑器之上。</summary>
        internal void SyncTopMost(bool topMost)
        {
            if (disposed || !floating || floatForm.IsDisposed) return;
            if (floatForm.TopMost != topMost) floatForm.TopMost = topMost;
        }

        /// <summary>按当前语言刷新条与浮窗上的文本。</summary>
        internal void ApplyLanguage()
        {
            bar.ApplyLanguage();
            if (!floatForm.IsDisposed) floatForm.Text = bar.BarTitle;
        }

        /// <summary>
        /// 把停靠状态写入用户设置（不调用 <c>app.Default.Save()</c>，由调用方统一保存）。
        /// </summary>
        internal void SaveState()
        {
            if (disposed) return;

            app.Default.QuickToggle_Visible = barVisible;
            app.Default.QuickToggle_Floating = floating;
            if (floating && floatForm.Visible)
            {
                app.Default.QuickToggle_Float_X = floatForm.Location.X;
                app.Default.QuickToggle_Float_Y = floatForm.Location.Y;
            }
            app.Default.QuickToggle_States = bar.GetCheckedStates();
            app.Default.QuickToggle_HiddenGroups = bar.GetHiddenGroups();
        }

        #region 停靠 / 浮动切换

        private void DockCore()
        {
            MoveBar(dockRow);
            floating = false;
            if (floatForm.Visible) floatForm.Hide();
            dockRow.Visible = barVisible;
            bar.IsFloating = false;
            bar.ApplyLanguage();
            RaiseStateChanged();
        }

        private void FloatCore(Point? location)
        {
            // 弹出前记下条在屏幕上的位置：用作“就地在工具栏下方弹出”的默认位置
            Rectangle barScreenBefore = bar.IsHandleCreated ? bar.RectangleToScreen(bar.ClientRectangle) : Rectangle.Empty;

            MoveBar(floatForm);
            floating = true;
            floatForm.Refit();

            floatForm.Location = ClampToScreen(location ?? DefaultFloatLocation(barScreenBefore), floatForm.Size);
            floatForm.Show();
            SyncTopMost(owner.TopMost);

            dockRow.Visible = false;
            bar.IsFloating = true;
            bar.ApplyLanguage();
            RaiseStateChanged();
        }

        private void ApplyVisibility()
        {
            if (floating)
            {
                if (barVisible) FloatCore(SavedFloatLocation() ?? floatForm.Location);
                else floatForm.Hide();
                dockRow.Visible = false;
            }
            else
            {
                dockRow.Visible = barVisible;
            }
        }

        private void MoveBar(Control parent)
        {
            if (bar.Parent == parent) return;
            // 先摘再挂：停靠行会在移除时把高度归零，加入时按条的首选高度重新撑开
            bar.Parent?.Controls.Remove(bar);
            parent.Controls.Add(bar);
            parent.PerformLayout();
        }

        private Point DefaultFloatLocation(Rectangle barScreenBefore)
        {
            if (barScreenBefore.IsEmpty) return floatForm.Location;
            return new Point(barScreenBefore.X, barScreenBefore.Bottom + 4);
        }

        private static Point ClampToScreen(Point location, Size size)
        {
            Rectangle area = Screen.FromPoint(location).WorkingArea;
            int x = Math.Clamp(location.X, area.Left - size.Width + 60, area.Right - 60);
            int y = Math.Clamp(location.Y, area.Top, area.Bottom - 30);
            return new Point(x, y);
        }

        private Point? SavedFloatLocation()
        {
            int x = app.Default.QuickToggle_Float_X;
            int y = app.Default.QuickToggle_Float_Y;
            if (x < 0 || y < 0) return null;

            // 上次的浮窗位置可能已经不在任何屏幕内（拔掉显示器 / 改了分辨率），此时回退到默认位置
            bool onScreen = false;
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.Contains(x + 20, y + 5))
                {
                    onScreen = true;
                    break;
                }
            }
            return onScreen ? new Point(x, y) : null;
        }

        #endregion

        #region 拖动吸附

        private void Bar_DragStarted(object? sender, QuickToggleDragEventArgs e)
        {
            if (disposed) return;
            dragging = true;

            if (floating)
            {
                dragOriginCursor = e.CursorScreen;
                dragOriginFormLocation = floatForm.Location;
            }
            else
            {
                // 从停靠状态拖出：先弹出为浮窗，再整体平移，使鼠标仍抓住条上的同一点
                FloatCore(null);
                Point desiredBarScreen = new(e.CursorScreen.X - e.GrabOffset.X, e.CursorScreen.Y - e.GrabOffset.Y);
                Point currentBarScreen = bar.PointToScreen(Point.Empty);
                floatForm.Location = new Point(
                    floatForm.Location.X + desiredBarScreen.X - currentBarScreen.X,
                    floatForm.Location.Y + desiredBarScreen.Y - currentBarScreen.Y);
                dragOriginCursor = e.CursorScreen;
                dragOriginFormLocation = floatForm.Location;
            }

            UpdateHint(IsInSnapZone(e.CursorScreen));
        }

        private void Bar_DragMoved(object? sender, QuickToggleDragEventArgs e)
        {
            if (disposed || !dragging) return;

            Point location = new(
                dragOriginFormLocation.X + e.CursorScreen.X - dragOriginCursor.X,
                dragOriginFormLocation.Y + e.CursorScreen.Y - dragOriginCursor.Y);
            if (floatForm.Location != location) floatForm.Location = location;

            UpdateHint(IsInSnapZone(e.CursorScreen));
        }

        private void Bar_DragEnded(object? sender, QuickToggleDragEventArgs e)
        {
            if (disposed) return;
            dragging = false;
            UpdateHint(false);

            if (IsInSnapZone(e.CursorScreen))
            {
                Dock();
                return;
            }

            // 停在屏幕外会让用户找不回浮窗，松手时夹回可见区域
            floatForm.Location = ClampToScreen(floatForm.Location, floatForm.Size);
            SaveState();
        }

        /// <summary>拖到停靠行所在的一行（或菜单栏上）即视为要吸附回去。</summary>
        private bool IsInSnapZone(Point cursorScreen)
        {
            Rectangle target = DockTargetScreenRect();
            target.Inflate(0, SnapDistance);
            if (target.Contains(cursorScreen)) return true;

            Control? anchor = snapAnchor;
            return anchor != null && anchor.IsHandleCreated &&
                anchor.RectangleToScreen(anchor.ClientRectangle).Contains(cursorScreen);
        }

        /// <summary>吸附目标矩形：菜单栏正下方、与主窗口同宽的一行（屏幕坐标）。</summary>
        private Rectangle DockTargetScreenRect()
        {
            int menuBottom = snapAnchor != null ? snapAnchor.Bottom : 0;
            Point topLeft = owner.PointToScreen(new Point(0, menuBottom));
            int height = Math.Max(bar.PreferredSize.Height, 1);
            return new Rectangle(topLeft, new Size(owner.ClientSize.Width, height));
        }

        private void UpdateHint(bool show)
        {
            if (show)
            {
                Rectangle target = DockTargetScreenRect();
                hintForm.Bounds = new Rectangle(target.X, target.Y, Math.Max(target.Width, 1), HintHeight);
                if (!hintForm.Visible) hintForm.Show();
            }
            else if (hintForm.Visible)
            {
                hintForm.Hide();
            }
        }

        #endregion

        #region 条的事件

        private void Bar_FloatingToggleRequested(object? sender, EventArgs e)
        {
            ToggleFloating();
        }

        private void Bar_HideRequested(object? sender, EventArgs e)
        {
            BarVisible = false;
        }

        private void Bar_ContentChanged(object? sender, EventArgs e)
        {
            // 条不自动调整大小，内容变化后由宿主重新计算行高 / 浮窗尺寸
            if (disposed) return;
            if (dockRow.HasBar) dockRow.PerformLayout();
            if (floating) floatForm.Refit();
        }

        private void Bar_ToggleChanged(object? sender, QuickToggleChangedEventArgs e)
        {
            SaveAndFlush();
        }

        private void Bar_GroupsVisibilityChanged(object? sender, EventArgs e)
        {
            // 右键菜单隐藏/显示功能区：立即写入设置，下次启动保持
            SaveAndFlush();
        }

        /// <summary>
        /// 把开关条状态写入设置并立刻落盘。
        /// <para />写设置失败（配置文件只读/被占用等）不应让点开关变成崩溃，记录一条日志即可。
        /// </summary>
        private void SaveAndFlush()
        {
            SaveState();
            try
            {
                app.Default.Save();
            }
            catch (Exception ex)
            {
                Log.ConsoleLog("Save quick toggle state failed.\r\n" + ex, Log.LogType.Program, Log.LogLevel.Warning);
            }
        }

        private void FloatForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (disposed || suppressReDock) return;
            // 程序退出（含主窗口关闭时自动关闭所属窗口）时正常关闭，不做吸附
            if (e.CloseReason is CloseReason.ApplicationExitCall or CloseReason.WindowsShutDown
                or CloseReason.TaskManagerClosing or CloseReason.FormOwnerClosing)
            {
                return;
            }
            if (owner.IsDisposed || owner.Disposing) return;

            // 点浮窗的关闭按钮 = 吸附回工具栏（而不是丢失快捷开关条）
            e.Cancel = true;
            Dock();
        }

        private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

        #endregion

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            suppressReDock = true;

            bar.DragStarted -= Bar_DragStarted;
            bar.DragMoved -= Bar_DragMoved;
            bar.DragEnded -= Bar_DragEnded;
            bar.FloatingToggleRequested -= Bar_FloatingToggleRequested;
            bar.HideRequested -= Bar_HideRequested;
            bar.ContentChanged -= Bar_ContentChanged;
            bar.ToggleChanged -= Bar_ToggleChanged;
            bar.GroupsVisibilityChanged -= Bar_GroupsVisibilityChanged;

            if (hintForm.Visible) hintForm.Hide();
            hintForm.Dispose();

            // 把条放回停靠行再销毁浮窗：条始终属于主窗口的控件树，不会被浮窗一起销毁
            MoveBar(dockRow);
            if (floatForm.Visible) floatForm.Hide();
            floatForm.Dispose();
        }
    }
}
