using OpenTK;
using OpenTK.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Objects;

namespace osucatch_editor_realtimeviewer
{
    /*
    * screen size: 640x480
    * playfield size: 512x384
    * 
    * TimePerPixels = ApproachTime / 432  //is it right?????
    * 
    * |                  |
    * |==================|
    * |<---width: 640--->|
    * | <--width: 512--> |
    * |                  |
    * |------------------| N screen catcher | ΔTime = N * RealApproachTime = N * ApproachTime / 0.85
    * |                  |
    * |==================| (screen top) | ΔTime = ApproachTime
    * |                  |
    * |                  |
    * |                  |
    * |------------------| catcher height: 408 (current time) | ΔTime = 0  // is it right?????
    * |                  | 
    * |==================| screen height: 480 (screen bottom) | ΔTime = -72 * TimePerPixels = -ApproachTime * 3 / 17
    * |                  |
    * |                  |
    * |                  |
    * |------------------| -N screen catcher | ΔTime = -N * RealApproachTime = -N * ApproachTime / 0.85
    * |                  |
    * |==================|
    * |                  |
    */

    public class DrawingHelper
    {
        /// <summary>
        /// Editor's current time of beatmap (ms).
        /// </summary>
        public float CurrentTime { get; set; }
        public ControlPointInfo? ControlPointInfo { get; set; }
        List<BarLine> BarLines { get; set; }
        public List<PalpableCatchHitObject> CatchHitObjects { get; set; }

        /// <summary>
        /// CatchHitObjects which near the editor's current time.
        /// </summary>
        public List<PalpableCatchHitObject> NearbyHitObjects { get; set; }
        public int ApproachTime { get; set; }

        /// <summary>
        /// The time spent for fruit to move one pixel. ( = ApproachTime / 432 )
        /// </summary>
        public float TimePerPixels { get; set; }
        private int CircleDiameter { get; set; }
        public HitObjectLabelType LabelType { get; set; }
        public List<Color4> CustomComboColours { get; set; }

        public List<Color4> DefaultCustomComboColours = new() {
            new (255, 191, 191, 255),
            new (128, 191, 255, 255),
            new (128, 255, 128, 255),
            new (191, 128, 255, 255),
            new (128, 255, 255, 255),
        };

        public List<Bookmark> Bookmarks { get; set; } = new();

        /// <summary>
        /// How many screens add up to the height of canvas.
        /// </summary>
        public int ScreensContain { get; set; }

        public DrawingHelper()
        {
            ScreensContain = 4;
            CurrentTime = 0;
            LabelType = HitObjectLabelType.None;
            CatchHitObjects = new List<PalpableCatchHitObject> { };
            NearbyHitObjects = new List<PalpableCatchHitObject> { };
            BarLines = new List<BarLine> { };
            CustomComboColours = DefaultCustomComboColours;
        }

        public void LoadBeatmap(IBeatmap convertedBeatmap, int mods = 0)
        {
            ControlPointInfo = convertedBeatmap.ControlPointInfo;
            BarLines = convertedBeatmap.BarLines;
            CatchHitObjects = Form1.currentBeatmapConverter.GetPalpableObjects(convertedBeatmap);
            Form1.currentBeatmapConverter.CalHitObjectLabel(convertedBeatmap, CatchHitObjects, LabelType);

            float moddedAR = convertedBeatmap.Difficulty.ApproachRate;
            ApproachTime = (int)((moddedAR < 5) ? 1800 - moddedAR * 120 : 1200 - (moddedAR - 5) * 150);
            TimePerPixels = ApproachTime / 432f;
            float moddedCS = convertedBeatmap.Difficulty.CircleSize;
            CircleDiameter = (int)(108.848 - moddedCS * 8.9646);
            CustomComboColours = convertedBeatmap.CustomComboColours;
            if (CustomComboColours.Count <= 0) CustomComboColours = DefaultCustomComboColours;
        }

