// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Rulesets.Objects.Legacy
{
    public static class LegacyRulesetExtensions
    {
        /// <summary>
        /// Introduces floating-point errors to post-multiplied beat length for legacy rulesets that depend on it.
        /// You should definitely not use this unless you know exactly what you're doing.
        /// </summary>
        public static double GetPrecisionAdjustedBeatLength(IHasSliderVelocity hasSliderVelocity, TimingControlPoint timingControlPoint)
        {
            double sliderVelocityAsBeatLength = -100 / hasSliderVelocity.SliderVelocityMultiplier;

            // Note: In stable, the division occurs on floats, but with compiler optimisations turned on actually seems to occur on doubles via some .NET black magic (possibly inlining?).
            double bpmMultiplier = sliderVelocityAsBeatLength < 0 ? Math.Clamp((float)-sliderVelocityAsBeatLength, 10, 1000) / 100.0 : 1;

            return timingControlPoint.BeatLength * bpmMultiplier;
        }

        /// <summary>
        /// Calculates scale from a CS value, with an optional fudge that was historically applied to the osu! ruleset.
        /// </summary>
        public static float CalculateScaleFromCircleSize(float circleSize, bool applyFudge = false)
        {
            // The following comment is copied verbatim from osu-stable:
            //
            //   Builds of osu! up to 2013-05-04 had the gamefield being rounded down, which caused incorrect radius calculations
            //   in widescreen cases. This ratio adjusts to allow for old replays to work post-fix, which in turn increases the lenience
            //   for all plays, but by an amount so small it should only be effective in replays.
            //
            // To match expectations of gameplay we need to apply this multiplier to circle scale. It's weird but is what it is.
            // It works out to under 1 game pixel and is generally not meaningful to gameplay, but is to replay playback accuracy.
            const float broken_gamefield_rounding_allowance = 1.00041f;

            return (float)(1.0f - 0.7f * IBeatmapDifficultyInfo.DifficultyRange(circleSize)) / 2 * (applyFudge ? broken_gamefield_rounding_allowance : 1);
        }
    }
}
