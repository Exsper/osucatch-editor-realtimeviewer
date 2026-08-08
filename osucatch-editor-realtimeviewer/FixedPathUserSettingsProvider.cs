using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Xml.Linq;

namespace osucatch_editor_realtimeviewer
{
    /// <summary>
    /// 不分版本/安装位置的用户配置提供程序。
    /// 默认的 LocalFileSettingsProvider 会把配置写到
    /// %LocalAppData%\&lt;公司名&gt;\&lt;产品名&gt;_&lt;位置hash&gt;\&lt;程序集版本&gt;\user.config，
    /// 版本号或安装位置变化就会产生新目录、丢失旧配置。
    /// 该提供程序把所有版本统一读写到固定路径
    /// %LocalAppData%\OsuCatch-Editor-RealtimeViewer\user.config，
    /// 并在首次运行时自动把旧版本目录里最新的配置迁移过来。
    /// </summary>
    public class FixedPathUserSettingsProvider : SettingsProvider
    {
        private const string SettingsRoot = "OsuCatch-Editor-RealtimeViewer";
        // .NET 会把 Company/Product 名截断到 25 字符，旧版本实际生成的目录名
        private const string LegacyRoot = "OsuCatch-Editor-RealtimeV";
        private const string SettingsFileName = "user.config";
        private const string SectionName = "osucatch_editor_realtimeviewer.app";

        public override string ApplicationName { get; set; } = SettingsRoot;

        private static readonly object syncRoot = new();
        private static string? configPath;
        private static bool migrationChecked;

        /// <summary>配置文件路径（供崩溃日志引用）。</summary>
        public static string SettingsFilePath => ConfigPath;

        private static string ConfigPath =>
            configPath ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                SettingsRoot,
                SettingsFileName);

        public override void Initialize(string name, NameValueCollection config)
        {
            // ApplicationSettingsBase 从特性解析提供程序时传入的 name 可能为 null
            base.Initialize(name ?? "FixedPathUserSettingsProvider", config);
            if (config?["applicationName"] is string appName && !string.IsNullOrEmpty(appName))
            {
                ApplicationName = appName;
            }
        }

        public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context, SettingsPropertyCollection collection)
        {
            var values = new SettingsPropertyValueCollection();
            if (collection == null || collection.Count == 0) return values;

            EnsureMigrated();

            XElement? section = null;
            if (File.Exists(ConfigPath))
            {
                try
                {
                    XDocument doc = XDocument.Load(ConfigPath);
                    section = doc.Root?.Element("userSettings")?.Element(SectionName);
                }
                catch
                {
                    // 配置文件损坏时按默认值启动
                }
            }

            foreach (SettingsProperty property in collection)
            {
                var value = new SettingsPropertyValue(property);
                XElement? setting = section?.Elements("setting")
                    .FirstOrDefault(e => (string?)e.Attribute("name") == property.Name);
                if (setting != null)
                {
                    try
                    {
                        value.PropertyValue = Deserialize(setting.Element("value")?.Value ?? "", property);
                    }
                    catch
                    {
                        // 单项解析失败时使用默认值
                    }
                }
                values.Add(value);
            }
            return values;
        }

        public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection collection)
        {
            if (collection == null || collection.Count == 0) return;

            lock (syncRoot)
            {
                string dir = Path.GetDirectoryName(ConfigPath)!;
                Directory.CreateDirectory(dir);

                XDocument doc;
                if (File.Exists(ConfigPath))
                {
                    try
                    {
                        doc = XDocument.Load(ConfigPath);
                    }
                    catch
                    {
                        doc = new XDocument(new XElement("configuration"));
                    }
                }
                else
                {
                    doc = new XDocument(new XElement("configuration"));
                }

                XElement? userSettings = doc.Root?.Element("userSettings");
                if (userSettings == null)
                {
                    userSettings = new XElement("userSettings");
                    doc.Root?.Add(userSettings);
                }

                XElement? section = userSettings.Element(SectionName);
                if (section == null)
                {
                    section = new XElement(SectionName);
                    userSettings.Add(section);
                }

                foreach (SettingsPropertyValue prop in collection)
                {
                    XElement? setting = section.Elements("setting")
                        .FirstOrDefault(e => (string?)e.Attribute("name") == prop.Name);
                    if (setting == null)
                    {
                        setting = new XElement("setting", new XAttribute("name", prop.Name), new XAttribute("serializeAs", "String"));
                        section.Add(setting);
                    }
                    setting.SetAttributeValue("serializeAs", "String");
                    setting.Element("value")?.Remove();
                    setting.Add(new XElement("value", Serialize(prop.PropertyValue, prop.Property)));
                }

                // 先写临时文件再移动，避免中途崩溃留下半截配置
                string tempPath = ConfigPath + ".tmp";
                doc.Save(tempPath);
                File.Move(tempPath, ConfigPath, overwrite: true);
            }
        }

        /// <summary>
        /// 首次运行时，若固定路径还没有配置文件，
        /// 则从旧版本目录（%LocalAppData%\OsuCatch-Editor-RealtimeV）里找出最新的 user.config 迁移过来。
        /// </summary>
        private static void EnsureMigrated()
        {
            if (migrationChecked) return;
            lock (syncRoot)
            {
                if (migrationChecked) return;
                migrationChecked = true;

                if (File.Exists(ConfigPath)) return;

                string? legacyFile = FindNewestLegacyConfig();
                if (legacyFile == null) return;

                try
                {
                    XDocument legacy = XDocument.Load(legacyFile);
                    XElement? legacySection = legacy.Root?.Element("userSettings")?.Elements().FirstOrDefault();
                    if (legacySection == null) return;

                    string dir = Path.GetDirectoryName(ConfigPath)!;
                    Directory.CreateDirectory(dir);

                    XDocument doc = new XDocument(
                        new XElement("configuration",
                            new XElement("userSettings",
                                new XElement(SectionName,
                                    legacySection.Elements("setting").Select(s => new XElement(s))))));
                    doc.Save(ConfigPath);
                }
                catch
                {
                    // 迁移失败不影响启动，配置从默认值开始
                }
            }
        }

        private static string? FindNewestLegacyConfig()
        {
            try
            {
                string legacyRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    LegacyRoot);
                if (!Directory.Exists(legacyRoot)) return null;

                string? newest = null;
                DateTime newestTime = DateTime.MinValue;
                foreach (string evidenceDir in Directory.GetDirectories(legacyRoot))
                {
                    foreach (string versionDir in Directory.GetDirectories(evidenceDir))
                    {
                        string candidate = Path.Combine(versionDir, SettingsFileName);
                        if (!File.Exists(candidate)) continue;
                        DateTime time = File.GetLastWriteTime(candidate);
                        if (time > newestTime)
                        {
                            newestTime = time;
                            newest = candidate;
                        }
                    }
                }
                return newest;
            }
            catch
            {
                return null;
            }
        }

        private static string Serialize(object? value, SettingsProperty property)
        {
            if (value == null) return "";
            TypeConverter converter = TypeDescriptor.GetConverter(property.PropertyType);
            if (converter.CanConvertTo(typeof(string)))
            {
                return converter.ConvertToInvariantString(value) ?? "";
            }
            return value.ToString() ?? "";
        }

        private static object? Deserialize(string raw, SettingsProperty property)
        {
            if (property.PropertyType == typeof(string)) return raw;
            TypeConverter converter = TypeDescriptor.GetConverter(property.PropertyType);
            if (converter.CanConvertFrom(typeof(string)))
            {
                return converter.ConvertFromInvariantString(raw);
            }
            return Convert.ChangeType(raw, property.PropertyType);
        }
    }
}
