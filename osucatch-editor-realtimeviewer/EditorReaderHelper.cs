using Editor_Reader;

namespace osucatch_editor_realtimeviewer
{
    using System.Diagnostics;

    public class EditorReaderHelper
    {
        private static readonly EditorReader reader = new();

        private bool Is_Doing_SetProcess = false;
        private bool Is_Doing_FetchEditor = false;
        private bool Is_Osu_Running = false;
        public bool Is_Editor_Running = false;

        public string beatmap_path = "";
        public string beatmap_title = "";

        private int fetchAll_Failed_Count = 0;
        private const int FetchAll_MaxRetry_Count = 10;

        // 高频/低频分离：多数 tick 只读 EditorTime，全量读取有间隔限制
        private BeatmapInfoCollection? cachedCollection;
        private string cachedTitle = "";
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();
        private long lastEditorCheckTimestamp;
        private long lastFullFetchTimestamp;
        private bool editorCheckSucceeded;
        private const long FullCheckIntervalMs = 150;

        public EditorReaderHelper()
        {
            reader.autoDeStack = true;
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
        /// <returns>Is success or not.</returns>
        public bool FetchEditor()
        {
            // 高频路径：FullCheckIntervalMs 内已经完整验证过 editor，直接复用上次结果
            if (editorCheckSucceeded && stopwatch.ElapsedMilliseconds - lastEditorCheckTimestamp < FullCheckIntervalMs)
            {
                Is_Editor_Running = true;
                return true;
            }

            beatmap_title = "";
            string title = FetchTitle();
            if (title == "")
            {
                Log.ConsoleLog("Empty osu title.", Log.LogType.EditorReader, Log.LogLevel.Info);
                Is_Editor_Running = false;
                beatmap_path = "";
                editorCheckSucceeded = false;
                return false;
            }
            if (!title.EndsWith(".osu"))
            {
                Log.ConsoleLog("Osu title is not editor: " + title, Log.LogType.EditorReader, Log.LogLevel.Info);
                Is_Editor_Running = false;
                beatmap_path = "";
                editorCheckSucceeded = false;
                return false;
            }
            if (reader.EditorNeedsReload())
            {
                Log.ConsoleLog("Editor needs Reload.", Log.LogType.EditorReader, Log.LogLevel.Info);
                try
                {
                    if (Is_Doing_SetProcess || Is_Doing_FetchEditor)
                    {
                        Log.ConsoleLog("Still fetching editor.", Log.LogType.EditorReader, Log.LogLevel.Info);
                        editorCheckSucceeded = false;
                        return false;
                    }
                    if (reader.ProcessNeedsReload())
                    {
                        Log.ConsoleLog("Process needs reload.", Log.LogType.EditorReader, Log.LogLevel.Info);
                        editorCheckSucceeded = false;
                        return false;
                    }
                    Log.ConsoleLog("Try fetch editor.", Log.LogType.EditorReader, Log.LogLevel.Info);
                    Is_Doing_FetchEditor = true;
                    reader.FetchEditor();
                    Is_Doing_FetchEditor = false;
                    Log.ConsoleLog("Fetch editor successfully.", Log.LogType.EditorReader, Log.LogLevel.Info);
                    Is_Osu_Running = true;
                    Is_Editor_Running = true;
                }
                catch (Exception ex)
                {
                    Log.ConsoleLog("Fetch editor failed.\r\n" + ex, Log.LogType.EditorReader, Log.LogLevel.Error);
                    Is_Doing_FetchEditor = false;
                    Is_Editor_Running = false;
                    beatmap_path = "";
                    editorCheckSucceeded = false;
                    return false;
                }
            }
            Is_Editor_Running = true;
            beatmap_title = title;
            lastEditorCheckTimestamp = stopwatch.ElapsedMilliseconds;
            editorCheckSucceeded = true;
            return true;
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
        /// </summary>
        private BeatmapInfoCollection? FetchWithCache(bool filterNearby, double partialLoadingHalfTimeSpan)
        {
            try
            {
                if (fetchAll_Failed_Count > FetchAll_MaxRetry_Count)
                {
                    Log.ConsoleLog("Refetching editor...", Log.LogType.EditorReader, Log.LogLevel.Warning);
                    fetchAll_Failed_Count = 0;
                    FetchEditor();
                    cachedCollection = null;
                    return null;
                }

                if (!IsFullFetchDue())
                {
                    // 高频路径：只刷新播放头时间，其余数据沿用上次全量读取
                    cachedCollection!.EditorTime = reader.EditorTime();
                    if (app.Default.Selected_Show) RefreshSelection(cachedCollection);
                    fetchAll_Failed_Count = 0;
                    return cachedCollection;
                }

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
                if (app.Default.Selected_Show) RefreshSelection(cachedCollection);
                return thisReaderData;
            }
            catch (Exception ex)
            {
                Log.ConsoleLog("FetchAll failed.(" + fetchAll_Failed_Count + ")\r\n" + ex.ToString(), Log.LogType.EditorReader, Log.LogLevel.Error);
                fetchAll_Failed_Count++;
                return null;
            }
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

            foreach (ReaderHitObjectWithSelect line in collection.HitObjectLines)
            {
                line.IsSelect = line.MasterIndex >= 0 && line.MasterIndex < selectionScratch.Length && selectionScratch[line.MasterIndex];
            }
        }

        private bool[] selectionScratch = Array.Empty<bool>();

        private bool IsFullFetchDue()
        {
            if (cachedCollection == null) return true;
            if (cachedTitle != beatmap_title) return true;
            if (stopwatch.ElapsedMilliseconds - lastFullFetchTimestamp >= FullCheckIntervalMs) return true;

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
        public List<ReaderHitObjectWithSelect> HitObjectLines;
        public List<Editor_Reader.ControlPoint> ControlPoints;
        public List<Editor_Reader.HitObject> HitObjects;

        public BeatmapInfoCollection()
        {
            ContainingFolder = "";
            Filename = "";
            Bookmarks = [];
            ControlPointLines = new();
            HitObjectLines = new();
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
            HitObjectLines = reader.hitObjects.Select((ho, i) => new ReaderHitObjectWithSelect(ho.ToString(), ho.IsSelected, i)).ToList();
            ControlPoints = reader.controlPoints;
            HitObjects = reader.hitObjects;

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
            HitObjectLines = NearbyHitObjects.Select((pair) => new ReaderHitObjectWithSelect(pair.Object.ToString(), pair.Object.IsSelected, pair.Index)).ToList();
            ControlPoints = reader.controlPoints;
            HitObjects = NearbyHitObjects.Select((pair) => pair.Object).ToList();

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
