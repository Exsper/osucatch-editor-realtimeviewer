// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.



namespace osu.Game.Beatmaps.ControlPoints
{
    /// <remarks>
    /// Note that going forward, this control point type should always be assigned directly to HitObjects.
    /// </remarks>
    public class DifficultyControlPoint : ControlPoint, IEquatable<DifficultyControlPoint>
    {
        public static readonly DifficultyControlPoint DEFAULT = new DifficultyControlPoint
        {
        };

        /// <summary>
        /// Whether or not slider ticks should be generated at this control point.
        /// This exists for backwards compatibility with maps that abuse NaN slider velocity behavior on osu!stable (e.g. /b/2628991).
        /// </summary>
        public bool GenerateTicks { get; set; } = true;

        private double sliderVelocity = 1;

        /// <summary>
        /// This field is added to ensure backward compability for osu!stable.
        /// This field doesn't exist in current osu!lazer.
        /// </summary>
        private double sliderVelocityAsBeatLength = -100;

        /// <summary>
        /// The slider velocity at this control point.
        /// </summary>
        public double SliderVelocity
        {
            get => sliderVelocity;
            set => sliderVelocity = Math.Clamp(value, 0.1, 10);
        }
        public double SliderVelocityAsBeatLength
        {
            get => sliderVelocityAsBeatLength;
            set => sliderVelocityAsBeatLength = Math.Clamp(value, -1000, -10);
        }

        public DifficultyControlPoint()
        {
        }

        public override bool IsRedundant(ControlPoint? existing)
            => existing is DifficultyControlPoint existingDifficulty
               && GenerateTicks == existingDifficulty.GenerateTicks
               && SliderVelocity == existingDifficulty.SliderVelocity;

        public override void CopyFrom(ControlPoint other)
        {
            SliderVelocity = ((DifficultyControlPoint)other).SliderVelocity;
            GenerateTicks = ((DifficultyControlPoint)other).GenerateTicks;

            base.CopyFrom(other);
        }

        public override bool Equals(ControlPoint? other)
            => other is DifficultyControlPoint otherDifficultyControlPoint
               && Equals(otherDifficultyControlPoint);

        public bool Equals(DifficultyControlPoint? other)
            => base.Equals(other)
               && GenerateTicks == other.GenerateTicks
               && SliderVelocity == other.SliderVelocity;

        // ReSharper disable once NonReadonlyMemberInGetHashCode
        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), SliderVelocity, GenerateTicks);
    }
}
