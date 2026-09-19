using OpenTK.Graphics;

namespace osucatch_editor_realtimeviewer
{
    /// <summary>
    /// 拍线（小节线细分）显示模式。
    /// <para />取值与用户设置 <c>app.Default.BarLine_Mode</c> 一一对应，<b>数值即持久化值</b>，
    /// 因此只能往后追加，不能插入或重排（否则旧配置会错位）。
    /// <para />枚举顺序同时也是快捷开关栏滑块与设置菜单下拉框的显示顺序（由疏到密）。
    /// </summary>
    public enum BarLineMode
    {
        /// <summary>不显示拍线。</summary>
        Hidden = 0,

        /// <summary>每 4 拍一条细分线（设置菜单旧文案“显示每4拍”）。</summary>
        Every4Beats = 1,

        /// <summary>每 2 拍一条细分线。</summary>
        Every2Beats = 2,

        /// <summary>每一拍一条细分线。</summary>
        EveryBeat = 3,

        /// <summary>每 1/2 拍一条细分线（红色）。</summary>
        EveryHalfBeat = 4,

        /// <summary>每 1/3 拍一条细分线（紫色）。</summary>
        EveryThirdBeat = 5,

        /// <summary>每 1/4 拍一条细分线（1/2 拍红、1/4 拍蓝）。</summary>
        EveryQuarterBeat = 6,

        /// <summary>每 1/5 拍一条细分线（黄色）。</summary>
        EveryFifthBeat = 7,

        /// <summary>每 1/6 拍一条细分线（1/2 拍红、1/3 拍紫）。</summary>
        EverySixthBeat = 8,

        /// <summary>每 1/7 拍一条细分线（黄色）。</summary>
        EverySeventhBeat = 9,

        /// <summary>每 1/8 拍一条细分线（1/2 拍红、1/4 拍蓝、1/8 拍黄）。</summary>
        EveryEighthBeat = 10,
    }

    /// <summary>
    /// 拍线模式的取值 / 文案 / 配色助手：绘制（<see cref="DrawingHelper"/>）、
    /// 设置窗口下拉框与快捷开关栏滑块共用，避免三处各写一套映射。
    /// </summary>
    internal static class BarLineSettings
    {
        /// <summary>滑块 / 下拉框里的全部选项，顺序与 <see cref="BarLineMode"/> 一致（由疏到密）。</summary>
        internal static readonly BarLineMode[] AllModes =
        {
            BarLineMode.Hidden,
            BarLineMode.Every4Beats,
            BarLineMode.Every2Beats,
            BarLineMode.EveryBeat,
            BarLineMode.EveryHalfBeat,
            BarLineMode.EveryThirdBeat,
            BarLineMode.EveryQuarterBeat,
            BarLineMode.EveryFifthBeat,
            BarLineMode.EverySixthBeat,
            BarLineMode.EverySeventhBeat,
            BarLineMode.EveryEighthBeat,
        };

        /// <summary>拍线模式数量（滑块 / 下拉框的选项个数）。</summary>
        internal static int ModeCount => AllModes.Length;

        /// <summary>
        /// 当前生效的拍线模式（从用户设置里读）。
        /// <para />读的时候顺带做一次旧配置迁移：旧版本把“是否显示小节线”和细分档位分两个设置存，
        /// 新版本合并成一个模式值；旧配置里 <c>BarLine_Mode</c> 是 0（未写过），
        /// 此时按旧的布尔值 + 细分下拉框换算，让升级后的用户保持原来的显示效果。
        /// </summary>
        internal static BarLineMode CurrentMode
        {
            get
            {
                int mode = app.Default.BarLine_Mode;
                if (mode > 0) return Clamp(mode);
                return FromLegacySettings(app.Default.BarLine_Show, app.Default.BarLine_Subdivide);
            }
            set
            {
                app.Default.BarLine_Mode = (int)Clamp((int)value);
                // 旧设置一并写成“已显示”，避免下次升级/回退时被旧的 False 再次覆盖
                app.Default.BarLine_Show = value != BarLineMode.Hidden;
            }
        }

