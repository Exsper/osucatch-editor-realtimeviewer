// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using OpenTK.Graphics;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Objects.Types;
using osucatch_editor_realtimeviewer;

namespace osu.Game.Beatmaps.Formats
{
    public class LegacyBeatmapDecoder : LegacyDecoder<Beatmap>
    {
        /// <summary>
        /// An offset which needs to be applied to old beatmaps (v4 and lower) to correct timing changes that were applied at a game client level.
        /// </summary>
        public const int EARLY_VERSION_TIMING_OFFSET = 24;

        /// <summary>
        /// A small adjustment to the start time of sample control points to account for rounding/precision errors.
        /// </summary>
        /// <remarks>
        /// Compare: https://github.com/peppy/osu-stable-reference/blob/master/osu!/GameplayElements/HitObjects/HitObject.cs#L319
        /// </remarks>
        public const double CONTROL_POINT_LENIENCY = 5;

        private Beatmap beatmap = null!;
        private ConvertHitObjectParser parser = null!;

        /// <summary>
        /// Whether beatmap or runtime offsets should be applied. Defaults on; only disable for testing purposes.
        /// </summary>
        public bool ApplyOffsets = true;

        private readonly int offset;

        public LegacyBeatmapDecoder(int version = LATEST_VERSION)
            : base(version)
        {

            offset = FormatVersion < 5 ? EARLY_VERSION_TIMING_OFFSET : 0;
        }

        protected override Beatmap CreateTemplateObject()
        {
            var templateBeatmap = base.CreateTemplateObject();
            templateBeatmap.ControlPointInfo = new LegacyControlPointInfo();
            return templateBeatmap;
        }

        protected override void ParseStreamInto(BeatmapInfoCollection thisReaderData, Beatmap beatmap, List<string>? colourLines)
        {
            this.beatmap = beatmap;
            this.beatmap.BeatmapInfo.BeatmapVersion = FormatVersion;
            parser = new ConvertHitObjectParser(getOffsetTime(), FormatVersion);

            // handle Difficulty
            var difficulty = beatmap.Difficulty;
            difficulty.ApproachRate = thisReaderData.ApproachRate;
            difficulty.DrainRate = thisReaderData.HPDrainRate;
            difficulty.CircleSize = thisReaderData.CircleSize;
            difficulty.OverallDifficulty = thisReaderData.OverallDifficulty;
            difficulty.SliderMultiplier = thisReaderData.SliderMultiplier;
            difficulty.SliderTickRate = thisReaderData.SliderTickRate;

            // handle TimingPoint
            thisReaderData.ControlPointLines.ForEach(line => handleTimingPoint(line));

            // handle HitObject
            thisReaderData.HitObjectLines.ForEach(lineWithSelect => handleHitObject(lineWithSelect.HitObjectLine, lineWithSelect.IsSelect));

            // handle Colours
            if (colourLines != null)
            {
                colourLines.ForEach(line => handleColours(line));
            }

            applyDifficultyRestrictions(beatmap.Difficulty, beatmap);

            flushPendingPoints();

            // Objects may be out of order *only* if a user has manually edited an .osu file.
            // Unfortunately there are ranked maps in this state (example: https://osu.ppy.sh/s/594828).
            // OrderBy is used to guarantee that the parsing order of hitobjects with equal start times is maintained (stably-sorted)
            // The parsing order of hitobjects matters in mania difficulty calculation
            this.beatmap.HitObjects = this.beatmap.HitObjects.OrderBy(h => h.StartTime).ToList();

            foreach (var hitObject in this.beatmap.HitObjects)
            {
                applyDefaults(hitObject);
            }
        }

        /// <summary>
        /// Ensures that all <see cref="BeatmapDifficulty"/> settings are within the allowed ranges.
        /// See also: https://github.com/peppy/osu-stable-reference/blob/0e425c0d525ef21353c8293c235cc0621d28338b/osu!/GameplayElements/Beatmaps/Beatmap.cs#L567-L614
        /// </summary>
        private static void applyDifficultyRestrictions(BeatmapDifficulty difficulty, Beatmap beatmap)
        {
            difficulty.DrainRate = Math.Clamp(difficulty.DrainRate, 0, 10);

            // mania uses "circle size" for key count, thus different allowable range
            difficulty.CircleSize = beatmap.BeatmapInfo.Ruleset.OnlineID != 3
                ? Math.Clamp(difficulty.CircleSize, 0, 10)
                : Math.Clamp(difficulty.CircleSize, 1, 18);

            difficulty.OverallDifficulty = Math.Clamp(difficulty.OverallDifficulty, 0, 10);
            difficulty.ApproachRate = Math.Clamp(difficulty.ApproachRate, 0, 10);

            difficulty.SliderMultiplier = Math.Clamp(difficulty.SliderMultiplier, 0.4, 3.6);
            difficulty.SliderTickRate = Math.Clamp(difficulty.SliderTickRate, 0.5, 8);
        }

        private void applyDefaults(HitObject hitObject)
        {
            DifficultyControlPoint difficultyControlPoint = (beatmap.ControlPointInfo as LegacyControlPointInfo)?.DifficultyPointAt(hitObject.StartTime) ?? DifficultyControlPoint.DEFAULT;

            if (hitObject is IHasGenerateTicks hasGenerateTicks)
                hasGenerateTicks.GenerateTicks = difficultyControlPoint.GenerateTicks;

            if (hitObject is IHasSliderVelocity hasSliderVelocity)
            {
                hasSliderVelocity.SliderVelocityMultiplier = difficultyControlPoint.SliderVelocity;
                hasSliderVelocity.SliderVelocityAsBeatLength = difficultyControlPoint.SliderVelocityAsBeatLength;
            }

            hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
        }

