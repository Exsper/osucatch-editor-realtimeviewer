using OpenTK;
using OpenTK.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.UI;
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

        // 每帧复用的缓冲列表，避免反复分配
        private readonly List<BarLine> scratchBarLines = new();
        private readonly List<TimingControlPoint> timingControlPoints = new();
        private readonly List<DifficultyControlPoint> difficultyControlPoints = new();
        private readonly List<TimingControlPoint> scratchTimingPoints = new();
        private readonly List<DifficultyControlPoint> scratchDifficultyPoints = new();
        private readonly HashSet<TimingControlPoint> timingPointSeen = new();
        private readonly HashSet<DifficultyControlPoint> difficultyPointSeen = new();

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
        /// 编辑器读取的物件行（含最新选中态），顺序与解码后的 HitObjects 一致。
        /// 绘制时通过 <see cref="PalpableCatchHitObject.SourceIndex"/> 实时查询选中态，
        /// 这样选中变化不需要触发全量解析/转换重建。
        /// </summary>
        public List<ReaderHitObjectWithSelect>? SelectionLines { get; set; }

        /// <summary>
        /// 参考模板谱面（只读，仅用于绘制下层虚线透明参考物件）。
        /// </summary>
        public TemplateBeatmapData? Template { get; set; }

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
            if (app.Default.Use_Stable_Converter)
            {
                CatchHitObjects = Form1.stableBeatmapConverter.GetPalpableObjects(convertedBeatmap, mods);
                Form1.stableBeatmapConverter.CalHitObjectLabel(convertedBeatmap, CatchHitObjects, LabelType);
            }
            else
            {
                CatchHitObjects = Form1.lazerBeatmapConverter.GetPalpableObjects(convertedBeatmap, mods);
                Form1.lazerBeatmapConverter.CalHitObjectLabel(convertedBeatmap, CatchHitObjects, LabelType);
            }

            float moddedAR = convertedBeatmap.Difficulty.ApproachRate;
            ApproachTime = (int)((moddedAR < 5) ? 1800 - moddedAR * 120 : 1200 - (moddedAR - 5) * 150);
            TimePerPixels = ApproachTime / 432f;
            float moddedCS = convertedBeatmap.Difficulty.CircleSize;
            CircleDiameter = (int)(108.848 - moddedCS * 8.9646);
            CustomComboColours = convertedBeatmap.CustomComboColours;
            if (CustomComboColours.Count <= 0) CustomComboColours = DefaultCustomComboColours;
        }

        /// <summary>
        /// 将后台流水线构建好的数据原子地应用到当前绘制实例。
        /// 只替换装载期字段，不动 CurrentTime / NearbyHitObjects / Bookmarks 等运行时状态。
        /// </summary>
        public void ApplyBuildResult(DrawingHelper staged)
        {
            CatchHitObjects = staged.CatchHitObjects;
            ControlPointInfo = staged.ControlPointInfo;
            BarLines = staged.BarLines;
            ApproachTime = staged.ApproachTime;
            TimePerPixels = staged.TimePerPixels;
            CircleDiameter = staged.CircleDiameter;
            CustomComboColours = staged.CustomComboColours;
            LabelType = staged.LabelType;
        }

        public void Draw()
        {
            BuildNearby();

            DrawTemplate();

            if (app.Default.Show_CubicFittingCurve) DrawSpline();

            timingControlPoints.Clear();
            difficultyControlPoints.Clear();

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
                scratchBarLines.Clear();
                foreach (BarLine barLine in BarLines)
                {
                    if (barLine.StartTime >= 0 && barLine.StartTime <= MaxStartTime + 1) scratchBarLines.Add(barLine);
                }
                DrawBarLines(scratchBarLines);
            }

            if (app.Default.TimingLine_ShowGreen)
            {
                scratchDifficultyPoints.Clear();
                difficultyPointSeen.Clear();
                foreach (DifficultyControlPoint cp in difficultyControlPoints)
                {
                    if (difficultyPointSeen.Add(cp)) scratchDifficultyPoints.Add(cp);
                }
                DrawDifficultyControPoints(scratchDifficultyPoints);
            }

            if (app.Default.TimingLine_ShowRed)
            {
                scratchTimingPoints.Clear();
                timingPointSeen.Clear();
                foreach (TimingControlPoint cp in timingControlPoints)
                {
                    if (timingPointSeen.Add(cp)) scratchTimingPoints.Add(cp);
                }
                DrawTimingPoints(scratchTimingPoints);
            }

            DrawBookmarkPlus(Bookmarks);

            DrawDistanceHelper();
        }

        /// <summary>
        /// 绘制模板谱面的参考物件：半透明白色虚线圆，置于主物件下层。
        /// 与主物件共用同一条时间轴（主图的 TimePerPixels），只画当前时间窗口内的物件。
        /// </summary>
        private void DrawTemplate()
        {
            if (Template == null || Template.Objects.Count <= 0) return;

            List<PalpableCatchHitObject> templateObjects = Template.Objects;
            double timeSpan = ScreensContain * ApproachTime * 1.25 + CircleDiameter * TimePerPixels * 2;
            int startIndex = TemplateLowerBound(templateObjects, CurrentTime - timeSpan);
            int endIndex = TemplateUpperBound(templateObjects, CurrentTime + timeSpan);

            Color4 templateColor = new Color4(1f, 1f, 1f, 0.35f);
            double baseY = (ScreensContain <= 1) ? 408 : 240.0 * ScreensContain;

            for (int k = startIndex; k <= endIndex; k++)
            {
                if (k < 0 || k >= templateObjects.Count) continue;
                PalpableCatchHitObject obj = templateObjects[k];

                double deltaTime = obj.StartTime - CurrentTime;
                if (ScreensContain <= 1)
                {
                    double upTime = ApproachTime + CircleDiameter * TimePerPixels;
                    double bottomTime = ApproachTime * 3 / 17 + CircleDiameter * TimePerPixels;
                    if (deltaTime > upTime || deltaTime < -bottomTime) continue;
                }
                else
                {
                    double span = ScreensContain * ApproachTime * 1.25;
                    if (deltaTime > span || deltaTime < -span) continue;
                }

                float diameter = Template.CircleDiameter;
                if (obj is TinyDroplet) diameter *= obj.Scale / 2f;
                else if (obj is Droplet) diameter *= obj.Scale;

                float posY = (float)(baseY - deltaTime / TimePerPixels);
                Canvas.DrawDashedCircleOutline(new Vector2(64 + obj.EffectiveX, posY), diameter / 2f, templateColor);
            }
        }

        private static int TemplateLowerBound(List<PalpableCatchHitObject> objects, double target)
        {
            int left = 0;
            int right = objects.Count - 1;
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                if (objects[mid].StartTime < target) left = mid + 1;
                else right = mid - 1;
            }
            return right >= 0 ? right : 0;
        }

        private static int TemplateUpperBound(List<PalpableCatchHitObject> objects, double target)
        {
            int left = 0;
            int right = objects.Count - 1;
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                if (objects[mid].StartTime <= target) left = mid + 1;
                else right = mid - 1;
            }
            return left < objects.Count ? left : objects.Count - 1;
        }

        public void DrawBarLines(List<BarLine> barLines)
        {
            int subdivide = app.Default.BarLine_Subdivide;
            bool drawSubdivisions = subdivide > 0 && ControlPointInfo != null;

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
                        if (drawSubdivisions) DrawBarLineSubdivisions(barLine, subdivide);
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
                        if (drawSubdivisions) DrawBarLineSubdivisions(barLine, subdivide);
                    }
                }
            });
        }

        /// <summary>
        /// 绘制小节线的拍点细分线：
        /// “显示到2拍”每隔 2 拍一条，“显示到拍”每一拍一条。
        /// 统一用淡白线（比小节线更淡），避免与 editor 中表示“拍”的 1/2、1/4 混淆。
        /// 不越过下一条小节线。
        /// </summary>
        private void DrawBarLineSubdivisions(BarLine barLine, int subdivide)
        {
            if (ControlPointInfo == null) return;

            TimingControlPoint timing = ControlPointInfo.TimingPointAt(barLine.StartTime);
            double beatLength = timing.BeatLength;
            if (beatLength <= 0) return;

            int beatsPerMeasure = timing.TimeSignature.Numerator;
            double nextBarTime = NextBarLineTime(barLine.StartTime);
            Color subdivisionColor = Color.FromArgb(90, Color.White);

            if (subdivide >= 2)
            {
                // 每一拍一条（2 拍位置也包含在内，颜色相同无需去重）
                for (int beat = 1; beat < beatsPerMeasure; beat++)
                {
                    double time = barLine.StartTime + beat * beatLength;
                    if (time < nextBarTime) DrawSubdivisionLine(time, subdivisionColor);
                }
            }
            else if (subdivide >= 1)
            {
                // 每隔 2 拍一条
                for (int beat = 2; beat < beatsPerMeasure; beat += 2)
                {
                    double time = barLine.StartTime + beat * beatLength;
                    if (time < nextBarTime) DrawSubdivisionLine(time, subdivisionColor);
                }
            }
        }

        private void DrawSubdivisionLine(double time, Color color)
        {
            double deltaTime = time - CurrentTime;
            double baseY = (ScreensContain <= 1) ? 408 : 240.0 * ScreensContain;

            if (ScreensContain <= 1)
            {
                double upTime = ApproachTime;
                double bottomTime = ApproachTime * 3 / 17;
                if (deltaTime > upTime || deltaTime < -bottomTime) return;
            }
            else
            {
                double span = ScreensContain * ApproachTime * 1.25;
                if (deltaTime > span || deltaTime < -span) return;
            }

            int posY = (int)(baseY - deltaTime / TimePerPixels);
            Canvas.DrawLine(new Vector2(64, posY), new Vector2(576, posY), color);
        }

        /// <summary>
        /// BarLines 按时间升序，二分查找第一条晚于指定时间的小节线。
        /// </summary>
        private double NextBarLineTime(double time)
        {
            int left = 0;
            int right = BarLines.Count - 1;
            double result = double.MaxValue;
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                if (BarLines[mid].StartTime > time)
                {
                    result = BarLines[mid].StartTime;
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                }
            }
            return result;
        }

        /// <summary>
        /// 距离辅助线（光锥）：当当前时间点上有 Fruit 时，
        /// 从“上个物件”中心向左上/右上画两条放射线。
        /// 白线 = 1x 走路速度（BASE_WALK_SPEED=0.5），红线 = 2x 走路速度（BASE_DASH_SPEED=1.0），
        /// 用于判断当前放置物件相对上个物件的可达距离。
        /// </summary>
        private void DrawDistanceHelper()
        {
            if (!app.Default.Show_Distance_Helper) return;
            if (CatchHitObjects == null || CatchHitObjects.Count <= 0) return;

            int currentIndex = FindFruitIndexAtTime(CurrentTime);
            if (currentIndex < 0) return;

            PalpableCatchHitObject? previous = ((Fruit)CatchHitObjects[currentIndex]).lastObject;
            if (previous == null) return;

            double baseY = (ScreensContain <= 1) ? 408 : 240.0 * ScreensContain;
            double topY = (ScreensContain <= 1)
                ? baseY - (ApproachTime + CircleDiameter * TimePerPixels)
                : baseY - ScreensContain * ApproachTime * 1.25;
            if (topY >= baseY) return;

            double anchorX = 64 + previous.EffectiveX;
            double anchorY = baseY - (previous.StartTime - CurrentTime) / TimePerPixels;

            // 1x 走路速度：白线
            DrawConeRays(anchorX, anchorY, topY, Catcher.BASE_WALK_SPEED, Color.White);
            // 2x 走路速度：红线
            DrawConeRays(anchorX, anchorY, topY, Catcher.BASE_DASH_SPEED, Color.Red);
        }

        /// <summary>
        /// 从锚点画两条对称的向上放射线（右上、左上），延伸到可视窗口顶部，超出 playfield 时在边缘截断。
        /// 屏幕坐标下时间轴向上为未来，速度 s 的斜率为 dy/dx = -1/(s * TimePerPixels)。
        /// </summary>
        private void DrawConeRays(double anchorX, double anchorY, double topY, double speed, Color color)
        {
            if (speed <= 0 || TimePerPixels <= 0) return;

            double slope = 1.0 / (speed * TimePerPixels);
            Vector2 anchor = new Vector2((float)anchorX, (float)anchorY);

            Canvas.DrawLine(anchor, ConeRayEndpoint(anchorX, anchorY, topY, slope, +1), color);
            Canvas.DrawLine(anchor, ConeRayEndpoint(anchorX, anchorY, topY, slope, -1), color);
        }

        private static Vector2 ConeRayEndpoint(double anchorX, double anchorY, double topY, double slope, double direction)
        {
            const double playLeft = 64;
            const double playRight = 576;

            double dyToTop = anchorY - topY;
            double xAtTop = anchorX + direction * dyToTop / slope;

            if (direction > 0)
            {
                if (xAtTop > playRight)
                    return new Vector2((float)playRight, (float)(anchorY - slope * (playRight - anchorX)));
                return new Vector2((float)xAtTop, (float)topY);
            }

            if (xAtTop < playLeft)
                return new Vector2((float)playLeft, (float)(anchorY - slope * (anchorX - playLeft)));
            return new Vector2((float)xAtTop, (float)topY);
        }

        /// <summary>
        /// 在 CatchHitObjects（按 StartTime 升序）中找时间点（±1ms）上的 Fruit。
        /// </summary>
        private int FindFruitIndexAtTime(double time)
        {
            const double tolerance = 1.0;

            int left = 0;
            int right = CatchHitObjects.Count - 1;
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                if (CatchHitObjects[mid].StartTime < time - tolerance) left = mid + 1;
                else right = mid - 1;
            }

            for (int i = left; i < CatchHitObjects.Count && CatchHitObjects[i].StartTime <= time + tolerance; i++)
            {
                if (CatchHitObjects[i] is Fruit) return i;
            }
            return -1;
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
            if (isSelected && SelectionLines != null && hitObject.SourceIndex >= 0 && hitObject.SourceIndex < SelectionLines.Count)
                isSelected = SelectionLines[hitObject.SourceIndex].IsSelect;

            if (hitObject is TinyDroplet) Canvas.DrawTinyDroplet(pos, CircleDiameter, hitObject.Scale, color, withColor, hitObject.HyperDash, isSelected);
            else if (hitObject is Droplet) Canvas.DrawDroplet(pos, CircleDiameter, hitObject.Scale, color, withColor, hitObject.HyperDash, isSelected);
            else if (hitObject is Fruit) Canvas.DrawFruit(pos, CircleDiameter, color, withColor, hitObject.HyperDash, isSelected);
            else if (hitObject is Banana) Canvas.DrawBanana(pos, CircleDiameter, isSelected);

            if (LabelType != HitObjectLabelType.None && (hitObject is Fruit || (hitObject is Droplet && hitObject is not TinyDroplet)))
            {
                // 标签文本在重建后不再变化，按 LabelType 缓存避免每帧 ToString 分配
                if (hitObject.CachedLabelType != LabelType)
                {
                    hitObject.CachedLabel = hitObject.GetLabelString(LabelType);
                    hitObject.CachedLabelType = LabelType;
                }
                Canvas.DrawHitObjectLabel(hitObject.CachedLabel, pos, CircleDiameter, app.Default.Color_HitObject_Label);
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
            int splitCount = (int)((tMax - tMin) / 20);
            if (splitCount > 100) splitCount = 100;
            List<Vector2> splinePoints = new List<Vector2>();
            for (int i = 0; i < splitCount; i++)
            {
                float tVal = tMin + (tMax - tMin) * i / splitCount;
                float xVal = spline.InterpolateX(tVal);
                if (xVal < 0) xVal = 0;
                else if (xVal > 512) xVal = 512;
                double baseY = (ScreensContain <= 1) ? 408 : 240.0 * this.ScreensContain;
                double deltaTime = tVal - CurrentTime;
                Vector2 pos = new Vector2(64 + xVal, (float)(baseY - deltaTime / TimePerPixels));
                splinePoints.Add(pos);
            }
            for (int i = 1; i < splinePoints.Count; i++)
            {
                Canvas.DrawLine(splinePoints[i - 1], splinePoints[i], app.Default.Curve_Color, app.Default.Curve_Width, (LineType)(app.Default.Curve_LineStyle * 2), beforeTextures: true);
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
