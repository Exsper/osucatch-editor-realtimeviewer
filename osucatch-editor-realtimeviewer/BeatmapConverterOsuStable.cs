using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Editor_Reader;
using Microsoft.VisualBasic.Devices;
using OpenTK.Graphics.OpenGL;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Utils;
using osuTK;

namespace osucatch_editor_realtimeviewer
{
    public class BeatmapConverterOsuStable : BeatmapConverter
    {

        public override List<PalpableCatchHitObject> GetPalpableObjects(IBeatmap beatmap)
        {
            Log.ConsoleLog("Building hitobjects.", Log.LogType.BeatmapConverter, Log.LogLevel.Debug);

            List<PalpableCatchHitObject> palpableObjects = new List<PalpableCatchHitObject>();
            HitObjectManagerCatch manager = new();

            foreach (var currentObject in beatmap.HitObjects)
            {
                if (currentObject is Fruit fruitObject)
                    palpableObjects.Add(fruitObject);

                else if (currentObject is JuiceStream)
                {
                    foreach (var juice in manager.convertSlider(beatmap, (JuiceStream)currentObject))
                    {
                        if (juice is PalpableCatchHitObject palpableObject) {
                            juice.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
                            palpableObjects.Add(palpableObject);
                        }
                    }
                }
                else if (currentObject is BananaShower)
                {
                    foreach (var banana in currentObject.NestedHitObjects)
                    {
                        if (banana is PalpableCatchHitObject palpableObject)
                            palpableObjects.Add(palpableObject);
                    }
                }
            }

            palpableObjects.Sort((h1, h2) => h1.StartTime.CompareTo(h2.StartTime));
            
            // The precise value of catcherWidth in stable depends on window resolution due to floating-point errors.
            // Here we just use a simplified formula so that catcherWidth only depends on beatmap CircleSize. 
            initialiseHyperDash((float)(106.75f * (1.7f - 0.14f * beatmap.Difficulty.CircleSize)), palpableObjects);

            return palpableObjects;
        }

        private static void initialiseHyperDash(float catcherWidth, List<PalpableCatchHitObject> hitObjects)
        {
            var palpableObjects = CatchBeatmap.GetPalpableObjects(hitObjects)
                                              .Where(h => h is Fruit || (h is Droplet && h is not TinyDroplet))
                                              .ToArray();

            double halfCatcherWidth = catcherWidth / 2;

            int lastDirection = 0;
            double lastExcess = halfCatcherWidth;

            for (int i = 0; i < palpableObjects.Length - 1; i++)
            {
                var currentObject = palpableObjects[i];
                var nextObject = palpableObjects[i + 1];

                // Reset variables in-case values have changed (e.g. after applying HR)
                currentObject.HyperDashTarget = null;
                currentObject.DistanceToHyperDash = 0;

                int thisDirection = nextObject.EffectiveX > currentObject.EffectiveX ? 1 : -1;

                // Int truncation added to match osu!stable.
                double timeToNext = (int)nextObject.StartTime - (int)currentObject.StartTime - 1000f / 60f / 4; // 1/4th of a frame of grace time, taken from osu-stable
                double distanceToNext = Math.Abs(nextObject.EffectiveX - currentObject.EffectiveX) - (lastDirection == thisDirection ? lastExcess : halfCatcherWidth);
                float distanceToHyper = (float)(timeToNext * Catcher.BASE_DASH_SPEED - distanceToNext);

                if (distanceToHyper < 0)
                {
                    currentObject.HyperDashTarget = nextObject;
                    lastExcess = halfCatcherWidth;
                }
                else
                {
                    currentObject.DistanceToHyperDash = distanceToHyper;
                    lastExcess = Math.Clamp(distanceToHyper, 0, halfCatcherWidth);
                }

                lastDirection = thisDirection;
            }
        }

        internal class Segment : ICloneable
        {
            internal Vector2 Start;

            internal Vector2 End;

            internal float Length
            {
                get { return (End - Start).Length; }
            }

            internal Segment(Vector2 Start, Vector2 End)
            {
                this.Start = Start;
                this.End = End;
            }

