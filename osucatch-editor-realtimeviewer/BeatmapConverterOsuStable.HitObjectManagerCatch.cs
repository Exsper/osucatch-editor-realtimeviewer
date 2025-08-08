using System.Net.Quic;
using System.Runtime.InteropServices;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Utils;

namespace osucatch_editor_realtimeviewer
{
    public partial class BeatmapConverterOsuStable
    {
        private class HitObjectManagerCatch
        {
            private LegacyRandom random = new(1337);
            private List<PalpableCatchHitObject> palpableObjects = new();
            private bool isHardRock;
            private IBeatmap beatmap;

            private float lastStartX;
            private int lastStartTime;

            public HitObjectManagerCatch(IBeatmap beatmap, int mods)
            {
                isHardRock = mods == (1 << 4);
                this.beatmap = beatmap;
            }

            [DllImport("StableCompatLib.dll", EntryPoint = "randomNextCalc")]
            private static extern int RandomNextStableCompat0(int value, int lowerBound, int upperBound);

            private int RandomNextStableCompat(int lowerBound, int upperBound) => RandomNextStableCompat0(random.Next(), lowerBound, upperBound);
            
            internal List<PalpableCatchHitObject> AddFruit(Fruit fruit)
            {
                if (isHardRock)
                {
                    fruit = WarpSpacing(fruit);
                }
                palpableObjects.Add(fruit);
                return [fruit];
            }

            private Fruit WarpSpacing(Fruit fruit)
            {
                if (lastStartX == 0)
                {
                    lastStartX = fruit.OriginalX;
                    lastStartTime = (int) fruit.StartTime;
                    return fruit;
                }

                float diff = lastStartX - fruit.OriginalX;
                int timeDiff = (int) fruit.StartTime - lastStartTime;

                if (timeDiff > 1000)
                {
                    lastStartX = fruit.OriginalX;
                    lastStartTime = (int)fruit.StartTime;
                    return fruit;
                }

                if (diff == 0)
                {
                    bool right = random.NextBool();
                    float rand = Math.Min(20, RandomNextStableCompat(0, timeDiff / 4));
                    float x = fruit.OriginalX;
                    if (right)
                    {
                        if (x + rand <= 512)
                            x += rand;
                        else
                            x -= rand;
                    }
                    else
                    {
                        if (x - rand >= 0)
                            x -= rand;
                        else
                            x += rand;
                    }
                    return new Fruit
                    {
                        StartTime = fruit.StartTime,
                        X = x,
                        ComboIndex = fruit.ComboIndex,
                        IsSelected = fruit.IsSelected
                    };
                }

                {
                    float x = fruit.OriginalX;
                    if (Math.Abs(diff) < timeDiff / 3)
                    {
                        if (diff > 0)
                        {
                            if (x - diff > 0)
                                x -= diff;
                        }
                        else
                        {
                            if (x - diff < 512)
                                x -= diff;
                        }
                    }

                    lastStartX = x;
                    lastStartTime = (int)fruit.StartTime;
                    return new Fruit
                    {
                        StartTime = fruit.StartTime,
                        X = x,
                        ComboIndex = fruit.ComboIndex,
                        IsSelected = fruit.IsSelected
                    };
                }
            }

            internal List<PalpableCatchHitObject> AddJuiceStream(JuiceStream juiceStream)
            {
                var hitObjects = ConvertSlider(beatmap, juiceStream, out LegacySliderAdditionalData data);

                lastStartX = juiceStream.OriginalX + juiceStream.Path.ControlPoints.Last().Position.X;
                lastStartTime = data.StartTime;

                foreach (var juice in hitObjects)
                {
                    if (juice is PalpableCatchHitObject palpableObject)
                    {
                        juice.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
                        palpableObjects.Add(palpableObject);
                    }
                }
                return hitObjects;
            }

            internal List<PalpableCatchHitObject> AddBananaShower(BananaShower bananaShower)
            {
                var hitObjects = ConvertSpinner(beatmap, bananaShower);
                foreach (var banana in hitObjects)
                {
                    if (banana is PalpableCatchHitObject palpableObject)
                    {
                        banana.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
                        palpableObjects.Add(palpableObject);
                    }
                }
                return hitObjects;
            }

