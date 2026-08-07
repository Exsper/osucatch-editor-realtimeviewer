using System.Configuration;

namespace osucatch_editor_realtimeviewer
{
    /// <summary>
    /// 让 app 设置类使用不分版本的固定路径配置提供程序，
    /// 这样版本号更新后仍读写同一个 user.config。
    /// </summary>
    [SettingsProvider(typeof(FixedPathUserSettingsProvider))]
    internal sealed partial class app
    {
    }
}
