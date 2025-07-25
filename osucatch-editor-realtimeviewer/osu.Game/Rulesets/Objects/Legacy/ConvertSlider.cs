// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Rulesets.Objects.Legacy
{
    /// <summary>
    /// Legacy "Slider" hit object type.
    /// </summary>
    /// <remarks>
    /// Only used for parsing beatmaps and not gameplay.
    /// </remarks>
    internal class ConvertSlider : ConvertHitObject, IHasPathWithRepeats, IHasSliderVelocity, IHasGenerateTicks
    {
        /// <summary>
        /// Scoring distance with a speed-adjusted beat length of 1 second.
        /// </summary>
        private const float base_scoring_distance = 100;

        /// <summary>
        /// <see cref="ConvertSlider"/>s don't need a curve since they're converted to ruleset-specific hitobjects.
        /// </summary>
        public SliderPath Path { get; set; } = null!;

        public double Distance => Path.Distance;

        public int RepeatCount { get; set; }


        public double Duration
        {
            get => this.SpanCount() * Distance / Velocity;
            set => throw new System.NotSupportedException($"Adjust via {nameof(RepeatCount)} instead"); // can be implemented if/when needed.
        }

        public double EndTime => StartTime + Duration;

        public double Velocity = 1;

        public double sliderVelocityMultiplier = 1;

        public double SliderVelocityMultiplier
        {
            get => sliderVelocityMultiplier;
            set => sliderVelocityMultiplier = value;
        }

        private double sliderVelocityAsBeatLength = -100;

        public double SliderVelocityAsBeatLength
        {
            get => sliderVelocityAsBeatLength;
            set => sliderVelocityAsBeatLength = Math.Clamp(value, -1000, -10);
        }

        public bool GenerateTicks { get; set; } = true;

        public ConvertSlider()
        {
            LegacyType = LegacyHitObjectType.Slider;
        }

        protected override void ApplyDefaultsToSelf(ControlPointInfo controlPointInfo, IBeatmapDifficultyInfo difficulty)
        {
            base.ApplyDefaultsToSelf(controlPointInfo, difficulty);

            TimingControlPoint timingPoint = controlPointInfo.TimingPointAt(StartTime);

            double scoringDistance = base_scoring_distance * difficulty.SliderMultiplier * SliderVelocityMultiplier;

            Velocity = scoringDistance / timingPoint.BeatLength;
        }
    }
}
