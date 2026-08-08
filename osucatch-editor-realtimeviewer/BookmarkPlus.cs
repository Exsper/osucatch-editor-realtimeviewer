using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace osucatch_editor_realtimeviewer
{
    public enum LineType
    {
        Solid = 0,
        Dash = 1,
        Dot = 2,
        DashDot = 3,
        DashDotDot = 4,
    }

    public class LineStyle : IComparable
    {
        public int Id { get; set; }
        public LineType Style { get; set; }  // Solid, Dash, Dot, DashDot, DashDotDot
        public int Width { get; set; }  // 1, 2, 3, 4, 5 (px)

        [JsonConverter(typeof(ColorConverter))]
        public Color Color { get; set; }
        public string Comment { get; set; }

        public int CompareTo(object? lineStyle)
        {
            if (lineStyle == null) return 1;
            LineStyle otherLineStyle = lineStyle as LineStyle;
            if (otherLineStyle == null) throw new ArgumentException("Object is not a LineStyle.");
            return otherLineStyle.Id - Id;
        }
    }

    public class Bookmark : IComparable
    {
        public double Time { get; set; }
        public int StyleId { get; set; }  // 关联LineStyle的Id

        public int CompareTo(object? bookmark)
        {
            if (bookmark == null) return 1;
            Bookmark otherBookmark = bookmark as Bookmark;
            if (otherBookmark == null) throw new ArgumentException("Object is not a Bookmark.");
            return (int)(otherBookmark.Time - Time);
        }
    }

    public class BookmarkData
    {
        public List<LineStyle> LineStyles { get; set; } = new();
        public List<Bookmark> Bookmarks { get; set; } = new();

        public void SortLineStyles()
        {
            LineStyles.Sort();
        }

        public void SortBookmarks()
        {
            Bookmarks.Sort();
        }
    }

    public static class BookmarkPlus
    {
        public static readonly BookmarkData defaultBookmarkStyles = new BookmarkData
        {
            LineStyles =
            {
                new LineStyle { Id = 1, Style = LineType.Dash, Width = 3, Color = Color.Red, Comment = "HyperDash" },
                new LineStyle { Id = 2, Style = LineType.Dash, Width = 3, Color = Color.Pink, Comment = "Dash" },
                new LineStyle { Id = 3, Style = LineType.Dot, Width = 2, Color = Color.Yellow, Comment = "AntiFlow" },
                new LineStyle { Id = 4, Style = LineType.Dot, Width = 2, Color = Color.White, Comment = "Move" },
                new LineStyle { Id = 5, Style = LineType.Solid, Width = 1, Color = Color.Blue, Comment = "Type 5" },
                new LineStyle { Id = 6, Style = LineType.Solid, Width = 1, Color = Color.Green, Comment = "Type 6" },
                new LineStyle { Id = 7, Style = LineType.Solid, Width = 1, Color = Color.Aqua, Comment = "Type 7" },
                new LineStyle { Id = 8, Style = LineType.Solid, Width = 1, Color = Color.Purple, Comment = "Type 8" },
            },
            Bookmarks =
            {
                // new Bookmark { Time = 1611, StyleId = 1 },
                // new Bookmark { Time = 727, StyleId = 2 },
            }
        };

        public static void ApplyLineStyles(BookmarkData data)
        {
            for (int i = 0; i < data.LineStyles.Count; i++)
            {
                string propertyName1 = "Bookmark_LineStyle_" + (i + 1);
                PropertyInfo? prop1 = typeof(app).GetProperty(propertyName1);
                prop1?.SetValue(app.Default, (int)data.LineStyles[i].Style);

                string propertyName2 = "Bookmark_Width_" + (i + 1);
                PropertyInfo? prop2 = typeof(app).GetProperty(propertyName2);
                prop2?.SetValue(app.Default, data.LineStyles[i].Width);

                string propertyName3 = "Bookmark_Color_" + (i + 1);
                PropertyInfo? prop3 = typeof(app).GetProperty(propertyName3);
                prop3?.SetValue(app.Default, data.LineStyles[i].Color);

                string propertyName4 = "Bookmark_Comment_" + (i + 1);
                PropertyInfo? prop4 = typeof(app).GetProperty(propertyName4);
                prop4?.SetValue(app.Default, data.LineStyles[i].Comment);
            }

            app.Default.Save();
        }

        public static void SaveBookmarkData(string filePath, BookmarkData data)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,  // 美化格式，方便阅读
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 避免中文转义
            };

            // 获取文件所在目录的路径
            string? directoryPath = Path.GetDirectoryName(filePath);

            // 确保目录路径不为空且目录存在
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                // 创建所有缺失的目录（递归创建）
                Directory.CreateDirectory(directoryPath);
            }

            string json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(filePath, json);
        }

        public static BookmarkData LoadBookmarkData(string filePath)
        {
            if (!File.Exists(filePath))
                return new BookmarkData();

            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<BookmarkData>(json) ?? new BookmarkData();
        }

        public static bool SaveBookmarksToFile(string filePath, List<Bookmark> bookmarks)
        {
            BookmarkData currentBookmarkData = new BookmarkData
            {
                LineStyles =
                {
                    new LineStyle { Id = 1, Style = (LineType)app.Default.Bookmark_LineStyle_1, Width = app.Default.Bookmark_Width_1, Color = app.Default.Bookmark_Color_1, Comment = app.Default.Bookmark_Comment_1 },
                    new LineStyle { Id = 2, Style = (LineType)app.Default.Bookmark_LineStyle_2, Width = app.Default.Bookmark_Width_2, Color = app.Default.Bookmark_Color_2, Comment = app.Default.Bookmark_Comment_2 },
                    new LineStyle { Id = 3, Style = (LineType)app.Default.Bookmark_LineStyle_3, Width = app.Default.Bookmark_Width_3, Color = app.Default.Bookmark_Color_3, Comment = app.Default.Bookmark_Comment_3 },
                    new LineStyle { Id = 4, Style = (LineType)app.Default.Bookmark_LineStyle_4, Width = app.Default.Bookmark_Width_4, Color = app.Default.Bookmark_Color_4, Comment = app.Default.Bookmark_Comment_4 },
                    new LineStyle { Id = 5, Style = (LineType)app.Default.Bookmark_LineStyle_5, Width = app.Default.Bookmark_Width_5, Color = app.Default.Bookmark_Color_5, Comment = app.Default.Bookmark_Comment_5 },
                    new LineStyle { Id = 6, Style = (LineType)app.Default.Bookmark_LineStyle_6, Width = app.Default.Bookmark_Width_6, Color = app.Default.Bookmark_Color_6, Comment = app.Default.Bookmark_Comment_6 },
                    new LineStyle { Id = 7, Style = (LineType)app.Default.Bookmark_LineStyle_7, Width = app.Default.Bookmark_Width_7, Color = app.Default.Bookmark_Color_7, Comment = app.Default.Bookmark_Comment_7 },
                    new LineStyle { Id = 8, Style = (LineType)app.Default.Bookmark_LineStyle_8, Width = app.Default.Bookmark_Width_8, Color = app.Default.Bookmark_Color_8, Comment = app.Default.Bookmark_Comment_8 },
                },
                Bookmarks = bookmarks
            };

            try
            {
                SaveBookmarkData(filePath, currentBookmarkData);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occurred while saving bookmarkplus file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log.ConsoleLog("Save BookmarkPlus to file failed.\n" + ex, Log.LogType.Bookmark, Log.LogLevel.Error);
                return false;
            }
        }


        public static List<Bookmark> loadBookmarksFromFile(string filePath, bool overwriteStyle = false)
        {
            try
            {
                BookmarkData bookmarkData = LoadBookmarkData(filePath);
                if (overwriteStyle)
                {
                    ApplyLineStyles(bookmarkData);
                }

                return bookmarkData.Bookmarks;
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occurred while reading bookmarkplus file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log.ConsoleLog("Read BookmarkPlus from file failed.\n" + ex, Log.LogType.Bookmark, Log.LogLevel.Error);
                return new List<Bookmark>();
            }
        }


        public static LineType GetLineStyleByStyleId(int styleId)
        {
            switch (styleId)
            {
                case 1: return (LineType)app.Default.Bookmark_LineStyle_1;
                case 2: return (LineType)app.Default.Bookmark_LineStyle_2;
                case 3: return (LineType)app.Default.Bookmark_LineStyle_3;
                case 4: return (LineType)app.Default.Bookmark_LineStyle_4;
                case 5: return (LineType)app.Default.Bookmark_LineStyle_5;
                case 6: return (LineType)app.Default.Bookmark_LineStyle_6;
                case 7: return (LineType)app.Default.Bookmark_LineStyle_7;
                case 8: return (LineType)app.Default.Bookmark_LineStyle_8;
                default: return LineType.Solid;
            }
        }

        public static int GetLineWidthByStyleId(int styleId)
        {
            switch (styleId)
            {
                case 1: return app.Default.Bookmark_Width_1;
                case 2: return app.Default.Bookmark_Width_2;
                case 3: return app.Default.Bookmark_Width_3;
                case 4: return app.Default.Bookmark_Width_4;
                case 5: return app.Default.Bookmark_Width_5;
                case 6: return app.Default.Bookmark_Width_6;
                case 7: return app.Default.Bookmark_Width_7;
                case 8: return app.Default.Bookmark_Width_8;
                default: return 1;
            }
        }

        public static Color GetLineColorByStyleId(int styleId)
        {
            switch (styleId)
            {
                case 1: return app.Default.Bookmark_Color_1;
                case 2: return app.Default.Bookmark_Color_2;
                case 3: return app.Default.Bookmark_Color_3;
                case 4: return app.Default.Bookmark_Color_4;
                case 5: return app.Default.Bookmark_Color_5;
                case 6: return app.Default.Bookmark_Color_6;
                case 7: return app.Default.Bookmark_Color_7;
                case 8: return app.Default.Bookmark_Color_8;
                default: return Color.Blue;
            }
        }

        public static string GetLineLabelByStyleId(int styleId)
        {
            switch (styleId)
            {
                case 1: return app.Default.Bookmark_Comment_1;
                case 2: return app.Default.Bookmark_Comment_2;
                case 3: return app.Default.Bookmark_Comment_3;
                case 4: return app.Default.Bookmark_Comment_4;
                case 5: return app.Default.Bookmark_Comment_5;
                case 6: return app.Default.Bookmark_Comment_6;
                case 7: return app.Default.Bookmark_Comment_7;
                case 8: return app.Default.Bookmark_Comment_8;
                default: return "Type " + styleId;
            }
        }
    }

    public class ColorConverter : JsonConverter<Color>
    {
        // 序列化：将 Color 转换为 JSON 字符串
        public override void Write(
            Utf8JsonWriter writer,
            Color value,
            JsonSerializerOptions options)
        {
            // 格式化为十六进制字符串（含透明度）
            writer.WriteStringValue($"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}");
        }

        // 反序列化：将 JSON 字符串转换为 Color
        public override Color Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            string? hex = reader.GetString();
            if (string.IsNullOrEmpty(hex)
                || !hex.StartsWith("#")
                || hex.Length != 9) // 期望格式 #AARRGGBB
            {
                throw new JsonException("Invalid color format. Expected #AARRGGBB");
            }

            return Color.FromArgb(
                alpha: Convert.ToByte(hex.Substring(1, 2), 16), // 从索引1开始取2字符
                red: Convert.ToByte(hex.Substring(3, 2), 16),
                green: Convert.ToByte(hex.Substring(5, 2), 16),
                blue: Convert.ToByte(hex.Substring(7, 2), 16)
            );
        }
    }

    public class BookmarkManager
    {
        public List<Bookmark> Bookmarks { get; set; } = new();

        public string BeatmapFolder;
        public string BeatmapFilename;

        public BookmarkManager()
        {
        }

        public BookmarkManager(List<Bookmark> bookmarks) => Bookmarks = bookmarks;

        public bool IsBeatmapChanged(string beatmapFolder, string beatmapFilename)
        {
            if (beatmapFolder != BeatmapFolder || beatmapFilename != BeatmapFilename)
            {
                // 切换新谱面
                BeatmapFolder = beatmapFolder;
                BeatmapFilename = beatmapFilename;
                Bookmarks.Clear();
                if (app.Default.Bookmark_AutoLoadSave)
                {
                    // 自动加载新谱面的书签
                    string filepath = Path.Combine(app.Default.Bookmark_FolderPath, BeatmapFolder, BeatmapFilename) + ".bps";
                    Bookmarks = BookmarkPlus.loadBookmarksFromFile(filepath, app.Default.Bookmark_AutoLoadWithStyle);
                }
                return true;
            }
            return false;
        }

        public void AddBookmark(Bookmark bookmark)
        {
            Bookmarks.Add(bookmark);
            Bookmarks.Sort();
        }

        public void DelBookmarkByIndex(int index)
        {
            Bookmarks.RemoveAt(index);
            // 应该不用重新排序
        }

        public List<Bookmark> Add_Del_Bookmark(Bookmark bookmark)
        {
            // 规则：同时间&同类型=删除 同时间不同类型=替换 不同时间=添加
            int findSameTimeBookmarkIndex = FindSameTimeBookmark(Bookmarks.ToArray(), bookmark);
            if (findSameTimeBookmarkIndex < 0) AddBookmark(bookmark);
            else if (findSameTimeBookmarkIndex < Bookmarks.Count)
            {
                int oldStyleId = Bookmarks.ElementAt(findSameTimeBookmarkIndex).StyleId;
                DelBookmarkByIndex(findSameTimeBookmarkIndex);
                if (oldStyleId != bookmark.StyleId) AddBookmark(bookmark);
            }
            return Bookmarks;
        }


        private int FindSameTimeBookmark(Bookmark[] bookmarks, Bookmark target)
        {
            const double MaxTimeRangeAsSameTime = 100;

            if (Bookmarks.Count <= 0) return -1;

            // 执行二分查找
            int index = Array.BinarySearch(bookmarks, target);

            // 找到确切匹配
            if (index >= 0)
                return index;

            // 计算目标应插入的位置
            int insertIndex = ~index;

            // 处理边界情况
            if (insertIndex == 0)
            {
                if (Math.Abs(bookmarks[0].Time - target.Time) <= MaxTimeRangeAsSameTime) return 0;
                else return -1;
            }
            if (insertIndex == bookmarks.Length)
            {
                if (Math.Abs(bookmarks[bookmarks.Length - 1].Time - target.Time) <= MaxTimeRangeAsSameTime) return bookmarks.Length - 1;
                else return -1;
            }

            // 获取插入位置左右两侧的元素
            Bookmark left = bookmarks[insertIndex - 1];
            Bookmark right = bookmarks[insertIndex];

            // 比较差值
            double leftDeltaTime = Math.Abs(target.Time - left.Time);
            double rightDeltaTime = Math.Abs(target.Time - right.Time);
            if (leftDeltaTime <= rightDeltaTime && leftDeltaTime <= MaxTimeRangeAsSameTime) return insertIndex - 1;
            else if (rightDeltaTime <= leftDeltaTime && rightDeltaTime <= MaxTimeRangeAsSameTime) return insertIndex;
            else return -1;
        }
    }
}
