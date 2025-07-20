using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Catch.Difficulty.Skills;
using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osucatch_editor_realtimeviewer
{

    public class BeatmapConverter
    {
        public static Ruleset catchRulest => new CatchRuleset();

        static Mod[] NoMod = Array.Empty<Mod>();

        static Mod[] EZMod = { new CatchModEasy().CreateInstance() };

        static Mod[] HRMod = { new CatchModHardRock().CreateInstance() };

        static Mod[] GetMods(string[] Mods, Ruleset ruleset)
        {
            if (Mods == null)
                return NoMod;

            foreach (var modString in Mods)
            {
                if (modString == "EZ") return EZMod;
                else if (modString == "HR") return HRMod;
            }

            return NoMod;
        }

        public static string[] GetModsString(int raw_mods)
        {
            List<string> modsArr = new List<string>();
            if ((raw_mods & 2) > 0) modsArr.Add("EZ");
            if ((raw_mods & 16) > 0) modsArr.Add("HR");
            return modsArr.ToArray();
        }

        public static IBeatmap Execute(Beatmap beatmap, Mod[] mods)
        {
            Log.ConsoleLog("Converting beatmap.", Log.LogType.BeatmapConverter, Log.LogLevel.Debug);
            FlatWorkingBeatmap workingBeatmap = new FlatWorkingBeatmap(beatmap);
            IBeatmap playableBeatmap = workingBeatmap.GetPlayableBeatmap(catchRulest, mods);
            if (playableBeatmap == null) throw new Exception("This beatmap is invalid or is not a ctb beatmap.");
            return playableBeatmap;
        }

        public static IBeatmap GetConvertedBeatmap(Beatmap beatmap, string[] mods)
        {
            Ruleset ruleset = catchRulest;
            return Execute(beatmap, GetMods(mods, ruleset));
        }

        public static IBeatmap GetConvertedBeatmap(Beatmap beatmap, int mods)
        {
            Ruleset ruleset = catchRulest;
            return Execute(beatmap, GetMods(GetModsString(mods), ruleset));
        }

        public static List<PalpableCatchHitObject> GetPalpableObjects(IBeatmap beatmap, HitObjectLabelType labelType)
        {
            Log.ConsoleLog("Building hitobjects.", Log.LogType.BeatmapConverter, Log.LogLevel.Debug);

            List<PalpableCatchHitObject> palpableObjects = new List<PalpableCatchHitObject>();

            foreach (var currentObject in beatmap.HitObjects)
            {
                if (currentObject is Fruit fruitObject)
                    palpableObjects.Add(fruitObject);

                else if (currentObject is JuiceStream)
                {
                    foreach (var juice in currentObject.NestedHitObjects)
                    {
                        if (juice is PalpableCatchHitObject palpableObject)
                            palpableObjects.Add(palpableObject);
                    }
                }
                else if (currentObject is BananaShower)
                {
                    foreach (var banana in currentObject.NestedHitObjects)
                    {
                        if (banana is PalpableCatchHitObject palpableObject)
                            palpableObjects.Add(palpableObject);
                    }
                }
            }

            palpableObjects.Sort((h1, h2) => h1.StartTime.CompareTo(h2.StartTime));

            List<PalpableCatchHitObject> comboHitObjects = new();
            // Build lastObject
            PalpableCatchHitObject? lastObject = null;
            int CurrentCombo = -1;
            // In 2B beatmaps, it is possible that a normal Fruit is placed in the middle of a JuiceStream.
            foreach (var hitObject in palpableObjects)
            {
                // We want to only consider fruits that contribute to the combo.
                if (hitObject is Banana || hitObject is TinyDroplet)
                    continue;

                if (lastObject != null)
                    hitObject.lastObject = lastObject;

                CurrentCombo++;
                hitObject.CurrentCombo = CurrentCombo;
                comboHitObjects.Add(hitObject);

                lastObject = hitObject;
            }

            // HitObject label
            switch (labelType)
            {
                // Calculate Distance
                case HitObjectLabelType.Distance_SameWithEditor:
                case HitObjectLabelType.Distance_NoSliderVelocityMultiplier:
                case HitObjectLabelType.Distance_CompareWithWalkSpeed:
                    {
                        CalDistance(beatmap, comboHitObjects);
                        break;
                    }

                case HitObjectLabelType.Difficulty_Stars:
                    {
                        beatmap.BeatmapInfo.StarRating = CalDifficulty(beatmap, comboHitObjects);
                        break;
                    }
                case HitObjectLabelType.FruitCountInCombo:
                    {
                        CalFruitCountInCombo(comboHitObjects);
                        break;
                    }
            }

            return palpableObjects;
        }

        private static void CalDistance(IBeatmap beatmap, List<PalpableCatchHitObject> comboHitObjects)
        {
            foreach (var currentObject in comboHitObjects)
            {
                CalDistanceToNext(beatmap, currentObject);
            }
        }

        public static void CalDistanceToNext(IBeatmap beatmap, PalpableCatchHitObject hitObject)
        {
            if (hitObject.lastObject == null) return;
            double timeToNext = (int)hitObject.StartTime - (int)hitObject.lastObject.StartTime;
            double distanceToNext = Math.Abs(hitObject.EffectiveX - hitObject.lastObject.EffectiveX);
            DifficultyControlPoint nextDifficultyControlPoint = (beatmap.ControlPointInfo as LegacyControlPointInfo)?.DifficultyPointAt(hitObject.StartTime) ?? DifficultyControlPoint.DEFAULT;
            var nextTimingPoint = beatmap.ControlPointInfo.TimingPointAt(hitObject.StartTime);
            if (timeToNext <= 0) return;
            hitObject.lastObject.XDistToNext_CompareWithWalkSpeed = distanceToNext / timeToNext / Catcher.BASE_WALK_SPEED;
            if (hitObject.lastObject.XDistToNext_CompareWithWalkSpeed <= 0 || hitObject.lastObject.XDistToNext_CompareWithWalkSpeed > 100)
            {
                hitObject.lastObject.XDistToNext_CompareWithWalkSpeed = 0;
                return;
            }
            if (beatmap.Difficulty.SliderMultiplier <= 0 || nextTimingPoint.BeatLength <= 0) return;
            hitObject.lastObject.XDistToNext_NoSliderVelocityMultiplier = distanceToNext / (beatmap.Difficulty.SliderMultiplier * 100) / (timeToNext / nextTimingPoint.BeatLength);
            if (hitObject.lastObject.XDistToNext_NoSliderVelocityMultiplier <= 0 || hitObject.lastObject.XDistToNext_NoSliderVelocityMultiplier > 100)
            {
                hitObject.lastObject.XDistToNext_NoSliderVelocityMultiplier = 0;
                return;
            }
            if (nextDifficultyControlPoint.SliderVelocity <= 0) return;
            hitObject.lastObject.XDistToNext_SameWithEditor = hitObject.lastObject.XDistToNext_NoSliderVelocityMultiplier / nextDifficultyControlPoint.SliderVelocity;
            if (hitObject.lastObject.XDistToNext_SameWithEditor <= 0 || hitObject.lastObject.XDistToNext_SameWithEditor > 100)
            {
                hitObject.lastObject.XDistToNext_SameWithEditor = 0;
                return;
            }

        }

        public static double CalDifficulty(IBeatmap beatmap, List<PalpableCatchHitObject> comboHitObjects)
        {
            const double difficulty_multiplier = 4.59;
            const float normalized_hitobject_radius = 41.0f;
            float halfCatcherWidth = Catcher.CalculateCatchWidth(beatmap.Difficulty) * 0.5f;
            // For circle sizes above 5.5, reduce the catcher width further to simulate imperfect gameplay.
            halfCatcherWidth *= 1 - (Math.Max(0, beatmap.Difficulty.CircleSize - 5.5f) * 0.0625f);
            // We will scale everything by this factor, so we can assume a uniform CircleSize among beatmaps.
            float scalingFactor = normalized_hitobject_radius / halfCatcherWidth;

            StrainSkill skill = new Movement(halfCatcherWidth, 1);

            for (int i = 0; i < comboHitObjects.Count; i++)
            {
                CalDifficultyToLast(skill, comboHitObjects[i], scalingFactor);
            }

            return Math.Sqrt(skill.DifficultyValue()) * difficulty_multiplier;
        }

        public static void CalDifficultyToLast(StrainSkill skill, PalpableCatchHitObject hitObject, float scalingFactor)
        {
            hitObject.NormalizedPosition = hitObject.EffectiveX * scalingFactor;
            hitObject.LastNormalizedPosition = (hitObject.lastObject == null) ? 0 : hitObject.lastObject.EffectiveX * scalingFactor;
            // Every strain interval is hard capped at the equivalent of 375 BPM streaming speed as a safety measure
            hitObject.DeltaTime = (hitObject.StartTime - ((hitObject.lastObject == null) ? 0 : hitObject.lastObject.StartTime));
            hitObject.StrainTime = Math.Max(40, hitObject.DeltaTime);
            skill.Process(hitObject);
        }

        private static void CalFruitCountInCombo(List<PalpableCatchHitObject> comboHitObjects)
        {
            int fruitCount = 0;
            int lastHitObjectComboIndex = -1;
            foreach (var currentObject in comboHitObjects)
            {
                if (currentObject is Fruit)
                {
                    if (currentObject.lastObject == null)
                    {
                        fruitCount++;
                        currentObject.FruitCountInCombo = fruitCount;
                        lastHitObjectComboIndex = currentObject.ComboIndex;
                        continue;
                    }

                    if (currentObject.ComboIndex == lastHitObjectComboIndex)
                    {
                        fruitCount++;
                        currentObject.FruitCountInCombo = fruitCount;
                    }
                    else
                    {
                        fruitCount = 1;
                        currentObject.FruitCountInCombo = fruitCount;
                        currentObject.lastObject.IsComboEnd = true;
                        lastHitObjectComboIndex = currentObject.ComboIndex;
                    }
                }
            }
        }
    }

    /// <summary>
    /// The type of shown label beside hitobject.
    /// </summary>
    public enum HitObjectLabelType
    {
        None,
        Distance_SameWithEditor,
        Distance_NoSliderVelocityMultiplier,
        Distance_CompareWithWalkSpeed,
        Difficulty_Stars,
        FruitCountInCombo,
    }

}