        public void Draw()
        {
            BuildNearby();

            if (app.Default.Show_CubicFittingCurve) DrawSpline();

            List<TimingControlPoint> timingControlPoints = new List<TimingControlPoint>();
            List<DifficultyControlPoint> difficultyControlPoints = new List<DifficultyControlPoint>();

            double MaxStartTime = -1;

            for (int b = NearbyHitObjects.Count - 1; b >= 0; b--)
            {
                PalpableCatchHitObject hitObject = NearbyHitObjects[b];

                if (MaxStartTime < 0 || hitObject.StartTime > MaxStartTime) MaxStartTime = hitObject.StartTime;

                double deltaTime = hitObject.StartTime - CurrentTime;
                if (ScreensContain > 1)
                {
                    double timeSpan = ScreensContain * ApproachTime * 1.25;
                    if (deltaTime <= timeSpan && deltaTime >= -timeSpan)
                    {
                        this.DrawHitcircle(hitObject, deltaTime);
                    }
                }
                else
                {
                    double upTime = ApproachTime + CircleDiameter * TimePerPixels;
                    double bottomTime = ApproachTime * 3 / 17 + CircleDiameter * TimePerPixels;
                    if (deltaTime <= upTime && deltaTime >= -bottomTime)
                    {
                        this.DrawHitcircle(hitObject, deltaTime);
                    }
                }


                if (app.Default.TimingLine_ShowRed && ControlPointInfo != null)
                {
                    var timingControlPoint = hitObject.GetTimingPoint(ControlPointInfo);
                    timingControlPoints.Add(timingControlPoint);
                }
                if (app.Default.TimingLine_ShowGreen && ControlPointInfo != null)
                {
                    var difficultyControlPoint = hitObject.GetDifficultyControlPoint(ControlPointInfo);
                    difficultyControlPoints.Add(difficultyControlPoint);
                }
            }


            if (app.Default.BarLine_Show)
            {
                List<BarLine> barLines = BarLines.Where((barLine) => barLine.StartTime >= 0 && barLine.StartTime <= MaxStartTime + 1).ToList();
                DrawBarLines(barLines);
            }

            if (app.Default.TimingLine_ShowGreen)
            {
                difficultyControlPoints = difficultyControlPoints.Distinct().ToList();
                DrawDifficultyControPoints(difficultyControlPoints);
            }

            if (app.Default.TimingLine_ShowRed)
            {
                timingControlPoints = timingControlPoints.Distinct().ToList();
                DrawTimingPoints(timingControlPoints);
            }

            DrawBookmarkPlus(Bookmarks);
        }

        public void DrawBarLines(List<BarLine> barLines)
        {
            barLines.ForEach(barLine =>
            {
                if (barLine.StartTime < 0) return;
                double deltaTime = barLine.StartTime - CurrentTime;
                if (ScreensContain > 1)
                {
                    double timeSpan = ScreensContain * ApproachTime * 1.25;
                    if (deltaTime <= timeSpan && deltaTime >= -timeSpan)
                    {
                        int posY = (int)(240.0 * ScreensContain - deltaTime / TimePerPixels);
                        Vector2 rp0 = new Vector2(64, posY);
                        Vector2 rp1 = new Vector2(576, posY);
                        if (barLine.Major) Canvas.DrawLine(rp0, rp1, Color.LightGray);
                        else Canvas.DrawLine(rp0, rp1, Color.Gray);
                    }
                }
                else
                {
                    double upTime = ApproachTime;
                    double bottomTime = ApproachTime * 3 / 17;
                    if (deltaTime <= upTime && deltaTime >= -bottomTime)
                    {
                        int posY = (int)(384 - deltaTime / TimePerPixels);
                        Vector2 rp0 = new Vector2(64, posY);
                        Vector2 rp1 = new Vector2(576, posY);
                        if (barLine.Major) Canvas.DrawLine(rp0, rp1, Color.LightGray);
                        else Canvas.DrawLine(rp0, rp1, Color.Gray);
                    }
                }
            });
        }

