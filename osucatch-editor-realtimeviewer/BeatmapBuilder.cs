using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using System.Text;
using System.Text.RegularExpressions;

namespace osucatch_editor_realtimeviewer
{
    public static class BeatmapBuilder
    {
        private static Decoder<Beatmap> beatmapDecoder => new LegacyBeatmapDecoder();

        #region Build beatmap file (for backup)

        public static string BuildNewBeatmapFileFromFilepath(string orgpath, BeatmapInfoCollection thisReaderData)
        {
            using (StreamReader file = File.OpenText(orgpath))
            {
                return BuildNewBeatmapFile(file, thisReaderData);
            }
        }

        private static string BuildNewBeatmapFile(StreamReader file, BeatmapInfoCollection thisReaderData)
        {
            StringBuilder newfile = new StringBuilder();
            string? line;
            bool isMultiLine = false;
            bool hasTiming = false;
            while ((line = file.ReadLine()) != null)
            {
                if (!line.StartsWith("Tags") && line.Length > 1000)
                {
                    // Known bug: ":0|0" repeat
                    if (line.Length > 10000 && line.IndexOf(":0|0:0|0:0|0:0|0:0|0:0|0:0|0:0|0") > 0)
                    {
                        throw new Exception("Found an incorrect \":0|0 repeat\" line.");
                    }
                    Log.ConsoleLog("Maybe an incorrect line: " + line, Log.LogType.BeatmapBuilder, Log.LogLevel.Debug);
                }

                if (isMultiLine)
                {
                    if (line.StartsWith("["))
                    {
                        isMultiLine = false;
                    }
                    else continue;
                }

                // replace necessary things
                if (Regex.IsMatch(line, "^PreviewTime:")) newfile.AppendLine("PreviewTime: " + thisReaderData.PreviewTime);
                else if (Regex.IsMatch(line, "^StackLeniency:")) newfile.AppendLine("StackLeniency: " + thisReaderData.StackLeniency);

                // force ctb mode
                // if (Regex.IsMatch(line, "^Mode:")) newfile += "Mode: 2" + "\r\n";

                else if (Regex.IsMatch(line, "^HPDrainRate:")) newfile.AppendLine("HPDrainRate:" + thisReaderData.HPDrainRate);
                else if (Regex.IsMatch(line, "^CircleSize:")) newfile.AppendLine("CircleSize:" + thisReaderData.CircleSize);
                else if (Regex.IsMatch(line, "^OverallDifficulty:")) newfile.AppendLine("OverallDifficulty:" + thisReaderData.OverallDifficulty);
                else if (Regex.IsMatch(line, "^ApproachRate:")) newfile.AppendLine("ApproachRate:" + thisReaderData.ApproachRate);

                else if (Regex.IsMatch(line, "^SliderMultiplier:")) newfile.AppendLine("SliderMultiplier:" + thisReaderData.SliderMultiplier);
                else if (Regex.IsMatch(line, "^SliderTickRate:")) newfile.AppendLine("SliderTickRate:" + thisReaderData.SliderTickRate);

                else if (Regex.IsMatch(line, "^Bookmarks:"))
                {
                    newfile.Append("Bookmarks: ");
                    for (int i = 0; i < thisReaderData.Bookmarks.Length; i++)
                    {
                        if (i > 0) newfile.Append(',');
                        newfile.Append(thisReaderData.Bookmarks[i]);
                    }
                    newfile.Append("\r\n");
                }

                else if (Regex.IsMatch(line, @"^\[TimingPoints\]"))
                {
                    hasTiming = true;
                    newfile.AppendLine("[TimingPoints]");
                    for (int i = 0; i < thisReaderData.ControlPointLines.Count; i++)
                    {
                        newfile.AppendLine(thisReaderData.ControlPointLines[i]);
                    }
                    isMultiLine = true;
                }
                else if (Regex.IsMatch(line, @"^\[HitObjects\]"))
                {
                    // fix when no timing
                    if (!hasTiming)
                    {
                        newfile.AppendLine("[TimingPoints]");
                        for (int i = 0; i < thisReaderData.ControlPointLines.Count; i++)
                        {
                            newfile.AppendLine(thisReaderData.ControlPointLines[i]);
                        }
                        newfile.AppendLine("\r\n");
                    }
                    newfile.AppendLine("[HitObjects]");
                    for (int i = 0; i < thisReaderData.HitObjectLines.Count; i++)
                    {
                        newfile.AppendLine(thisReaderData.HitObjectLines[i].HitObjectLine);
                    }
                    isMultiLine = true;
                }
                else newfile.AppendLine(line);
            }
            return newfile.ToString();
        }

        #endregion

        #region Build beatmap