            internal Vector2 ClosestPointTo(Vector2 p)
            {
                Vector2 v = End - Start;
                Vector2 w = p - Start;

                float c1 = Vector2.Dot(w, v);
                if (c1 <= 0)
                    return Start;

                float c2 = Vector2.Dot(v, v);
                if (c2 <= c1)
                    return End;

                float b = c1 / c2;
                Vector2 pB = Start + b * v;
                return pB;
            }

            public object Clone()
            {
                return this.MemberwiseClone();
            }
        };

        internal class LegacySliderAdditionalData
        {
            public int SpanCount;

            public int StartTime;

            public int EndTime;

            public Vector2 Position;

            public Vector2 EndPosition;

            public double ExpectedDistance;

            public double Velocity;

            public double CurveLength;

            public Vector2 Position2;

            public List<int> SliderRepeatPoints = new List<int>();

            public List<int> SliderScoreTimingPoints = new List<int>();

            public List<double> CumulativeLengths = new List<double>();

            public List<Segment> CurveSegmentPath = new List<Segment>();

            public LegacySliderAdditionalData(IBeatmap beatmap, JuiceStream slider)
            {
                StartTime = (int)slider.StartTime;
                Position = new(slider.OriginalX, slider.Y);

                compute(beatmap, slider);
            }

            private double computeVelocity(
                TimingControlPoint timingControlPoint,
                double sliderVelocityAsBeatLength,
                double difficultySliderTickRate,
                double sliderComboPointDistance)
            {
                float mult = (float)(-sliderVelocityAsBeatLength) / 100f;
                double beatLength = timingControlPoint.BeatLength * mult;
                return sliderComboPointDistance * difficultySliderTickRate * (1000f / beatLength);
            }

            private List<Vector2> rebuildCurvePoints(Vector2 offset, SliderPath path)
            {
                List<Vector2> curvePoints = new();
                for (int i = 0; i < path.ControlPoints.Count; i++)
                {
                    PathControlPoint curvePoint = path.ControlPoints[i];
                    curvePoints.Add(offset + curvePoint.Position);
                    if (i > 0 && curvePoint.Type != null)
                        curvePoints.Add(offset + curvePoint.Position);
                }
                return curvePoints;
            }

            public int GetTimeByLength(float length)
            {
                return (int)(StartTime + (length / Velocity) * 1000);
            }

            public Vector2 GetLengthByPosition(float length)
            {
                if (CurveSegmentPath.Count == 0 || CumulativeLengths.Count == 0)
                    return Position;

                if (length == 0)
                    return CurveSegmentPath[0].Start;

                double end = CumulativeLengths[CumulativeLengths.Count - 1];
                if (length >= end)
                    return CurveSegmentPath[CurveSegmentPath.Count - 1].End;

                int i = CumulativeLengths.BinarySearch(length);
                if (i < 0)
                    i = Math.Min(~i, CumulativeLengths.Count - 1);

                double lengthNext = CumulativeLengths[i];
                double lengthPrevious = i == 0 ? 0 : CumulativeLengths[i - 1];

                Vector2 res = CurveSegmentPath[i].Start;

                if (lengthNext != lengthPrevious)
                    res += (CurveSegmentPath[i].End - CurveSegmentPath[i].Start) * (float)((length - lengthPrevious) / (lengthNext - lengthPrevious));

                return res;
            }

            public Vector2 GetPositionByTime(int time)
            {

                if (time < StartTime || time > EndTime) return Position;

                float pos = (time - StartTime) / ((float) (EndTime - StartTime) / SpanCount);

                if (pos % 2 > 1)
                    pos = 1 - (pos % 1);
                else
                    pos = pos % 1;

                float lengthRequired = (float)(ExpectedDistance * pos);
                return GetLengthByPosition(lengthRequired);
            }