        public void DrawBookmarkPlus(List<Bookmark> bookmarks)
        {
            bookmarks.ForEach(bookmark =>
            {
                if (bookmark.Time < 0) return;
                double deltaTime = bookmark.Time - CurrentTime;
                if (ScreensContain > 1)
                {
                    double timeSpan = ScreensContain * ApproachTime * 1.25;
                    if (deltaTime <= timeSpan && deltaTime >= -timeSpan)
                    {
                        int posY = (int)(240.0 * ScreensContain - deltaTime / TimePerPixels);
                        Vector2 rp0 = new Vector2(64, posY);
                        Vector2 rp1 = new Vector2(576, posY);
                        int width = BookmarkPlus.GetLineWidthByStyleId(bookmark.StyleId);
                        Color color = BookmarkPlus.GetLineColorByStyleId(bookmark.StyleId);
                        LineType lineType = BookmarkPlus.GetLineStyleByStyleId(bookmark.StyleId);
                        string label = BookmarkPlus.GetLineLabelByStyleId(bookmark.StyleId);
                        Canvas.DrawLine(rp0, rp1, color, width, lineType);
                        Canvas.DrawBookmarkLabel(label, color, posY);
                    }
                }
                else
                {
                    double upTime = ApproachTime;
                    double bottomTime = ApproachTime * 3 / 17;
                    if (deltaTime <= upTime && deltaTime >= -bottomTime)
                    {
                        int posY = (int)(384 - deltaTime / TimePerPixels);
                        Vector2 rp0 = new Vector2(64, posY);
                        Vector2 rp1 = new Vector2(576, posY);
                        int width = BookmarkPlus.GetLineWidthByStyleId(bookmark.StyleId);
                        Color color = BookmarkPlus.GetLineColorByStyleId(bookmark.StyleId);
                        LineType lineType = BookmarkPlus.GetLineStyleByStyleId(bookmark.StyleId);
                        string label = BookmarkPlus.GetLineLabelByStyleId(bookmark.StyleId);
                        Canvas.DrawLine(rp0, rp1, color, width, lineType);
                        Canvas.DrawBookmarkLabel(label, color, posY);
                    }
                }
            });
        }

        public void DrawTimingPoints(List<TimingControlPoint> timingControlPoints)
        {
            timingControlPoints.ForEach(timingControlPoint =>
            {
                if (timingControlPoint.Time < 0 || timingControlPoint.BPM <= 0) return;
                double deltaTime = timingControlPoint.Time - CurrentTime;
                if (ScreensContain > 1)
                {
                    double timeSpan = ScreensContain * ApproachTime * 1.25;
                    if (deltaTime <= timeSpan && deltaTime >= -timeSpan)
                    {
                        int posY = (int)(240.0 * ScreensContain - deltaTime / TimePerPixels);
                        Canvas.DrawBPMLabel(timingControlPoint.BPM, posY);
                    }
                }
                else
                {
                    double upTime = ApproachTime;
                    double bottomTime = ApproachTime * 3 / 17;
                    if (deltaTime <= upTime && deltaTime >= -bottomTime)
                    {
                        int posY = (int)(384 - deltaTime / TimePerPixels);
                        Canvas.DrawBPMLabel(timingControlPoint.BPM, posY);
                    }
                }
            });
        }

        public void DrawDifficultyControPoints(List<DifficultyControlPoint> difficultyControlPoints)
        {
            difficultyControlPoints.ForEach(difficultyControlPoint =>
            {
                if (difficultyControlPoint.Time < 0 || difficultyControlPoint.SliderVelocity <= 0) return;
                double deltaTime = difficultyControlPoint.Time - CurrentTime;
                if (ScreensContain > 1)
                {
                    double timeSpan = ScreensContain * ApproachTime * 1.25;
                    if (deltaTime <= timeSpan && deltaTime >= -timeSpan)
                    {
                        int posY = (int)(240.0 * ScreensContain - deltaTime / TimePerPixels);
                        Canvas.DrawSVLabel(difficultyControlPoint.SliderVelocity, posY);
                    }
                }
                else
                {
                    double upTime = ApproachTime;
                    double bottomTime = ApproachTime * 3 / 17;
                    if (deltaTime <= upTime && deltaTime >= -bottomTime)
                    {
                        int posY = (int)(384 - deltaTime / TimePerPixels);
                        Canvas.DrawSVLabel(difficultyControlPoint.SliderVelocity, posY);
                    }
                }
            });
        }