        /// <summary>把任意整数折算成合法模式（越界时夹到最近的合法值）。</summary>
        internal static BarLineMode Clamp(int value)
        {
            if (value <= (int)BarLineMode.Hidden) return BarLineMode.Hidden;
            if (value >= (int)BarLineMode.EveryEighthBeat) return BarLineMode.EveryEighthBeat;
            return (BarLineMode)value;
        }

        /// <summary>
        /// 旧版本设置迁移：旧配置存的是“显示小节线”复选框 + 细分下拉框（0/1/2 = 小节 / 2拍 / 拍）。
        /// 新版本把两者合并成一个模式值，因此读设置时按旧语义换算一次。
        /// </summary>
        internal static BarLineMode FromLegacySettings(bool show, int legacySubdivide)
        {
            if (!show) return BarLineMode.Hidden;
            return legacySubdivide switch
            {
                >= 2 => BarLineMode.EveryBeat,
                1 => BarLineMode.Every2Beats,
                _ => BarLineMode.Hidden,
            };
        }

        /// <summary>
        /// 整拍（1/1）细分线的颜色：淡白（沿用旧版本“显示到拍”的配色）。
        /// <para />它比小节线更淡、也与红色 BPM 线区分，因此“每拍”以及 1/2 ~ 1/8 各档
        /// 都要在每一拍上画出这条淡白线。
        /// </para>
        /// <para />透明度用 150 而不是旧版本的 90：黑色背景 + 1px 线宽下，90 太接近底色，
        /// 肉眼几乎分辨不出，用户会以为某一拍“没画”。
        /// </para>
        /// </summary>
        internal static readonly Color BeatLineColor = Color.FromArgb(150, Color.White);

        /// <summary>细分线的固定颜色（方案 A：与 osu! 编辑器的 1/2 红、1/4 蓝一致）。</summary>
        private static readonly Color Color1_2 = Color.FromArgb(255, 246, 74, 74);      // 红
        private static readonly Color Color1_3 = Color.FromArgb(255, 176, 106, 255);   // 紫
        private static readonly Color Color1_4 = Color.FromArgb(255, 88, 148, 255);    // 蓝
        private static readonly Color Color1_5 = Color.FromArgb(255, 255, 216, 82);    // 黄
        private static readonly Color Color1_6 = Color.FromArgb(255, 255, 130, 214);   // 品红
        private static readonly Color Color1_7 = Color.FromArgb(255, 255, 216, 82);   // 黄
        private static readonly Color Color1_8 = Color.FromArgb(255, 255, 235, 120);   // 浅黄
        private static readonly Color FallbackColor = Color.FromArgb(255, 170, 170, 170);

        /// <summary>
        /// 细分密度档位：每 <c>1/</c><see cref="SubdivisionTier.Denominator"/> 拍画一条线，
        /// “每2拍 / 每4拍”这类跨拍间隔用 <see cref="SubdivisionTier.StepBeats"/> 表示。
        /// </summary>
        internal readonly struct SubdivisionTier
        {
            internal SubdivisionTier(int denominator, int stepBeats)
            {
                Denominator = denominator;
                StepBeats = stepBeats;
            }

            /// <summary>每拍被分成的份数（1/2 拍 = 2，1/8 拍 = 8）。</summary>
            internal int Denominator { get; }

            /// <summary>跨拍间隔（“每2拍”= 2，“每4拍”= 4）；没有跨拍间隔时为 0。</summary>
            internal int StepBeats { get; }
        }

        /// <summary>取模式的细分档位；<see cref="BarLineMode.Hidden"/> 返回的分母为 0。</summary>
        internal static SubdivisionTier GetSubdivisionTier(BarLineMode mode)
        {
            return mode switch
            {
                BarLineMode.Every4Beats => new SubdivisionTier(1, 4),
                BarLineMode.Every2Beats => new SubdivisionTier(1, 2),
                BarLineMode.EveryBeat => new SubdivisionTier(1, 0),
                BarLineMode.EveryHalfBeat => new SubdivisionTier(2, 0),
                BarLineMode.EveryThirdBeat => new SubdivisionTier(3, 0),
                BarLineMode.EveryQuarterBeat => new SubdivisionTier(4, 0),
                BarLineMode.EveryFifthBeat => new SubdivisionTier(5, 0),
                BarLineMode.EverySixthBeat => new SubdivisionTier(6, 0),
                BarLineMode.EverySeventhBeat => new SubdivisionTier(7, 0),
                BarLineMode.EveryEighthBeat => new SubdivisionTier(8, 0),
                _ => new SubdivisionTier(0, 0),
            };
        }

