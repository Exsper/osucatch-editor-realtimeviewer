using System.Text;

namespace osucatch_editor_realtimeviewer
{
    /// <summary>
    /// 快捷开关条：类似 <see cref="ToolStrip"/> 的工具条控件。
    /// <para />它既能吸附在主窗口菜单栏下方的一行里，也能拖出来变成浮动小窗口：
    /// 按住左侧手柄（或条上的空白处）拖动即可拖出 / 拖回，双击手柄或点击右端按钮同样可以切换。
    /// <para />条上的内容按“功能区”组织：每个功能区有一段文字标题和若干按钮 / 下拉框 / 滑块，
    /// 功能区之间用分隔线隔开；在条上点右键可以在右键菜单里勾选要显示哪些功能区。
    /// </summary>
    /// <remarks>
    /// 典型用法：
    /// <code>
    /// // 不属于任何功能区、固定在最左边、始终显示的开关
    /// quickToggleBar.AddStandaloneToggle("FreezePreviewTime", "⏸️");
    ///
    /// // 功能区；标题传空字符串就只留按钮（如 MOD 区的 NM / EZ / HR）
    /// QuickToggleGroup mods = quickToggleBar.AddGroup("Mod", "");
    /// mods.AddToggle("ModNM", "NM");
    /// mods.AddToggle("ModEZ", "EZ");
    /// mods.AddToggle("ModHR", "HR");
    ///
    /// quickToggleBar.ToggleChanged += (s, e) => { /* 立即生效 */ };
    /// quickToggleBar.GroupChanged += (s, e) => { /* 下拉框/滑块切换 */ };
    /// </code>
    /// 按钮勾选状态可通过 <see cref="GetCheckedStates"/> / <see cref="ApplyCheckedStates"/> 持久化，
    /// 功能区显示状态可通过 <see cref="GetHiddenGroups"/> / <see cref="ApplyHiddenGroups"/> 持久化。
    /// </remarks>
    internal sealed class QuickToggleBar : ToolStrip
    {
        /// <summary>拖动判定阈值（屏幕像素）：小于该距离视为普通点击，不会拖出浮窗。</summary>
        private const int DragThreshold = 4;

        /// <summary>拖动轮询间隔（毫秒）。这里用轮询而不是鼠标捕获：条拖出时会被移到另一个窗体里，
        /// 重新挂父窗口可能丢失鼠标捕获，轮询鼠标物理状态更可靠（Wine 下同样可用）。</summary>
        private const int DragPollIntervalMs = 15;

        private readonly Dictionary<string, ToolStripButton> toggles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, QuickToggleGroup> groups = new(StringComparer.Ordinal);
        private readonly List<QuickToggleGroup> groupOrder = new();

        /// <summary>
        /// 不属于任何功能区、固定排在条最左边的开关（如“固定预览时刻”）。
        /// 它们不带功能区标题，永远显示、也不出现在右键菜单的功能区勾选里。
        /// </summary>
        private readonly List<ToolStripButton> standaloneToggles = new();

        /// <summary>浮窗/吸附切换按钮：手柄之外的第二种操作方式，便于发现该功能。</summary>
        private readonly ToolStripButton floatingButton = new();

        private readonly ContextMenuStrip barContextMenu = new();
        private readonly ToolStripMenuItem floatMenuItem = new();
        private readonly ToolStripMenuItem hideMenuItem = new();
        private readonly ToolStripSeparator contextMenuSeparator = new();

        private readonly System.Windows.Forms.Timer dragTimer = new() { Interval = DragPollIntervalMs };

        private Point dragStartCursor;
        private Point dragGrabOffset;
        private bool dragArmed;
        private bool dragging;
        private long lastDragAreaClickTicks;
        private Point lastDragAreaClickPoint;
        private bool suppressToggleEvents;
        private bool rebuilding;
        private string title = "Quick Toggles";

