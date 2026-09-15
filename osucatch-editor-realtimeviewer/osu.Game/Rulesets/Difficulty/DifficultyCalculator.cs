// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Difficulty
{
    /// <summary>
    /// A beatmap difficulty calculator.
    /// </summary>
    public abstract class DifficultyCalculator
    {
        /// <summary>
        /// The beatmap for which difficulty is being calculated.
        /// </summary>
        protected readonly IBeatmap Beatmap;

        private readonly Mod[] mods;

        protected DifficultyCalculator(Ruleset ruleset, IWorkingBeatmap beatmap)
        {
            mods = Array.Empty<Mod>();

            Beatmap = beatmap.GetPlayableBeatmap(ruleset, mods);
        }

        /// <summary>
        /// The difficulty calculation version.
        /// </summary>
        /// <remarks>
        /// This is used to determine which difficulty values are outdated and need to be recalculated.
        /// </remarks>
        public abstract int Version { get; }

        /// <summary>
        /// Mods for which the difficulty calculation is allowed to be customised by.
        /// </summary>
        protected abstract Mod[] DifficultyAdjustmentMods { get; }

        /// <summary>
        /// Calculates the difficulty of the beatmap using all mod combinations applicable to the beatmap.
        /// </summary>
        /// <returns>A collection of difficulty attributes.</returns>
        public DifficultyAttributes Calculate()
        {
            return Calculate(mods);
        }

        /// <summary>
        /// Calculates the difficulty of the beatmap using a specific mod combination.
        /// </summary>
        /// <param name="mods">The mods that should be applied to the beatmap.</param>
        /// <returns>A structure describing the difficulty of the beatmap.</returns>
        public DifficultyAttributes Calculate(Mod[] mods)
        {
            return ComputeDifficultyAttributes(mods);
        }

        /// <summary>
        /// Calculates the difficulty of the beatmap using all mod combinations applicable to the beatmap.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A collection of difficulty attributes.</returns>
        public IEnumerable<DifficultyAttributes> CalculateAll(CancellationToken cancellationToken = default)
        {
            foreach (var combination in CreateDifficultyAdjustmentModCombinations())
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException();

                yield return Calculate(combination);
            }
        }

        /// <summary>
        /// Computes the difficulty attributes for the beatmap.
        /// </summary>
        /// <param name="mods">The mods to apply.</param>
        /// <returns>The difficulty attributes.</returns>
        private DifficultyAttributes ComputeDifficultyAttributes(Mod[] mods)
        {
            IBeatmap beatmap = Beatmap.Clone();

            foreach (var mod in mods.OfType<IApplicableToDifficulty>())
                mod.ApplyToDifficulty(beatmap.Difficulty);

            // This embedded port of the difficulty framework only supports osu!catch, whose
            // difficulty adjustment mods (EZ / HR) never change the gameplay clock rate.
            const double clock_rate = 1.0;

            if (beatmap.HitObjects.Count == 0)
                return CreateDifficultyAttributes(beatmap, mods, Array.Empty<Skill>());

            var difficultyHitObjects = CreateDifficultyHitObjects(beatmap, clock_rate);
            var skills = CreateSkills(beatmap, mods);

            if (difficultyHitObjects.Count > 0)
            {
                foreach (var skill in skills)
                {
                    foreach (var difficultyHitObject in difficultyHitObjects)
                        skill.Process(difficultyHitObject);
                }
            }

            return CreateDifficultyAttributes(beatmap, mods, skills);
        }

        /// <summary>
        /// Creates all <see cref="Mod"/> combinations which customise the difficulty calculation.
        /// </summary>
        /// <remarks>
        /// All combinations will contain at least one mod from <see cref="DifficultyAdjustmentMods"/>.
        /// </remarks>
        public IEnumerable<Mod[]> CreateDifficultyAdjustmentModCombinations()
        {
            Mod[] adjustmentMods = DifficultyAdjustmentMods;

            if (adjustmentMods.Length == 0)
                yield break;

            // Generate all non-empty subsets of the adjustment mods (2^n - 1 combinations).
            for (int i = 1; i < 1 << adjustmentMods.Length; i++)
            {
                var combination = new List<Mod>();

                for (int j = 0; j < adjustmentMods.Length; j++)
                {
                    if ((i & (1 << j)) > 0)
                        combination.Add(adjustmentMods[j]);
                }

                yield return combination.ToArray();
            }
        }

        /// <summary>
        /// Creates the <see cref="DifficultyAttributes"/> to be returned for a beatmap.
        /// </summary>
        /// <param name="beatmap">The beatmap to create the attributes for.</param>
        /// <param name="mods">The mods to apply.</param>
        /// <param name="skills">The skills which processed the beatmap.</param>
        /// <returns>The difficulty attributes.</returns>
        protected abstract DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills);

        /// <summary>
        /// Creates the <see cref="DifficultyHitObject"/>s to be processed.
        /// </summary>
        /// <param name="beatmap">The beatmap to create the objects for.</param>
        /// <param name="clockRate">The rate at which the gameplay clock is run at.</param>
        /// <returns>The difficulty hit objects.</returns>
        protected abstract List<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate);

        /// <summary>
        /// Creates the <see cref="Skill"/>s to process the <see cref="DifficultyHitObject"/>s with.
        /// </summary>
        /// <param name="beatmap">The beatmap to create the skills for.</param>
        /// <param name="mods">The mods to apply.</param>
        /// <returns>The skills.</returns>
        protected abstract Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods);
    }
}