        /// <summary>
        /// 模式对应的档位文字，用在快捷开关栏滑块下方的状态标签上。
        /// <para />英文刻意写短（<c>1/8 beat</c> 而不是 <c>Every 1/8 beat</c>）：这段文字直接决定
        /// 吸附在左 / 右侧时那一列的宽度，长句子会把整列撑得很宽；缩略写法配合功能区的
        /// “Bar Lines” 标题一样看得懂。设置窗口里的下拉项另有更详细的文案（含配色说明）。
        /// </para>
        /// </summary>
        internal static string GetDisplayName(BarLineMode mode)
        {
            bool chinese = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "zh";
            return mode switch
            {
                BarLineMode.Every4Beats => chinese ? "每4拍" : "4 beats",
                BarLineMode.Every2Beats => chinese ? "每2拍" : "2 beats",
                BarLineMode.EveryBeat => chinese ? "每拍" : "1 beat",
                BarLineMode.EveryHalfBeat => chinese ? "每1/2拍" : "1/2 beat",
                BarLineMode.EveryThirdBeat => chinese ? "每1/3拍" : "1/3 beat",
                BarLineMode.EveryQuarterBeat => chinese ? "每1/4拍" : "1/4 beat",
                BarLineMode.EveryFifthBeat => chinese ? "每1/5拍" : "1/5 beat",
                BarLineMode.EverySixthBeat => chinese ? "每1/6拍" : "1/6 beat",
                BarLineMode.EverySeventhBeat => chinese ? "每1/7拍" : "1/7 beat",
                BarLineMode.EveryEighthBeat => chinese ? "每1/8拍" : "1/8 beat",
                _ => chinese ? "不显示" : "Off",
            };
        }

        /// <summary>
        /// 拍线状态文字里的显示名称：只写当前档位（如“每2拍”）。
        /// 只写档位、不写配色——线上什么颜色用户看一眼就知道，写进文字反而啰嗦；
        /// 也不带“拍线：”这类前缀：所在功能区本身就叫“拍线”，吸附在左 / 右侧竖排时前缀还会把整列撑宽。
        /// </summary>
        internal static string GetOptionName(BarLineMode mode) => GetDisplayName(mode);

        /// <summary>
        /// 某条细分线的颜色。颜色由该时刻在<b>整拍里的最简分数</b>决定，
        /// 例如 1/8 拍模式下第 4 个细分（= 1/2 拍）用红色、第 2 个（= 1/4 拍）用蓝色。
        /// </summary>
        /// <param name="numerator">细分序号（1 起）。</param>
        /// <param name="denominator">每拍被分成的份数。</param>
        internal static Color GetBeatLineColor(int numerator, int denominator)
        {
            if (numerator <= 0 || denominator <= 0) return FallbackColor;
            int divisor = GreatestCommonDivisor(numerator, denominator);
            return GetColorForReducedDenominator(denominator / divisor);
        }

        /// <summary>把“细分序号 / 每拍份数”约分成最简分数（绘制时判断该拍点是不是整拍用）。</summary>
        internal static (int Numerator, int Denominator) Reduce(int numerator, int denominator)
        {
            if (numerator <= 0 || denominator <= 0) return (numerator, denominator);
            int divisor = GreatestCommonDivisor(numerator, denominator);
            return (numerator / divisor, denominator / divisor);
        }

        private static Color GetColorForReducedDenominator(int denominator)
        {
            return denominator switch
            {
                2 => Color1_2,
                3 => Color1_3,
                4 => Color1_4,
                5 => Color1_5,
                6 => Color1_6,
                7 => Color1_7,
                8 => Color1_8,
                _ => FallbackColor,
            };
        }

        private static int GreatestCommonDivisor(int a, int b)
        {
            while (b != 0)
            {
                (a, b) = (b, a % b);
            }
            return Math.Max(a, 1);
        }
    }
}
