namespace osucatch_editor_realtimeviewer
{
    /// <summary>
    /// 快捷开关条的停靠行：吸附在顶部时占据菜单栏正下方的一行，
    /// 吸附在左 / 右侧时占据画布旁边的一列；浮动或隐藏时尺寸归零，
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
            Width = 0;
            Visible = false;
            Margin = Padding.Empty;
            Padding = Padding.Empty;
            TabStop = false;
        }

        internal bool HasBar => bar != null && bar.Parent == this;

        /// <summary>当前是否竖排（吸附在左 / 右侧）。</summary>
        internal bool IsVertical => Dock is DockStyle.Left or DockStyle.Right;

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
            if (IsVertical)
            {
                if (Width != 0) Width = 0;
            }
            else if (Height != 0)
            {
                Height = 0;
            }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (bar == null || bar.Parent != this) return;

            if (IsVertical)
            {
                // 列宽由条的竖排宽度决定；条本身不自动调整大小，高度始终铺满可用区域
                int height = Math.Max(Height, 1);
                int width = Math.Max(bar.GetPreferredSize(new Size(0, height)).Width, 1);
                bar.Bounds = new Rectangle(0, 0, width, height);
                bar.NoteDockedThickness(vertical: true, width);
                if (Width != width) Width = width;
                return;
            }

            // 行高由条的首选高度决定；条本身不自动调整大小，宽度始终铺满窗口。
            // 首选高度必须按“实际可用宽度”问条要：条用 Flow 布局换行排布，传一个很宽的宽度
            // 只会得到“单行”的高度，行会被压成一行高，第二行往后的控件全被裁掉看不见。
            int rowWidth = Math.Max(Width, 1);
            int rowHeight = Math.Max(bar.GetPreferredSize(new Size(rowWidth, 0)).Height, 1);
            bar.Bounds = new Rectangle(0, 0, rowWidth, rowHeight);
            bar.NoteDockedThickness(vertical: false, rowHeight);
            if (Height != rowHeight) Height = rowHeight;
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
    /// 快捷开关条的停靠 / 浮动管理器：在“菜单栏下方的停靠行”、“画布左 / 右侧的停靠列”
    /// 和“浮动小窗口”之间搬运开关条，处理拖出、拖回时的吸附判定（顶部 / 左侧 / 右侧），
    /// 并把停靠位置与开关状态持久化到用户设置。
    /// </summary>
    internal sealed class QuickToggleDocking : IDisposable
    {
        /// <summary>吸附判定距离（屏幕像素）：拖到停靠区附近这么多像素内松手即吸附过去。</summary>
        private const int SnapDistance = 32;

        /// <summary>吸附提示条厚度（屏幕像素）。</summary>
        private const int HintHeight = 6;

        /// <summary>三个可吸附的位置，按“顶部 → 左侧 → 右侧”的顺序判定。</summary>
        private static readonly QuickToggleDockSide[] DockSides =
        {
            QuickToggleDockSide.Top,
            QuickToggleDockSide.Left,
            QuickToggleDockSide.Right,
        };

        private readonly Form owner;
        private readonly QuickToggleBar bar;
        private readonly QuickToggleDockRow dockRow;
        private readonly Control? snapAnchor;
        private readonly Control? bottomAnchor;
        private readonly QuickToggleFloatForm floatForm;
        private readonly QuickToggleDropHintForm hintForm;

        private bool barVisible = true;
        private bool floating;
        private QuickToggleDockSide dockSide = QuickToggleDockSide.Top;
        private bool dragging;
        private bool disposed;
        private bool suppressReDock;

        private Point dragOriginCursor;
        private Point dragOriginFormLocation;

        internal QuickToggleDocking(Form owner, QuickToggleBar bar, QuickToggleDockRow dockRow, Control? snapAnchor, Control? bottomAnchor = null)
        {
            this.owner = owner;
            this.bar = bar;
            this.dockRow = dockRow;
            this.snapAnchor = snapAnchor;
            this.bottomAnchor = bottomAnchor;

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
            bar.DockSideRequested += Bar_DockSideRequested;
            bar.ContentChanged += Bar_ContentChanged;
            bar.ToggleChanged += Bar_ToggleChanged;
            bar.GroupsVisibilityChanged += Bar_GroupsVisibilityChanged;

            dockRow.Controls.Add(bar);
        }

        /// <summary>停靠状态（停靠位置 / 浮动 / 显示 / 隐藏）发生变化时触发。</summary>
        internal event EventHandler? StateChanged;

        /// <summary>当前是否浮动为小窗口。</summary>
        internal bool IsFloating => floating;

        /// <summary>
        /// 当前吸附位置：顶部（菜单栏下方一行，控件横排）或左 / 右侧（画布旁边一列，控件竖排）。
        /// 写入即生效并持久化；正在浮动时只改变浮动窗口的排布方向，不会自动吸附回去。
        /// </summary>
        internal QuickToggleDockSide DockSide
        {
            get => dockSide;
            set
            {
                if (disposed || dockSide == value) return;
                dockSide = value;
                ApplyDockSide();
                SaveState();
                RaiseStateChanged();
            }
        }

        /// <summary>吸附到指定位置；正在浮动时一并吸附回主窗口。</summary>
        internal void DockTo(QuickToggleDockSide side)
        {
            DockSide = side;
            if (disposed) return;
            Dock();
        }

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
        internal void ApplyStartupState(bool visible, bool floatMode, QuickToggleDockSide side)
        {
            if (disposed) return;

            dockSide = side;
            ApplyDockSide();

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

        /// <summary>
        /// 把停靠行挂进主窗口的停靠布局：由宿主在把停靠行加入 <c>Controls</c> 之后调用。
        /// 行在控件集合里的位置取决于吸附位置，见 <see cref="PlaceDockRow"/>。
        /// </summary>
        internal void ApplyDockSide()
        {
            if (disposed) return;

            dockRow.Dock = dockSide switch
            {
                QuickToggleDockSide.Left => DockStyle.Left,
                QuickToggleDockSide.Right => DockStyle.Right,
                _ => DockStyle.Top,
            };

            bar.DockSide = dockSide;
            PlaceDockRow();

            if (floating) floatForm.Refit();
            else dockRow.PerformLayout();
            owner.PerformLayout();
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
            app.Default.QuickToggle_DockSide = dockSide.ToSettingValue();
            if (floating && floatForm.Visible)
            {
                app.Default.QuickToggle_Float_X = floatForm.Location.X;
                app.Default.QuickToggle_Float_Y = floatForm.Location.Y;
            }
            app.Default.QuickToggle_States = bar.GetCheckedStates();
            app.Default.QuickToggle_HiddenGroups = bar.GetHiddenGroups();
        }

        #region 停靠 / 浮动切换

        /// <summary>
        /// 安排停靠行的位置。WinForms 按控件集合的倒序停靠控件（下标大的先被安排），
        /// 因此集合顺序直接决定了行与菜单栏 / 状态栏 / 画布的关系：
        /// <list type="bullet">
        /// <item>顶部吸附：[状态栏, 画布, 停靠行, 菜单栏] —— 与既有布局完全一致：
        /// 行先于画布被安排，正好把画布挤到菜单栏下方；</item>
        /// <item>左 / 右吸附：[画布, 停靠行, 状态栏, 菜单栏] —— 画布先让出一列宽度给行，
        /// 行夹在菜单栏与状态栏之间，画布也不会有一块被行盖住。</item>
        /// </list>
        /// </summary>
        private void PlaceDockRow()
        {
            Control.ControlCollection controls = owner.Controls;
            if (!controls.Contains(dockRow)) return;

            Control? canvas = FindFillControl();
            Control?[] order = dockSide == QuickToggleDockSide.Top
                ? new[] { bottomAnchor, canvas, dockRow, snapAnchor }
                : new[] { canvas, dockRow, bottomAnchor, snapAnchor };

            // SetChildIndex 是“先摘下来、再插到指定下标”，按目标下标从小到大逐个摆放即可
            // 得到想要的顺序（前几个位置在摆放过程中不会被后面的操作打乱）
            owner.SuspendLayout();
            try
            {
                int index = 0;
                foreach (Control? control in order)
                {
                    if (control == null || !controls.Contains(control)) continue;
                    if (controls.GetChildIndex(control) != index) controls.SetChildIndex(control, index);
                    index++;
                }
            }
            finally
            {
                owner.ResumeLayout(performLayout: false);
            }
        }

        /// <summary>找出铺满剩余区域的画布控件（<see cref="DockStyle.Fill"/>）：停靠行要相对它摆位。</summary>
        private Control? FindFillControl()
        {
            foreach (Control control in owner.Controls)
            {
                if (control == dockRow || control == snapAnchor || control == bottomAnchor) continue;
                if (control.Dock == DockStyle.Fill) return control;
            }
            return null;
        }

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
            // 先摘再挂：停靠行会在移除时把行高 / 列宽归零，加入时按条的首选尺寸重新撑开
            bar.Parent?.Controls.Remove(bar);
            parent.Controls.Add(bar);
            parent.PerformLayout();
        }

        /// <summary>浮窗的默认位置：贴着条原来在屏幕上的位置弹出来，横排弹到下方、竖排弹到侧面。</summary>
        private Point DefaultFloatLocation(Rectangle barScreenBefore)
        {
            if (barScreenBefore.IsEmpty) return floatForm.Location;

            return dockSide switch
            {
                QuickToggleDockSide.Left => new Point(barScreenBefore.Right + 4, barScreenBefore.Y),
                QuickToggleDockSide.Right => new Point(barScreenBefore.Left - floatForm.Width - 4, barScreenBefore.Y),
                _ => new Point(barScreenBefore.X, barScreenBefore.Bottom + 4),
            };
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

            UpdateHint(TryGetSnapSide(e.CursorScreen, out QuickToggleDockSide side) ? side : null);
        }

        private void Bar_DragMoved(object? sender, QuickToggleDragEventArgs e)
        {
            if (disposed || !dragging) return;

            Point location = new(
                dragOriginFormLocation.X + e.CursorScreen.X - dragOriginCursor.X,
                dragOriginFormLocation.Y + e.CursorScreen.Y - dragOriginCursor.Y);
            if (floatForm.Location != location) floatForm.Location = location;

            UpdateHint(TryGetSnapSide(e.CursorScreen, out QuickToggleDockSide side) ? side : null);
        }

        private void Bar_DragEnded(object? sender, QuickToggleDragEventArgs e)
        {
            if (disposed) return;
            dragging = false;
            UpdateHint(null);

            if (TryGetSnapSide(e.CursorScreen, out QuickToggleDockSide side))
            {
                // 拖到哪一侧就吸附到哪一侧（顶部 / 左侧 / 右侧），并顺带切换横排 / 竖排
                DockTo(side);
                return;
            }

            // 停在屏幕外会让用户找不回浮窗，松手时夹回可见区域
            floatForm.Location = ClampToScreen(floatForm.Location, floatForm.Size);
            SaveState();
        }

        /// <summary>
        /// 判断松手位置要吸附到哪一侧：拖到某个吸附区附近（或菜单栏上）即命中，
        /// 同时命中多个时取最近的一个，已经吸附着的那一侧略有优先，避免抖动时来回跳。
        /// </summary>
        private bool TryGetSnapSide(Point cursorScreen, out QuickToggleDockSide side)
        {
            side = QuickToggleDockSide.Top;

            // 菜单栏本身也算吸附区：拖到菜单栏上松手即吸附回顶部
            Control? anchor = snapAnchor;
            if (anchor != null && anchor.IsHandleCreated &&
                anchor.RectangleToScreen(anchor.ClientRectangle).Contains(cursorScreen))
            {
                return true;
            }

            int best = int.MaxValue;
            foreach (QuickToggleDockSide candidate in DockSides)
            {
                int distance = DistanceToRect(DockTargetScreenRect(candidate), cursorScreen);
                if (candidate == dockSide) distance -= SnapDistance / 2;
                if (distance > SnapDistance || distance >= best) continue;

                best = distance;
                side = candidate;
            }

            return best != int.MaxValue;
        }

        /// <summary>点到矩形的距离：在矩形内为 0，取两个方向上偏移量的较大值（靠近边缘即算命中）。</summary>
        private static int DistanceToRect(Rectangle rect, Point point)
        {
            int dx = Math.Max(Math.Max(rect.Left - point.X, 0), point.X - rect.Right);
            int dy = Math.Max(Math.Max(rect.Top - point.Y, 0), point.Y - rect.Bottom);
            return Math.Max(dx, dy);
        }

        /// <summary>
        /// 某个吸附位置对应的目标矩形（屏幕坐标）：
        /// 顶部是菜单栏正下方、与主窗口同宽的一行；左 / 右是菜单栏与状态栏之间的一个竖条。
        /// </summary>
        private Rectangle DockTargetScreenRect(QuickToggleDockSide side)
        {
            Point origin = owner.PointToScreen(Point.Empty);
            int top = origin.Y + (snapAnchor != null ? snapAnchor.Bottom : 0);
            int bottom = bottomAnchor != null && bottomAnchor.IsHandleCreated
                ? origin.Y + bottomAnchor.Top
                : origin.Y + owner.ClientSize.Height;
            int height = Math.Max(bottom - top, 1);

            if (side == QuickToggleDockSide.Top)
            {
                return new Rectangle(
                    origin.X,
                    top,
                    Math.Max(owner.ClientSize.Width, 1),
                    Math.Max(bar.PreferredThickness(vertical: false), 1));
            }

            int thickness = Math.Max(bar.PreferredThickness(vertical: true), 1);
            return side == QuickToggleDockSide.Left
                ? new Rectangle(origin.X, top, thickness, height)
                : new Rectangle(origin.X + Math.Max(owner.ClientSize.Width - thickness, 0), top, thickness, height);
        }

        /// <summary>显示 / 隐藏吸附提示：顶部是一条横线，左 / 右是一条竖线。</summary>
        private void UpdateHint(QuickToggleDockSide? side)
        {
            if (!side.HasValue)
            {
                if (hintForm.Visible) hintForm.Hide();
                return;
            }

            Rectangle target = DockTargetScreenRect(side.Value);
            Rectangle bounds = side.Value switch
            {
                QuickToggleDockSide.Left => new Rectangle(target.X, target.Y, HintHeight, Math.Max(target.Height, 1)),
                QuickToggleDockSide.Right => new Rectangle(target.Right - HintHeight, target.Y, HintHeight, Math.Max(target.Height, 1)),
                _ => new Rectangle(target.X, target.Y, Math.Max(target.Width, 1), HintHeight),
            };

            if (hintForm.Bounds != bounds) hintForm.Bounds = bounds;
            if (!hintForm.Visible) hintForm.Show();
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

        /// <summary>右键菜单里选了新的吸附位置：切过去并吸附回主窗口。</summary>
        private void Bar_DockSideRequested(object? sender, QuickToggleDockSideEventArgs e)
        {
            DockTo(e.Side);
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
            bar.DockSideRequested -= Bar_DockSideRequested;
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
