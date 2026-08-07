using osu.Game.Rulesets.Catch.Objects;

namespace osucatch_editor_realtimeviewer
{
    /// <summary>
    /// 参考模板谱面数据（只读）：从用户选择的 .osu 文件解析并转换得到，
    /// 仅用于在画面上叠加显示虚线参考物件。
    /// </summary>
    public class TemplateBeatmapData
    {
        public string FilePath = "";
        public string Filename = "";

        /// <summary>
        /// 转换后的可接物件，按 StartTime 升序。
        /// </summary>
        public List<PalpableCatchHitObject> Objects = new();

        /// <summary>
        /// 模板自身的 CS 对应的物件直径。
        /// </summary>
        public float CircleDiameter;

        /// <summary>
        /// 模板自身的 AR 对应的接近时间（ms），仅用于窗口参考。
        /// </summary>
        public int ApproachTime;
    }
}