            private void compute(IBeatmap beatmap, JuiceStream slider)
            {
                double sliderComboPointDistance = (100 * beatmap.Difficulty.SliderMultiplier) / beatmap.Difficulty.SliderTickRate;

                Velocity = computeVelocity(
                    beatmap.ControlPointInfo.TimingPointAt(slider.StartTime),
                    slider.SliderVelocityAsBeatLength,
                    beatmap.Difficulty.SliderTickRate,
                    sliderComboPointDistance
                );

                #region Calculate Path

                List<Segment> path = new List<Segment>();

                SliderPath pathData = slider.Path;

                // The first control point should have a non-null PathType
                PathType pathType = pathData.ControlPoints[0].Type ?? PathType.LINEAR;
                List<Vector2> curvePoints = rebuildCurvePoints(new Vector2(slider.OriginalX, slider.Y), pathData);

                const int subSegmentCount = 50;
                
                switch (pathType.Type)
                {
                    case SplineType.Catmull:
                        for (int j = 0; j < curvePoints.Count - 1; j++)
                        {
                            Vector2 v1 = (j - 1 >= 0 ? curvePoints[j - 1] : curvePoints[j]);
                            Vector2 v2 = curvePoints[j];
                            Vector2 v3 = (j + 1 < curvePoints.Count ? curvePoints[j + 1] : v2 + (v2 - v1));
                            Vector2 v4 = (j + 2 < curvePoints.Count ? curvePoints[j + 2] : v3 + (v3 - v2));

                            for (int k = 0; k < subSegmentCount; k++)
                                path.Add(new Segment(
                                    Vector2.CatmullRom(v1, v2, v3, v4, (float)k / subSegmentCount),
                                    Vector2.CatmullRom(v1, v2, v3, v4, (float)(k + 1) / subSegmentCount)
                                ));
                        }
                        break;

                    case SplineType.BSpline:
                        int lastIndex = 0;

                        for (int i = 0; i < curvePoints.Count; i++)
                        {
                            if (beatmap.BeatmapInfo.BeatmapVersion > 6)
                            {
                                bool lastPointInCurrentPart = i < curvePoints.Count - 2 && curvePoints[i] == curvePoints[i + 1];
                                if (lastPointInCurrentPart || i == curvePoints.Count - 1)
                                {
                                    List<Vector2> currentPartControlPoints = curvePoints.GetRange(lastIndex, i - lastIndex + 1);

                                    if (beatmap.BeatmapInfo.BeatmapVersion > 8 && currentPartControlPoints.Count == 2)
                                    {
                                        path.Add(new Segment(currentPartControlPoints[0], currentPartControlPoints[1]));
                                    }
                                    else if (beatmap.BeatmapInfo.BeatmapVersion > 8 && beatmap.BeatmapInfo.BeatmapVersion < 10 && currentPartControlPoints.Count != 2)
                                    {
                                        List<Vector2> bezierPoints = LegacyMathHelper.CreateBezierWrong(currentPartControlPoints);
                                        for (int j = 1; j < bezierPoints.Count; j++)
                                            path.Add(new Segment(bezierPoints[j - 1], bezierPoints[j]));
                                    }
                                    else
                                    {
                                        List<Vector2> bezierPoints = LegacyMathHelper.CreateBezier(currentPartControlPoints);
                                        for (int j = 1; j < bezierPoints.Count; j++)
                                            path.Add(new Segment(bezierPoints[j - 1], bezierPoints[j]));
                                    }
                                    if (lastPointInCurrentPart) i++;
                                    lastIndex = i;
                                }
                            }
                            else
                            {
                                if ((i > 0 && curvePoints[i] == curvePoints[i - 1]) || i == curvePoints.Count - 1)
                                {
                                    List<Vector2> currentPartControlPoints = curvePoints.GetRange(lastIndex, i - lastIndex + 1);
                                    List<Vector2> bezierPoints = LegacyMathHelper.CreateBezier(currentPartControlPoints);
                                    for (int j = 1; j < bezierPoints.Count; j++)
                                        path.Add(new Segment(bezierPoints[j - 1], bezierPoints[j]));
                                    lastIndex = i;
                                }
                            }
                        }
                        break;

                    case SplineType.PerfectCurve:
                        if (curvePoints.Count < 3)
                            goto case SplineType.Linear;
                        if (curvePoints.Count > 3)
                            goto case SplineType.BSpline;

                        Vector2 p1 = curvePoints[0];
                        Vector2 p2 = curvePoints[1];
                        Vector2 p3 = curvePoints[2];

                        if (LegacyMathHelper.IsStraightLine(p1, p2, p3))
                        {
                            goto case SplineType.Linear;
                        }

                        Vector2 center;
                        float radius;
                        double startAngle, endAngle;

                        LegacyMathHelper.CircleThroughPoints(p1, p2, p3, out center, out radius, out startAngle, out endAngle);

                        CurveLength = Math.Abs((endAngle - startAngle) * radius);
                        int segments = (int)(CurveLength * 0.125f);

                        Vector2 lastPoint = p1;

                        for (int i = 1; i < segments; i++)
                        {
                            double progress = (double)i / (double)segments;
                            double t = endAngle * progress + startAngle * (1 - progress);

                            Vector2 newPoint = LegacyMathHelper.CirclePoint(center, radius, t);
                            path.Add(new Segment(lastPoint, newPoint));

                            lastPoint = newPoint;
                        }

                        path.Add(new Segment(lastPoint, p3));
                        break;

                    case SplineType.Linear:
                        for (int i = 1; i < curvePoints.Count; i++)
                        {
                            path.Add(new Segment(curvePoints[i - 1], curvePoints[i]));
                        }
                        break;
                }

                CurveSegmentPath = path;

                #endregion

                #region Calculate slider length

                const double LENGTH_EPS = 0.0001;

                CurveLength = 0;
                for (int i = 0; i < path.Count; i++)
                    CurveLength += path[i].Length;
                int expectedComboCount = 0;
                double tickDistance = (beatmap.BeatmapInfo.BeatmapVersion < 8) ? sliderComboPointDistance :
                    (sliderComboPointDistance / ((float)(-slider.SliderVelocityAsBeatLength) / 100f));

                if (CurveLength > 0)
                {
                    if (pathData.ExpectedDistance == null || pathData.ExpectedDistance == 0)
                    {
                        expectedComboCount = (int)(CurveLength / tickDistance);
                        pathData.ExpectedDistance = CurveLength;
                    }
                    // After previous check, ExpectedDistance should not be null
                    if (tickDistance > pathData.ExpectedDistance)
                        tickDistance = (double)pathData.ExpectedDistance;
                    double cutLength = CurveLength - (double)pathData.ExpectedDistance;
                    while (path.Count > 0)
                    {
                        Segment lastSegment = path[path.Count - 1];
                        float lastSegmentLength = Vector2.Distance(lastSegment.Start, lastSegment.End);

                        if (lastSegmentLength > cutLength + LENGTH_EPS)
                        {
                            if (lastSegment.End != lastSegment.Start)
                            {
                                lastSegment.End = lastSegment.Start + Vector2.Normalize(lastSegment.End - lastSegment.Start) * (lastSegment.Length - (float)cutLength);
                            }
                            break;
                        }

                        path.Remove(lastSegment);
                        cutLength -= lastSegmentLength;
                    }
                    ExpectedDistance = (double)pathData.ExpectedDistance;
                }

                if (path.Count > 0)
                {
                    if (CumulativeLengths == null) CumulativeLengths = new List<double>(path.Count);
                    else CumulativeLengths.Clear();

                    double totalLength = 0.0;

                    foreach (Segment l in path)
                    {
                        totalLength += l.Length;
                        CumulativeLengths.Add(totalLength);
                    }
                }

                #endregion

                if (path.Count < 1)
                    return;

                #region Calculate Position
                {
                    double scoringLengthTotal = 0;
                    double currentTime = StartTime;

                    Vector2 p1 = new Vector2();
                    Vector2 p2 = new Vector2();

                    double scoringDistance = 0;

                    Position2 = path[path.Count - 1].End;

                    SpanCount = HasRepeatsExtensions.SpanCount(slider);
                    for (int i = 0; i < SpanCount; i++)
                    {
                        double distanceRemain = CumulativeLengths[CumulativeLengths.Count - 1];
                        bool skipTick = false;
                        int reverseStartTime = (int) currentTime;

                        double minTickDistanceFromEnd = 0.01 * Velocity;

                        bool reverse = (i % 2) == 1;

                        int start = reverse ? path.Count - 1 : 0;
                        int end = reverse ? -1 : path.Count;
                        int direction = reverse ? -1 : 1;

                        for (int j = start; j != end; j += direction)
                        {
                            Segment l = path[j];
                            float distance = (float)(CumulativeLengths[j] - (j == 0 ? 0 : CumulativeLengths[j - 1]));

                            if (reverse)
                            {
                                p1 = l.End;
                                p2 = l.Start;
                            }
                            else
                            {
                                p1 = l.Start;
                                p2 = l.End;
                            }

                            double duration = 1000F * distance / Velocity;

                            currentTime += duration;
                            scoringDistance += distance;

                            while (scoringDistance >= tickDistance && !skipTick)
                            {
                                scoringLengthTotal += tickDistance;
                                scoringDistance -= tickDistance;
                                distanceRemain -= tickDistance;

                                skipTick = distanceRemain <= minTickDistanceFromEnd;
                                if (skipTick)
                                    break;

                                int scoreTime = GetTimeByLength((float)scoringLengthTotal);
                                SliderScoreTimingPoints.Add(scoreTime);
                            }
                        }

                        scoringLengthTotal += scoringDistance;
                        SliderScoreTimingPoints.Add(GetTimeByLength((float)scoringLengthTotal));

                        if (skipTick)
                            scoringDistance = 0;
                        else
                        {
                            scoringLengthTotal -= tickDistance - scoringDistance;
                            scoringDistance = tickDistance - scoringDistance;
                        }
                    }

                    EndPosition = p2;
                    EndTime = (int)currentTime;

                    if (SliderScoreTimingPoints.Count > 0)
                        SliderScoreTimingPoints[SliderScoreTimingPoints.Count - 1] = Math.Max(StartTime + (EndTime - StartTime) / 2, SliderScoreTimingPoints[SliderScoreTimingPoints.Count - 1] - 36);

                    SliderRepeatPoints.Clear();
                    int timingPointsPerSegment = SliderScoreTimingPoints.Count / SpanCount;
                    if (timingPointsPerSegment > 0)
                        for (int i = 0; i < SliderScoreTimingPoints.Count - 1; i++)
                            if ((i + 1) % timingPointsPerSegment == 0)
                                SliderRepeatPoints.Add(SliderScoreTimingPoints[i]);
                }
                #endregion
            }
        }

