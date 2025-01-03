﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Difficulty;
using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch
{
    public class CatchRuleset : Ruleset, ILegacyRuleset
    {
        public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap) => new CatchBeatmapConverter(beatmap, this);

        public override IBeatmapProcessor CreateBeatmapProcessor(IBeatmap beatmap) => new CatchBeatmapProcessor(beatmap);

        public const string SHORT_NAME = "fruits";

        public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;

        public override string Description => "osu!catch";

        public override string ShortName => SHORT_NAME;

        public override string PlayingVerb => "Catching fruit";


        public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new CatchDifficultyCalculator(this, beatmap);

        public int LegacyID => 2;


        /// <seealso cref="CatchHitObject.ApplyDefaultsToSelf"/>
        public override BeatmapDifficulty GetRateAdjustedDisplayDifficulty(IBeatmapDifficultyInfo difficulty, double rate)
        {
            BeatmapDifficulty adjustedDifficulty = new BeatmapDifficulty(difficulty);

            double preempt = IBeatmapDifficultyInfo.DifficultyRange(adjustedDifficulty.ApproachRate, CatchHitObject.PREEMPT_MAX, CatchHitObject.PREEMPT_MID, CatchHitObject.PREEMPT_MIN);
            preempt /= rate;
            adjustedDifficulty.ApproachRate = (float)IBeatmapDifficultyInfo.InverseDifficultyRange(preempt, CatchHitObject.PREEMPT_MAX, CatchHitObject.PREEMPT_MID, CatchHitObject.PREEMPT_MIN);

            return adjustedDifficulty;
        }

        public override bool EditorShowScrollSpeed => false;
    }
}
