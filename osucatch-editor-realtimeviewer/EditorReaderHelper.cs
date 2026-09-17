using Editor_Reader;

namespace osucatch_editor_realtimeviewer
{
    using System.Diagnostics;

    public class EditorReaderHelper
    {
        private readonly EditorReader reader = new();

        /// <summary>
        /// 编辑器读取状态：区分"编辑器本来就没打开"（正常空闲，例如 test mode / 选歌）
        /// 与"在编辑器里但读不到"（异常），调用方据此决定是安静等待还是记录错误。
        /// </summary>
        public enum EditorState
        {
            Unknown,
            /// <summary>已成功绑定编辑器，数据可读。</summary>
            Active,
            /// <summary>osu! 在运行，但当前不在编辑器（test mode / 选歌 / 加载中）。</summary>
            NotInEditor,
            /// <summary>当前在编辑器里，但绑定或读取失败。</summary>
            Failed,
        }

        /// <summary>最近一次编辑器检查的结果，供调用方区分空闲与故障。</summary>
        public EditorState LastEditorState { get; private set; } = EditorState.Unknown;

        private bool Is_Doing_SetProcess = false;
        private bool Is_Osu_Running = false;
        public bool Is_Editor_Running = false;

        private int fetchEditor_Failed_Count = 0;
        private const int FetchEditor_MaxRetry_Count = 10;

        public string beatmap_path = "";
        public string beatmap_title = "";

        private int fetchAll_Failed_Count = 0;
        private const int FetchAll_MaxRetry_Count = 10;

        // 连续读取失败后的指数退避区间：编辑器批量修改（拖拽/撤销/加载）期间会连续读到
        // 不一致的快照，此时按最高频率反复重读既无意义又会拖慢 osu!。
        private const long FetchAll_RetryBaseIntervalMs = 200;
        private const long FetchAll_RetryMaxIntervalMs = 3000;

        // 高频/低频分离：多数 tick 只读 EditorTime，全量读取有间隔限制
        private BeatmapInfoCollection? cachedCollection;
        private string cachedTitle = "";
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();
        private long lastEditorCheckTimestamp;
        private long lastEditorFailedTimestamp;
        private long lastFullFetchTimestamp;
        private long lastFullFetchAttemptTimestamp;
        private bool editorCheckSucceeded;
        /// <summary>
        /// FetchEditor 失败后的最小重试间隔：避免以 Drawing_Interval（默认 20ms）级别的
        /// 高频循环反复做编辑器校验。真正的重扫由 <see cref="StartBackgroundRebind"/>
        /// 自己的退避（2 秒起）控制，因此这里可以放短一些，让 test mode 退出后尽快恢复。
        /// </summary>
        private const long FetchEditor_RetryIntervalMs = 250;
        /// <summary>
        /// 全量（物件数据）读取间隔：编辑器在前台且鼠标正在移动时用 FullRead_Interval（默认 20ms，
        /// 保持作图期间的跟随手感），其它情况用 LowFreqRead_Interval（默认 100ms）。
        /// <para />注意：拖动/放置物件时，画面里唯一的动态内容就是"物件跟着鼠标走"，而物件当前位置
        /// 只能从编辑器内存读到，因此这个间隔直接等于跟手的帧率（20ms ≈ 50fps，50ms 会看出明显卡顿）。
        /// 不要把默认值调大来"省读取"；真要降低读取量，应该只对正在拖动/选中的物件做增量读取，
        /// 而不是降低整个谱面的重读频率。
        /// </summary>
        private static long FullCheckIntervalMs => Math.Clamp(app.Default.FullRead_Interval, 5, 10000);
        private static long LowFreqCheckIntervalMs => Math.Clamp(app.Default.LowFreqRead_Interval, 5, 10000);

        /// <summary>
        /// 后台重绑是否正在进行。
        /// <para />内存扫描最长可能耗时 ScanTimeoutMs（默认 30 秒），绝不能在绘制循环上同步执行，
        /// 否则读取失败期间画面会整段停住（issue 里的"卡死"）。扫描期间循环只绘制上一份数据。
        /// </summary>
        public bool IsRebinding => rebinding;

        private volatile bool rebinding;
        private int rebindAttemptCount;
        private long nextRebindAllowedTimestamp;

        /// <summary>重绑失败后的最小间隔，逐次翻倍至上限，避免不断重复整轮内存扫描。</summary>
        private const int Rebind_MinIntervalMs = 2000;
        private const int Rebind_MaxIntervalMs = 60000;

        public EditorReaderHelper()
        {
            reader.autoDeStack = true;
        }

