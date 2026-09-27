using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osuTK;

namespace osucatch_editor_realtimeviewer
{
    public partial class BeatmapConverterOsuStable
    {
        /// <summary>
        /// 诊断/对照用开关：关掉后每条滑条都重新解析曲线，
        /// 用于验证"命中缓存"与"重新计算"的结果是否一致。
        /// </summary>
        public static bool SliderCurveCacheEnabled
        {
            get => SliderCurveCache.Enabled;
            set => SliderCurveCache.Enabled = value;
        }

        /// <summary>当前滑条曲线缓存里的条目数（诊断用）。</summary>
        public static int SliderCurveCacheCount => SliderCurveCache.Count;

        /// <summary>
        /// 至今有多少条滑条走了"平移近似"（拖动过程中的快速路径）。
        /// 调用方在一次重建前后各取一次，即可判断这份结果里是否含有近似值。
        /// </summary>
        public static long SliderCurveCacheApproximationCount => Interlocked.Read(ref SliderCurveCache.approximationCount);

        /// <summary>
        /// 缓存统计（诊断用）：命中原位置 / 停下后补算 / 平移近似 / 未命中。
        /// </summary>
        public static (long ExactHits, long Settles, long Shifts, long Misses) SliderCurveCacheStats
            => (Interlocked.Read(ref SliderCurveCache.exactHits),
                Interlocked.Read(ref SliderCurveCache.settleHits),
                Interlocked.Read(ref SliderCurveCache.shiftHits),
                Interlocked.Read(ref SliderCurveCache.misses));

        /// <summary>清空滑条曲线缓存（切换谱面/换图时调用）。</summary>
        public static void ClearSliderCurveCache() => SliderCurveCache.Clear();

        /// <summary>
        /// 滑条曲线缓存（稳定版转换器用）。
        ///
        /// <para>
        /// <b>为什么可以复用：</b><see cref="LegacySliderAdditionalData"/> 里所有昂贵的东西
        /// ——曲线采样（Catmull / Bezier / 圆弧）、累积长度、计分与滴落时刻——
        /// 只取决于「滑条自身的相对几何 + 时间/速度参数」，与滑条在谱面上的绝对位置无关
        /// （.osu 里滑条控制点存的就是相对滑条起点的坐标，见
        /// <c>ConvertHitObjectParser.convertPathString</c> 中的 <c>readPoint</c>）。
        /// 所以按「相对几何 + 参数」缓存一份结果，滑条被整体平移（拖动）以后仍然能用。
        /// </para>
        ///
        /// <para>
        /// <b>怎么保证结果不变：</b>缓存里保存的是"某个位置上的完整结果"，并且：
        /// <list type="number">
        /// <item>滑条还在原位置（<see cref="Prototype.Position"/>）时，直接复用那份结果，
        /// 逐位一致——静止看谱、编辑别的物件都走这条路；</item>
        /// <item>滑条换了位置时，先按双精度平移快速给出一份结果（见
        /// <see cref="LegacySliderAdditionalData.Shifted"/>），先把画面跟上；</item>
        /// <item>同一个几何第二次在同一个新位置被请求时（说明拖动已经停下、
        /// 或这是粘贴出来的另一条同形状滑条），就按新位置完整算一遍并留下精确结果，
        /// 之后一直逐位一致。</item>
        /// </list>
        /// 也就是说：只有"滑条正在被拖动的那一两帧"用的是平移近似，
        /// 停下来之后立刻收敛回精确结果。收敛需要"同一个位置再来一次"，
        /// 而编辑器数据不变时不会自动重建，所以 <c>Form1</c> 在数据稳定后会主动补算
        /// （<c>UsedApproximateSliderCurve</c> + <c>MaxExactRebuildAttempts</c>）。
        /// </para>
        ///
        /// <para>
        /// <b>精度：</b>停在原地时逐位一致；拖动中的平移近似绝大多数只差 1 ulp
        /// （≈1e-5 px），个别控制点很多、又跨了浮点指数分界的滑条可能差到 ~0.5 px，
        /// 停下来补算一次即恢复逐位一致。需要完全不做近似的场合，
        /// 把 <see cref="BeatmapConverterOsuStable.SliderCurveCacheEnabled"/> 置 false
        /// 即可（等价于"只缓存原位置的滑条"）。
        /// </para>
        ///
        /// <para>
        /// <b>为什么能平移：</b>.osu 里坐标都是整数，平移量也是整数；
        /// StableCompatLib 用 80 位扩展精度算曲线，<c>midpoint</c> / <c>bezierApproximate</c> /
        /// <c>catmullRom</c> / <c>flatJudge</c> 的中间量都是可精确表示的整数或分数，
        /// 对"所有输入同时平移整数 T"在数学上是等变的。之所以还可能有 1 ulp 级差异，
        /// 是因为绝对坐标下 80 位中间量的舍入位置会随数量级变化，采样细分也可能因此不同；
        /// 这也是"停下来补算一次"存在的原因。
        /// </para>
        ///
        /// <para>
        /// 唯一不能这样处理的是 <see cref="SplineType.PerfectCurve"/>：圆弧拟合要算三点外接圆，
        /// 里面用到坐标乘积（<c>circleThroughPoints</c>），对平移不是等变的。
        /// 这类滑条控制点恒为 3 个（很便宜），直接跳过缓存，保持原有行为。
        /// </para>
        /// </summary>
        private static class SliderCurveCache
        {
            /// <summary>缓存条目上限；超出后按插入顺序淘汰最旧的。</summary>
            private const int MaxEntries = 4096;

