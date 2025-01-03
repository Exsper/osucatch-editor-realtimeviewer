﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Extensions;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using System.Diagnostics;

namespace osu.Game.Beatmaps
{
    public abstract class WorkingBeatmap : IWorkingBeatmap
    {
        public readonly BeatmapInfo BeatmapInfo;

        // TODO: remove once the fallback lookup is not required (and access via `working.BeatmapInfo.Metadata` directly).
        public BeatmapMetadata Metadata => BeatmapInfo.Metadata;

        private CancellationTokenSource loadCancellationSource = new CancellationTokenSource();

        private readonly object beatmapFetchLock = new object();



        protected WorkingBeatmap(BeatmapInfo beatmapInfo)
        {
            BeatmapInfo = beatmapInfo;
        }

        #region Resource getters


        protected abstract IBeatmap GetBeatmap();


        #endregion

        #region Async load control

        public void BeginAsyncLoad() => loadBeatmapAsync();

        public void CancelAsyncLoad()
        {
            lock (beatmapFetchLock)
            {
                loadCancellationSource?.Cancel();
                loadCancellationSource = new CancellationTokenSource();

                if (beatmapLoadTask?.IsCompleted != true)
                    beatmapLoadTask = null;
            }
        }

        #endregion



        #region Beatmap

        public virtual bool BeatmapLoaded
        {
            get
            {
                lock (beatmapFetchLock)
                    return beatmapLoadTask?.IsCompleted ?? false;
            }
        }

        public IBeatmap Beatmap
        {
            get
            {
                try
                {
                    return loadBeatmapAsync().GetResultSafely();
                }
                catch
                {
                    return null;
                }
            }
        }

        private Task<IBeatmap> beatmapLoadTask;

        private Task<IBeatmap> loadBeatmapAsync()
        {
            lock (beatmapFetchLock)
            {
                return beatmapLoadTask ??= Task.Factory.StartNew(() =>
                {
                    // Todo: Handle cancellation during beatmap parsing
                    var b = GetBeatmap() ?? new Beatmap();

                    // The original beatmap version needs to be preserved as the database doesn't contain it
                    BeatmapInfo.BeatmapVersion = b.BeatmapInfo.BeatmapVersion;

                    // Use the database-backed info for more up-to-date values (beatmap id, ranked status, etc)
                    b.BeatmapInfo = BeatmapInfo;

                    return b;
                }, loadCancellationSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }

        #endregion

        #region Playable beatmap

        public IBeatmap GetPlayableBeatmap(Ruleset ruleset, IReadOnlyList<Mod> mods = null)
        {
            try
            {
                using (var cancellationTokenSource = new CancellationTokenSource(10_000))
                {
                    // don't apply the default timeout when debugger is attached (may be breakpointing / debugging).
                    return GetPlayableBeatmap(ruleset, mods ?? Array.Empty<Mod>(), Debugger.IsAttached ? new CancellationToken() : cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                throw new BeatmapLoadTimeoutException(BeatmapInfo);
            }
        }

        public virtual IBeatmap GetPlayableBeatmap(Ruleset ruleset, IReadOnlyList<Mod> mods, CancellationToken token)
        {
            IBeatmapConverter converter = CreateBeatmapConverter(Beatmap, ruleset);

            // Check if the beatmap can be converted
            if (Beatmap.HitObjects.Count > 0 && !converter.CanConvert())
                throw new Exception($"{nameof(Beatmaps.Beatmap)} can not be converted for the ruleset (ruleset: {ruleset.ShortName}, converter: {converter}).");

            // Apply conversion mods
            foreach (var mod in mods.OfType<IApplicableToBeatmapConverter>())
            {
                token.ThrowIfCancellationRequested();
                mod.ApplyToBeatmapConverter(converter);
            }

            // Convert
            IBeatmap converted = converter.Convert(token);

            // Apply conversion mods to the result
            foreach (var mod in mods.OfType<IApplicableAfterBeatmapConversion>())
            {
                token.ThrowIfCancellationRequested();
                mod.ApplyToBeatmap(converted);
            }

            // Apply difficulty mods
            if (mods.Any(m => m is IApplicableToDifficulty))
            {
                foreach (var mod in mods.OfType<IApplicableToDifficulty>())
                {
                    token.ThrowIfCancellationRequested();
                    mod.ApplyToDifficulty(converted.Difficulty);
                }
            }

            var processor = ruleset.CreateBeatmapProcessor(converted);

            if (processor != null)
            {
                foreach (var mod in mods.OfType<IApplicableToBeatmapProcessor>())
                    mod.ApplyToBeatmapProcessor(processor);

                processor.PreProcess();
            }

            // Compute default values for hitobjects, including creating nested hitobjects in-case they're needed
            foreach (var obj in converted.HitObjects)
            {
                token.ThrowIfCancellationRequested();
                obj.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty, token);
            }

            foreach (var mod in mods.OfType<IApplicableToHitObject>())
            {
                foreach (var obj in converted.HitObjects)
                {
                    token.ThrowIfCancellationRequested();
                    mod.ApplyToHitObject(obj);
                }
            }

            processor?.PostProcess();

            foreach (var mod in mods.OfType<IApplicableToBeatmap>())
            {
                token.ThrowIfCancellationRequested();
                mod.ApplyToBeatmap(converted);
            }

            return converted;
        }

        /// <summary>
        /// Creates a <see cref="IBeatmapConverter"/> to convert a <see cref="IBeatmap"/> for a specified <see cref="Ruleset"/>.
        /// </summary>
        /// <param name="beatmap">The <see cref="IBeatmap"/> to be converted.</param>
        /// <param name="ruleset">The <see cref="Ruleset"/> for which <paramref name="beatmap"/> should be converted.</param>
        /// <returns>The applicable <see cref="IBeatmapConverter"/>.</returns>
        protected virtual IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap, Ruleset ruleset) => ruleset.CreateBeatmapConverter(beatmap);

        #endregion

        public override string ToString() => BeatmapInfo.ToString();

        public abstract Stream GetStream(string storagePath);

        IBeatmapInfo IWorkingBeatmap.BeatmapInfo => BeatmapInfo;

        private class BeatmapLoadTimeoutException : TimeoutException
        {
            public BeatmapLoadTimeoutException(BeatmapInfo beatmapInfo)
                : base($"Timed out while loading beatmap ({beatmapInfo}).")
            {
            }
        }
    }
}