        internal QuickToggleBar()
        {
            Name = "quickToggleBar";
            GripStyle = ToolStripGripStyle.Visible;
            // 尺寸由宿主（停靠行 / 浮窗）统一管理，避免 AutoSize 与宿主布局互相打架
            AutoSize = false;
            Dock = DockStyle.None;
            TabStop = false;
            // 始终单行：窗口不够宽时放不下的项收进右端角标（Chevron）的下拉里，条本身不会变高
            CanOverflow = true;
            LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;

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
            barContextMenu.Items.Add(contextMenuSeparator);
            ContextMenuStrip = barContextMenu;

            dragTimer.Tick += DragTimer_Tick;

            ApplyLanguage();
        }

        #region 事件

        /// <summary>开关按钮被用户勾选/取消时触发（从设置恢复时不触发）。</summary>
        internal event EventHandler<QuickToggleChangedEventArgs>? ToggleChanged;

        /// <summary>功能区里的下拉框 / 滑块被用户改动时触发。</summary>
        internal event EventHandler<QuickToggleGroupChangedEventArgs>? GroupChanged;

        /// <summary>显示的功能区增删导致内容变化时触发：宿主据此重新计算停靠行高度 / 浮窗尺寸。</summary>
        internal event EventHandler? ContentChanged;

        #endregion

        #region 功能区

        /// <summary>
        /// 添加一个功能区（可选标题 + 若干按钮/控件）。功能区之间自动插入分隔线。
        /// 同一个 <paramref name="key"/> 重复调用会返回已有的功能区（便于语言切换后重设文本）。
        /// </summary>
        /// <param name="key">功能区标识，用于持久化显示状态，需保持稳定。</param>
        /// <param name="label">
        /// 功能区标题（显示在按钮之前）。传空字符串表示不加标题——
        /// 例如 MOD 区只有 NM/EZ/HR 三个按钮，用户一看便知，标题反而占地方；
        /// 此时该功能区依然出现在右键菜单里（菜单项文字回退为 <paramref name="key"/>）。
        /// </param>
        internal QuickToggleGroup AddGroup(string key, string label)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);

            if (groups.TryGetValue(key, out QuickToggleGroup? existing))
            {
                existing.SetLabel(label);
                return existing;
            }