            /// <summary>缓存线段总数上限；超出后同样淘汰（按 <see cref="Entry.SegmentCost"/> 计）。</summary>
            private const long MaxSegments = 1_500_000;

            /// <summary>同一条几何最多在几个位置上留精确结果（复制粘贴出来的多条同形状滑条）。</summary>
            private const int MaxPrototypes = 8;

            /// <summary>记住最近请求过的一批位置，用来判断"同一个位置是不是连着来了两次"。</summary>
            private const int RecentPositionWindow = 8;

            /// <summary>某个位置上的完整计算结果（绝对坐标）。</summary>
            private sealed class Prototype
            {
                internal Vector2 Position;
                internal LegacySliderAdditionalData Data = null!;
            }

            private sealed class Entry
            {
                internal ulong Hash;

                // ---- 缓存键（相对几何 + 时间/速度参数）----
                internal int BeatmapVersion;
                internal bool Flip;
                internal int StartTime;
                internal double TimingBeatLength;
                internal double SliderVelocityAsBeatLength;
                internal double SliderMultiplier;
                internal double SliderTickRate;
                internal int RepeatCount;
                internal bool HasExpectedDistance;
                internal double ExpectedDistance;
                internal Vector2[] Points = Array.Empty<Vector2>();
                internal PathType?[] Types = Array.Empty<PathType?>();

                // ---- 结果 ----
                internal readonly List<Prototype> Prototypes = new();
                internal bool ExpectedDistanceWasUnset;

                private readonly Vector2[] recentPositions = new Vector2[RecentPositionWindow];
                private int recentCount;

                internal int SegmentCost
                {
                    get
                    {
                        int segments = Prototypes.Count > 0 ? Prototypes[0].Data.CurveSegmentPath.Count : 0;
                        return segments * Prototypes.Count;
                    }
                }

                /// <summary>找出该位置上已有的精确结果。</summary>
                internal LegacySliderAdditionalData? Find(Vector2 position)
                {
                    foreach (Prototype prototype in Prototypes)
                    {
                        if (prototype.Position == position)
                            return prototype.Data;
                    }

                    return null;
                }