            private void initaliseHyperDash(float catcherWidth)
            {
                initialiseHyperDash(catcherWidth, palpableObjects);
            }

            public List<PalpableCatchHitObject> ConvertSlider(IBeatmap beatmap, JuiceStream juiceStream, out LegacySliderAdditionalData sliderData)
            {

                List<PalpableCatchHitObject> palpableHitObjects = new();

                palpableHitObjects.Add(new Fruit
                {
                    StartTime = juiceStream.StartTime,
                    X = juiceStream.OriginalX,
                    ComboIndex = juiceStream.ComboIndex,
                    IsSelected = juiceStream.IsSelected
                });

                sliderData = new(beatmap, juiceStream);

                int lastTime = (int)juiceStream.StartTime;

                int fruitIndex = 1;
                for (int i = 0; i < sliderData.SliderScoreTimingPoints.Count; i++)
                {
                    int time = sliderData.SliderScoreTimingPoints[i];

                    if (time - lastTime > 80)
                    {
                        float var = (time - lastTime);
                        while (var > 100)
                            var /= 2;

                        for (float j = lastTime + var; j < time; j += var)
                        {
                            TinyDroplet tinyDroplet = new TinyDroplet
                            {
                                StartTime = (int)j,
                                X = sliderData.GetPositionByTime((int)j).X + RandomNextStableCompat(-20, 20),
                                ComboIndex = juiceStream.ComboIndex,
                                IsSelected = juiceStream.IsSelected
                            };
                            palpableHitObjects.Add(tinyDroplet);
                        }
                    }

                    lastTime = time;

                    if (i < sliderData.SliderScoreTimingPoints.Count - 1)
                    {
                        int repeatLocation = sliderData.SliderRepeatPoints.BinarySearch(time);

                        if (repeatLocation >= 0)
                        {
                            palpableHitObjects.Add(new Fruit
                            {
                                StartTime = time,
                                X = repeatLocation % 2 == 1 ? juiceStream.OriginalX : sliderData.Position2.X,
                                ComboIndex = juiceStream.ComboIndex,
                                IsSelected = juiceStream.IsSelected
                            });
                            fruitIndex++;
                        }
                        else
                        {
                            random.Next();
                            palpableHitObjects.Add(new Droplet
                            {
                                StartTime = time,
                                X = sliderData.GetPositionByTime(time).X,
                                ComboIndex = juiceStream.ComboIndex,
                                IsSelected = juiceStream.IsSelected
                            });
                        }
                    }
                }

                palpableHitObjects.Add(new Fruit
                {
                    StartTime = sliderData.EndTime,
                    X = sliderData.EndPosition.X,
                    ComboIndex = juiceStream.ComboIndex,
                    IsSelected = juiceStream.IsSelected
                });

                return palpableHitObjects;
            }

            public List<PalpableCatchHitObject> ConvertSpinner(IBeatmap beatmap, BananaShower bananaShower)
            {
                List<PalpableCatchHitObject> palpableHitObjects = new List<PalpableCatchHitObject>();

                float interval = (int)bananaShower.EndTime - (int)bananaShower.StartTime;
                while (interval > 100)
                    interval /= 2;

                if (interval <= 0)
                {
                    return palpableHitObjects;
                }

                int count = 0;
                for (float currentTime = (int)bananaShower.StartTime; currentTime <= (int)bananaShower.EndTime; currentTime += interval)
                {
                    palpableHitObjects.Add(new Banana
                    {
                        StartTime = (int)currentTime,
                        OriginalX = RandomNextStableCompat(0, 512),
                        BananaIndex = count,
                        IsSelected = bananaShower.IsSelected
                    });
                    random.Next();
                    random.Next();
                    random.Next();
                    count++;
                }
                return palpableHitObjects;
            }

            public List<PalpableCatchHitObject> GetPalpableObjects()
            {
                // The precise value of catcherWidth in stable depends on window resolution due to floating-point errors.
                // Here we just use a simplified formula so catcherWidth only depends on beatmap CircleSize.
                initaliseHyperDash((float)(106.75f * (1.7f - 0.14f * beatmap.Difficulty.CircleSize)));
                return palpableObjects;
            }
        }

    }
}
