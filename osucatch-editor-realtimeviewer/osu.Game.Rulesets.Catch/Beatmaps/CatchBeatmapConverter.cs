// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Rulesets.Catch.Beatmaps
{
    public class CatchBeatmapConverter : BeatmapConverter<CatchHitObject>
    {
        public CatchBeatmapConverter(IBeatmap beatmap, Ruleset ruleset)
            : base(beatmap, ruleset)
        {
        }

        public override bool CanConvert() => Beatmap.HitObjects.All(h => h is IHasXPosition);

        protected override IEnumerable<CatchHitObject> ConvertHitObject(HitObject obj, IBeatmap beatmap, CancellationToken cancellationToken)
        {
            var xPositionData = obj as IHasXPosition;
            var yPositionData = obj as IHasYPosition;
            var comboData = obj as IHasCombo;
            var sliderVelocityData = obj as IHasSliderVelocity;

            switch (obj)
            {
                case IHasPathWithRepeats curveData:
                    return new JuiceStream
                    {
                        StartTime = obj.StartTime,
                        Path = curveData.Path,
                        RepeatCount = curveData.RepeatCount,
                        X = xPositionData?.X ?? 0,
                        Y = yPositionData?.Y ?? 0,
                        NewCombo = comboData?.NewCombo ?? false,
                        ComboOffset = comboData?.ComboOffset ?? 0,
                        LegacyConvertedY = yPositionData?.Y ?? CatchHitObject.DEFAULT_LEGACY_CONVERT_Y,
                        // prior to v8, speed multipliers don't adjust for how many ticks are generated over the same distance.
                        // this results in more (or less) ticks being generated in <v8 maps for the same time duration.
                        TickDistanceMultiplier = beatmap.BeatmapInfo.BeatmapVersion < 8 ? 1f / ((LegacyControlPointInfo)beatmap.ControlPointInfo).DifficultyPointAt(obj.StartTime).SliderVelocity : 1,
                        SliderVelocityMultiplier = sliderVelocityData?.SliderVelocityMultiplier ?? 1,
                        SliderVelocityAsBeatLength = sliderVelocityData?.SliderVelocityAsBeatLength ?? -100,
                        IsSelected = obj.IsSelected,
                    }.Yield();

                case IHasDuration endTime:
                    return new BananaShower
                    {
                        StartTime = obj.StartTime,
                        Duration = endTime.Duration,
                        NewCombo = comboData?.NewCombo ?? false,
                        ComboOffset = comboData?.ComboOffset ?? 0,
                        IsSelected = obj.IsSelected,
                    }.Yield();

                default:
                    return new Fruit
                    {
                        StartTime = obj.StartTime,
                        NewCombo = comboData?.NewCombo ?? false,
                        ComboOffset = comboData?.ComboOffset ?? 0,
                        X = xPositionData?.X ?? 0,
                        LegacyConvertedY = yPositionData?.Y ?? CatchHitObject.DEFAULT_LEGACY_CONVERT_Y,
                        IsSelected = obj.IsSelected,
                    }.Yield();
            }
        }

        protected override Beatmap<CatchHitObject> CreateBeatmap() => new CatchBeatmap();
    }
}