                internal void Remember(Vector2 position)
                {
                    if (recentCount < recentPositions.Length)
                    {
                        recentPositions[recentCount++] = position;
                        return;
                    }

                    for (int i = 1; i < recentPositions.Length; i++)
                        recentPositions[i - 1] = recentPositions[i];
                    recentPositions[^1] = position;
                }

                /// <summary>这个位置是不是最近已经来过一次（连续两次同位置 = 拖动停下 / 同形状滑条的第二条）。</summary>
                internal bool SeenRecently(Vector2 position)
                {
                    for (int i = 0; i < recentCount; i++)
                    {
                        if (recentPositions[i] == position)
                            return true;
                    }

                    return false;
                }

                internal void AddPrototype(Prototype prototype, ref long cachedSegments)
                {
                    int cost = prototype.Data.CurveSegmentPath.Count;
                    Prototypes.Add(prototype);
                    cachedSegments += cost;

                    while (Prototypes.Count > MaxPrototypes)
                    {
                        cachedSegments -= Prototypes[0].Data.CurveSegmentPath.Count;
                        Prototypes.RemoveAt(0);
                    }
                }

                internal LegacySliderAdditionalData MostRecent => Prototypes[^1].Data;
            }

            private static readonly Dictionary<ulong, List<Entry>> buckets = new();
            private static readonly Queue<Entry> inserted = new();
            private static readonly object syncRoot = new();
            private static long cachedSegments;

            /// <summary>走了"平移近似"的次数，供调用方判断结果里是否含近似值。</summary>
            internal static long approximationCount;

            /// <summary>统计（仅诊断用）：命中既有位置、命中后补算、平移近似、未命中。</summary>
            internal static long exactHits, settleHits, shiftHits, misses;

            /// <summary>诊断/对照用开关：关掉后每条滑条都重新解析。</summary>
            public static bool Enabled { get; set; } = true;

            public static int Count
            {
                get { lock (syncRoot) return inserted.Count; }
            }

            internal static void Clear()
            {
                lock (syncRoot)
                {
                    buckets.Clear();
                    inserted.Clear();
                    cachedSegments = 0;
                }
            }

            /// <summary>
            /// 尝试命中缓存，返回可直接使用的（绝对坐标）结果。
            /// </summary>
            /// <param name="declaredExpectedDistance">
            /// .osu 里写的像素长度。<b>必须传未被动过手脚的原值</b>：
            /// 稳定版转换器会把空/0 改写成曲线长度。
            /// </param>
            internal static LegacySliderAdditionalData? TryGet(
                IBeatmap beatmap,
                JuiceStream slider,
                bool flip,
                double? declaredExpectedDistance,
                Vector2 position)
            {
                if (!Enabled || !IsCacheable(slider))
                    return null;

                ulong hash = calculateHash(beatmap, slider, flip, declaredExpectedDistance);

                lock (syncRoot)
                {
                    if (!buckets.TryGetValue(hash, out List<Entry>? list))
                        return null;

                    foreach (Entry entry in list)
                    {
                        if (!matches(entry, beatmap, slider, flip, declaredExpectedDistance))
                            continue;

                        if (entry.ExpectedDistanceWasUnset)
                            slider.Path.ExpectedDistance = entry.MostRecent.CurveLength;

                        return resolve(entry, beatmap, slider, flip, position);
                    }
                }

                Interlocked.Increment(ref misses);
                return null;
            }

