using System.Text;

namespace osucatch_editor_realtimeviewer
{
    /// <summary>
    /// 快捷开关条：类似 <see cref="ToolStrip"/> 的工具条控件。
    /// <para />它既能吸附在主窗口菜单栏下方的一行里，也能拖出来变成浮动小窗口：
    /// 按住左侧手柄（或条上的空白处）拖动即可拖出 / 拖回，双击手柄或点击右端按钮同样可以切换。
    /// </summary>
    /// <remarks>
    /// 开关内容由调用方添加，例如：
    /// <code>
    /// quickToggleBar.AddToggle("ShowDistanceLabel", "距离标签", app.Default.Show_Distance_Helper);
    /// quickToggleBar.ToggleChanged += (s, e) => { /* 立即生效 */ };
    /// </code>
    /// 勾选状态可通过 <see cref="GetCheckedStates"/> / <see cref="ApplyCheckedStates"/> 持久化到用户设置。
    /// </remarks>
    internal sealed class QuickToggleBar : ToolStrip
    {
        /// <summary>拖动判定阈值（屏幕像素）：小于该距离视为普通点击，不会拖出浮窗。</summary>
        private const int DragThreshold = 4;

        /// <summary>拖动轮询间隔（毫秒）。这里用轮询而不是鼠标捕获：条拖出时会被移到另一个窗体里，
        /// 重新挂父窗口可能丢失鼠标捕获，轮询鼠标物理状态更可靠（Wine 下同样可用）。</summary>
        private const int DragPollIntervalMs = 15;

        private readonly Dictionary<string, ToolStripButton> toggles = new(StringComparer.Ordinal);

        /// <summary>浮窗/吸附切换按钮：手柄之外的第二种操作方式，便于发现该功能。</summary>
        private readonly ToolStripButton floatingButton = new();

        private readonly ContextMenuStrip barContextMenu = new();
        private readonly ToolStripMenuItem floatMenuItem = new();
        private readonly ToolStripMenuItem hideMenuItem = new();

        private readonly System.Windows.Forms.Timer dragTimer = new() { Interval = DragPollIntervalMs };

        private Point dragStartCursor;
        private Point dragGrabOffset;
        private bool dragArmed;
        private bool dragging;
        private long lastDragAreaClickTicks;
        private Point lastDragAreaClickPoint;
        private bool suppressToggleEvents;
        private string title = "Quick Toggles";

        internal QuickToggleBar()
        {
            Name = "quickToggleBar";
            GripStyle = ToolStripGripStyle.Visible;
            // 尺寸由宿主（停靠行 / 浮窗）统一管理，避免 AutoSize 与宿主布局互相打架
            AutoSize = false;
            Dock = DockStyle.None;
            TabStop = false;
            CanOverflow = true;

            floatingButton.Name = "quickToggleFloatingButton";
            floatingButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            floatingButton.Alignment = ToolStripItemAlignment.Right;
            // 窗口很窄时右端按钮不能被收进溢出菜单，否则没法把条吸附回来
            floatingButton.Overflow = ToolStripItemOverflow.Never;
            floatingButton.AutoToolTip = false;
            floatingButton.Click += (sender, e) => FloatingToggleRequested?.Invoke(this, EventArgs.Empty);
            Items.Add(floatingButton);

            floatMenuItem.Name = "quickToggleFloatMenuItem";
            floatMenuItem.Click += (sender, e) => FloatingToggleRequested?.Invoke(this, EventArgs.Empty);
            hideMenuItem.Name = "quickToggleHideMenuItem";
            hideMenuItem.Click += (sender, e) => HideRequested?.Invoke(this, EventArgs.Empty);
            barContextMenu.Items.Add(floatMenuItem);
            barContextMenu.Items.Add(hideMenuItem);
            ContextMenuStrip = barContextMenu;

            dragTimer.Tick += DragTimer_Tick;

            ApplyLanguage();
        }

        #region 开关内容

        /// <summary>开关被用户勾选/取消时触发（从设置恢复时不触发）。</summary>
        internal event EventHandler<QuickToggleChangedEventArgs>? ToggleChanged;

        /// <summary>开关增删导致内容变化时触发：宿主据此重新计算停靠行高度 / 浮窗尺寸。</summary>
        internal event EventHandler? ContentChanged;

        /// <summary>向条上添加一个即时开关，返回可继续定制（图标、提示等）的按钮。</summary>
        /// <param name="key">开关标识，用于读写状态与持久化，需保持稳定。</param>
        /// <param name="text">按钮文本，后续语言资源可按 <paramref name="key"/> 覆盖。</param>
        /// <param name="isChecked">初始勾选状态。</param>
        internal ToolStripButton AddToggle(string key, string text, bool isChecked = false)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);

