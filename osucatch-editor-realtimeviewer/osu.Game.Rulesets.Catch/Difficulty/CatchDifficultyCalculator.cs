// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;using osu.Game.Rulesets.Catch.Difficulty.Skills;
using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Difficulty
{
    /// <summary>
    /// The osu!catch difficulty calculator.
    /// </summary>
    /// <remarks>
    /// The difficulty calculation in this viewer is performed per-hitobject (see
    /// <c>osucatch_editor_realtimeviewer.BeatmapConverter.CalDifficulty</c>) so that a star rating
    /// can be drawn beside each fruit, but it walks the exact same
    /// <see cref="CatchDifficultyHitObject"/> / <see cref="MovementEvaluator"/> / <see cref="Movement"/>
    /// pipeline as the official calculator below.
    /// </remarks>
    public class CatchDifficultyCalculator : DifficultyCalculator
    {
        private const double difficulty_multiplier = 4.59;

        /// <summary>
        /// osu!catch difficulty calculation version.
        /// </summary>
        /// <remarks>
        /// Version history:
        /// 20260706   osu!catch star rating update: movement difficulty split into
        ///            <see cref="CatchDifficultyHitObject"/> (preprocessing), <see cref="MovementEvaluator"/>
        ///            (evaluation) and <see cref="Movement"/> (skill), adding the linear spacing nerf and
        ///            the three-object buzz pattern detection.
        /// </remarks>
        public override int Version => 20260706;

        public CatchDifficultyCalculator(Ruleset ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            if (beatmap.HitObjects.Count == 0 || skills.Length == 0)
                return new CatchDifficultyAttributes { Mods = mods };

            CatchDifficultyAttributes attributes = new CatchDifficultyAttributes
            {
                StarRating = Math.Sqrt(skills.OfType<Movement>().Single().DifficultyValue()) * difficulty_multiplier,
                Mods = mods,
                MaxCombo = getMaxCombo(beatmap),
            };

            return attributes;
        }

        protected override List<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            CatchHitObject? lastObject = null;

            List<DifficultyHitObject> objects = new List<DifficultyHitObject>(beatmap.HitObjects.Count);

            float halfCatcherWidth = Catcher.CalculateCatchWidth(beatmap.Difficulty) * 0.5f;

            // For circle sizes above 5.5, reduce the catcher width further to simulate imperfect gameplay.
            halfCatcherWidth *= 1 - (Math.Max(0, beatmap.Difficulty.CircleSize - 5.5f) * 0.0625f);

            // In 2B beatmaps, it is possible that a normal Fruit is placed in the middle of a JuiceStream.
            foreach (var hitObject in CatchBeatmap.GetPalpableObjects(beatmap.HitObjects))
            {
                // We want to only consider fruits that contribute to the combo.
                if (hitObject is Banana || hitObject is TinyDroplet)
                    continue;

                if (lastObject != null)
                    objects.Add(new CatchDifficultyHitObject(hitObject, lastObject, clockRate, halfCatcherWidth, objects, objects.Count));

                lastObject = hitObject;
            }

            return objects;
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        {
            return new Skill[]
            {
                new Movement(mods),
            };
        }

        // This project's mod set only contains EZ / HR, neither of which adjusts the gameplay clock rate.
        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new CatchModEasy(),
            new CatchModHardRock(),
        };

        /// <summary>
        /// Returns the maximum achievable combo for the beatmap.
        /// </summary>
        /// <remarks>
        /// Only fruits and droplets contribute to the combo; bananas and tiny droplets do not.
        /// </remarks>
        private static int getMaxCombo(IBeatmap beatmap)
        {
            int combo = 0;

            foreach (var hitObject in CatchBeatmap.GetPalpableObjects(beatmap.HitObjects))
            {
                if (hitObject is Banana || hitObject is TinyDroplet)
                    continue;

                combo++;
            }

            return combo;
        }
    }
}
