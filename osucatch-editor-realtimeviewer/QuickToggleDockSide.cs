namespace osucatch_editor_realtimeviewer
{
    /// <summary>
    /// 快捷开关栏的吸附位置：菜单栏下方的顶部横条，或者预览画布左 / 右侧的竖列。
    /// </summary>
    internal enum QuickToggleDockSide
    {
        /// <summary>吸附在菜单栏下方，条上的控件横排。</summary>
        Top,

        /// <summary>吸附在预览画布左侧，条上的控件竖排（滑块也是竖向的）。</summary>
        Left,

        /// <summary>吸附在预览画布右侧，条上的控件竖排（滑块也是竖向的）。</summary>
        Right,
    }

    internal static class QuickToggleDockSideHelper
    {
        /// <summary>左 / 右侧吸附时为 true：条上的控件竖排。</summary>
        internal static bool IsVertical(this QuickToggleDockSide side) => side != QuickToggleDockSide.Top;

        /// <summary>解析设置里保存的吸附位置；缺失或非法值一律回退为顶部。</summary>
        internal static QuickToggleDockSide ParseDockSide(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return QuickToggleDockSide.Top;

            return Enum.TryParse(value.Trim(), ignoreCase: true, out QuickToggleDockSide side) && Enum.IsDefined(side)
                ? side
                : QuickToggleDockSide.Top;
        }

        /// <summary>写成可保存到设置的字符串。</summary>
        internal static string ToSettingValue(this QuickToggleDockSide side) => side.ToString();
    }
}