            if (toggles.TryGetValue(key, out ToolStripButton? existing)) return existing;

            ToolStripButton item = new()
            {
                Name = key,
                Text = text,
                ToolTipText = text,
                AutoToolTip = false,
                CheckOnClick = true,
                Checked = isChecked,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Alignment = ToolStripItemAlignment.Left,
            };
            item.CheckedChanged += (sender, e) =>
            {
                if (suppressToggleEvents) return;
                ToggleChanged?.Invoke(this, new QuickToggleChangedEventArgs(key, item.Checked));
            };

            toggles[key] = item;
            Items.Add(item);
            ContentChanged?.Invoke(this, EventArgs.Empty);
            return item;
        }

        /// <summary>移除一个开关。</summary>
        internal void RemoveToggle(string key)
        {
            if (!toggles.Remove(key, out ToolStripButton? item)) return;
            Items.Remove(item);
            item.Dispose();
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>读取开关的勾选状态；开关不存在时返回 false。</summary>
        internal bool IsChecked(string key) => toggles.TryGetValue(key, out ToolStripButton? item) && item.Checked;

        /// <summary>设置开关的勾选状态（会触发 <see cref="ToggleChanged"/>）。</summary>
        internal void SetChecked(string key, bool isChecked)
        {
            if (toggles.TryGetValue(key, out ToolStripButton? item)) item.Checked = isChecked;
        }

        /// <summary>把所有开关状态序列化成可写入设置的字符串。</summary>
        internal string GetCheckedStates()
        {
            StringBuilder builder = new();
            foreach (KeyValuePair<string, ToolStripButton> pair in toggles)
            {
                if (builder.Length > 0) builder.Append(';');
                builder.Append(pair.Key).Append('=').Append(pair.Value.Checked ? "True" : "False");
            }
            return builder.ToString();
        }

        /// <summary>从设置字符串恢复开关状态（不触发 <see cref="ToggleChanged"/>，避免启动时重复应用）。</summary>
        internal void ApplyCheckedStates(string? states)
        {
            if (string.IsNullOrEmpty(states)) return;

            suppressToggleEvents = true;
            try
            {
                foreach (string entry in states.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    int separator = entry.IndexOf('=');
                    if (separator <= 0) continue;

                    string key = entry[..separator].Trim();
                    string value = entry[(separator + 1)..].Trim();
                    if (toggles.TryGetValue(key, out ToolStripButton? item) && bool.TryParse(value, out bool isChecked))
                    {
                        item.Checked = isChecked;
                    }
                }
            }
            finally
            {
                suppressToggleEvents = false;
            }
        }

        #endregion

        #region 停靠 / 浮动

        /// <summary>请求把条拖出为浮窗 / 吸附回工具栏（双击手柄、点击右端按钮或右键菜单）。</summary>
        internal event EventHandler? FloatingToggleRequested;

        /// <summary>请求隐藏整个快捷开关栏。</summary>
        internal event EventHandler? HideRequested;

        /// <summary>开始拖动（需要弹出为浮窗）时触发。</summary>
        internal event EventHandler<QuickToggleDragEventArgs>? DragStarted;

        /// <summary>拖动过程中触发。</summary>
        internal event EventHandler<QuickToggleDragEventArgs>? DragMoved;

        /// <summary>拖动结束（鼠标左键抬起）时触发。</summary>
        internal event EventHandler<QuickToggleDragEventArgs>? DragEnded;

        /// <summary>当前是否处于浮动小窗口状态，由停靠管理器维护。</summary>
        internal bool IsFloating { get; set; }

        /// <summary>浮窗标题 / 提示用的名称。</summary>
        internal string BarTitle => title;

        /// <summary>浮窗尺寸：按内容计算，并限制在工作区之内。</summary>
        internal Size PreferredFloatSize()
        {
            Size preferred = GetPreferredSize(new Size(4096, 0));
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            return new Size(
                Math.Max(96, Math.Min(preferred.Width, workingArea.Width)),
                Math.Max(24, Math.Min(preferred.Height, workingArea.Height)));
        }

        /// <summary>按当前语言刷新条上的固定文本（按钮、右键菜单、浮窗标题）。</summary>
        internal void ApplyLanguage()
        {
            bool chinese = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "zh";

            title = chinese ? "快捷开关栏" : "Quick Toggle Bar";
            floatingButton.Text = IsFloating ? (chinese ? "吸附" : "Dock") : (chinese ? "浮动" : "Float");
            floatingButton.ToolTipText = chinese
                ? "浮动为小窗口 / 吸附回工具栏（也可拖动左侧手柄或双击）"
                : "Float as a window / dock back to the toolbar (or drag the grip)";
            floatMenuItem.Text = IsFloating
                ? (chinese ? "吸附到工具栏" : "Dock to toolbar")
                : (chinese ? "浮动为小窗口" : "Float as window");
            hideMenuItem.Text = chinese ? "隐藏快捷开关栏" : "Hide quick toggle bar";

            if (Parent is QuickToggleFloatForm floatForm) floatForm.Text = title;
        }

        #endregion

        #region 拖出手柄

        /// <summary>手柄或空白处都算拖动区：手柄太窄时用户也能轻松把条拖出来。</summary>
        private bool IsDragArea(Point location)
        {
            if (GripStyle == ToolStripGripStyle.Visible && GripRectangle.Contains(location)) return true;
            return GetItemAt(location.X, location.Y) == null;
        }

        protected override void OnMouseDown(MouseEventArgs mea)
        {
            base.OnMouseDown(mea);

            if (mea.Button != MouseButtons.Left || !IsDragArea(mea.Location)) return;

            long now = DateTime.UtcNow.Ticks;
            long doubleClickTicks = TimeSpan.FromMilliseconds(SystemInformation.DoubleClickTime).Ticks;
            Size doubleClickSize = SystemInformation.DoubleClickSize;
            bool isDoubleClick = now - lastDragAreaClickTicks <= doubleClickTicks
                && Math.Abs(mea.X - lastDragAreaClickPoint.X) <= doubleClickSize.Width
                && Math.Abs(mea.Y - lastDragAreaClickPoint.Y) <= doubleClickSize.Height;

            lastDragAreaClickTicks = now;
            lastDragAreaClickPoint = mea.Location;

            if (isDoubleClick)
            {
                // 双击手柄：停靠 <-> 浮动
                lastDragAreaClickTicks = 0;
                FloatingToggleRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            dragArmed = true;
            dragging = false;
            dragStartCursor = Cursor.Position;
            Point barScreen = PointToScreen(Point.Empty);
            dragGrabOffset = new Point(dragStartCursor.X - barScreen.X, dragStartCursor.Y - barScreen.Y);
            dragTimer.Start();
        }

        private void DragTimer_Tick(object? sender, EventArgs e)
        {
            if (!dragArmed && !dragging)
            {
                dragTimer.Stop();
                return;
            }

            if ((Control.MouseButtons & MouseButtons.Left) == 0)
            {
                EndDragSession();
                return;
            }

            Point cursor = Cursor.Position;
            if (!dragging)
            {
                if (Math.Abs(cursor.X - dragStartCursor.X) < DragThreshold &&
                    Math.Abs(cursor.Y - dragStartCursor.Y) < DragThreshold)
                {
                    return;
                }

                dragging = true;
                DragStarted?.Invoke(this, new QuickToggleDragEventArgs(cursor, dragGrabOffset));
            }

            DragMoved?.Invoke(this, new QuickToggleDragEventArgs(cursor, dragGrabOffset));
        }

        private void EndDragSession()
        {
            dragTimer.Stop();
            bool wasDragging = dragging;
            dragging = false;
            dragArmed = false;
            if (wasDragging) DragEnded?.Invoke(this, new QuickToggleDragEventArgs(Cursor.Position, dragGrabOffset));
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            // 条被移到浮窗里时可能丢失捕获：这里只做兜底，正常结束依赖上面的轮询
            if (!dragging && !dragArmed) return;
            if ((Control.MouseButtons & MouseButtons.Left) == 0) EndDragSession();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                dragTimer.Stop();
                dragTimer.Dispose();
                barContextMenu.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>开关状态变化事件参数。</summary>
    internal sealed class QuickToggleChangedEventArgs : EventArgs
    {
        internal QuickToggleChangedEventArgs(string key, bool isChecked)
        {
            Key = key;
            IsChecked = isChecked;
        }

        /// <summary>开关标识（<see cref="QuickToggleBar.AddToggle"/> 中传入的 key）。</summary>
        internal string Key { get; }

        /// <summary>新的勾选状态。</summary>
        internal bool IsChecked { get; }
    }

    /// <summary>拖动事件参数，坐标均为屏幕坐标。</summary>
    internal sealed class QuickToggleDragEventArgs : EventArgs
    {
        internal QuickToggleDragEventArgs(Point cursorScreen, Point grabOffset)
        {
            CursorScreen = cursorScreen;
            GrabOffset = grabOffset;
        }

        /// <summary>当前鼠标位置（屏幕坐标）。</summary>
        internal Point CursorScreen { get; }

        /// <summary>鼠标相对条左上角的偏移（屏幕坐标差值），拖出浮窗时用它保持鼠标抓住的位置不变。</summary>
        internal Point GrabOffset { get; }
    }
}
