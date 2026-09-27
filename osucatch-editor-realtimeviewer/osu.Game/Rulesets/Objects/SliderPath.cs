// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.


using osu.Framework.Utils;
using osu.Game.Rulesets.Objects.Types;
using osuTK;
using System.Diagnostics;

namespace osu.Game.Rulesets.Objects
{
    public class SliderPath
    {
        /// <summary>
        /// The current version of this <see cref="SliderPath"/>. Updated when any change to the path occurs.
        /// </summary>
        public int version = 0;

        /// <summary>
        /// The user-set distance of the path. If non-null, <see cref="Distance"/> will match this value,
        /// and the path will be shortened/lengthened to match this length.
        /// </summary>
        public double? ExpectedDistance = 0;

        /// <summary>
        /// The control points of the path.
        /// </summary>
        public List<PathControlPoint> ControlPoints = new List<PathControlPoint>();

        private readonly List<Vector2> calculatedPath = new List<Vector2>();
        private readonly List<double> cumulativeLength = new List<double>();

        /// <summary>
        /// Any additional length of the path which was optimised out during piecewise approximation, but should still be considered as part of <see cref="calculatedLength"/>.
        /// </summary>
        /// <remarks>
        /// This is a hack for Catmull paths.
        /// </remarks>
        private double optimisedLength;

        /// <summary>
        /// The final calculated length of the path.
        /// </summary>
        private double calculatedLength;

        private readonly List<int> segmentEnds = new List<int>();
        private double[] segmentEndDistances = Array.Empty<double>();

        /// <summary>
        /// Creates a new <see cref="SliderPath"/>.
        /// </summary>
        public SliderPath()
        {
        }

        /// <summary>
        /// Creates a new <see cref="SliderPath"/> initialised with a list of control points.
        /// </summary>
        /// <param name="controlPoints">An optional set of <see cref="PathControlPoint"/>s to initialise the path with.</param>
        /// <param name="expectedDistance">A user-set distance of the path that may be shorter or longer than the true distance between all control points.
        /// The path will be shortened/lengthened to match this length. If null, the path will use the true distance between all control points.</param>
        public SliderPath(PathControlPoint[] controlPoints, double? expectedDistance = null)
            : this()
        {
            ControlPoints.AddRange(controlPoints);
            ExpectedDistance = expectedDistance;
        }

        /// <summary>
        /// The distance of the path after lengthening/shortening to account for <see cref="ExpectedDistance"/>.
        /// </summary>

        public double Distance
        {
            get
            {
                ensureValid();
                return cumulativeLength.Count == 0 ? 0 : cumulativeLength[^1];
            }
        }

        private bool optimiseCatmull;

        /// <summary>
        /// Whether to optimise Catmull path segments, usually resulting in removing bulbs around stacked knots.
        /// </summary>
        /// <remarks>
        /// This changes the path shape and should therefore not be used.
        /// </remarks>
        public bool OptimiseCatmull
        {
            get => optimiseCatmull;
            set
            {
                optimiseCatmull = value;
                invalidate();
            }
        }

        /// <summary>
        /// Computes the position on the slider at a given progress that ranges from 0 (beginning of the path)
        /// to 1 (end of the path).
        /// </summary>
        /// <param name="progress">Ranges from 0 (beginning of the path) to 1 (end of the path).</param>
        public Vector2 PositionAt(double progress)
        {
            ensureValid();

            double d = progressToDistance(progress);
            return interpolateVertices(indexOfDistance(d), d);
        }

        private void invalidate()
        {
            version++;

            // 让下一次访问重新计算（见 ensureValid 的说明）
            isValid = false;
        }

        /// <summary>
        /// 曲线（<see cref="calculatedPath"/> / <see cref="cumulativeLength"/>）是否已经算过、且输入没变过。
        /// </summary>
        private bool isValid;

        /// <summary>
        /// 上次计算时输入的指纹（控制点 + OptimiseCatmull + ExpectedDistance）。
        /// </summary>
        private ulong validFingerprint;