        /// <summary>
        /// 在后台线程重新绑定 osu! 进程与编辑器地址（必要时重扫整个地址空间）。
        /// 扫描期间 <see cref="IsRebinding"/> 为 true，调用方应跳过所有读取、继续绘制上一份数据。
        /// </summary>
        private void StartBackgroundRebind(string reason)
        {
            if (rebinding) return;

            long now = stopwatch.ElapsedMilliseconds;
            if (now < nextRebindAllowedTimestamp) return;

            rebindAttemptCount++;
            nextRebindAllowedTimestamp = now + Math.Min((long)Rebind_MinIntervalMs << Math.Min(rebindAttemptCount - 1, 8), Rebind_MaxIntervalMs);
            rebinding = true;

            Log.ConsoleLog("Start background editor re-bind (" + reason + "), attempt " + rebindAttemptCount + ".", Log.LogType.EditorReader, Log.LogLevel.Info);

            Task.Run(() =>
            {
                try
                {
                    if (reader.ProcessNeedsReload())
                    {
                        reader.SetProcess();
                        Is_Osu_Running = true;
                    }

                    // 强制重扫：清掉旧的编辑器地址与扫描退避/游标状态
                    reader.ResetEditor();
                    reader.FetchEditor();
                    Log.ConsoleLog("Background editor re-bind succeeded.", Log.LogType.EditorReader, Log.LogLevel.Info);
                }
                catch (Exception ex)
                {
                    Log.ConsoleLog("Background editor re-bind failed.\r\n" + ex, Log.LogType.EditorReader, Log.LogLevel.Error);
                }
                finally
                {
                    rebinding = false;
                }
            });
        }

        /// <summary>
        /// 强制丢弃当前的进程/编辑器绑定与所有缓存，下一个 tick 会重新查找 osu! 进程并重新扫描编辑器地址。
        /// 供"Reset"菜单与"连续读取失败"路径使用：此前的 Reset 只是新建 Helper，
        /// 而 EditorReader 是静态实例，编辑器地址与扫描状态都没被清掉，卡住时按 Reset 等于没按。
        /// </summary>
        /// <param name="resetRebindBackoff">手动 Reset 时传 true：立即允许下一次重扫，
        /// 而不是等内部退避（最长 60 秒）过去。</param>
        public void ForceRebind(bool resetRebindBackoff = false)
        {
            Is_Osu_Running = false;
            Is_Doing_SetProcess = false;
            Is_Editor_Running = false;
            editorCheckSucceeded = false;
            LastEditorState = EditorState.Unknown;

            fetchEditor_Failed_Count = 0;
            fetchAll_Failed_Count = 0;
            cachedCollection = null;
            cachedTitle = "";
            lastEditorCheckTimestamp = 0;
            lastEditorFailedTimestamp = 0;
            lastFullFetchTimestamp = 0;
            lastFullFetchAttemptTimestamp = 0;

            if (resetRebindBackoff)
            {
                rebindAttemptCount = 0;
                nextRebindAllowedTimestamp = 0;
            }

            try
            {
                reader.ResetEditor();
            }
            catch (Exception ex)
            {
                Log.ConsoleLog("Reset editor address failed.\r\n" + ex, Log.LogType.EditorReader, Log.LogLevel.Error);
            }

            Log.ConsoleLog("Editor binding was reset manually.", Log.LogType.EditorReader, Log.LogLevel.Info);
        }

        /// <summary>
        /// 当前连接的 osu! 进程 ID。
        /// </summary>
        public int? OsuProcessId => reader.OsuProcessId;