            /// <summary>在缓存里定位/生成该位置上的结果。</summary>
            private static LegacySliderAdditionalData resolve(Entry entry, IBeatmap beatmap, JuiceStream slider, bool flip, Vector2 position)
            {
                LegacySliderAdditionalData? exact = entry.Find(position);
                if (exact != null)
                {
                    Interlocked.Increment(ref exactHits);
                    entry.Remember(position);
                    return exact;
                }

                if (entry.SeenRecently(position))
                {
                    // 同一个位置连着来了两次：说明已经停下（或这是复制出来的另一条同形状滑条），
                    // 在这里补一次精确计算，之后就一直精确复用。
                    Interlocked.Increment(ref settleHits);
                    Prototype prototype = new()
                    {
                        Position = position,
                        Data = LegacySliderAdditionalData.ComputeAbsolute(beatmap, slider, flip, position),
                    };

                    entry.AddPrototype(prototype, ref cachedSegments);
                    entry.Remember(position);
                    return prototype.Data;
                }

                entry.Remember(position);

                // 拖动过程中的快速路径：按双精度平移已有结果（与原型位置相同则逐位一致）
                LegacySliderAdditionalData reference = entry.MostRecent;
                Interlocked.Increment(ref approximationCount);
                Interlocked.Increment(ref shiftHits);
                return LegacySliderAdditionalData.Shifted(reference, position - reference.Position);
            }

            /// <summary>把一条刚算好的结果（绝对坐标）存进缓存。</summary>
            internal static void Store(
                IBeatmap beatmap,
                JuiceStream slider,
                bool flip,
                double? declaredExpectedDistance,
                LegacySliderAdditionalData computed,
                Vector2 position,
                bool expectedDistanceWasUnset)
            {
                if (!Enabled || !IsCacheable(slider))
                    return;

                int count = slider.Path.ControlPoints.Count;
                Entry entry = new()
                {
                    BeatmapVersion = beatmap.BeatmapInfo.BeatmapVersion,
                    Flip = flip,
                    StartTime = (int)slider.StartTime,
                    TimingBeatLength = beatmap.ControlPointInfo.TimingPointAt(slider.StartTime).BeatLength,
                    SliderVelocityAsBeatLength = slider.SliderVelocityAsBeatLength,
                    SliderMultiplier = beatmap.Difficulty.SliderMultiplier,
                    SliderTickRate = beatmap.Difficulty.SliderTickRate,
                    RepeatCount = slider.RepeatCount,
                    ExpectedDistance = declaredExpectedDistance ?? 0,
                    HasExpectedDistance = declaredExpectedDistance.HasValue,
                    Points = new Vector2[count],
                    Types = new PathType?[count],
                    ExpectedDistanceWasUnset = expectedDistanceWasUnset,
                };

                for (int i = 0; i < count; i++)
                {
                    entry.Points[i] = slider.Path.ControlPoints[i].Position;
                    entry.Types[i] = slider.Path.ControlPoints[i].Type;
                }

                entry.Remember(position);
                entry.AddPrototype(new Prototype { Position = position, Data = computed }, ref cachedSegments);

                ulong hash = calculateHash(beatmap, slider, flip, declaredExpectedDistance);
                entry.Hash = hash;

                lock (syncRoot)
                {
                    if (!buckets.TryGetValue(hash, out List<Entry>? list))
                    {
                        list = new List<Entry>(1);
                        buckets[hash] = list;
                    }

                    list.Add(entry);
                    inserted.Enqueue(entry);

                    while (inserted.Count > MaxEntries || cachedSegments > MaxSegments)
                    {
                        Entry oldest = inserted.Dequeue();
                        cachedSegments -= oldest.SegmentCost;
                        if (!buckets.TryGetValue(oldest.Hash, out List<Entry>? oldList))
                            continue;

                        oldList.Remove(oldest);
                        if (oldList.Count == 0)
                            buckets.Remove(oldest.Hash);
                    }
                }
            }

            /// <summary>
            /// 只有 PerfectCurve 需要绕开缓存（外接圆计算对平移不等变）。
            /// </summary>
            internal static bool IsCacheable(JuiceStream slider)
            {
                if (slider.Path.ControlPoints.Count == 0)
                    return false;

                PathType pathType = slider.Path.ControlPoints[0].Type ?? PathType.LINEAR;
                return pathType.Type != SplineType.PerfectCurve;
            }

