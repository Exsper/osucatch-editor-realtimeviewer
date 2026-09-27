using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using System.Globalization;
using System.Text;

namespace osucatch_editor_realtimeviewer
{

    public partial class BeatmapConverterOsuStable : BeatmapConverter
    {
        /// <summary>
        /// 稳定版转换器不需要 lazer 生成的嵌套物件：<see cref="GetPalpableObjects"/> 里
        /// <see cref="HitObjectManagerCatch"/> 会按 osu!stable 的算法重建全部水滴/香蕉。
        /// 大滑条谱面上这一步是整个重建最贵的单项（例如 1.3 万控制点的滑条要几十毫秒）。
        ///
        /// <para>
        /// 例外：会介入 <see cref="osu.Game.Beatmaps.IBeatmapProcessor"/> 的 mod（目前是 HR，
        /// 见 <c>CatchModHardRock</c>）。<c>CatchBeatmapProcessor.ApplyPositionOffsets</c> 会按
        /// "先遍历各滑条的嵌套物件消费随机数、再给水果加 HardRock 偏移"的顺序使用同一个
        /// <c>LegacyRandom</c>，跳过嵌套物件会改变随机数序列、进而改变水果的 <c>XOffset</c>，
        /// 所以这类 mod 下仍然照旧生成。
        /// </para>
        /// </summary>
        protected override bool RequiresNestedHitObjects(Mod[] mods)
            => mods.Any(mod => mod is IApplicableToBeatmapProcessor);

        public override List<PalpableCatchHitObject> GetPalpableObjects(IBeatmap beatmap, int mods)
        {
            Log.ConsoleLog("Building hitobjects.", Log.LogType.BeatmapConverter, Log.LogLevel.Debug);
            HitObjectManagerCatch manager = new(beatmap, mods, false);

            int sourceIndex = 0;
            foreach (var currentObject in beatmap.HitObjects)
            {
                if (currentObject is Fruit fruitObject)
                {
                    manager.AddFruit(fruitObject, sourceIndex);
                }
                else if (currentObject is JuiceStream juiceStream)
                {
                    manager.AddJuiceStream(juiceStream, sourceIndex);
                }
                else if (currentObject is BananaShower bananaShower)
                {
                    manager.AddBananaShower(bananaShower, sourceIndex);
                }
                sourceIndex++;
            }

            return manager.GetPalpableObjects();
        }

        public string BuildConversionMapping(IBeatmap beatmap, int mods)
        {
            Log.ConsoleLog("Building hitobjects.", Log.LogType.BeatmapConverter, Log.LogLevel.Debug);

            List<(osu.Game.Rulesets.Objects.HitObject original, List<PalpableCatchHitObject> converted)> palpableObjects = new();
            HitObjectManagerCatch manager = new(beatmap, mods, true);

            int sourceIndex = 0;
            foreach (var currentObject in beatmap.HitObjects)
            {
                List<PalpableCatchHitObject> currentObjectConvert = new List<PalpableCatchHitObject>();
                if (currentObject is Fruit fruitObject)
                {
                    currentObjectConvert = manager.AddFruit(fruitObject, sourceIndex);
                }
                else if (currentObject is JuiceStream juiceStream)
                {
                    currentObjectConvert = manager.AddJuiceStream(juiceStream, sourceIndex);
                }
                else if (currentObject is BananaShower bananaShower)
                {
                    currentObjectConvert = manager.AddBananaShower(bananaShower, sourceIndex);
                }
                palpableObjects.Add(new(currentObject, currentObjectConvert));
                sourceIndex++;
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
