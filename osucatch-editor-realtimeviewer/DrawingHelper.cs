using OpenTK;
using OpenTK.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Legacy;
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
        /// 预览时刻（ms）：画布按这个时刻绘制。默认跟随编辑器当前时刻，
        /// 打开“固定预览时刻”快捷开关后会被钉在某一刻不再跟随。
        /// </summary>
        public float CurrentTime { get; set; }

        /// <summary>
        /// 编辑器当前时刻（ms）：始终跟随编辑器，不受“固定预览时刻”影响。
        /// 距离辅助线（光锥）等表示“当前编辑位置”的提示必须用这个时刻，
        /// 否则固定预览后就看不到当前放置物件相对上个物件的可达距离了。
        /// </summary>
        public float EditorTime { get; set; }

        /// <summary>
        /// 是否处于“固定预览时刻”模式：为 true 时 <see cref="CurrentTime"/> 被钉住不再跟随编辑器，
        /// 判定线改画在 <see cref="EditorTime"/> 对应的画面位置上（见 Canvas.DrawJudgementLine）。
        /// </summary>
        public bool FixedPreviewTime { get; set; }

        /// <summary>
        /// 垂直（时间轴）缩放比例：只拉伸 / 压缩画面的 Y 轴，X 轴（物件的横向位置）保持不动。
        /// <para />1.0 = 不缩放；大于 1 把时间轴拉长（同样高度里看到的时刻更少、物件在竖直方向更分散），
        /// 小于 1 把时间轴压扁（同样高度里看到更多时刻）。缩放以判定线
        /// （<see cref="JudgeLineBaseY"/>，即当前时刻所在高度）为基准，判定线本身不动。
        /// </para>
        /// <para />由快捷开关栏的“Y缩放”滑块设置，只作用于当前会话，
        /// <b>不写入设置文件</b>，因此每次启动都从 1.0 开始。
        /// </para>
        /// </summary>
        public float VerticalScale { get; set; } = 1f;

        public ControlPointInfo? ControlPointInfo { get; set; }
        List<BarLine> BarLines { get; set; }
        public List<PalpableCatchHitObject> CatchHitObjects { get; set; }
        public double SliderMultiplier { get; set; }

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
        /// 高频 tick 时由 EditorReaderHelper 原位刷新 IsSelect，绘制时通过
        /// <see cref="PalpableCatchHitObject.SourceIndex"/> 实时查询，
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
            EditorTime = 0;
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
            SliderMultiplier = convertedBeatmap.Difficulty.SliderMultiplier;
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
            SliderMultiplier = staged.SliderMultiplier;
            ApproachTime = staged.ApproachTime;
            TimePerPixels = staged.TimePerPixels;
            CircleDiameter = staged.CircleDiameter;
            CustomComboColours = staged.CustomComboColours;
            LabelType = staged.LabelType;
        }

        /// <summary>
        /// 当前时刻（deltaTime = 0）在画面上的 Y 坐标，也就是判定线的基准高度。
        /// </summary>
        public double JudgeLineBaseY => (ScreensContain <= 1) ? 408 : 240.0 * ScreensContain;

        /// <summary>
        /// 垂直缩放后“一个毫秒对应多少屏幕 Y 像素”，也就是 <see cref="TimePerPixels"/> 的倒数再乘上
        /// <see cref="VerticalScale"/>。时间 → 画面的换算一律走这里，避免各处各写一遍缩放。
        /// <para />数据无效时返回 0（调用方据此跳过绘制）。
        /// </para>
        /// </summary>
        public double PixelsPerMs => (TimePerPixels > 0 && VerticalScale > 0) ? VerticalScale / TimePerPixels : 0;

        /// <summary>
        /// 把时间差（ms）换算成画面上的 Y 偏移（像素，正数表示时刻更晚、在画面上更高）。
        /// 未缩放时等于 <c>deltaTime / TimePerPixels</c>。
        /// </summary>
        public double TimeDeltaToPixels(double deltaTime) => deltaTime * PixelsPerMs;

        /// <summary>
        /// 把画面上的 Y 偏移（像素）换算回时间差（ms）：<see cref="TimeDeltaToPixels"/> 的逆运算，
        /// 用于固定预览整页翻页等“先定画面位置再反推时刻”的场合。
        /// </summary>
        public double PixelsToTimeDelta(double pixels) => (VerticalScale > 0) ? pixels * TimePerPixels / VerticalScale : 0;

        /// <summary>
        /// 整页翻页后判定线与画面边缘保留的距离（占可视高度的比例）。
        /// <para />留余量有两个作用：翻页后判定线不贴边（看得清），
        /// 并且翻页落点与触发边界之间留出足够间隔——否则落点正好压在边缘上时，
        /// 浮点换算的零点几像素误差会让它在“刚好越界/刚好没越界”之间来回翻页，肉眼看到不停闪烁。
        /// </summary>
        private const double PageMarginRatio = 0.06;

        /// <summary>
        /// 固定预览模式下按“整页”跟随 editor：画面本身不连续滚动，但 editor 的位置始终留在画面内。
        /// <para />editor 时刻在画面上的位置（判定线）越过画面上边缘 <paramref name="visibleTopY"/> 时，
        /// 预览时刻整页向前翻，使判定线落到画面下边缘内侧；
        /// 越过下边缘时反向翻页，使判定线落到上边缘内侧（内侧距离见 <see cref="PageMarginRatio"/>）。
        /// 翻页后的时刻由 editor 时刻直接反推，所以 editor 一次跳很远（拖动进度条）也只需一页即可到位。
        /// </summary>
        /// <returns>是否发生了翻页。未处于固定预览模式或时刻数据无效时不做事。</returns>
        public bool PageToKeepEditorVisible(double visibleTopY, double visibleBottomY)
        {
            if (!FixedPreviewTime || !(PixelsPerMs > 0)) return false;

            double baseY = JudgeLineBaseY;
            double lineY = baseY - TimeDeltaToPixels(EditorTime - CurrentTime);
            if (lineY >= visibleTopY && lineY <= visibleBottomY) return false;

            double margin = Math.Max((visibleBottomY - visibleTopY) * PageMarginRatio, 1);
            double targetY = (lineY < visibleTopY) ? visibleBottomY - margin : visibleTopY + margin;
            CurrentTime = (float)(EditorTime - PixelsToTimeDelta(baseY - targetY));
            return true;
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


            // 拍线：模式为“不显示”时整数档位为 0，这里直接跳过整段绘制
            BarLineMode barLineMode = BarLineSettings.CurrentMode;
            if (BarLineSettings.GetSubdivisionTier(barLineMode).Denominator > 0)
            {
                scratchBarLines.Clear();
                foreach (BarLine barLine in BarLines)
                {
                    if (barLine.StartTime >= 0 && barLine.StartTime <= MaxStartTime + 1) scratchBarLines.Add(barLine);
                }
                DrawBarLines(scratchBarLines, barLineMode);
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
        /// 绘制模板谱面的参考物件：半透明虚线圆（颜色/透明度可在设置中调整），置于主物件下层。
        /// 与主物件共用同一条时间轴（主图的 TimePerPixels），只画当前时间窗口内的物件。
        /// </summary>
        private void DrawTemplate()
        {
            if (Template == null || Template.Objects.Count <= 0) return;

            List<PalpableCatchHitObject> templateObjects = Template.Objects;
            double timeSpan = ScreensContain * ApproachTime * 1.25 + CircleDiameter * TimePerPixels * 2;
            int startIndex = TemplateLowerBound(templateObjects, CurrentTime - timeSpan);
            int endIndex = TemplateUpperBound(templateObjects, CurrentTime + timeSpan);

            Color templateArgb = app.Default.Template_Color;
            float templateAlpha = Math.Clamp(app.Default.Template_Alpha, 0, 100) / 100f;
            Color4 templateColor = new Color4(templateArgb.R / 255f, templateArgb.G / 255f, templateArgb.B / 255f, templateAlpha);
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

                float posY = (float)(baseY - TimeDeltaToPixels(deltaTime));
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

        public void DrawBarLines(List<BarLine> barLines, BarLineMode mode)
        {
            BarLineSettings.SubdivisionTier tier = BarLineSettings.GetSubdivisionTier(mode);
            bool drawSubdivisions = tier.Denominator > 0 && ControlPointInfo != null;

            barLines.ForEach(barLine =>
            {
                if (barLine.StartTime < 0) return;
                double deltaTime = barLine.StartTime - CurrentTime;
                int posY;
                if (ScreensContain > 1)
                {
                    double timeSpan = ScreensContain * ApproachTime * 1.25;
                    if (deltaTime > timeSpan || deltaTime < -timeSpan) return;
                    posY = (int)(240.0 * ScreensContain - TimeDeltaToPixels(deltaTime));
                }
                else
                {
                    double upTime = ApproachTime;
                    double bottomTime = ApproachTime * 3 / 17;
                    if (deltaTime > upTime || deltaTime < -bottomTime) return;
                    posY = (int)(384 - TimeDeltaToPixels(deltaTime));
                }

                Vector2 rp0 = new Vector2(64, posY);
                Vector2 rp1 = new Vector2(576, posY);
                if (barLine.Major) Canvas.DrawLine(rp0, rp1, Color.LightGray);
                else Canvas.DrawLine(rp0, rp1, Color.Gray);
                if (drawSubdivisions) DrawBarLineSubdivisions(barLine, tier);
            });
        }

        /// <summary>
        /// 绘制一条小节线<b>之后</b>那一段（到下一根小节线为止）的拍点细分线。
        /// <para />绘制区间取半开区间 <c>[这条小节线, 下一根小节线)</c>：每条细分线正好只由
        /// 它左边的那根小节线画一次，既不重复也不遗漏——包括谱面第一根小节线。
        /// </para>
        /// <list type="bullet">
        /// <item>“每1/N拍”（含“每拍”）：每一拍 N 等分，整拍（1/1）用淡白线、
        /// 更细的拍点用 <see cref="BarLineSettings.GetBeatLineColor"/> 的颜色；</item>
        /// <item>“每2拍 / 每4拍”：只在间隔的整数倍拍点画，画到的都是整拍 → 淡白线。</item>
        /// </list>
        /// 各拍点的时刻由这条小节线自身时刻 + 该处的拍长推出（不从“上一条小节线”反推，
        /// 避免第一根小节线之前没有参照线时整段拍线缺失）；上界用下一条小节线兜底，
        /// 防止拍长/拍号变化时细分线越过小节线画到下一小节里。
        /// </summary>
        private void DrawBarLineSubdivisions(BarLine barLine, BarLineSettings.SubdivisionTier tier)
        {
            if (ControlPointInfo == null || tier.Denominator <= 0) return;

            double barTime = barLine.StartTime;
            double nextBarTime = NextBarLineTime(barTime);

            TimingControlPoint timing = ControlPointInfo.TimingPointAt(barTime);
            double beatLength = timing.BeatLength;
            if (!(beatLength > 0)) return;

            int beatsPerMeasure = Math.Max(1, timing.TimeSignature.Numerator);
            int subdivisions = beatsPerMeasure * tier.Denominator;
            // 跨拍间隔（每2拍 / 每4拍）：只在间隔的整数倍拍点画 → 这些拍点都是整拍
            int gridStep = tier.StepBeats > 0 ? Math.Max(1, tier.StepBeats * tier.Denominator) : 1;

            for (int i = gridStep; i < subdivisions; i += gridStep)
            {
                double time = barTime + i * beatLength / tier.Denominator;
                if (time >= nextBarTime - TimingEpsilon) break;
                if (time < 0) continue;

                // 约分成最简分数后判断：分母为 1 就是整拍，用淡白线
                (int numerator, int denominator) = BarLineSettings.Reduce(i, tier.Denominator);

                DrawSubdivisionLine(
                    time,
                    denominator == 1 ? BarLineSettings.BeatLineColor : BarLineSettings.GetBeatLineColor(numerator, denominator));
            }
        }

        /// <summary>拍线细分的比较容差（ms）：避免浮点误差把“贴着小节线”的细分画出来。</summary>
        private const double TimingEpsilon = 0.001;

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

            int posY = (int)(baseY - TimeDeltaToPixels(deltaTime));
            Canvas.DrawLine(new Vector2(64, posY), new Vector2(576, posY), color);
        }

        /// <summary>
        /// BarLines 按时间升序，二分查找指定时间之后最近的一条小节线；
        /// 后面没有小节线时返回 <see cref="double.PositiveInfinity"/>。
        /// </summary>
        private double NextBarLineTime(double time)
        {
            int left = 0;
            int right = BarLines.Count - 1;
            double result = double.PositiveInfinity;
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                if (BarLines[mid].StartTime > time + TimingEpsilon)
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
        /// 距离辅助线（光锥）：当编辑器当前时刻点上有 Fruit 时，
        /// 从“上个物件”中心向左上/右上画两条放射线。
        /// 白线默认 1x、红线默认 2x（SameWithEditor 速度倍率，可在设置中修改），
        /// 用于判断当前放置物件相对上个物件的可达距离。
        /// <para />画哪个物件由编辑器当前时刻（<see cref="EditorTime"/>）决定，
        /// 这样固定预览时刻后辅助线仍然指示当前编辑位置的可达距离；
        /// 而锚点在画面上的位置要按预览时刻（<see cref="CurrentTime"/>）换算——画面就是按预览时刻画的，
        /// 用 editor 时刻会让锚点脱离画面上那个物件，并随 editor 时刻前进一起向下漂移。
        /// <para />垂直缩放（<see cref="VerticalScale"/>）下锚点高度、可见时长的像素高度与射线斜率
        /// 都按同一比例换算，射线才仍然贴着“可达距离”的边界（见 <see cref="DrawConeRays"/>）。
        /// </para>
        /// </summary>
        private void DrawDistanceHelper()
        {
            if (!app.Default.Show_Distance_Helper) return;
            if (CatchHitObjects == null || CatchHitObjects.Count <= 0) return;
            if (!(PixelsPerMs > 0)) return;

            int currentIndex = FindFruitIndexAtTime(EditorTime);
            if (currentIndex < 0) return;

            PalpableCatchHitObject? previous = ((Fruit)CatchHitObjects[currentIndex]).lastObject;
            if (previous == null) return;

            double baseY = (ScreensContain <= 1) ? 408 : 240.0 * ScreensContain;
            // 射线向上延伸到“可视时长”的顶端：这段时长在垂直缩放后占的像素高度也跟着变，
            // 所以这里必须一起缩放，否则放大时间轴时射线会提前被截断。
            double topTime = (ScreensContain <= 1)
                ? ApproachTime + CircleDiameter * TimePerPixels
                : ScreensContain * ApproachTime * 1.25;
            double topY = baseY - TimeDeltaToPixels(topTime);
            if (topY >= baseY) return;

            double anchorX = 64 + previous.EffectiveX;
            double anchorY = DistanceHelperAnchorY(previous.StartTime, baseY);

            // SameWithEditor 速度换算（参考 BeatmapConverter.CalDistanceToNext）：
            // 水平速度 = 倍率 × (SliderMultiplier × 100 × SliderVelocity) / BeatLength
            double whiteSpeed = CalcSameWithEditorSpeed(app.Default.Distance_Helper_White_Speed);
            double redSpeed = CalcSameWithEditorSpeed(app.Default.Distance_Helper_Red_Speed);

            DrawConeRays(anchorX, anchorY, topY, whiteSpeed, Color.White);
            DrawConeRays(anchorX, anchorY, topY, redSpeed, Color.Red);
        }

        /// <summary>
        /// 距离辅助线锚点（上个物件）在画面上的 Y 坐标。
        /// 基准时间是预览时刻 <see cref="CurrentTime"/>：画面上物件的位置就是按这个时刻算的，
        /// 换成 editor 时刻会让锚点落到画面上另一个位置（固定预览后还会随 editor 前进而向下漂移）。
        /// </summary>
        private double DistanceHelperAnchorY(double previousStartTime, double baseY)
            => baseY - TimeDeltaToPixels(previousStartTime - CurrentTime);

        /// <summary>
        /// 按 SameWithEditor 语义换算水平速度（px/ms）：
        /// speed = 倍率 × (SliderMultiplier × 100 × SliderVelocity) / BeatLength。
        /// 与 <see cref="BeatmapConverter.CalDistanceToNext"/> 的 XDistToNext_SameWithEditor 一致，
        /// 参数取编辑器当前时刻（<see cref="EditorTime"/>）处的拍长与滑条速度。数据无效时返回 0（对应射线不画）。
        /// </summary>
        private double CalcSameWithEditorSpeed(double multiplier)
        {
            if (!(multiplier > 0) || ControlPointInfo == null || !(SliderMultiplier > 0)) return 0;

            TimingControlPoint timing = ControlPointInfo.TimingPointAt(EditorTime);
            DifficultyControlPoint difficulty = (ControlPointInfo as LegacyControlPointInfo)?.DifficultyPointAt(EditorTime) ?? DifficultyControlPoint.DEFAULT;

            double beatLength = timing.BeatLength;
            double sliderVelocity = difficulty.SliderVelocity;
            if (!(beatLength > 0) || !(sliderVelocity > 0)) return 0;

            return multiplier * (SliderMultiplier * 100 * sliderVelocity) / beatLength;
        }

        /// <summary>
        /// 从锚点画两条对称的向上放射线（右上、左上），延伸到可视窗口顶部，超出 playfield 时在边缘截断。
        /// 屏幕坐标下时间轴向上为未来；水平速度 s（px/ms）在未缩放时的斜率为 dy/dx = -1/(s * TimePerPixels)。
        /// <para /><b>垂直缩放的影响</b>：X 轴不受缩放影响，而画面上 1 像素高度对应的时长从
        /// <c>TimePerPixels</c> 变成 <c>TimePerPixels / VerticalScale</c>，即向上 dy 像素可用的时间变成
        /// <c>dy / PixelsPerMs</c>；这段时间能横移 <c>s * dy / PixelsPerMs</c> 像素，
        /// 于是斜率按同一比例变成 <c>VerticalScale / (s * TimePerPixels)</c>——
        /// 时间轴拉长时射线更陡，压扁时更平，射线始终贴着当前速度下的可达距离边界。
        /// </para>
        /// </summary>
        private void DrawConeRays(double anchorX, double anchorY, double topY, double speed, Color color)
        {
            if (speed <= 0 || !(PixelsPerMs > 0)) return;

            double slope = VerticalScale / (speed * TimePerPixels);
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
                        int posY = (int)(240.0 * ScreensContain - TimeDeltaToPixels(deltaTime));
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
                        int posY = (int)(384 - TimeDeltaToPixels(deltaTime));
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
                        int posY = (int)(240.0 * ScreensContain - TimeDeltaToPixels(deltaTime));
                        Canvas.DrawBPMLabel(timingControlPoint.BPM, posY);
                    }
                }
                else
                {
                    double upTime = ApproachTime;
                    double bottomTime = ApproachTime * 3 / 17;
                    if (deltaTime <= upTime && deltaTime >= -bottomTime)
                    {
                        int posY = (int)(384 - TimeDeltaToPixels(deltaTime));
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
                        int posY = (int)(240.0 * ScreensContain - TimeDeltaToPixels(deltaTime));
                        Canvas.DrawSVLabel(difficultyControlPoint.SliderVelocity, posY);
                    }
                }
                else
                {
                    double upTime = ApproachTime;
                    double bottomTime = ApproachTime * 3 / 17;
                    if (deltaTime <= upTime && deltaTime >= -bottomTime)
                    {
                        int posY = (int)(384 - TimeDeltaToPixels(deltaTime));
                        Canvas.DrawSVLabel(difficultyControlPoint.SliderVelocity, posY);
                    }
                }
            });
        }

        private void DrawHitcircle(PalpableCatchHitObject hitObject, double deltaTime)
        {
            double baseY = (ScreensContain <= 1) ? 408 : 240.0 * this.ScreensContain;
            Vector2 pos = new Vector2(64 + hitObject.EffectiveX, (float)(baseY - TimeDeltaToPixels(deltaTime)));
            bool withColor = app.Default.Combo_Colour;
            int comboColorIndex = (hitObject.ComboIndex) % CustomComboColours.Count;
            Color4 color = CustomComboColours[comboColorIndex];

            bool isSelected = false;
            if (app.Default.Selected_Show)
            {
                // 实时选中态优先（高频刷新，不依赖重建）；表不可用时回退到转换快照的选中标志
                if (SelectionLines != null && hitObject.SourceIndex >= 0 && hitObject.SourceIndex < SelectionLines.Count)
                    isSelected = SelectionLines[hitObject.SourceIndex].IsSelect;
                else
                    isSelected = hitObject.IsSelected;
            }

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
                Vector2 pos = new Vector2(64 + xVal, (float)(baseY - TimeDeltaToPixels(deltaTime)));
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