        /// <summary>
        /// 一次读取同时取得 beatmap 文件版本和 [Colours] 块内容（原实现会读两次文件）。
        /// </summary>
        private static void ReadBeatmapFileMetadata(string orgpath, out List<string>? colourLines, out int version)
        {
            colourLines = new List<string>();
            version = 14;
            bool foundColours = false;
            using (StreamReader file = File.OpenText(orgpath))
            {
                string? line;
                while ((line = file.ReadLine()) != null)
                {
                    if (line.StartsWith("osu file format v"))
                    {
                        version = int.Parse(line.Substring(17));
                        continue;
                    }

                    if (foundColours) continue;

                    if (line.StartsWith("[Colours]"))
                    {
                        foundColours = true;
                        string? innerLine;
                        while ((innerLine = file.ReadLine()) != null)
                        {
                            if (innerLine.StartsWith("[")) break;
                            if (innerLine.Trim() == "") continue;
                            colourLines.Add(innerLine);
                        }
                    }
                }
            }
        }

        public static Beatmap? BuildNewBeatmapWithFilePath(BeatmapInfoCollection thisReaderData, string beatmappath, out List<string>? colourLines)
        {
            try
            {
                ReadBeatmapFileMetadata(beatmappath, out colourLines, out int version);
                thisReaderData.BeatmapVersion = version;
            }
            catch (Exception ex)
            {
                Log.ConsoleLog("Can not read colors from path: " + beatmappath + "\r\n" + ex, Log.LogType.BeatmapBuilder, Log.LogLevel.Warning);
                colourLines = null;
            }
            return BuildNewBeatmapWithColorString(thisReaderData, colourLines);
        }

        public static Beatmap? BuildNewBeatmapWithColorString(BeatmapInfoCollection thisReaderData, List<string>? colourLines)
        {
            return BuildNewBeatmap(thisReaderData, colourLines);
        }

        private static Beatmap? BuildNewBeatmap(BeatmapInfoCollection thisReaderData, List<string>? colourLines)
        {
            try
            {
                Log.ConsoleLog("Building beatmap.", Log.LogType.BeatmapBuilder, Log.LogLevel.Debug);
                // var beatmap = beatmapDecoder.Decode(thisReaderData, colourLines);
                var beatmap = new LegacyBeatmapDecoder(thisReaderData.BeatmapVersion).Decode(thisReaderData, colourLines);
                return beatmap;
            }
            catch (Exception ex)
            {
                Log.ConsoleLog("Building beatmap failed.\r\n" + ex, Log.LogType.BeatmapBuilder, Log.LogLevel.Error);
                return null;
            }
        }

        public static Beatmap? BuildNewBeatmapFromBeatmapFile(string path)
        {
            try
            {
                StreamReader reader = new StreamReader(path);
                string? line;
                bool currentTiming = false;
                bool currentHitObject = false;
                BeatmapInfoCollection info = new();
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0)
                    {
                        continue;
                    }
                    if (line.StartsWith("osu file format v"))
                    {
                        info.BeatmapVersion = int.Parse(line.Substring(17));
                        continue;
                    }
                    if (line == "[TimingPoints]")
                    {
                        currentTiming = true;
                        currentHitObject = false;
                        continue;
                    }
                    if (line == "[HitObjects]")
                    {
                        currentTiming = false;
                        currentHitObject = true;
                        continue;
                    }
                    if (line.StartsWith('[') && line.EndsWith(']'))
                    {
                        currentTiming = false;
                        currentHitObject = false;
                        continue;
                    }

                    if (currentTiming)
                    {
                        info.ControlPointLines.Add(line);
                        continue;
                    }
                    if (currentHitObject)
                    {
                        info.HitObjectLines.Add(new(line, false));
                        continue;
                    }

                    string[] split = line.Split(":", 2, StringSplitOptions.TrimEntries);
                    if (split.Length < 2)
                    {
                        continue;
                    }
                    switch (split[0])
                    {
                        case "PreviewTime":
                            info.PreviewTime = int.Parse(split[1]);
                            break;
                        case "StackLeniency":
                            info.StackLeniency = float.Parse(split[1]);
                            break;
                        case "HPDrainRate":
                            info.HPDrainRate = float.Parse(split[1]);
                            break;
                        case "CircleSize":
                            info.CircleSize = float.Parse(split[1]);
                            break;
                        case "OverallDifficulty":
                            info.OverallDifficulty = float.Parse(split[1]);
                            break;
                        case "ApproachRate":
                            info.ApproachRate = float.Parse(split[1]);
                            break;
                        case "SliderMultiplier":
                            info.SliderMultiplier = double.Parse(split[1]);
                            break;
                        case "SliderTickRate":
                            info.SliderTickRate = double.Parse(split[1]);
                            break;
                        case "Bookmarks":
                            info.Bookmarks = split[1].Split(',').Select(int.Parse).ToArray();
                            break;
                    }
                }

                info.Filename = Path.GetFileName(path);
                string? containingFolder = Path.GetDirectoryName(path);
                if (containingFolder != null)
                    info.ContainingFolder = containingFolder;
                info.NumControlPoints = info.ControlPointLines.Count;
                info.NumObjects = info.HitObjectLines.Count;
                info.IsFull = true;
                return BuildNewBeatmapWithFilePath(info, path, out var _);
            }
            catch (Exception ex)
            {
                Log.ConsoleLog("Building beatmap failed.\r\n" + ex, Log.LogType.BeatmapBuilder, Log.LogLevel.Error);
                return null;
            }
        }

        #endregion
    }
}