        internal class HitObjectManagerCatch
        {
            private LegacyRandom random = new(1337);

            public List<PalpableCatchHitObject> convertSlider(IBeatmap beatmap, JuiceStream juiceStream)
            {

                #region Calculate Drops

                List<PalpableCatchHitObject> palpableHitObjects = new();

                palpableHitObjects.Add(new Fruit
                {
                    StartTime = juiceStream.StartTime,
                    X = juiceStream.OriginalX,
                    ComboIndex = juiceStream.ComboIndex
                });

                LegacySliderAdditionalData sliderData = new(beatmap, juiceStream);

                int lastTime = (int)juiceStream.StartTime;

                int fruitIndex = 1;
                for (int i = 0; i < sliderData.SliderScoreTimingPoints.Count; i++)
                {
                    int time = sliderData.SliderScoreTimingPoints[i];

                    if (time - lastTime > 80)
                    {
                        float var = (time - lastTime);
                        while (var > 100)
                            var /= 2;

                        for (float j = lastTime + var; j < time; j += var)
                        {
                            TinyDroplet tinyDroplet = new TinyDroplet
                            {
                                StartTime = (int)j,
                                X = sliderData.GetPositionByTime((int)j).X + random.Next(-20, 20),
                                ComboIndex = juiceStream.ComboIndex
                            };
                            palpableHitObjects.Add(tinyDroplet);
                        }
                    }

                    lastTime = time;

                    if (i < sliderData.SliderScoreTimingPoints.Count - 1)
                    {
                        int repeatLocation = sliderData.SliderRepeatPoints.BinarySearch(time);

                        if (repeatLocation >= 0)
                        {
                            palpableHitObjects.Add(new Fruit
                            {
                                StartTime = time,
                                X = repeatLocation % 2 == 1 ? juiceStream.OriginalX : sliderData.Position2.X,
                                ComboIndex = juiceStream.ComboIndex
                            });
                            fruitIndex++;
                        }
                        else
                        {
                            palpableHitObjects.Add(new Droplet
                            {
                                StartTime = time,
                                X = sliderData.GetPositionByTime(time).X,
                                ComboIndex = juiceStream.ComboIndex
                            });
                        }
                    }
                }

                palpableHitObjects.Add(new Fruit
                {
                    StartTime = sliderData.EndTime,
                    X = sliderData.EndPosition.X,
                    ComboIndex = juiceStream.ComboIndex
                });

                #endregion

                return palpableHitObjects;
            }

        }

    }
}
