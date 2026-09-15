// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Difficulty
{
    /// <summary>
    /// Describes the difficulty of a beatmap, as output by a <see cref="DifficultyCalculator"/>.
    /// </summary>
    public class DifficultyAttributes
    {
        protected const int ATTRIB_ID_AIM = 1;
        protected const int ATTRIB_ID_SPEED = 3;
        protected const int ATTRIB_ID_MAX_COMBO = 9;
        protected const int ATTRIB_ID_DIFFICULTY = 11;

        /// <summary>
        /// The mods which were applied to the beatmap.
        /// </summary>
        public Mod[] Mods { get; set; } = Array.Empty<Mod>();

        /// <summary>
        /// The combined star rating of all skills.
        /// </summary>
        public double StarRating { get; set; }

        /// <summary>
        /// The maximum achievable combo.
        /// </summary>
        public int MaxCombo { get; set; }

        /// <summary>
        /// Creates new <see cref="DifficultyAttributes"/>.
        /// </summary>
        public DifficultyAttributes()
        {
        }

        /// <summary>
        /// Creates new <see cref="DifficultyAttributes"/>.
        /// </summary>
        /// <param name="mods">The mods which were applied to the beatmap.</param>
        /// <param name="starRating">The combined star rating of all skills.</param>
        public DifficultyAttributes(Mod[] mods, double starRating)
        {
            Mods = mods;
            StarRating = starRating;
        }

        /// <summary>
        /// Converts this <see cref="DifficultyAttributes"/> to osu-web compatible database attribute mappings.
        /// </summary>
        /// <remarks>
        /// See: osu_difficulty_attribs table.
        /// </remarks>
        public virtual IEnumerable<(int attributeId, object value)> ToDatabaseAttributes()
        {
            yield return (ATTRIB_ID_MAX_COMBO, MaxCombo);
        }
    }
}