        private void DrawHitcircle(PalpableCatchHitObject hitObject, double deltaTime)
        {
            double baseY = (ScreensContain <= 1) ? 408 : 240.0 * this.ScreensContain;
            Vector2 pos = new Vector2(64 + hitObject.EffectiveX, (float)(baseY - deltaTime / TimePerPixels));
            bool withColor = app.Default.Combo_Colour;
            int comboColorIndex = (hitObject.ComboIndex) % CustomComboColours.Count;
            Color4 color = CustomComboColours[comboColorIndex];

            bool isSelected = (app.Default.Selected_Show) ? hitObject.IsSelected : false;

            if (hitObject is TinyDroplet) Canvas.DrawTinyDroplet(pos, CircleDiameter, hitObject.Scale, color, withColor, hitObject.HyperDash, isSelected);
            else if (hitObject is Droplet) Canvas.DrawDroplet(pos, CircleDiameter, hitObject.Scale, color, withColor, hitObject.HyperDash, isSelected);
            else if (hitObject is Fruit) Canvas.DrawFruit(pos, CircleDiameter, color, withColor, hitObject.HyperDash, isSelected);
            else if (hitObject is Banana) Canvas.DrawBanana(pos, CircleDiameter, isSelected);

            if (LabelType != HitObjectLabelType.None && (hitObject is Fruit || (hitObject is Droplet && hitObject is not TinyDroplet)))
            {
                string labelString = hitObject.GetLabelString(LabelType);
                Canvas.DrawHitObjectLabel(labelString, pos, CircleDiameter, app.Default.Color_HitObject_Label);
            }
        }

        public void BuildNearby()
        {
            NearbyHitObjects.Clear();
            if (this.CatchHitObjects == null)
            {
                throw new Exception("Please LoadBeatmap before Drawing.");
            }
            double timeSpan = ScreensContain * ApproachTime * 1.25 + CircleDiameter * TimePerPixels * 2;
            int startIndex = (ScreensContain <= 1) ? this.HitObjectsLowerBound(CurrentTime - ApproachTime * 3 / 17 - CircleDiameter * TimePerPixels) : this.HitObjectsLowerBound(CurrentTime - timeSpan / 2);
            int endIndex = (ScreensContain <= 1) ? this.HitObjectsUpperBound(CurrentTime + ApproachTime + CircleDiameter * TimePerPixels) : this.HitObjectsUpperBound(CurrentTime + timeSpan / 2);
            // Console.WriteLine(startIndex + "->" + endIndex);
            for (int k = startIndex; k <= endIndex; k++)
            {
                if (k < 0)
                {
                    continue;
                }
                else if (k >= this.CatchHitObjects.Count)
                {
                    break;
                }
                this.NearbyHitObjects.Add(this.CatchHitObjects[k]);
            }
        }


        private void DrawSpline()
        {
            List<PointF> points = new List<PointF>();
            this.NearbyHitObjects.ForEach((obj) =>
            {
                if (obj is not Banana && obj is not TinyDroplet)
                points.Add(new PointF(obj.EffectiveX, (float)obj.StartTime));
            });
            if (points.Count <= 2) return;
            CubicSpline spline = new CubicSpline(points);
            float tMin = points.Min(p => p.Y);
            float tMax = points.Max(p => p.Y);
            int splitCount = (int)((tMax - tMin) / 5);
            if (splitCount > 2000) splitCount = 2000;
            for (int i = 0; i < splitCount; i++)
            {
                float tVal = tMin + (tMax - tMin) * i / splitCount;
                float xVal = spline.InterpolateX(tVal);
                if (xVal < 0) xVal = 0;
                else if (xVal > 512) xVal = 512;
                double baseY = (ScreensContain <= 1) ? 408 : 240.0 * this.ScreensContain;
                double deltaTime = tVal - CurrentTime;
                Vector2 pos = new Vector2(64 + xVal, (float)(baseY - deltaTime / TimePerPixels));
                Canvas.DrawSplinePoint(pos);
            }
        }

        private int HitObjectsLowerBound(double target)
        {
            if (this.CatchHitObjects == null) return 0;
            int left = 0;
            int right = this.CatchHitObjects.Count - 1;
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                double midTime = this.CatchHitObjects[mid].StartTime;
                if (midTime < target)
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }
            return right >= 0 ? right : 0;
        }

