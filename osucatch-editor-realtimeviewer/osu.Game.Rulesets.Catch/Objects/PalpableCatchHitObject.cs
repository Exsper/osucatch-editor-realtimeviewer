// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.


using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Rulesets.Objects.Types;
using osucatch_editor_realtimeviewer;


namespace osu.Game.Rulesets.Catch.Objects
{
    /// <summary>
    /// Represents a single object that can be caught by the catcher.
    /// This includes normal fruits, droplets, and bananas but excludes objects that act only as a container of nested hit objects.
    /// </summary>
    public abstract class PalpableCatchHitObject : CatchHitObject, IHasComboInformation
    {
        /// <summary>
        /// 该物件在解码后 beatmap.HitObjects 中的下标（与 EditorReader 的 HitObjectLines 顺序一致），
        /// 用于绘制时实时查询选中态，避免选中变化触发全量重建。
        /// </summary>
        public int SourceIndex = -1;

        /// <summary>
        /// 按 LabelType 缓存的标签文本，避免每帧重复格式化。
        /// </summary>
        public string CachedLabel = "";

        /// <summary>
        /// 生成 <see cref="CachedLabel"/> 时使用的标签类型。
        /// </summary>
        public HitObjectLabelType CachedLabelType = (HitObjectLabelType)(-1);

        /// <summary>
        /// Difference between the distance to the next object
        /// and the distance that would have triggered a hyper dash.
        /// A value close to 0 indicates a difficult jump (for difficulty calculation).
        /// </summary>
        public float DistanceToHyperDash { get; set; }

        private bool hyperDash;

        /// <summary>
        /// Whether this fruit can initiate a hyperdash.
        /// </summary>
        public bool HyperDash => hyperDash;

        private CatchHitObject? hyperDashTarget;

        /// <summary>
        /// The target fruit if we are to initiate a hyperdash.
        /// </summary>
        public CatchHitObject? HyperDashTarget
        {
            get => hyperDashTarget;
            set
            {
                hyperDashTarget = value;
                hyperDash = value != null;
            }
        }

        public int CurrentCombo = 0;
        public int FruitCountInCombo = 0;
        public bool IsComboEnd = false;

        public TimingControlPoint GetTimingPoint(ControlPointInfo controlPointInfo)
        {
            return controlPointInfo.TimingPointAt(StartTime);
        }

        public DifficultyControlPoint GetDifficultyControlPoint(ControlPointInfo controlPointInfo)
        {
            return (controlPointInfo as LegacyControlPointInfo)?.DifficultyPointAt(StartTime) ?? DifficultyControlPoint.DEFAULT;
        }

        public float NormalizedPosition;
        public float LastNormalizedPosition;
        /// <summary>
        /// Milliseconds elapsed since the start time of the previous PalpableCatchHitObject, with a minimum of 40ms.
        /// </summary>
        public double StrainTime;

        /// <summary>
        /// The previous fruit/droplet to calculate distance or difficulty
        /// </summary>
        public PalpableCatchHitObject? lastObject { get; set; }
        public double DeltaTime;

        public double XDistToNext_SameWithEditor = 0;
        public double XDistToNext_NoSliderVelocityMultiplier = 0;
        public double XDistToNext_CompareWithWalkSpeed = 0;

        public double DifficultyToLast = 0;

        public string GetLabelString(HitObjectLabelType lt)
        {
            switch (lt)
            {
                case HitObjectLabelType.None: return "";
                case HitObjectLabelType.FruitCountInCombo:
                    {
                        if (!IsComboEnd) return "";
                        return "x" + FruitCountInCombo.ToString();
                    }
                case HitObjectLabelType.Difficulty_Stars:
                    {
                        if (DifficultyToLast < 0.01) return "";
                        else return DifficultyToLast.ToString("F2") + "*";
                    }
                case HitObjectLabelType.Distance_SameWithEditor:
                    {
                        if (XDistToNext_SameWithEditor < 0.01) return "";
                        else return "x" + XDistToNext_SameWithEditor.ToString("F2");
                    }
                case HitObjectLabelType.Distance_NoSliderVelocityMultiplier:
                    {
                        if (XDistToNext_NoSliderVelocityMultiplier < 0.01) return "";
                        else return "x" + XDistToNext_NoSliderVelocityMultiplier.ToString("F2");
                    }
                case HitObjectLabelType.Distance_CompareWithWalkSpeed:
                    {
                        if (XDistToNext_CompareWithWalkSpeed < 0.01) return "";
                        else return "x" + XDistToNext_CompareWithWalkSpeed.ToString("F2");
                    }
                default: return "";
            }
        }
    }
}