        /// <summary>
        /// 保证曲线与累积长度可用。
        /// <para>
        /// 曲线逼近（尤其是控制点很多的滑条）非常昂贵，而 <see cref="Distance"/> / <see cref="PositionAt"/>
        /// 在生成物件时会被反复调用、且每次都要求"确保有效"。此前这里是无条件重算的，
        /// 一条 1 万多个控制点的滑条会被整块重算十几次，单条就能吃掉几十毫秒。
        /// </para>
        /// <para>
        /// 因此这里按输入指纹做记忆化：指纹不变就直接复用上次的结果。
        /// 用指纹而不是单纯一个布尔标记，是因为 <see cref="ControlPoints"/> 是公开的
        /// <see cref="List{T}"/>，外部可以直接改动/增删而不会通知本对象；
        /// 指纹（O(控制点数)）比重新逼近（O(控制点数²) 甚至更多）便宜好几个数量级，
        /// 又能保证任何真实改动都会被察觉。
        /// </para>
        /// <para>
        /// <see cref="ExpectedDistance"/> 变化的语义与上游 lazer 一致：重置为未计算再算一次
        /// （<see cref="calculateLength"/> 会按 ExpectedDistance 截断/延长曲线，必须与路径一起重算）。
        /// </para>
        /// </summary>
        private void ensureValid()
        {
            ulong fingerprint = calculateFingerprint();

            if (isValid && fingerprint == validFingerprint)
                return;

            calculatePath();
            calculateLength();

            validFingerprint = fingerprint;
            isValid = true;
        }

        /// <summary>
        /// 计算当前输入的指纹：控制点（坐标 + 段类型）、<see cref="OptimiseCatmull"/> 与 <see cref="ExpectedDistance"/>。
        /// <para>只是用来判断"输入有没有变"，不做任何分配。</para>
        /// </summary>
        private ulong calculateFingerprint()
        {
            unchecked
            {
                const ulong prime = 1099511628211UL;

                ulong hash = 14695981039346656037UL;
                hash = (hash ^ (ulong)(uint)ControlPoints.Count) * prime;

                for (int i = 0; i < ControlPoints.Count; i++)
                {
                    PathControlPoint point = ControlPoints[i];

                    hash = (hash ^ (uint)BitConverter.SingleToInt32Bits(point.Position.X)) * prime;
                    hash = (hash ^ (uint)BitConverter.SingleToInt32Bits(point.Position.Y)) * prime;

                    PathType? type = point.Type;
                    if (type == null)
                        hash = (hash ^ 0x9E3779B97F4A7C15UL) * prime;
                    else
                    {
                        hash = (hash ^ (uint)type.Value.Type) * prime;
                        hash = (hash ^ (uint)(type.Value.Degree ?? -1)) * prime;
                    }
                }

                hash = (hash ^ (optimiseCatmull ? 0x1UL : 0x2UL)) * prime;

                if (ExpectedDistance is double expectedDistance)
                    hash = (hash ^ (ulong)BitConverter.DoubleToInt64Bits(expectedDistance)) * prime;
                else
                    hash = (hash ^ 0x3UL) * prime;

                return hash;
            }
        }

