using System;
using System.Globalization;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Quic;
using System.Text;
using Editor_Reader;
using OpenTK.Graphics.OpenGL;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Mods;

namespace osucatch_editor_realtimeviewer
{

    public partial class BeatmapConverterOsuStable : BeatmapConverter
    {

        public override List<PalpableCatchHitObject> GetPalpableObjects(IBeatmap beatmap, int mods)
        {
            Log.ConsoleLog("Building hitobjects.", Log.LogType.BeatmapConverter, Log.LogLevel.Debug);
            HitObjectManagerCatch manager = new(beatmap, mods);

            float? lastPosition = null;
            double lastStartTime = 0;
            bool isFirstSame = true;

            foreach (var currentObject in beatmap.HitObjects)
            {
                if (currentObject is Fruit fruitObject)
                {
                    manager.AddFruit(fruitObject);
                }
                else if (currentObject is JuiceStream juiceStream)
                {
                    manager.AddJuiceStream(juiceStream);
                }
                else if (currentObject is BananaShower bananaShower)
                {
                    manager.AddBananaShower(bananaShower);
                }
            }

            return manager.GetPalpableObjects();
        }

        public string BuildConversionMapping(IBeatmap beatmap, int mods)
        {
            Log.ConsoleLog("Building hitobjects.", Log.LogType.BeatmapConverter, Log.LogLevel.Debug);

            List<(osu.Game.Rulesets.Objects.HitObject original, List<PalpableCatchHitObject> converted)> palpableObjects = new();
            HitObjectManagerCatch manager = new(beatmap, mods);

            foreach (var currentObject in beatmap.HitObjects)
            {
                List<PalpableCatchHitObject> currentObjectConvert = new List<PalpableCatchHitObject>();
                if (currentObject is Fruit fruitObject)
                {
                    currentObjectConvert = manager.AddFruit(fruitObject);
                }
                else if (currentObject is JuiceStream juiceStream)
                {
                    currentObjectConvert = manager.AddJuiceStream(juiceStream);
                }
                else if (currentObject is BananaShower bananaShower)
                {
                    currentObjectConvert = manager.AddBananaShower(bananaShower);
                }
                palpableObjects.Add(new(currentObject, currentObjectConvert));
            }

            palpableObjects.Sort((h1, h2) => h1.original.StartTime.CompareTo(h2.original.StartTime));

            manager.GetPalpableObjects();

            StringBuilder conversionMapping = new StringBuilder();
            conversionMapping.AppendLine("{");
            conversionMapping.AppendLine("""    "Mappings": [""");
            conversionMapping.AppendJoin(",\n", palpableObjects.Select(objectConvert =>
            {
                string subObjectString = string.Join(",\n", objectConvert.converted.Select(hitObject => $$"""
                                {
                                    "StartTime": {{doubleToString(hitObject.StartTime)}},
                                    "Position": {{floatToString(hitObject.EffectiveX)}},
                                    "#=zvnXjJz7N45MN": {{(hitObject.HyperDash ? "true" : "false")}}
                                }
                """));
                return $$"""
                        {
                            "StartTime": {{doubleToString(objectConvert.original.StartTime)}},
                            "Objects": [
                {{subObjectString}}
                            ]
                        }
                """;
            }));
            conversionMapping.AppendLine();
            conversionMapping.AppendLine("    ]");
            conversionMapping.AppendLine("}");
            return conversionMapping.ToString();
        }

        private static string doubleToString(double value)
        {
            string current = value.ToString("G17", CultureInfo.InvariantCulture);
            if (!double.IsNaN(value) && !double.IsInfinity(value) && current.IndexOf('.') == -1 && current.IndexOf('E') == -1 && current.IndexOf('e') == -1)
            {
                current += ".0";
            }
            return current;
        }

        private static string floatToString(float value)
        {
            string current = value.ToString("G9", CultureInfo.InvariantCulture); ;
            if (!float.IsNaN(value) && !float.IsInfinity(value) && current.IndexOf('.') == -1 && current.IndexOf('E') == -1 && current.IndexOf('e') == -1)
            {
                current += ".0";
            }
            return current;
        }

        private static void initialiseHyperDash(float catcherWidth, List<PalpableCatchHitObject> hitObjects)
        {
            hitObjects.Sort((h1, h2) => h1.StartTime.CompareTo(h2.StartTime));
            var palpableObjects = CatchBeatmap.GetPalpableObjects(hitObjects)
                                              .Where(h => h is Fruit || (h is Droplet && h is not TinyDroplet))
                                              .ToArray();

            float halfCatcherWidth = catcherWidth / 2;

            int lastDirection = 0;
            float lastExcess = halfCatcherWidth;

            for (int i = 0; i < palpableObjects.Length - 1; i++)
            {
                var currentObject = palpableObjects[i];
                var nextObject = palpableObjects[i + 1];

                // Reset variables in-case values have changed (e.g. after applying HR)
                currentObject.HyperDashTarget = null;
                currentObject.DistanceToHyperDash = 0;

                int thisDirection = nextObject.EffectiveX > currentObject.EffectiveX ? 1 : -1;

                // Int truncation added to match osu!stable.
                float timeToNext = (int)nextObject.StartTime - (int)currentObject.StartTime - 1000f / 60f / 4; // 1/4th of a frame of grace time, taken from osu-stable
                float distanceToNext = Math.Abs(nextObject.EffectiveX - currentObject.EffectiveX) - (lastDirection == thisDirection ? lastExcess : halfCatcherWidth);
                float distanceToHyper = timeToNext - distanceToNext;

                if (timeToNext < distanceToNext)
                {
                    currentObject.HyperDashTarget = nextObject;
                    lastExcess = halfCatcherWidth;
                }
                else
                {
                    currentObject.DistanceToHyperDash = distanceToHyper;
                    lastExcess = Math.Clamp(distanceToHyper, 0, halfCatcherWidth);
                }

                lastDirection = thisDirection;
            }
        }

    }
}
