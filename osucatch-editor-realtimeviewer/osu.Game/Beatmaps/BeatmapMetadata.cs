﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// A realm model containing metadata for a beatmap.
    /// </summary>
    /// <remarks>
    /// This is currently stored against each beatmap difficulty, even when it is duplicated.
    /// It is also provided via <see cref="BeatmapSetInfo"/> for convenience and historical purposes.
    /// A future effort could see this converted to an <see cref="EmbeddedObject"/> or potentially de-duped
    /// and shared across multiple difficulties in the same set, if required.
    ///
    /// Note that difficulty name is not stored in this metadata but in <see cref="BeatmapInfo"/>.
    /// </remarks>
    [Serializable]
    public class BeatmapMetadata : IBeatmapMetadataInfo
    {
        public string Title { get; set; } = string.Empty;

        public string TitleUnicode { get; set; } = string.Empty;

        public string Artist { get; set; } = string.Empty;

        public string ArtistUnicode { get; set; } = string.Empty;

        public string Author { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public string Tags { get; set; } = string.Empty;



        public BeatmapMetadata()
        {
        }
    }
}