        private void calculatePath()
        {
            calculatedPath.Clear();
            segmentEnds.Clear();
            optimisedLength = 0;

            if (ControlPoints.Count == 0)
                return;

            Vector2[] vertices = new Vector2[ControlPoints.Count];
            for (int i = 0; i < ControlPoints.Count; i++)
                vertices[i] = ControlPoints[i].Position;

            int start = 0;

            for (int i = 0; i < ControlPoints.Count; i++)
            {
                if (ControlPoints[i].Type == null && i < ControlPoints.Count - 1)
                    continue;

                // The current vertex ends the segment
                var segmentVertices = vertices.AsSpan().Slice(start, i - start + 1);
                var segmentType = ControlPoints[start].Type ?? PathType.LINEAR;

                // No need to calculate path when there is only 1 vertex
                if (segmentVertices.Length == 1)
                    calculatedPath.Add(segmentVertices[0]);
                else if (segmentVertices.Length > 1)
                {
                    List<Vector2> subPath = calculateSubPath(segmentVertices, segmentType);

                    // Skip the first vertex if it is the same as the last vertex from the previous segment
                    bool skipFirst = calculatedPath.Count > 0 && subPath.Count > 0 && calculatedPath.Last() == subPath[0];

                    for (int j = skipFirst ? 1 : 0; j < subPath.Count; j++)
                        calculatedPath.Add(subPath[j]);
                }

                if (i > 0)
                {
                    // Remember the index of the segment end
                    segmentEnds.Add(calculatedPath.Count - 1);
                }

                // Start the new segment at the current vertex
                start = i;
            }
        }

        private List<Vector2> calculateSubPath(ReadOnlySpan<Vector2> subControlPoints, PathType type)
        {
            switch (type.Type)
            {
                case SplineType.Linear:
                    return PathApproximator.LinearToPiecewiseLinear(subControlPoints);

                case SplineType.PerfectCurve:
                    {
                        if (subControlPoints.Length != 3)
                            break;

                        CircularArcProperties circularArcProperties = new CircularArcProperties(subControlPoints);

                        // `PathApproximator` will already internally revert to B-spline if the arc isn't valid.
                        if (!circularArcProperties.IsValid)
                            break;

                        // taken from https://github.com/ppy/osu-framework/blob/1201e641699a1d50d2f6f9295192dad6263d5820/osu.Framework/Utils/PathApproximator.cs#L181-L186
                        int subPoints = (2f * circularArcProperties.Radius <= 0.1f) ? 2 : Math.Max(2, (int)Math.Ceiling(circularArcProperties.ThetaRange / (2.0 * Math.Acos(1f - (0.1f / circularArcProperties.Radius)))));

                        // 1000 subpoints requires an arc length of at least ~120 thousand to occur
                        // See here for calculations https://www.desmos.com/calculator/umj6jvmcz7
                        if (subPoints >= 1000)
                            break;

                        List<Vector2> subPath = PathApproximator.CircularArcToPiecewiseLinear(subControlPoints);

                        // If for some reason a circular arc could not be fit to the 3 given points, fall back to a numerically stable bezier approximation.
                        if (subPath.Count == 0)
                            break;

                        return subPath;
                    }

                case SplineType.Catmull:
                    {
                        List<Vector2> subPath = PathApproximator.CatmullToPiecewiseLinear(subControlPoints);

                        if (!OptimiseCatmull)
                            return subPath;

                        // At draw time, osu!stable optimises paths by only keeping piecewise segments that are 6px apart.
                        // For the most part we don't care about this optimisation, and its additional heuristics are hard to reproduce in every implementation.
                        //
                        // However, it matters for Catmull paths which form "bulbs" around sequential knots with identical positions,
                        // so we'll apply a very basic form of the optimisation here and return a length representing the optimised portion.
                        // The returned length is important so that the optimisation doesn't cause the path to get extended to match the value of ExpectedDistance.

                        List<Vector2> optimisedPath = new List<Vector2>(subPath.Count);

                        Vector2? lastStart = null;
                        double lengthRemovedSinceStart = 0;

                        for (int i = 0; i < subPath.Count; i++)
                        {
                            if (lastStart == null)
                            {
                                optimisedPath.Add(subPath[i]);
                                lastStart = subPath[i];
                                continue;
                            }

                            Debug.Assert(i > 0);

                            double distFromStart = Vector2.Distance(lastStart.Value, subPath[i]);
                            lengthRemovedSinceStart += Vector2.Distance(subPath[i - 1], subPath[i]);

                            // See PathApproximator.catmull_detail.
                            const int catmull_detail = 50;
                            const int catmull_segment_length = catmull_detail * 2;

                            // Either 6px from the start, the last vertex at every knot, or the end of the path.
                            if (distFromStart > 6 || (i + 1) % catmull_segment_length == 0 || i == subPath.Count - 1)
                            {
                                optimisedPath.Add(subPath[i]);
                                optimisedLength += lengthRemovedSinceStart - distFromStart;

                                lastStart = null;
                                lengthRemovedSinceStart = 0;
                            }
                        }

                        return optimisedPath;
                    }
            }

            return PathApproximator.BSplineToPiecewiseLinear(subControlPoints, type.Degree ?? subControlPoints.Length);
        }