        private int HitObjectsUpperBound(double target)
        {
            if (this.CatchHitObjects == null) return 0;
            int left = 0;
            int right = this.CatchHitObjects.Count - 1;
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                double midTime = this.CatchHitObjects[mid].StartTime;
                if (midTime <= target)
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }
            return left < this.CatchHitObjects.Count ? left : this.CatchHitObjects.Count - 1;
        }
    }


    public class CubicSpline
    {
        private readonly double[] t;
        private readonly double[] x;
        private readonly SplineSegment[] segments;

        public CubicSpline(IEnumerable<PointF> points)
        {
            // 按 t 值排序点
            var sortedPoints = points.OrderBy(p => p.Y).ToArray();

            if (sortedPoints.Length < 2)
                throw new ArgumentException("Need at least 2 points");

            t = sortedPoints.Select(p => (double)p.Y).ToArray();
            x = sortedPoints.Select(p => (double)p.X).ToArray();

            segments = CalculateSplineCoefficients();
        }

        private SplineSegment[] CalculateSplineCoefficients()
        {
            int n = t.Length - 1; // 段数

            if (n == 1)
            {
                // 只有两个点 - 线性插值
                double slope = (x[1] - x[0]) / (t[1] - t[0]);
                return new[]
                {
                new SplineSegment
                {
                    A = x[0],
                    B = slope,
                    C = 0,
                    D = 0,
                    T0 = t[0],
                    T1 = t[1]
                }
            };
            }

            // 计算步长 h[i] = t[i+1] - t[i]
            double[] h = new double[n];
            for (int i = 0; i < n; i++)
                h[i] = t[i + 1] - t[i];

            // 计算 alpha 数组
            double[] alpha = new double[n];
            for (int i = 1; i < n; i++)
            {
                alpha[i] = 3 * ((x[i + 1] - x[i]) / h[i] -
                             (x[i] - x[i - 1]) / h[i - 1]);
            }

            // 初始化三对角矩阵
            double[] l = new double[n + 1];
            double[] mu = new double[n + 1];
            double[] z = new double[n + 1];
            double[] c = new double[n + 1];

            // 边界条件：自然样条（二阶导数为零）
            l[0] = 1;
            mu[0] = 0;
            z[0] = 0;

            // 前向消元
            for (int i = 1; i < n; i++)
            {
                l[i] = 2 * (t[i + 1] - t[i - 1]) - h[i - 1] * mu[i - 1];
                mu[i] = h[i] / l[i];
                z[i] = (alpha[i] - h[i - 1] * z[i - 1]) / l[i];
            }

            // 边界条件
            l[n] = 1;
            z[n] = 0;
            c[n] = 0;

            // 回代计算 c 系数
            for (int i = n - 1; i >= 0; i--)
            {
                c[i] = z[i] - mu[i] * c[i + 1];
            }

            // 计算 b 和 d 系数
            double[] b = new double[n];
            double[] d = new double[n];

            for (int i = 0; i < n; i++)
            {
                b[i] = (x[i + 1] - x[i]) / h[i] -
                       h[i] * (c[i + 1] + 2 * c[i]) / 3;
                d[i] = (c[i + 1] - c[i]) / (3 * h[i]);
            }

            // 创建分段
            SplineSegment[] segments = new SplineSegment[n];
            for (int i = 0; i < n; i++)
            {
                segments[i] = new SplineSegment
                {
                    A = x[i],
                    B = b[i],
                    C = c[i],
                    D = d[i],
                    T0 = t[i],
                    T1 = t[i + 1]
                };
            }

            return segments;
        }

        public float InterpolateX(float tValue)
        {
            // 边界检查
            if (tValue <= t[0]) return (float)x[0];
            if (tValue >= t[^1]) return (float)x[^1];

            // 找到正确的分段
            int segmentIndex = Array.BinarySearch(t, tValue);
            if (segmentIndex < 0)
                segmentIndex = ~segmentIndex - 1;
            else if (segmentIndex >= segments.Length)
                segmentIndex = segments.Length - 1;

            SplineSegment seg = segments[segmentIndex];
            double dt = tValue - seg.T0;

            // 三次多项式计算：S(t) = a + b*dt + c*dt² + d*dt³
            return (float)(seg.A +
                          seg.B * dt +
                          seg.C * Math.Pow(dt, 2) +
                          seg.D * Math.Pow(dt, 3));
        }

        private class SplineSegment
        {
            public double A { get; set; }
            public double B { get; set; }
            public double C { get; set; }
            public double D { get; set; }
            public double T0 { get; set; }
            public double T1 { get; set; }
        }
    }
}
