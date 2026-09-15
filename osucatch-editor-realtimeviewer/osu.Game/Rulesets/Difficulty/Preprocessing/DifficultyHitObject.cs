// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Difficulty.Preprocessing
{
    /// <summary>
    /// Wraps a <see cref="HitObject"/> and provides additional information to be used for difficulty calculation.
    /// </summary>
    /// <remarks>
    /// Ported from osu! (ppy/osu) for the embedded difficulty calculation framework.
    /// The upstream hit window / <c>HitResult</c> members are intentionally omitted, as this project
    /// only embeds the parts of osu! required for osu!catch star rating calculation.
    /// </remarks>
    public class DifficultyHitObject
    {
        private readonly IReadOnlyList<DifficultyHitObject> difficultyHitObjects;

        /// <summary>
        /// The index of this <see cref="DifficultyHitObject"/> in the list of all <see cref="DifficultyHitObject"/>s.
        /// </summary>
        public int Index;

        /// <summary>
        /// The <see cref="HitObject"/> this <see cref="DifficultyHitObject"/> wraps.
        /// </summary>
        public readonly HitObject BaseObject;

        /// <summary>
        /// The last <see cref="HitObject"/> which occurs before <see cref="BaseObject"/>.
        /// </summary>
        public readonly HitObject LastObject;

        /// <summary>
        /// Amount of time elapsed between <see cref="BaseObject"/> and <see cref="LastObject"/>, adjusted by clockrate.
        /// </summary>
        public readonly double DeltaTime;

        /// <summary>
        /// Clockrate adjusted start time of <see cref="BaseObject"/>.
        /// </summary>
        public readonly double StartTime;

        /// <summary>
        /// Clockrate adjusted end time of <see cref="BaseObject"/>.
        /// </summary>
        public readonly double EndTime;

        /// <summary>
        /// Beatmap playback rate.
        /// </summary>
        public readonly double ClockRate;

        /// <summary>
        /// The star rating contribution of <see cref="BaseObject"/>, as calculated by the strain skill which processed it.
        /// </summary>
        /// <remarks>
        /// This project-specific extra is used to draw the "difficulty stars" label beside hitobjects,
        /// and is not part of the upstream osu! difficulty framework.
        /// </remarks>
        public double DifficultyToLast;

        /// <summary>
        /// Creates a new <see cref="DifficultyHitObject"/>.
        /// </summary>
        /// <param name="hitObject">The <see cref="HitObject"/> which this <see cref="DifficultyHitObject"/> wraps.</param>
        /// <param name="lastObject">The last <see cref="HitObject"/> which occurs before <paramref name="hitObject"/> in the beatmap.</param>
        /// <param name="clockRate">The rate at which the gameplay clock is run at.</param>
        /// <param name="objects">The list of <see cref="DifficultyHitObject"/>s in the current beatmap.</param>
        /// <param name="index">The index of this <see cref="DifficultyHitObject"/> in <paramref name="objects"/> list.</param>
        public DifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, int index)
        {
            difficultyHitObjects = objects;
            Index = index;
            BaseObject = hitObject;
            LastObject = lastObject;
            DeltaTime = (hitObject.StartTime - lastObject.StartTime) / clockRate;
            StartTime = hitObject.StartTime / clockRate;
            EndTime = hitObject.GetEndTime() / clockRate;
            ClockRate = clockRate;
        }

        public DifficultyHitObject Previous(int skipCount = 0)
        {
            int index = Index - (skipCount + 1);
            return index >= 0 && index < difficultyHitObjects.Count ? difficultyHitObjects[index] : null;
        }

        public DifficultyHitObject Next(int skipCount = 0)
        {
            int index = Index + (skipCount + 1);
            return index >= 0 && index < difficultyHitObjects.Count ? difficultyHitObjects[index] : null;
        }
    }
}
