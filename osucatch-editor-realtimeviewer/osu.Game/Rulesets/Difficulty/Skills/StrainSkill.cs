// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Difficulty.Skills
{
    /// <summary>
    /// Used to processes strain values of <see cref="DifficultyHitObject"/>s, keep track of strain levels caused by the processed objects
    /// and to calculate a final difficulty value representing the difficulty of hitting all the processed objects.
    /// </summary>
    /// <typeparam name="TObject">
    /// The ruleset-specific <see cref="DifficultyHitObject"/> implementation processed by this skill.
    /// </typeparam>
    public abstract class StrainSkill<TObject> : Skill
        where TObject : DifficultyHitObject
    {
        /// <summary>
        /// The weight by which each strain value decays.
        /// </summary>
        protected virtual double DecayWeight => 0.9;

        /// <summary>
        /// The length of each strain section.
        /// </summary>
        protected virtual int SectionLength => 400;

        private double currentSectionPeak; // We also keep track of the peak strain level in the current section.
        private double currentSectionEnd;
        private bool firstObjectProcessed;

        private readonly List<double> strainPeaks = new List<double>();

        protected StrainSkill(Mod[] mods)
            : base(mods)
        {
        }

        /// <summary>
        /// Returns the strain value at <see cref="DifficultyHitObject"/>. This value is calculated with or without respect to previous objects.
        /// </summary>
        protected abstract double StrainValueAt(TObject current);

        /// <summary>
        /// Process a <see cref="DifficultyHitObject"/> and update current strain values accordingly.
        /// </summary>
        public override void Process(DifficultyHitObject current)
        {
            var catchCurrent = (TObject)current;

            // The first object doesn't generate a strain, so we begin with an incremented section end.
            // (Upstream keys off a per-object "first object" flag set on the difficulty hit object; this
            // embedded port tracks it here instead, as the upstream flag is not part of the reduced port.)
            if (!firstObjectProcessed)
            {
                currentSectionEnd = Math.Ceiling(catchCurrent.StartTime / SectionLength) * SectionLength;
                firstObjectProcessed = true;
            }

            while (catchCurrent.StartTime > currentSectionEnd)
            {
                saveCurrentPeak();
                startNewSectionFrom(currentSectionEnd, catchCurrent);
                currentSectionEnd += SectionLength;
            }

            // StrainValueAt mutates the skill's internal strain state, so it must be called exactly once
            // per object. Calling it again for the per-object bookkeeping would apply the strain decay a
            // second time and double-count the strain value, inflating every strain peak.
            double strain = StrainValueAt(catchCurrent);

            ObjectDifficulties.Add(strain);

            // The per-object star rating shown beside hitobjects in the viewer.
            catchCurrent.DifficultyToLast = Math.Sqrt(strain / (1 - DecayWeight)) * 4.59;

            currentSectionPeak = Math.Max(strain, currentSectionPeak);
        }

        /// <summary>
        /// Saves the current peak strain level to the list of strain peaks, which will be used to calculate an overall difficulty.
        /// </summary>
        private void saveCurrentPeak()
        {
            strainPeaks.Add(currentSectionPeak);
        }

        /// <summary>
        /// Sets the initial strain level for a new section.
        /// </summary>
        /// <param name="time">The beginning of the new section in milliseconds.</param>
        /// <param name="current">The current hit object.</param>
        private void startNewSectionFrom(double time, TObject current)
        {
            // The maximum strain of the new section is not zero by default
            // This means we need to capture the strain level at the beginning of the new section, and use that as the initial peak level.
            currentSectionPeak = CalculateInitialStrain(time, current);
        }

        /// <summary>
        /// Retrieves the peak strain at a point in time.
        /// </summary>
        /// <param name="time">The time to retrieve the peak strain at.</param>
        /// <param name="current">The current hit object.</param>
        /// <returns>The peak strain.</returns>
        protected abstract double CalculateInitialStrain(double time, TObject current);

        /// <summary>
        /// Returns a live enumerable of the peak strains for each <see cref="SectionLength"/> section of the beatmap,
        /// including the peak of the current section.
        /// </summary>
        public IEnumerable<double> GetCurrentStrainPeaks() => strainPeaks.Append(currentSectionPeak);

        /// <summary>
        /// Returns the calculated difficulty value representing all <see cref="DifficultyHitObject"/>s that have been processed up to this point.
        /// </summary>
        public override double DifficultyValue()
        {
            double difficulty = 0;
            double weight = 1;

            // Sections with 0 strain are excluded to avoid worst-case time complexity of the following sort (e.g. /b/2351871).
            // These sections will not contribute to the difficulty.
            var peaks = GetCurrentStrainPeaks().Where(p => p > 0);

            // Difficulty is the weighted sum of the highest strains from every section.
            // We're sorting from highest to lowest strain.
            foreach (double strain in peaks.OrderDescending())
            {
                difficulty += strain * weight;
                weight *= DecayWeight;
            }

            return difficulty;
        }

        /// <summary>
        /// Not used: <see cref="Process"/> computes the strain directly so that <see cref="StrainValueAt"/>
        /// (which mutates the internal strain state) is invoked exactly once per object.
        /// </summary>
        protected override double ProcessInternal(DifficultyHitObject current)
            => throw new NotSupportedException($"{nameof(StrainSkill<TObject>)} computes strain in {nameof(Process)}.");
    }
}