            QuickToggleGroup group = new(this, key, label, showLabel: label.Length > 0);
            groups[key] = group;
            groupOrder.Add(group);
            RebuildItems();
            UpdateGroupMenuChecks();
            ContentChanged?.Invoke(this, EventArgs.Empty);
            return group;
        }

        /// <summary>功能区的显示状态发生变化（右键菜单勾选）时触发。</summary>
        internal event EventHandler? GroupsVisibilityChanged;

        /// <summary>功能区是否存在。</summary>
        internal bool HasGroup(string key) => groups.ContainsKey(key);

        /// <summary>功能区当前是否显示。</summary>
        internal bool IsGroupVisible(string key) => groups.TryGetValue(key, out QuickToggleGroup? group) && group.Visible;

        /// <summary>显示 / 隐藏指定功能区。</summary>
        internal void SetGroupVisible(string key, bool visible)
        {
            if (!groups.TryGetValue(key, out QuickToggleGroup? group)) return;
            if (group.Visible == visible) return;

            group.Visible = visible;
            RebuildItems();
            UpdateGroupMenuChecks();
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>把所有功能区显示状态序列化成可写入设置的字符串（只写被隐藏的功能区）。</summary>
        internal string GetHiddenGroups()
        {
            StringBuilder builder = new();
            foreach (QuickToggleGroup group in groupOrder)
            {
                if (group.Visible) continue;
                if (builder.Length > 0) builder.Append(';');
                builder.Append(group.Key);
            }
            return builder.ToString();
        }

        /// <summary>从设置字符串恢复功能区显示状态（不触发 <see cref="GroupsVisibilityChanged"/>）。</summary>
        internal void ApplyHiddenGroups(string? hidden)
        {
            HashSet<string> hiddenKeys = new(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(hidden))
            {
                foreach (string key in hidden.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    hiddenKeys.Add(key.Trim());
                }
            }

            // 没有记录过（空字符串）时保持默认：全部显示
            bool firstRun = string.IsNullOrEmpty(hidden);
            bool changed = false;
            foreach (QuickToggleGroup group in groupOrder)
            {
                bool visible = firstRun || !hiddenKeys.Contains(group.Key);
                if (group.Visible == visible) continue;
                group.Visible = visible;
                changed = true;
            }

            if (!changed) return;
            RebuildItems();
            UpdateGroupMenuChecks();
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>按固定的功能区顺序重建条上的显示项（隐藏的功能区不加入）。</summary>
        private void RebuildItems()
        {
            if (rebuilding) return;
            rebuilding = true;
            try
            {
                // 只摘掉功能区相关的项，右端“浮动/吸附”按钮始终保留。
                // 功能区之间的分隔线是本方法每次新建的，摘下来要一并释放，否则反复切换会漏句柄
                for (int i = Items.Count - 1; i >= 0; i--)
                {
                    ToolStripItem item = Items[i];
                    if (ReferenceEquals(item, floatingButton)) continue;
                    Items.RemoveAt(i);
                    if (item is ToolStripSeparator separator) separator.Dispose();
                }

                // 最左边先放不归属任何功能区的固定开关，再按顺序排各功能区
                foreach (ToolStripButton standalone in standaloneToggles) Items.Add(standalone);

                bool anyVisible = false;
                foreach (QuickToggleGroup group in groupOrder)
                {
                    if (!group.Visible) continue;
                    if (anyVisible) Items.Add(new ToolStripSeparator());
                    group.InsertItems(Items);
                    anyVisible = true;
                }
            }
            finally
            {
                rebuilding = false;
            }
        }

        #endregion

        #region 开关按钮

        /// <summary>
        /// 添加一个不属于任何功能区、固定排在条最左边的即时开关（如“固定预览时刻”）。
        /// <para />它没有功能区标题，也不会出现在右键菜单的功能区勾选里——功能区开关是用来
        /// 按需收起的，而这种独立的模式切换按钮必须一直看得见。
        /// </para>
        /// </summary>
        /// <param name="key">开关标识，用于读写状态与持久化，需保持稳定。</param>
        /// <param name="text">按钮文本。</param>
        /// <param name="isChecked">初始勾选状态。</param>
        internal ToolStripButton AddStandaloneToggle(string key, string text, bool isChecked = false)
        {
            if (toggles.TryGetValue(key, out ToolStripButton? existing)) return existing;

            ToolStripButton item = CreateToggleButton(key, text, isChecked);
            toggles[key] = item;
            standaloneToggles.Add(item);
            RebuildItems();
            ContentChanged?.Invoke(this, EventArgs.Empty);
            return item;
        }

        /// <summary>向指定功能区添加一个即时开关（勾选式按钮），返回可继续定制（图标、提示等）的按钮。</summary>
        /// <param name="key">开关标识，用于读写状态与持久化，需保持稳定。</param>
        /// <param name="groupKey">所属功能区标识。</param>
        /// <param name="text">按钮文本，后续语言资源可按 <paramref name="key"/> 覆盖。</param>
        /// <param name="isChecked">初始勾选状态。</param>
        internal ToolStripButton AddToggle(string key, string groupKey, string text, bool isChecked = false)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);

            if (toggles.TryGetValue(key, out ToolStripButton? existing)) return existing;

            ToolStripButton item = CreateToggleButton(key, text, isChecked);

            toggles[key] = item;
            AddItemToGroup(groupKey, item);
            ContentChanged?.Invoke(this, EventArgs.Empty);
            return item;
        }

        /// <summary>创建开关按钮本体（两种添加方式共用）。</summary>
        private ToolStripButton CreateToggleButton(string key, string text, bool isChecked)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);

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
                Overflow = ToolStripItemOverflow.AsNeeded,
            };
            item.CheckedChanged += (sender, e) =>
            {
                if (suppressToggleEvents) return;
                ToggleChanged?.Invoke(this, new QuickToggleChangedEventArgs(key, item.Checked));
            };
            return item;
        }

        /// <summary>移除一个开关（功能区开关或独立开关都适用）。</summary>
        internal void RemoveToggle(string key)
        {
            if (!toggles.Remove(key, out ToolStripButton? item)) return;
            RemoveItemFromGroup(item);
            standaloneToggles.Remove(item);
            RebuildItems();
            item.Dispose();
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        #region 按钮图标

        /// <summary>
        /// 按文件路径缓存的图标：同一个文件只从磁盘读一次。
        /// <para />缓存的是从文件流解码出来的 <see cref="Bitmap"/>，所有按钮共用同一个实例，
        /// 因此<b>不要</b>把返回的图片再交给会 Dispose 它的调用方。
        /// </para>
        /// </summary>
        private static readonly Dictionary<string, Image?> iconCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 设置开关按钮的图标。传 null 或文件不存在时只清空图标，按钮回退为显示文字。
        /// </summary>
        /// <param name="key">开关标识。</param>
        /// <param name="path">图标文件路径（通常是 exe 目录下的 icons\*.png）。</param>
        /// <param name="displayStyle">
        /// 设置成功时要切换到的显示方式：只显示图标（<see cref="ToolStripItemDisplayStyle.Image"/>）
        /// 或图标 + 文字。图标缺失时不变更显示方式，保证按钮不会变成空白。
        /// </param>
        internal void SetToggleImage(string key, string? path, ToolStripItemDisplayStyle displayStyle = ToolStripItemDisplayStyle.Image)
        {
            if (!toggles.TryGetValue(key, out ToolStripButton? item)) return;

            Image? image = LoadIcon(path);
            if (image == null)
            {
                item.Image = null;
                item.DisplayStyle = ToolStripItemDisplayStyle.Text;
            }
            else
            {
                item.Image = image;
                item.ImageScaling = ToolStripItemImageScaling.SizeToFit;
                item.DisplayStyle = displayStyle;
            }

            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>把功能区里所有开关按钮的显示方式统一改掉（如“果子标注”整组只显示图标）。</summary>
        internal void SetGroupTogglesDisplayStyle(string groupKey, ToolStripItemDisplayStyle displayStyle)
        {
            if (!groups.TryGetValue(groupKey, out QuickToggleGroup? group)) return;

            bool changed = false;
            foreach (ToolStripItem item in group.Items)
            {
                if (item is not ToolStripButton button) continue;
                if (button.Image == null) continue;      // 没有图标的按钮保持文字，避免变成空白
                button.DisplayStyle = displayStyle;
                changed = true;
            }

            if (changed) ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 把图标缩放到条的 <see cref="ToolStrip.ImageScalingSize"/> 大小。
        /// <para />不缩放的话，每个按钮都会按图片原始像素绘制：一旦 Windows 缩放不是 100%，
        /// 或者提供的是 24/32px 的高分屏图标，图标就会比按钮还高，把停靠行撑高。
        /// </para>
        /// </summary>
        private Image? LoadIcon(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (iconCache.TryGetValue(path, out Image? cached)) return cached;

            Image? image = null;
            if (File.Exists(path))
            {
                try
                {
                    using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using Image source = Image.FromStream(stream);
                    Size target = new(Math.Max(ImageScalingSize.Width, 1), Math.Max(ImageScalingSize.Height, 1));
                    if (source.Width == target.Width && source.Height == target.Height)
                    {
                        // 尺寸已经匹配（最常见的 16x16 @100%）：直接留一份位图副本，
                        // 避免持有文件流的解码依赖
                        image = new Bitmap(source);
                    }
                    else
                    {
                        Bitmap scaled = new(target.Width, target.Height);
                        using Graphics graphics = Graphics.FromImage(scaled);
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        graphics.DrawImage(source, 0, 0, target.Width, target.Height);
                        image = scaled;
                    }
                }
                catch (Exception ex)
                {
                    Log.ConsoleLog("Read icon file failed: " + path + "\r\n" + ex, Log.LogType.Drawing, Log.LogLevel.Warning);
                }
            }

            iconCache[path] = image;
            return image;
        }

        #endregion

        /// <summary>读取开关的勾选状态；开关不存在时返回 false。</summary>
        internal bool IsChecked(string key) => toggles.TryGetValue(key, out ToolStripButton? item) && item.Checked;

        /// <summary>
        /// 更新开关按钮的文本与提示（语言切换等场景），
        /// 并通知宿主重新计算浮窗尺寸（条自身不自动调整大小）。
        /// </summary>
        internal void SetToggleText(string key, string text, string? toolTip = null)
        {
            if (!toggles.TryGetValue(key, out ToolStripButton? item)) return;

            item.Text = text;
            item.ToolTipText = toolTip ?? text;
            item.AutoToolTip = false;
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>设置开关的勾选状态（会触发 <see cref="ToggleChanged"/>）。</summary>
        internal void SetChecked(string key, bool isChecked)
        {
            if (toggles.TryGetValue(key, out ToolStripButton? item)) item.Checked = isChecked;
        }

        /// <summary>
        /// 批量设置一批开关的勾选状态，期间不触发 <see cref="ToggleChanged"/>。
        /// 用于“一组里只能选一个”的单选式功能区（MOD、果子标注）：避免每设一个都触发一次重建。
        /// </summary>
        internal void SetCheckedSilently(IEnumerable<string> keys, string checkedKey)
        {
            bool previous = suppressToggleEvents;
            suppressToggleEvents = true;
            try
            {
                foreach (string key in keys)
                {
                    if (toggles.TryGetValue(key, out ToolStripButton? item)) item.Checked = key == checkedKey;
                }
            }
            finally
            {
                suppressToggleEvents = previous;
            }
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

        #region 内部：功能区与条上项的同步

        /// <summary>把新按钮归入指定功能区，并重建条上的显示项。</summary>
        private void AddItemToGroup(string targetGroupKey, ToolStripItem item)
        {
            if (!groups.TryGetValue(targetGroupKey, out QuickToggleGroup? group))
            {
                // 没有建功能区时退化成“不显示”：避免按钮被静默丢弃后找不到
                QuickToggleGroup fallback = AddGroup(targetGroupKey, targetGroupKey);
                fallback.AddItem(item);
                RebuildItems();
                return;
            }

            group.AddItem(item);
            RebuildItems();
        }

        private void RemoveItemFromGroup(ToolStripItem item)
        {
            foreach (QuickToggleGroup group in groupOrder) group.RemoveItem(item);
        }

        /// <summary>
        /// 功能区内部新增了控件（滑块 / 下拉框 / 文字）后，把该功能区的项重新插到条上并刷新宿主尺寸。
        /// <para />必须显式调用：功能区的项是“先攒在自己列表里、再整体插入条上”的，
        /// 少了这一步，新加的控件要等到下一次重建（例如用户在右键菜单里收起再展开该功能区）才会出现。
        /// </para>
        /// </summary>
        private void NotifyGroupItemsChanged()
        {
            RebuildItems();
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region 功能区值控件（供宿主读写）

        /// <summary>设置功能区里滑块的取值（不触发 <see cref="GroupChanged"/>）。</summary>
        internal void SetGroupSliderValue(string groupKey, string valueKey, int value)
        {
            if (groups.TryGetValue(groupKey, out QuickToggleGroup? group)) group.SetSliderValue(valueKey, value);
        }

        /// <summary>设置功能区里下拉框的选中项（不触发 <see cref="GroupChanged"/>）。</summary>
        internal void SetGroupComboIndex(string groupKey, string valueKey, int index)
        {
            if (groups.TryGetValue(groupKey, out QuickToggleGroup? group)) group.SetComboIndex(valueKey, index);
        }

        /// <summary>更新功能区里的一段只读文字。</summary>
        internal void SetGroupLabelText(string groupKey, string labelKey, string text)
        {
            if (groups.TryGetValue(groupKey, out QuickToggleGroup? group)) group.SetLabelText(labelKey, text);
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

        /// <summary>
        /// 浮窗尺寸：按内容计算，并限制在工作区之内。
        /// <para />宽度先夹到工作区，再按这个宽度问首选高度：条是换行排布的（Flow），
        /// 用一个很宽的宽度去问只会拿到“单行”的高度，窄屏上内容换行后就会被裁掉。
        /// </para>
        /// </summary>
        internal Size PreferredFloatSize()
        {
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            int width = Math.Max(96, Math.Min(GetPreferredSize(new Size(4096, 0)).Width, workingArea.Width));
            int height = Math.Max(24, Math.Min(GetPreferredSize(new Size(width, 0)).Height, workingArea.Height));
            return new Size(width, height);
        }

        /// <summary>
        /// 按当前语言刷新条上的固定文本（右端按钮、右键菜单、浮窗标题）。
        /// <para />功能区标题与按钮文本由调用方在语言切换后重新设置（见 Form1.ApplyQuickToggleLanguage）。
        /// </summary>
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
            contextMenuSeparator.Visible = groupOrder.Count > 0;

            for (int i = 0; i < groupOrder.Count; i++)
            {
                QuickToggleGroup group = groupOrder[i];
                string text = group.DisplayText;
                if (group.MenuItem != null) group.MenuItem.Text = text;
            }

            if (Parent is QuickToggleFloatForm floatForm) floatForm.Text = title;
        }

        #endregion

        #region 右键菜单：功能区勾选

        /// <summary>
        /// 把功能区显示状态同步到右键菜单的勾选项。
        /// 菜单项按功能区加入顺序排列，取消勾选即隐藏对应功能区。
        /// </summary>
        private void UpdateGroupMenuChecks()
        {
            foreach (QuickToggleGroup group in groupOrder)
            {
                if (group.MenuItem == null)
                {
                    ToolStripMenuItem item = new()
                    {
                        Name = "quickToggleGroup_" + group.Key,
                        CheckOnClick = true,
                    };
                    QuickToggleGroup captured = group;
                    item.Click += (sender, e) =>
                    {
                        // CheckOnClick 已经翻转了勾选状态，这里只负责应用
                        SetGroupVisible(captured.Key, item.Checked);
                        GroupsVisibilityChanged?.Invoke(this, EventArgs.Empty);
                    };
                    group.MenuItem = item;
                    barContextMenu.Items.Add(item);
                }

                group.MenuItem.Text = group.DisplayText;
                group.MenuItem.Checked = group.Visible;
            }
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

        /// <summary>
        /// 条上的一个功能区：可选的一段文字标题 + 若干按钮 / 下拉框 / 滑块，整体可以显示或隐藏。
        /// </summary>
        internal sealed class QuickToggleGroup
        {
            private readonly QuickToggleBar owner;
            private readonly List<ToolStripItem> items = new();
            private readonly ToolStripLabel? label;
            private readonly string fallbackText;
            private ToolStripMenuItem? menuItem;

            internal QuickToggleGroup(QuickToggleBar owner, string key, string labelText, bool showLabel)
            {
                this.owner = owner;
                Key = key;
                fallbackText = key;
                DisplayText = labelText.Length > 0 ? labelText : key;

                if (!showLabel) return;

                label = new ToolStripLabel
                {
                    Name = "quickToggleGroupLabel_" + key,
                    AutoToolTip = false,
                    DisplayStyle = ToolStripItemDisplayStyle.Text,
                    Alignment = ToolStripItemAlignment.Left,
                    Text = labelText,
                };
                items.Add(label);
            }

            /// <summary>功能区标识（持久化用的键）。</summary>
            internal string Key { get; }

            /// <summary>
            /// 功能区标题文本。没有标题的功能区（<see cref="AddGroup"/> 传空字符串）用标识兜底，
            /// 这样右键菜单里的勾选项仍然有文字可显示。
            /// </summary>
            internal string DisplayText { get; private set; }

            /// <summary>功能区当前是否显示（右键菜单可切换）。</summary>
            internal bool Visible { get; set; } = true;

            /// <summary>右键菜单里对应的勾选项，由条在同步菜单时创建。</summary>
            internal ToolStripMenuItem? MenuItem
            {
                get => menuItem;
                set => menuItem = value;
            }

            /// <summary>该功能区已加入的项（含标题与值控件）。</summary>
            internal IReadOnlyList<ToolStripItem> Items => items;

            /// <summary>设置功能区标题（语言切换时调用）。传空字符串只影响标题项，不影响右键菜单文字。</summary>
            internal void SetLabel(string text)
            {
                DisplayText = text.Length > 0 ? text : fallbackText;
                if (label != null) label.Text = text;
                if (menuItem != null) menuItem.Text = DisplayText;
            }

            /// <summary>
            /// 添加一段只读文字（如“拍线：每2拍”）。
            /// </summary>
            /// <param name="labelKey">文字标识，供 <see cref="SetLabelText"/> 更新用。</param>
            /// <param name="text">初始文字。</param>
            internal ToolStripLabel AddLabel(string labelKey, string text)
            {
                ToolStripLabel item = new()
                {
                    Name = "quickToggleValueLabel_" + labelKey,
                    Text = text,
                    AutoToolTip = false,
                    DisplayStyle = ToolStripItemDisplayStyle.Text,
                    Alignment = ToolStripItemAlignment.Left,
                    Overflow = ToolStripItemOverflow.AsNeeded,
                };
                labels[labelKey] = item;
                items.Add(item);
                owner.NotifyGroupItemsChanged();
                return item;
            }

            /// <summary>更新功能区里的一段只读文字。</summary>
            internal void SetLabelText(string labelKey, string text)
            {
                if (labels.TryGetValue(labelKey, out ToolStripLabel? item)) item.Text = text;
            }

            /// <summary>功能区里的只读文字项，键为文字标识。</summary>
            private readonly Dictionary<string, ToolStripLabel> labels = new(StringComparer.Ordinal);

            /// <summary>向功能区添加一个即时开关按钮。</summary>
            internal ToolStripButton AddToggle(string key, string text, bool isChecked = false)
                => owner.AddToggle(key, Key, text, isChecked);

            /// <summary>
            /// 添加一个“选择一个值”的下拉框（如拍线模式）。
            /// </summary>
            /// <param name="valueKey">值标识，用户改动时通过 <see cref="QuickToggleBar.GroupChanged"/> 上报。</param>
            /// <param name="optionTexts">下拉项文本，下标即取值。</param>
            /// <param name="selectedIndex">初始选中项。</param>
            internal ComboBox AddCombo(string valueKey, IReadOnlyList<string> optionTexts, int selectedIndex)
            {
                int width = 96;
                foreach (string text in optionTexts)
                {
                    width = Math.Max(width, TextRenderer.MeasureText(text, SystemFonts.DefaultFont).Width + 28);
                }
                width = Math.Min(width, 240);

                ComboBox combo = new()
                {
                    Name = "quickToggleCombo_" + valueKey,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    FlatStyle = FlatStyle.Flat,
                    IntegralHeight = false,
                    MaxDropDownItems = 16,
                    Width = width,
                    Height = 22,
                };
                foreach (string text in optionTexts) combo.Items.Add(text);
                combo.SelectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(combo.Items.Count - 1, 0));

                combo.SelectedIndexChanged += (sender, e) =>
                {
                    if (suppressedValues.Contains(valueKey) || combo.SelectedIndex < 0) return;
                    owner.GroupChanged?.Invoke(owner, new QuickToggleGroupChangedEventArgs(valueKey, combo.SelectedIndex));
                };

                ToolStripControlHost host = new(combo)
                {
                    Name = "quickToggleHost_" + valueKey,
                    AutoSize = false,
                    Width = width,
                    Height = 22,
                    Margin = new Padding(2, 2, 4, 2),
                };
                AddValueHost(host);
                ValueControls[valueKey] = combo;
                owner.NotifyGroupItemsChanged();
                return combo;
            }

            /// <summary>
            /// 添加一个滑块（如拍线密度）。
            /// </summary>
            /// <param name="valueKey">值标识，用户拖动时通过 <see cref="QuickToggleBar.GroupChanged"/> 上报。</param>
            /// <param name="minimum">最小值。</param>
            /// <param name="maximum">最大值。</param>
            /// <param name="value">初始值。</param>
            /// <param name="tickFrequency">刻度间隔（0 表示不画刻度）。</param>
            internal TrackBar AddSlider(string valueKey, int minimum, int maximum, int value, int tickFrequency)
            {
                TrackBar slider = new()
                {
                    Name = "quickToggleSlider_" + valueKey,
                    Minimum = minimum,
                    Maximum = maximum,
                    TickFrequency = Math.Max(tickFrequency, 1),
                    SmallChange = 1,
                    LargeChange = Math.Max((maximum - minimum) / 10, 1),
                    AutoSize = false,
                    Width = 130,
                    Height = 24,
                    Value = Math.Clamp(value, minimum, maximum),
                };

                slider.ValueChanged += (sender, e) =>
                {
                    if (suppressedValues.Contains(valueKey)) return;
                    owner.GroupChanged?.Invoke(owner, new QuickToggleGroupChangedEventArgs(valueKey, slider.Value));
                };

                ToolStripControlHost host = new(slider)
                {
                    Name = "quickToggleHost_" + valueKey,
                    AutoSize = false,
                    Width = slider.Width,
                    Height = slider.Height,
                    Margin = new Padding(2, 1, 4, 1),
                };
                AddValueHost(host);
                ValueControls[valueKey] = slider;
                owner.NotifyGroupItemsChanged();
                return slider;
            }

            /// <summary>功能区里由外部控制的值控件（下拉框 / 滑块），键为值标识。</summary>
            internal Dictionary<string, Control> ValueControls { get; } = new(StringComparer.Ordinal);

            /// <summary>正在被程序改写的值标识：期间的 <see cref="QuickToggleBar.GroupChanged"/> 不触发。</summary>
            private readonly HashSet<string> suppressedValues = new(StringComparer.Ordinal);

            /// <summary>
            /// 设置下拉框选中项（不触发 <see cref="QuickToggleBar.GroupChanged"/>）。
            /// </summary>
            internal void SetComboIndex(string valueKey, int index)
            {
                if (!ValueControls.TryGetValue(valueKey, out Control? control) || control is not ComboBox combo) return;
                int clamped = Math.Clamp(index, 0, Math.Max(combo.Items.Count - 1, 0));
                if (combo.SelectedIndex == clamped) return;

                suppressedValues.Add(valueKey);
                try
                {
                    combo.SelectedIndex = clamped;
                }
                finally
                {
                    suppressedValues.Remove(valueKey);
                }
            }

            /// <summary>
            /// 设置滑块位置（不触发 <see cref="QuickToggleBar.GroupChanged"/>）。
            /// </summary>
            internal void SetSliderValue(string valueKey, int value)
            {
                if (!ValueControls.TryGetValue(valueKey, out Control? control) || control is not TrackBar slider) return;
                int clamped = Math.Clamp(value, slider.Minimum, slider.Maximum);
                if (slider.Value == clamped) return;

                suppressedValues.Add(valueKey);
                try
                {
                    slider.Value = clamped;
                }
                finally
                {
                    suppressedValues.Remove(valueKey);
                }
            }

            /// <summary>读取下拉框选中项；控件不存在时返回 -1。</summary>
            internal int GetComboIndex(string valueKey)
                => ValueControls.TryGetValue(valueKey, out Control? control) && control is ComboBox combo ? combo.SelectedIndex : -1;

            /// <summary>读取滑块值；控件不存在时返回 0。</summary>
            internal int GetSliderValue(string valueKey)
                => ValueControls.TryGetValue(valueKey, out Control? control) && control is TrackBar slider ? slider.Value : 0;

            private void AddValueHost(ToolStripControlHost host)
            {
                items.Add(host);
            }

            internal void AddItem(ToolStripItem item) => items.Add(item);

            internal void RemoveItem(ToolStripItem item) => items.Remove(item);

            /// <summary>把功能区的项按顺序插入到条上（由条调用）。</summary>
            internal void InsertItems(ToolStripItemCollection target)
            {
                foreach (ToolStripItem item in items) target.Add(item);
            }
        }
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

    /// <summary>功能区里的下拉框 / 滑块变化事件参数。</summary>
    internal sealed class QuickToggleGroupChangedEventArgs : EventArgs
    {
        internal QuickToggleGroupChangedEventArgs(string key, int value)
        {
            Key = key;
            Value = value;
        }

        /// <summary>值标识（<c>AddCombo</c> / <c>AddSlider</c> 中传入的 key）。</summary>
        internal string Key { get; }

        /// <summary>新的取值（下拉框为下标，滑块为滑块值）。</summary>
        internal int Value { get; }
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
