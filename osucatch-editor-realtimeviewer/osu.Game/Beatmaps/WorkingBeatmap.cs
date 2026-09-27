// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Extensions;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osucatch_editor_realtimeviewer;
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
            => GetPlayableBeatmap(ruleset, mods, true);

        /// <summary>
        /// 与 <see cref="GetPlayableBeatmap(Ruleset, IReadOnlyList{Mod})"/> 相同，
        /// 但可以指定是否需要生成嵌套物件（见
        /// <see cref="HitObject.ApplyDefaults"/> 的 <c>createNestedHitObjects</c>）。
        /// </summary>
        public IBeatmap GetPlayableBeatmap(Ruleset ruleset, IReadOnlyList<Mod> mods, bool createNestedHitObjects)
        {
            try
            {
                using (var cancellationTokenSource = new CancellationTokenSource(10_000))
                {
                    // don't apply the default timeout when debugger is attached (may be breakpointing / debugging).
                    return GetPlayableBeatmap(ruleset, mods ?? Array.Empty<Mod>(), Debugger.IsAttached ? new CancellationToken() : cancellationTokenSource.Token, createNestedHitObjects);
                }
            }
            catch (OperationCanceledException)
            {
                throw new BeatmapLoadTimeoutException(BeatmapInfo);
            }
        }

        public virtual IBeatmap GetPlayableBeatmap(Ruleset ruleset, IReadOnlyList<Mod> mods, CancellationToken token)
            => GetPlayableBeatmap(ruleset, mods, token, true);

        public virtual IBeatmap GetPlayableBeatmap(Ruleset ruleset, IReadOnlyList<Mod> mods, CancellationToken token, bool createNestedHitObjects)
        {
            Log.ConsoleLog("Creating converter.", Log.LogType.BeatmapConverter, Log.LogLevel.Debug);

            IBeatmapConverter converter = CreateBeatmapConverter(Beatmap, ruleset);

            // Check if the beatmap can be converted
            if (Beatmap.HitObjects.Count > 0 && !converter.CanConvert())
                throw new Exception($"{nameof(Beatmaps.Beatmap)} can not be converted for the ruleset (ruleset: {ruleset.ShortName}, converter: {converter}).");

            Log.ConsoleLog("Converting.", Log.LogType.BeatmapConverter, Log.LogLevel.Debug);

            // Convert
            IBeatmap converted = converter.Convert(token);

            // Apply difficulty mods
            if (mods.Any(m => m is IApplicableToDifficulty))
            {
                foreach (var mod in mods.OfType<IApplicableToDifficulty>())
                {
                    token.ThrowIfCancellationRequested();
                    Log.ConsoleLog("Difficulty apply mod.", Log.LogType.BeatmapConverter, Log.LogLevel.Debug);
                    mod.ApplyToDifficulty(converted.Difficulty);
                }
            }

            Log.ConsoleLog("Building processor.", Log.LogType.BeatmapConverter, Log.LogLevel.Debug);

            var processor = ruleset.CreateBeatmapProcessor(converted);

            if (processor != null)
            {
                foreach (var mod in mods.OfType<IApplicableToBeatmapProcessor>())
                {
                    Log.ConsoleLog("Processor apply difficulty mod.", Log.LogType.BeatmapConverter, Log.LogLevel.Debug);
                    mod.ApplyToBeatmapProcessor(processor);
                }

                processor.PreProcess();
            }

            Log.ConsoleLog("Creating nested hitobjects.", Log.LogType.BeatmapConverter, Log.LogLevel.Debug);

            // Compute default values for hitobjects, including creating nested hitobjects in-case they're needed
            foreach (var obj in converted.HitObjects)
            {
                token.ThrowIfCancellationRequested();
                obj.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty, token, createNestedHitObjects);
            }

            Log.ConsoleLog("Nested hitobjects' indexing & offsets.", Log.LogType.BeatmapConverter, Log.LogLevel.Debug);

            processor?.PostProcess();

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