        private void handleTimingPoint(string line)
        {
            string[] split = line.Split(',');

            double time = getOffsetTime(Parsing.ParseDouble(split[0].Trim()));

            // beatLength is allowed to be NaN to handle an edge case in which some beatmaps use NaN slider velocity to disable slider tick generation (see LegacyDifficultyControlPoint).
            double beatLength = Parsing.ParseDouble(split[1].Trim(), allowNaN: true);

            // If beatLength is NaN, speedMultiplier should still be 1 because all comparisons against NaN are false.
            double speedMultiplier = beatLength < 0 ? 100.0 / -beatLength : 1;

            TimeSignature timeSignature = TimeSignature.SimpleQuadruple;
            if (split.Length >= 3)
                timeSignature = split[2][0] == '0' ? TimeSignature.SimpleQuadruple : new TimeSignature(Parsing.ParseInt(split[2]));

            bool timingChange = true;
            if (split.Length >= 7)
                timingChange = split[6][0] == '1';

            bool kiaiMode = false;
            bool omitFirstBarSignature = false;

            if (split.Length >= 8)
            {
                LegacyEffectFlags effectFlags = (LegacyEffectFlags)Parsing.ParseInt(split[7]);
                kiaiMode = effectFlags.HasFlag(LegacyEffectFlags.Kiai);
                omitFirstBarSignature = effectFlags.HasFlag(LegacyEffectFlags.OmitFirstBarLine);
            }

            if (timingChange)
            {
                if (double.IsNaN(beatLength))
                    throw new InvalidDataException("Beat length cannot be NaN in a timing control point");

                var controlPoint = CreateTimingControlPoint();

                controlPoint.BeatLength = beatLength;
                controlPoint.TimeSignature = timeSignature;
                controlPoint.OmitFirstBarLine = omitFirstBarSignature;

                addControlPoint(time, controlPoint, true);
            }

            int onlineRulesetID = beatmap.BeatmapInfo.Ruleset.OnlineID;

            addControlPoint(time, new DifficultyControlPoint
            {
                GenerateTicks = !double.IsNaN(beatLength),
                SliderVelocity = speedMultiplier,
                SliderVelocityAsBeatLength = beatLength
            }, timingChange);

            var effectPoint = new EffectControlPoint
            {
                KiaiMode = kiaiMode,
            };

            // osu!taiko and osu!mania use effect points rather than difficulty points for scroll speed adjustments.
            if (onlineRulesetID == 1 || onlineRulesetID == 3)
                effectPoint.ScrollSpeed = speedMultiplier;

            addControlPoint(time, effectPoint, timingChange);

        }

        private readonly List<ControlPoint> pendingControlPoints = new List<ControlPoint>();
        private readonly HashSet<Type> pendingControlPointTypes = new HashSet<Type>();
        private double pendingControlPointsTime;
        private void addControlPoint(double time, ControlPoint point, bool timingChange)
        {
            if (time != pendingControlPointsTime)
                flushPendingPoints();

            if (timingChange)
                pendingControlPoints.Insert(0, point);
            else
                pendingControlPoints.Add(point);

            pendingControlPointsTime = time;
        }

        private void flushPendingPoints()
        {
            // Changes from non-timing-points are added to the end of the list (see addControlPoint()) and should override any changes from timing-points (added to the start of the list).
            for (int i = pendingControlPoints.Count - 1; i >= 0; i--)
            {
                var type = pendingControlPoints[i].GetType();
                if (!pendingControlPointTypes.Add(type))
                    continue;

                beatmap.ControlPointInfo.Add(pendingControlPointsTime, pendingControlPoints[i]);
            }

            pendingControlPoints.Clear();
            pendingControlPointTypes.Clear();
        }

        private void handleHitObject(string line, bool isSelect = false)
        {
            var obj = parser.Parse(line);
            obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
            obj.IsSelected = isSelect;

            beatmap.HitObjects.Add(obj);
        }

        private Color4 convertSettingStringToColor4(string[] split, bool allowAlpha, KeyValuePair<string, string> pair)
        {
            if (split.Length != 3 && split.Length != 4)
                throw new InvalidOperationException($@"Color specified in incorrect format (should be R,G,B or R,G,B,A): {pair.Value}");

            Color4 colour;

            try
            {
                byte alpha = allowAlpha && split.Length == 4 ? byte.Parse(split[3]) : (byte)255;
                colour = new Color4(byte.Parse(split[0]), byte.Parse(split[1]), byte.Parse(split[2]), alpha);
            }
            catch
            {
                throw new InvalidOperationException(@"Color must be specified with 8-bit integer components");
            }

            return colour;
        }

        private void handleColours(string line)
        {
            var pair = SplitKeyVal(line);

            string[] split = pair.Value.Split(',');
            Color4 colour = convertSettingStringToColor4(split, false, pair);

            bool isCombo = pair.Key.StartsWith(@"Combo", StringComparison.Ordinal);

            if (isCombo)
            {
                beatmap.CustomComboColours.Add(colour);
            }
        }

        private int getOffsetTime(int time) => time + (ApplyOffsets ? offset : 0);

        private double getOffsetTime() => ApplyOffsets ? offset : 0;

        private double getOffsetTime(double time) => time + (ApplyOffsets ? offset : 0);

        protected virtual TimingControlPoint CreateTimingControlPoint() => new TimingControlPoint();
    }
}