        /// <summary>
        /// Fetch osu! process if needed for Editor Reader.
        /// </summary>
        /// <returns>Is success or not.</returns>
        public bool FetchProcess()
        {
            // 后台重绑正在操作 EditorReader：此时不要在循环线程上碰同一个实例
            if (rebinding)
            {
                Is_Editor_Running = false;
                return false;
            }

            bool isNeedReload = true;
            try
            {
                isNeedReload = reader.ProcessNeedsReload();
            }
            catch (Exception ex)
            {
                Log.ConsoleLog("Get osu! process failed.\r\n" + ex, Log.LogType.EditorReader, Log.LogLevel.Error);
            }

            if (!Is_Osu_Running || isNeedReload)
            {
                try
                {
                    Log.ConsoleLog("Osu! process needs refetch.", Log.LogType.EditorReader, Log.LogLevel.Info);
                    if (Is_Doing_SetProcess)
                    {
                        Log.ConsoleLog("Still fetching osu!.", Log.LogType.EditorReader, Log.LogLevel.Info);
                        return false;
                    }
                    Log.ConsoleLog("Try to fetch osu! process.", Log.LogType.EditorReader, Log.LogLevel.Info);
                    Is_Doing_SetProcess = true;
                    reader.SetProcess();
                    Is_Doing_SetProcess = false;
                    Log.ConsoleLog("Fetch osu! process successfully.", Log.LogType.EditorReader, Log.LogLevel.Info);
                    Is_Osu_Running = true;
                }
                catch (Exception ex)
                {
                    Log.ConsoleLog("No Osu!.exe found.", Log.LogType.EditorReader, Log.LogLevel.Info);
                    Log.ConsoleLog(ex.ToString(), Log.LogType.EditorReader, Log.LogLevel.Debug);
                    Is_Doing_SetProcess = false;
                    Is_Osu_Running = false;
                    Is_Editor_Running = false;
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Fetch osu! window's title from Editor Reader.
        /// </summary>
        /// <returns>osu! window's title.
        /// <para />"" if failed.</returns>
        public string FetchTitle()
        {
            try
            {
                string title = reader.ProcessTitle();
                return title;
            }
            catch (Exception ex)
            {
                Log.ConsoleLog("Get osu! title failed.\r\n" + ex, Log.LogType.EditorReader, Log.LogLevel.Error);
                Is_Osu_Running = false;
                return "";
            }
        }

        /// <summary>
        /// Fetch editor if needed for Editor Reader.
        /// </summary>
        /// <param name="force">true 时不再相信"地址看起来还有效"的廉价校验，直接重新绑定。
        /// 读取连续失败时必须用它，否则会被成功缓存吞掉——这正是此前"连败后重绑"无效的原因。</param>
        /// <returns>Is success or not.</returns>
        public bool FetchEditor(bool force = false)
        {
            // 后台重绑进行中：本轮不做任何读取（扫描可能长达数十秒）
            if (rebinding)
            {
                Is_Editor_Running = false;
                beatmap_path = "";
                LastEditorState = EditorState.Unknown;
                return false;
            }

            long elapsed = stopwatch.ElapsedMilliseconds;

            // 高频路径：FullCheckIntervalMs 内已经完整验证过 editor，直接复用上次结果
            if (!force && editorCheckSucceeded && elapsed - lastEditorCheckTimestamp < FullCheckIntervalMs)
            {
                Is_Editor_Running = true;
                LastEditorState = EditorState.Active;
                return true;
            }

            // 失败节流：上次检测失败后至少等 FetchEditor_RetryIntervalMs 再做下一次校验（0 表示还没检测过）
            if (!force && !editorCheckSucceeded && lastEditorFailedTimestamp != 0 &&
                elapsed - lastEditorFailedTimestamp < FetchEditor_RetryIntervalMs)
            {
                Is_Editor_Running = false;
                beatmap_path = "";
                return false;
            }

            lastEditorFailedTimestamp = elapsed;
            beatmap_title = "";
            string title = FetchTitle();
            if (title == "")
            {
                // osu! 没运行/窗口还没建好：属正常空闲，不是故障
                Log.ConsoleLog("Empty osu title.", Log.LogType.EditorReader, Log.LogLevel.Info);
                SetNotInEditor();
                return false;
            }
            if (!title.EndsWith(".osu"))
            {
                // test mode / 选歌 / 加载中：此刻本来就无法读取，属正常空闲，不是故障
                Log.ConsoleLog("Osu title is not editor: " + title, Log.LogType.EditorReader, Log.LogLevel.Info);
                SetNotInEditor();
                return false;
            }
            try
            {
                if (force || reader.EditorNeedsReload())
                {
                    Log.ConsoleLog(force ? "Editor binding is suspect, re-binding." : "Editor needs Reload.", Log.LogType.EditorReader, Log.LogLevel.Info);
                    Is_Editor_Running = false;
                    beatmap_path = "";
                    editorCheckSucceeded = false;
                    LastEditorState = EditorState.Unknown;

                    // 扫描放到后台线程：这一步最长可达 30 秒，同步执行会让画面整段停住
                    StartBackgroundRebind(force ? "read failures" : "editor address invalid");
                    return false;
                }
            }
            catch (Exception ex)
            {
                // 廉价校验本身失败（例如地址已不可读）：同样交给后台重绑，而不是在循环里扫内存
                Log.ConsoleLog("Fetch editor failed.\r\n" + ex, Log.LogType.EditorReader, Log.LogLevel.Error);
                Is_Editor_Running = false;
                beatmap_path = "";
                editorCheckSucceeded = false;
                LastEditorState = EditorState.Failed;

                fetchEditor_Failed_Count++;
                if (fetchEditor_Failed_Count > FetchEditor_MaxRetry_Count)
                {
                    Log.ConsoleLog("Refetching osu! process and editor...", Log.LogType.EditorReader, Log.LogLevel.Warning);
                    fetchEditor_Failed_Count = 0;
                    ForceRebind();
                }
                else
                {
                    StartBackgroundRebind("validation failed");
                }
                return false;
            }
            Is_Editor_Running = true;
            beatmap_title = title;
            lastEditorCheckTimestamp = stopwatch.ElapsedMilliseconds;
            editorCheckSucceeded = true;
            fetchEditor_Failed_Count = 0;
            rebindAttemptCount = 0;
            nextRebindAllowedTimestamp = 0;
            LastEditorState = EditorState.Active;
            return true;
        }

        /// <summary>
        /// 标记"当前不在编辑器"：清掉成功缓存，退出 test mode 后下一次检查会重新做真实校验。
        /// </summary>
        private void SetNotInEditor()
        {
            Is_Editor_Running = false;
            beatmap_path = "";
            editorCheckSucceeded = false;
            LastEditorState = EditorState.NotInEditor;
        }

        /// <summary>
        /// Call Editor Reader's FetchAll().
        /// </summary>
        /// <returns>An object with editor reader's primary data.
        /// <para />null if failed.</returns>
        public BeatmapInfoCollection? FetchAll()
        {
            return FetchWithCache(false, 0);
        }

        /// <summary>
        /// Call Editor Reader's FetchAll() and filter nearby hitobjects. Should disable backup.
        /// </summary>
        /// <param name="partialLoadingHalfTimeSpan">The half time span at reader time for filter hitobjects.
        /// <para />Warning: Cause RANDOM ERROR when using it. Should disable backup.</param>
        /// <returns>An object with editor reader's primary data.
        /// <para />null if failed.</returns>
        public BeatmapInfoCollection? FetchAll(double partialLoadingHalfTimeSpan)
        {
            return FetchWithCache(true, partialLoadingHalfTimeSpan);
        }

        /// <summary>
        /// 高频/低频分离读取：多数 tick 只读 EditorTime 并复用缓存数据，
        /// 只有在编辑器/地图变化、物件数量变化或超过 FullCheckIntervalMs 时才做全量读取。
        /// <para />读取失败不抛异常，只返回 null：调用方应继续用上一份有效数据绘制。
        /// </summary>
        private BeatmapInfoCollection? FetchWithCache(bool filterNearby, double partialLoadingHalfTimeSpan)
        {
            try
            {
                // 后台重绑正在操作 EditorReader：本轮不读，调用方继续绘制上一份数据
                if (rebinding) return null;

                long now = stopwatch.ElapsedMilliseconds;

                // 连续失败退避：这一轮直接不读，调用方继续绘制上一份有效数据
                if (fetchAll_Failed_Count > 0 && now - lastFullFetchAttemptTimestamp < FetchAllBackoffMs(fetchAll_Failed_Count))
                {
                    return null;
                }

                if (fetchAll_Failed_Count > FetchAll_MaxRetry_Count)
                {
                    // 连续读不到：强制重绑编辑器地址（忽略成功缓存），并丢弃缓存数据
                    Log.ConsoleLog("Refetching editor...", Log.LogType.EditorReader, Log.LogLevel.Warning);
                    fetchAll_Failed_Count = 0;
                    cachedCollection = null;
                    FetchEditor(true);
                    return null;
                }

                // 只有"绑定被认为是有效的"才允许走只读播放头的高频路径，
                // 否则会对着已失效的地址反复读 EditorTime
                if (!IsFullFetchDue() && editorCheckSucceeded)
                {
                    // 高频路径：只刷新播放头时间，其余数据沿用上次全量读取
                    cachedCollection!.EditorTime = reader.EditorTime();
                    cachedCollection.IsFreshFetch = false;
                    if (app.Default.Selected_Show) RefreshSelection(cachedCollection);
                    fetchAll_Failed_Count = 0;
                    return cachedCollection;
                }

                lastFullFetchAttemptTimestamp = now;
                Log.ConsoleLog("Start FetchAll().", Log.LogType.EditorReader, Log.LogLevel.Debug);
                bool needFetchFull = app.Default.Backup_Enabled && !filterNearby;
                reader.FetchAll(needFetchFull);
                BeatmapInfoCollection thisReaderData = filterNearby
                    ? new BeatmapInfoCollection(reader, partialLoadingHalfTimeSpan)
                    : new BeatmapInfoCollection(reader);

                Log.ConsoleLog("FetchAll complete.", Log.LogType.EditorReader, Log.LogLevel.Debug);

                cachedCollection = thisReaderData;
                cachedTitle = beatmap_title;
                lastFullFetchTimestamp = stopwatch.ElapsedMilliseconds;
                fetchAll_Failed_Count = 0;
                thisReaderData.IsFreshFetch = true;
                if (app.Default.Selected_Show) RefreshSelection(cachedCollection);
                return thisReaderData;
            }
            catch (Exception ex)
            {
                Log.ConsoleLog("FetchAll failed.(" + fetchAll_Failed_Count + ")\r\n" + ex.ToString(), Log.LogType.EditorReader, Log.LogLevel.Error);
                fetchAll_Failed_Count++;
                lastFullFetchAttemptTimestamp = stopwatch.ElapsedMilliseconds;

                // 读失败说明缓存的 editor 地址可能已经失效：让下一次 FetchEditor 做真实校验，
                // 而不是继续复用"上次校验成功"的缓存结果（否则重绑永远被缓存吞掉）
                editorCheckSucceeded = false;
                return null;
            }
        }

        /// <summary>
        /// 连续失败后的重试退避：200ms 起，逐次翻倍，上限 3 秒。
        /// 编辑器批量修改（拖拽/撤销/加载地图）时会连续读到不一致快照，退避可避免
        /// 以最高频率反复失败、把 osu! 和自身都拖慢。
        /// </summary>
        private static long FetchAllBackoffMs(int failedCount)
        {
            long backoff = FetchAll_RetryBaseIntervalMs << Math.Min(Math.Max(failedCount - 1, 0), 8);
            return Math.Min(backoff, FetchAll_RetryMaxIntervalMs);
        }

        /// <summary>
        /// 轻量刷新选中态：只读编辑器当前的选中列表（1~2 次 ReadProcessMemory），
        /// 原位更新 <see cref="BeatmapInfoCollection.HitObjectLines"/> 中每行的选中标志，供绘制实时查表。
        /// 读取失败时沿用旧状态，避免闪烁。
        /// </summary>
        private void RefreshSelection(BeatmapInfoCollection collection)
        {
            if (!reader.TryReadSelectedIndices(out int[] selectedIndices))
            {
                return;
            }

            if (selectionScratch.Length != collection.NumObjects)
            {
                selectionScratch = new bool[collection.NumObjects];
            }

            Array.Fill(selectionScratch, false);
            foreach (int index in selectedIndices)
            {
                if (index >= 0 && index < selectionScratch.Length)
                {
                    selectionScratch[index] = true;
                }
            }

            foreach (ReaderHitObjectWithSelect line in collection.SelectionLines)
            {
                line.IsSelect = line.MasterIndex >= 0 && line.MasterIndex < selectionScratch.Length && selectionScratch[line.MasterIndex];
            }
        }

        private bool[] selectionScratch = Array.Empty<bool>();

        private bool IsFullFetchDue()
        {
            if (cachedCollection == null) return true;
            if (cachedTitle != beatmap_title) return true;

            // 编辑器前台且鼠标正在移动时用 FullRead_Interval（作图期间保持跟随手感），
            // 其它情况（不在前台 / 鼠标静止）用 LowFreqRead_Interval。
            long interval = (ProcessFocus.IsEditorForeground(reader.OsuProcessId) && ProcessFocus.IsMouseMoving())
                ? FullCheckIntervalMs : LowFreqCheckIntervalMs;
            if (stopwatch.ElapsedMilliseconds - lastFullFetchTimestamp >= interval) return true;

            // 物件/控制点数量变化（增删）立即触发全量读取，不必等间隔
            if (reader.TryReadCounts(out int numObjects, out int numControlPoints, out _) &&
                (numObjects != cachedCollection.NumObjects || numControlPoints != cachedCollection.NumControlPoints))
            {
                return true;
            }
            return false;
        }
    }

    public class BeatmapInfoCollection
    {
        public bool IsFull;
        /// <summary>
        /// 本次 Fetch 是否为全量新数据。高频路径（数据未变）为 false，
        /// 供调用方跳过昂贵的差异比较与重建判断。
        /// </summary>
        public bool IsFreshFetch;

        public int NumControlPoints;
        public int NumObjects;
        public int EditorTime;
        public string ContainingFolder;
        public string Filename;
        public int PreviewTime;
        public float StackLeniency;
        public float HPDrainRate;
        public float CircleSize;
        public float OverallDifficulty;
        public float ApproachRate;
        public double SliderMultiplier;
        public double SliderTickRate;
        public int BeatmapVersion;
        public int[] Bookmarks;
        public List<string> ControlPointLines;
        private List<ReaderHitObjectWithSelect>? hitObjectLines;

        /// <summary>
        /// 每个物件一行 .osu 文本 + 选中标志。
        /// <para /><b>惰性构建</b>：这些字符串只在"重建谱面"（<see cref="BeatmapBuilder.BuildNewBeatmapWithColorString"/>）
        /// 与备份时才被消费；绘制路径只用 <see cref="ReaderHitObjectWithSelect.IsSelect"/>。
        /// 而 <c>ho.ToString()</c> 在 16000 物件的谱面上要 ~33ms —— 每次全量读取都构建它就是白白花掉的时间，
        /// 所以这里改成首次访问时才生成（并缓存）。
        /// <para />注意：访问前请确认 <c>thisReader=</c> 已赋值，且不要再往 <see cref="HitObjects"/> 里追加物件。
        /// </summary>
        public List<ReaderHitObjectWithSelect> HitObjectLines
        {
            get
            {
                if (hitObjectLines == null)
                {
                    var lines = new List<ReaderHitObjectWithSelect>(HitObjects.Count);
                    for (int i = 0; i < HitObjects.Count; i++)
                    {
                        Editor_Reader.HitObject ho = HitObjects[i];
                        lines.Add(new ReaderHitObjectWithSelect(ho.ToString(), ho.IsSelected, i));
                    }
                    hitObjectLines = lines;
                }
                return hitObjectLines;
            }
            set => hitObjectLines = value;
        }

        /// <summary>
        /// 显式生成每行 .osu 文本（等价于访问 <see cref="HitObjectLines"/>）。
        /// 在"决定要重建谱面"的那一刻调用：既不拖慢每次全量读取，又保证后台重建线程
        /// 拿到的是已经固定下来的表（此时读取循环已经不会再修改这个 collection）。
        /// </summary>
        public void EnsureHitObjectLines() => _ = HitObjectLines;

        /// <summary>
        /// 只建"每行对象"但不拼字符串：绘制高亮需要的 IsSelect 标志走这条路径，
        /// 避免在每次全量读取时付出 ToString() 的代价（16000 物件约 33ms/次）。
        /// <para />顺序与 <see cref="HitObjects"/> 一一对应，因此 MasterIndex 就是列表下标。
        /// </summary>
        public static List<ReaderHitObjectWithSelect> BuildSelectionLines(List<Editor_Reader.HitObject> hitObjects)
        {
            var lines = new List<ReaderHitObjectWithSelect>(hitObjects.Count);
            for (int i = 0; i < hitObjects.Count; i++)
            {
                lines.Add(new ReaderHitObjectWithSelect(null!, hitObjects[i].IsSelected, i));
            }
            return lines;
        }

        /// <summary>
        /// 绘制用（高频刷新选中态）的行表：只携带 IsSelect，不带 .osu 文本。
        /// <see cref="HitObjectLines"/> 仅在重建谱面/备份时才需要。
        /// </summary>
        public List<ReaderHitObjectWithSelect> SelectionLines = new();

        public List<Editor_Reader.ControlPoint> ControlPoints;
        public List<Editor_Reader.HitObject> HitObjects;

        public BeatmapInfoCollection()
        {
            ContainingFolder = "";
            Filename = "";
            Bookmarks = [];
            ControlPointLines = new();
            ControlPoints = new();
            HitObjects = new();
            IsFull = false;
            NumControlPoints = 0;
            NumObjects = 0;
            EditorTime = 0;
            PreviewTime = 0;
            StackLeniency = 0.7f;
            HPDrainRate = 5f;
            CircleSize = 5f;
            OverallDifficulty = 5f;
            ApproachRate = 5f;
            SliderMultiplier = 1.4;
            SliderTickRate = 1.0;
            BeatmapVersion = 14;
        }

        /// <summary>
        /// Check Editor Reader's data and make a copy of its current data.
        /// </summary>
        /// <param name="reader">EditorReader</param>
        /// <exception cref="Exception">Throw when Editor Reader's data is invalid.</exception>
        public BeatmapInfoCollection(EditorReader reader)
        {
            IsFull = true;

            // Check editor reader's data
            if (reader.hitObjects == null)
            {
                throw new Exception("HitObjects is null.");
            }
            // Fix Editor Reader
            // Modified from Mapping_Tools
            // https://github.com/OliBomby/Mapping_Tools/tree/master/Mapping_Tools/Classes/ToolHelpers/EditorReaderStuff.cs
            // Under MIT Licnece https://github.com/OliBomby/Mapping_Tools/blob/master/LICENCE
            if (!(reader.numControlPoints > 0 &&
                reader.controlPoints != null && reader.hitObjects != null &&
                reader.numControlPoints == reader.controlPoints.Count && reader.numObjects == reader.hitObjects.Count))
            {
                throw new Exception("Fetched data is invalid.");
            }
            bool FindInvalid = reader.hitObjects.Any(readerHitObject => readerHitObject.X > 1000 || readerHitObject.X < -1000 || readerHitObject.Y > 1000 || readerHitObject.Y < -1000 ||
            readerHitObject.SegmentCount > 9000 || readerHitObject.Type == 0 || readerHitObject.SampleSet > 1000 ||
            readerHitObject.SampleSetAdditions > 1000 || readerHitObject.SampleVolume > 1000);
            if (FindInvalid) throw new Exception("Find invalid hitObject.");
            // -----------------------

            NumControlPoints = reader.numControlPoints;
            NumObjects = reader.numObjects;
            EditorTime = reader.EditorTime();
            ContainingFolder = reader.ContainingFolder;
            Filename = reader.Filename;
            PreviewTime = reader.PreviewTime;
            StackLeniency = reader.StackLeniency;
            HPDrainRate = reader.HPDrainRate;
            CircleSize = reader.CircleSize;
            OverallDifficulty = reader.OverallDifficulty;
            ApproachRate = reader.ApproachRate;
            SliderMultiplier = reader.SliderMultiplier;
            SliderTickRate = reader.SliderTickRate;
            BeatmapVersion = reader.BeatmapVersion;
            Bookmarks = reader.bookmarks;
            ControlPointLines = reader.controlPoints.Select((cp) => cp.ToString()).ToList();
            ControlPoints = reader.controlPoints;
            HitObjects = reader.hitObjects;
            // HitObjectLines（.osu 文本）改为惰性构建：只有重建谱面/备份才需要，见属性注释
            SelectionLines = BuildSelectionLines(HitObjects);

            // We don't need breaks because editor force a new combo after every break.
        }

        /// <summary>
        /// Check Editor Reader's data, filter the objects near the editor time and make a copy of its current data.
        /// <para /> Warning: Cause RANDOM ERROR. Should disable backup.
        /// </summary>
        /// <param name="reader">EditorReader</param>
        /// <param name="partialLoadingHalfTimeSpan">The half time span at reader time for filter hitobjects.
        /// <para />Warning: Cause RANDOM ERROR when using it.</param>
        /// <exception cref="Exception">Throw when Editor Reader's data is invalid.</exception>
        public BeatmapInfoCollection(EditorReader reader, double partialLoadingHalfTimeSpan)
        {
            IsFull = false;

            // Check editor reader's data
            if (reader.hitObjects == null)
            {
                throw new Exception("HitObjects is null.");
            }
            // Fix Editor Reader
            // Modified from Mapping_Tools
            // https://github.com/OliBomby/Mapping_Tools/tree/master/Mapping_Tools/Classes/ToolHelpers/EditorReaderStuff.cs
            // Under MIT Licnece https://github.com/OliBomby/Mapping_Tools/blob/master/LICENCE
            if (!(reader.numControlPoints > 0 &&
                reader.controlPoints != null && reader.hitObjects != null &&
                reader.numControlPoints == reader.controlPoints.Count && reader.numObjects == reader.hitObjects.Count))
            {
                throw new Exception("Fetched data is invalid.");
            }

            EditorTime = reader.EditorTime();

            var NearbyHitObjects = FilterNearbyHitObjects(reader.hitObjects, partialLoadingHalfTimeSpan);

            bool FindInvalid = NearbyHitObjects.Any(pair => pair.Object.X > 1000 || pair.Object.X < -1000 || pair.Object.Y > 1000 || pair.Object.Y < -1000 ||
            pair.Object.SegmentCount > 9000 || pair.Object.Type == 0 || pair.Object.SampleSet > 1000 ||
            pair.Object.SampleSetAdditions > 1000 || pair.Object.SampleVolume > 1000);
            if (FindInvalid) throw new Exception("Find invalid hitObject.");
            // -----------------------

            NumControlPoints = reader.numControlPoints;
            NumObjects = reader.numObjects;
            ContainingFolder = reader.ContainingFolder;
            Filename = reader.Filename;
            PreviewTime = reader.PreviewTime;
            StackLeniency = reader.StackLeniency;
            HPDrainRate = reader.HPDrainRate;
            CircleSize = reader.CircleSize;
            OverallDifficulty = reader.OverallDifficulty;
            ApproachRate = reader.ApproachRate;
            SliderMultiplier = reader.SliderMultiplier;
            SliderTickRate = reader.SliderTickRate;
            BeatmapVersion = reader.BeatmapVersion;
            Bookmarks = reader.bookmarks;
            ControlPointLines = reader.controlPoints.Select((cp) => cp.ToString()).ToList();
            HitObjects = NearbyHitObjects.Select((pair) => pair.Object).ToList();
            // 过滤模式下不能走惰性：惰性表用列表下标当 MasterIndex，会丢掉 pair.Index 这个主列表下标
            SelectionLines = NearbyHitObjects.Select((pair) => new ReaderHitObjectWithSelect(pair.Object.ToString(), pair.Object.IsSelected, pair.Index)).ToList();
            ControlPoints = reader.controlPoints;

            // We don't need breaks because editor force a new combo after every break.
        }

        /// <summary>
        /// MUCH BOOST BUT IT CAUSE RANDOM ERROR.
        /// </summary>
        private List<(int Index, Editor_Reader.HitObject Object)> FilterNearbyHitObjects(List<Editor_Reader.HitObject> hitObject, double halfTimeSpan)
        {
            if (EditorTime < 0) return hitObject.Select((ho, i) => (i, ho)).ToList();
            List<(int, Editor_Reader.HitObject)> result = new();
            for (int i = 0; i < hitObject.Count; i++)
            {
                Editor_Reader.HitObject ho = hitObject[i];
                // keep sliders & spins（跨过当前时间点的物件）
                if (EditorTime - ho.StartTime >= 0 && ho.EndTime - EditorTime >= 0) { result.Add((i, ho)); continue; }
                // keep the objects which |endtime - nowtime| < 10s, or which starttime - nowtime < 10s
                if (EditorTime - ho.EndTime >= 0 && EditorTime - ho.EndTime <= halfTimeSpan) { result.Add((i, ho)); continue; }
                if (ho.StartTime - EditorTime >= 0 && ho.StartTime - EditorTime <= halfTimeSpan) { result.Add((i, ho)); continue; }
            }
            return result;
        }


        /// <summary>
        /// Check difference between two copy of Editor Reader's data.
        /// <para />To determine whether the previous beatmap built can be used directly without the need to rebuild.
        /// </summary>
        /// <param name="other">another BeatmapInfoCollection</param>
        /// <param name="isCheckSelected">Changes in object selection in editor will be considered different if it is true.
        /// <para />Set it to true for reanalyzing when showing selected hitobjects.</param>
        /// <returns>the level of different.</returns>
        public DifferenceType CheckDifference(BeatmapInfoCollection? other, bool isCheckSelected = false)
        {
            if (other is null) return DifferenceType.DifferentFile;
            if (ReferenceEquals(other, this)) return DifferenceType.None;

            if (ContainingFolder != other.ContainingFolder) return DifferenceType.DifferentFile;
            if (Filename != other.Filename) return DifferenceType.DifferentFile;

            if (IsFull != other.IsFull) return DifferenceType.DifferentObjects;

            if (NumControlPoints != other.NumControlPoints) return DifferenceType.DifferentObjects;
            if (NumObjects != other.NumObjects) return DifferenceType.DifferentObjects;

            if (HPDrainRate != other.HPDrainRate) return DifferenceType.DifferentObjects;
            if (CircleSize != other.CircleSize) return DifferenceType.DifferentObjects;
            if (OverallDifficulty != other.OverallDifficulty) return DifferenceType.DifferentObjects;
            if (ApproachRate != other.ApproachRate) return DifferenceType.DifferentObjects;
            if (SliderMultiplier != other.SliderMultiplier) return DifferenceType.DifferentObjects;
            if (SliderTickRate != other.SliderTickRate) return DifferenceType.DifferentObjects;

            if (ControlPoints.Count != other.ControlPoints.Count) return DifferenceType.DifferentObjects;
            for (int i = 0; i < ControlPoints.Count; i++)
            {
                if (!ControlPointEquals(ControlPoints[i], other.ControlPoints[i])) return DifferenceType.DifferentObjects;
            }

            if (HitObjects.Count != other.HitObjects.Count) return DifferenceType.DifferentObjects;
            for (int i = 0; i < HitObjects.Count; i++)
            {
                if (!HitObjectEquals(HitObjects[i], other.HitObjects[i], isCheckSelected)) return DifferenceType.DifferentObjects;
            }

            return DifferenceType.None;
        }

        private static bool ControlPointEquals(Editor_Reader.ControlPoint a, Editor_Reader.ControlPoint b)
        {
            return a.Offset == b.Offset && a.BeatLength == b.BeatLength &&
                   a.TimeSignature == b.TimeSignature && a.SampleSet == b.SampleSet &&
                   a.CustomSamples == b.CustomSamples && a.Volume == b.Volume &&
                   a.TimingChange == b.TimingChange && a.EffectFlags == b.EffectFlags;
        }

        private static bool HitObjectEquals(Editor_Reader.HitObject a, Editor_Reader.HitObject b, bool isCheckSelected)
        {
            if (a.StartTime != b.StartTime || a.EndTime != b.EndTime || a.Type != b.Type || a.SoundType != b.SoundType ||
                a.SegmentCount != b.SegmentCount || a.X != b.X || a.Y != b.Y || a.BaseX != b.BaseX || a.BaseY != b.BaseY ||
                a.SpatialLength != b.SpatialLength || a.CurveType != b.CurveType || a.curveLength != b.curveLength ||
                a.SampleVolume != b.SampleVolume || a.SampleSet != b.SampleSet || a.SampleSetAdditions != b.SampleSetAdditions ||
                a.CustomSampleSet != b.CustomSampleSet || a.SampleFile != b.SampleFile || a.unifiedSoundAddition != b.unifiedSoundAddition)
            {
                return false;
            }
            if (isCheckSelected && a.IsSelected != b.IsSelected) return false;

            if (!IntArrayEquals(a.SoundTypeList, b.SoundTypeList)) return false;
            if (!IntArrayEquals(a.SampleSetList, b.SampleSetList)) return false;
            if (!IntArrayEquals(a.SampleSetAdditionsList, b.SampleSetAdditionsList)) return false;
            if (!FloatArrayEquals(a.sliderCurvePoints, b.sliderCurvePoints)) return false;
            return true;
        }

        private static bool IntArrayEquals(int[]? a, int[]? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        private static bool FloatArrayEquals(float[]? a, float[]? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }

    public class ReaderHitObjectWithSelect
    {
        public string HitObjectLine;
        public bool IsSelect;
        /// <summary>
        /// 该物件在编辑器主物件列表中的下标（全量模式下与列表位置一致；
        /// 过滤模式下用于把选中态映射回主列表下标）。
        /// </summary>
        public int MasterIndex = -1;

        public ReaderHitObjectWithSelect(string hitObjectLine, bool IsSelect, int masterIndex = -1)
        {
            HitObjectLine = hitObjectLine;
            this.IsSelect = IsSelect;
            MasterIndex = masterIndex;
        }

        public bool EqualTo(ReaderHitObjectWithSelect? other, bool isCheckSelected = false)
        {
            if (other is null) return false;
            if (ReferenceEquals(other, this)) return true;

            if (isCheckSelected)
            {
                if (HitObjectLine == other.HitObjectLine && IsSelect == other.IsSelect) return true;
            }
            else
            {
                if (HitObjectLine == other.HitObjectLine) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// The level of difference between two BeatmapInfoCollection.
    /// </summary>
    public enum DifferenceType
    {
        /// <summary>
        /// No difference.
        /// </summary>
        None,

        /// <summary>
        /// Same .osu file in disk but changes in hitobjects or beatmap settings etc.
        /// </summary>
        DifferentObjects,

        /// <summary>
        /// Different .osu file in disk.
        /// </summary>
        DifferentFile
    }
}
