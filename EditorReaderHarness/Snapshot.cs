using Editor_Reader;

namespace EditorReaderHarness;

/// <summary>
/// 复刻主工程 <c>BeatmapInfoCollection</c> 的合法性校验（不引用主工程，因为那份实现依赖 WinForms/设置）。
/// 若主工程的校验规则有变化，这里必须同步修改，否则测量结果会失真。
/// </summary>
internal static class Snapshot
{
    public const int ReasonX = 1;
    public const int ReasonY = 2;
    public const int ReasonSegmentCount = 3;
    public const int ReasonType = 4;
    public const int ReasonSampleSet = 5;
    public const int ReasonSampleSetAdditions = 6;
    public const int ReasonSampleVolume = 7;

    public static string ReasonName(int r) => r switch
    {
        0 => "序号越界",
        1 => "X 越界",
        2 => "Y 越界",
        3 => "SegmentCount>9000",
        4 => "Type==0",
        5 => "SampleSet>1000",
        6 => "SampleSetAdditions>1000",
        7 => "SampleVolume>1000",
        _ => "未知"
    };

    /// <summary>与主工程一致的逐物件校验，返回 -1 表示合法。</summary>
    public static int RejectReason(HitObject ho)
    {
        if (ho.X > 1000 || ho.X < -1000) return ReasonX;
        if (ho.Y > 1000 || ho.Y < -1000) return ReasonY;
        if (ho.SegmentCount > 9000) return ReasonSegmentCount;
        if (ho.Type == 0) return ReasonType;
        if (ho.SampleSet > 1000) return ReasonSampleSet;
        if (ho.SampleSetAdditions > 1000) return ReasonSampleSetAdditions;
        if (ho.SampleVolume > 1000) return ReasonSampleVolume;
        return -1;
    }

    /// <summary>
    /// 逐物件校验。返回 false 时 <paramref name="detail"/> 给出第一个非法物件的字段值与下标。
    /// </summary>
    public static bool TryValidateObjects(EditorReader reader, out string detail, out int rejectCount, out Dictionary<int, long> reasons)
    {
        reasons = new Dictionary<int, long>();
        detail = "";
        rejectCount = 0;

        if (reader.hitObjects == null) { detail = "hitObjects == null"; return false; }

        for (int i = 0; i < reader.hitObjects.Count; i++)
        {
            HitObject ho = reader.hitObjects[i];
            int r = RejectReason(ho);
            if (r < 0) continue;

            rejectCount++;
            reasons[r] = reasons.GetValueOrDefault(r) + 1;
            if (detail.Length == 0)
            {
                detail = $"#{i} {ReasonName(r)}: X={ho.X} Y={ho.Y} BaseX={ho.BaseX} BaseY={ho.BaseY} Type={ho.Type} " +
                         $"Start={ho.StartTime} End={ho.EndTime} Seg={ho.SegmentCount} Vol={ho.SampleVolume} " +
                         $"SS={ho.SampleSet} SSA={ho.SampleSetAdditions} unified={ho.unifiedSoundAddition} curve={ho.sliderCurvePoints?.Length}";
            }
        }

        return rejectCount == 0;
    }

    /// <summary>
    /// 复刻 <c>BeatmapInfoCollection</c> 构造时的整体校验，返回失败原因（null 表示通过）。
    /// </summary>
    public static string? Validate(EditorReader reader, out int rejectCount, out Dictionary<int, long> reasons)
    {
        reasons = new Dictionary<int, long>();
        rejectCount = 0;

        if (reader.hitObjects == null) return "hitObjects == null";
        if (!(reader.numControlPoints > 0 && reader.controlPoints != null && reader.hitObjects != null))
        {
            return $"控制点校验失败: numControlPoints={reader.numControlPoints} controlPoints={(reader.controlPoints == null ? "null" : reader.controlPoints.Count.ToString())}";
        }
        if (reader.numControlPoints != reader.controlPoints.Count)
        {
            return $"控制点数不一致: reader={reader.numControlPoints} list={reader.controlPoints.Count}";
        }
        if (reader.numObjects != reader.hitObjects.Count)
        {
            return $"物件数不一致: reader={reader.numObjects} list={reader.hitObjects.Count}";
        }

        if (!TryValidateObjects(reader, out string detail, out rejectCount, out reasons))
        {
            return "非法物件 " + detail;
        }

        return null;
    }
}