        private void calculateLength()
        {
            calculatedLength = optimisedLength;
            cumulativeLength.Clear();
            cumulativeLength.Add(0);

            for (int i = 0; i < calculatedPath.Count - 1; i++)
            {
                Vector2 diff = calculatedPath[i + 1] - calculatedPath[i];
                calculatedLength += diff.Length;
                cumulativeLength.Add(calculatedLength);
            }

            // Store the distances of the segment ends now, because after shortening the indices may be out of range
            segmentEndDistances = new double[segmentEnds.Count];

            for (int i = 0; i < segmentEnds.Count; i++)
            {
                segmentEndDistances[i] = cumulativeLength[segmentEnds[i]];
            }

            if (ExpectedDistance is double expectedDistance && calculatedLength != expectedDistance)
            {
                // In osu-stable, if the last two path points of a slider are equal, extension is not performed.
                if (calculatedPath.Count >= 2 && calculatedPath[^1] == calculatedPath[^2] && expectedDistance > calculatedLength)
                {
                    cumulativeLength.Add(calculatedLength);
                    return;
                }

                // The last length is always incorrect
                cumulativeLength.RemoveAt(cumulativeLength.Count - 1);

                int pathEndIndex = calculatedPath.Count - 1;

                if (calculatedLength > expectedDistance)
                {
                    // The path will be shortened further, in which case we should trim any more unnecessary lengths and their associated path segments
                    while (cumulativeLength.Count > 0 && cumulativeLength[^1] >= expectedDistance)
                    {
                        cumulativeLength.RemoveAt(cumulativeLength.Count - 1);
                        calculatedPath.RemoveAt(pathEndIndex--);
                    }
                }

                if (pathEndIndex <= 0)
                {
                    // The expected distance is negative or zero
                    // TODO: Perhaps negative path lengths should be disallowed altogether
                    cumulativeLength.Add(0);
                    return;
                }

                // The direction of the segment to shorten or lengthen
                Vector2 dir = (calculatedPath[pathEndIndex] - calculatedPath[pathEndIndex - 1]).Normalized();

                calculatedPath[pathEndIndex] = calculatedPath[pathEndIndex - 1] + dir * (float)(expectedDistance - cumulativeLength[^1]);
                cumulativeLength.Add(expectedDistance);
            }
        }

        private int indexOfDistance(double d)
        {
            int i = cumulativeLength.BinarySearch(d);
            if (i < 0) i = ~i;

            return i;
        }

        private double progressToDistance(double progress)
        {
            return Math.Clamp(progress, 0, 1) * Distance;
        }

        private Vector2 interpolateVertices(int i, double d)
        {
            if (calculatedPath.Count == 0)
                return Vector2.Zero;

            if (i <= 0)
                return calculatedPath.First();
            if (i >= calculatedPath.Count)
                return calculatedPath.Last();

            Vector2 p0 = calculatedPath[i - 1];
            Vector2 p1 = calculatedPath[i];

            double d0 = cumulativeLength[i - 1];
            double d1 = cumulativeLength[i];

            // Avoid division by and almost-zero number in case two points are extremely close to each other.
            if (Precision.AlmostEquals(d0, d1))
                return p0;

            double w = (d - d0) / (d1 - d0);
            return p0 + (p1 - p0) * (float)w;
        }
    }
}
