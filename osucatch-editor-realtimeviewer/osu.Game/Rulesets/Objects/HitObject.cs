// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable


using osu.Framework.Extensions.ListExtensions;
using osu.Framework.Lists;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects.Types;


namespace osu.Game.Rulesets.Objects
{
    /// <summary>
    /// A HitObject describes an object in a Beatmap.
    /// <para>
    /// HitObjects may contain more properties for which you should be checking through the IHas* types.
    /// </para>
    /// </summary>
    public class HitObject
    {
        /// <summary>
        /// A small adjustment to the start time of control points to account for rounding/precision errors.
        /// </summary>
        private const double control_point_leniency = 1;

        /// <summary>
        /// Invoked after <see cref="ApplyDefaults"/> has completed on this <see cref="HitObject"/>.
        /// </summary>
        // TODO: This has no implicit unbind flow. Currently, if a Playfield manages HitObjects it will leave a bound event on this and cause the
        // playfield to remain in memory.
        public event Action<HitObject> DefaultsApplied;

        /// <summary>
        /// The time at which the HitObject starts.
        /// </summary>
        public double StartTime = 0;

        /// <summary>
        /// Is selected by editor.
        /// </summary>
        public bool IsSelected = false;


        private readonly List<HitObject> nestedHitObjects = new List<HitObject>();


        public SlimReadOnlyListWrapper<HitObject> NestedHitObjects => nestedHitObjects.AsSlimReadOnly();

        /// <summary>
        /// Applies default values to this HitObject.
        /// </summary>
        /// <param name="controlPointInfo">The control points.</param>
        /// <param name="difficulty">The difficulty settings to use.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="createNestedHitObjects">
        /// 是否生成嵌套物件（滑条的水滴/香蕉等）。
        /// <para />置 false 时只做 <see cref="ApplyDefaultsToSelf"/>，跳过 <see cref="CreateNestedHitObjects"/>
        /// 以及随之而来的排序/组合信息下发/递归。<b>调用方必须自己保证没人会用到
        /// <see cref="NestedHitObjects"/></b>——本项目的稳定版转换器就是这种情况（见
        /// <c>BeatmapConverterOsuStable</c>）：它自己按 osu!stable 的算法重建全部可接物件，
        /// 而 lazer 这一套在大滑条上要花掉整个重建一半的时间。
        /// </param>
        public void ApplyDefaults(ControlPointInfo controlPointInfo, IBeatmapDifficultyInfo difficulty, CancellationToken cancellationToken = default, bool createNestedHitObjects = true)
        {
            ApplyDefaultsToSelf(controlPointInfo, difficulty);

            nestedHitObjects.Clear();

            if (!createNestedHitObjects)
            {
                DefaultsApplied?.Invoke(this);
                return;
            }

            CreateNestedHitObjects(cancellationToken);

            if (this is IHasComboInformation hasCombo)
            {
                foreach (HitObject hitObject in nestedHitObjects)
                {
                    if (hitObject is IHasComboInformation n)
                    {
                        n.ComboIndex = hasCombo.ComboIndex;
                        n.ComboIndexWithOffsets = hasCombo.ComboIndexWithOffsets;
                        n.IndexInCurrentCombo = hasCombo.IndexInCurrentCombo;
                    }
                }
            }

            nestedHitObjects.Sort((h1, h2) => h1.StartTime.CompareTo(h2.StartTime));

            foreach (var h in nestedHitObjects)
                h.ApplyDefaults(controlPointInfo, difficulty, cancellationToken);

            DefaultsApplied?.Invoke(this);
        }

        protected virtual void ApplyDefaultsToSelf(ControlPointInfo controlPointInfo, IBeatmapDifficultyInfo difficulty)
        {
        }

        protected virtual void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
        }

        protected void AddNested(HitObject hitObject) => nestedHitObjects.Add(hitObject);


    }

    public static class HitObjectExtensions
    {
        /// <summary>
        /// Returns the end time of this object.
        /// </summary>
        /// <remarks>
        /// This returns the <see cref="IHasDuration.EndTime"/> where available, falling back to <see cref="HitObject.StartTime"/> otherwise.
        /// </remarks>
        /// <param name="hitObject">The object.</param>
        /// <returns>The end time of this object.</returns>
        public static double GetEndTime(this HitObject hitObject) => (hitObject as IHasDuration)?.EndTime ?? hitObject.StartTime;
    }
}