            private static ulong calculateHash(IBeatmap beatmap, JuiceStream slider, bool flip, double? declaredExpectedDistance)
            {
                unchecked
                {
                    const ulong prime = 1099511628211UL;

                    ulong hash = 14695981039346656037UL;
                    hash = (hash ^ (ulong)(uint)beatmap.BeatmapInfo.BeatmapVersion) * prime;
                    hash = (hash ^ (flip ? 1UL : 0UL)) * prime;
                    hash = (hash ^ (ulong)(uint)(int)slider.StartTime) * prime;
                    hash = (hash ^ (ulong)BitConverter.DoubleToInt64Bits(beatmap.ControlPointInfo.TimingPointAt(slider.StartTime).BeatLength)) * prime;
                    hash = (hash ^ (ulong)BitConverter.DoubleToInt64Bits(slider.SliderVelocityAsBeatLength)) * prime;
                    hash = (hash ^ (ulong)BitConverter.DoubleToInt64Bits(beatmap.Difficulty.SliderMultiplier)) * prime;
                    hash = (hash ^ (ulong)BitConverter.DoubleToInt64Bits(beatmap.Difficulty.SliderTickRate)) * prime;
                    hash = (hash ^ (ulong)(uint)slider.RepeatCount) * prime;

                    if (declaredExpectedDistance is double expectedDistance)
                        hash = (hash ^ (ulong)BitConverter.DoubleToInt64Bits(expectedDistance)) * prime;
                    else
                        hash = (hash ^ 0x5DEECE66DUL) * prime;

                    List<PathControlPoint> points = slider.Path.ControlPoints;
                    hash = (hash ^ (ulong)(uint)points.Count) * prime;

                    for (int i = 0; i < points.Count; i++)
                    {
                        hash = (hash ^ (uint)BitConverter.SingleToInt32Bits(points[i].Position.X)) * prime;
                        hash = (hash ^ (uint)BitConverter.SingleToInt32Bits(points[i].Position.Y)) * prime;

                        PathType? type = points[i].Type;
                        if (type == null)
                            hash = (hash ^ 0x9E3779B97F4A7C15UL) * prime;
                        else
                        {
                            hash = (hash ^ (uint)type.Value.Type) * prime;
                            hash = (hash ^ (uint)(type.Value.Degree ?? -1)) * prime;
                        }
                    }

                    return hash;
                }
            }

            /// <summary>
            /// 逐位比较（哈希只用来分桶，命中与否以这里为准，因此不存在哈希碰撞用错几何的风险）。
            /// </summary>
            private static bool matches(Entry entry, IBeatmap beatmap, JuiceStream slider, bool flip, double? declaredExpectedDistance)
            {
                if (entry.BeatmapVersion != beatmap.BeatmapInfo.BeatmapVersion) return false;
                if (entry.Flip != flip) return false;
                if (entry.StartTime != (int)slider.StartTime) return false;
                if (entry.TimingBeatLength != beatmap.ControlPointInfo.TimingPointAt(slider.StartTime).BeatLength) return false;
                if (entry.SliderVelocityAsBeatLength != slider.SliderVelocityAsBeatLength) return false;
                if (entry.SliderMultiplier != beatmap.Difficulty.SliderMultiplier) return false;
                if (entry.SliderTickRate != beatmap.Difficulty.SliderTickRate) return false;
                if (entry.RepeatCount != slider.RepeatCount) return false;
                if (entry.HasExpectedDistance != declaredExpectedDistance.HasValue) return false;
                if (declaredExpectedDistance is double expectedDistance && entry.ExpectedDistance != expectedDistance) return false;

                List<PathControlPoint> points = slider.Path.ControlPoints;
                if (entry.Points.Length != points.Count) return false;

                for (int i = 0; i < points.Count; i++)
                {
                    if (entry.Points[i] != points[i].Position) return false;
                    if (!Nullable.Equals(entry.Types[i], points[i].Type)) return false;
                }

                return true;
            }
        }
    }
}
