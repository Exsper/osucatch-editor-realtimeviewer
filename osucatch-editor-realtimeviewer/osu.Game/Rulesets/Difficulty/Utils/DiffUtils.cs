// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Difficulty.Utils
{
    /// <summary>
    /// Shared mathematical helpers used by difficulty calculation.
    /// </summary>
    /// <remarks>
    /// Ported from osu! (ppy/osu). Only the helpers required by the osu!catch difficulty
    /// calculation are embedded here; see upstream <c>DiffUtils</c> for the full set.
    /// </remarks>
    public static class DiffUtils
    {
        /// <summary>
        /// Square root of 2
        /// </summary>
        public const double SQRT2 = 1.4142135623730950;

        /// <summary>
        /// Converts BPM value into milliseconds
        /// </summary>
        /// <param name="bpm">Beats per minute</param>
        /// <param name="delimiter">Which rhythm delimiter to use, default is 1/4</param>
        /// <returns>BPM converted to milliseconds</returns>
        public static double BPMToMilliseconds(double bpm, int delimiter = 4) => 60000.0 / delimiter / bpm;

        /// <summary>
        /// Converts milliseconds value into a BPM value
        /// </summary>
        /// <param name="ms">Milliseconds</param>
        /// <param name="delimiter">Which rhythm delimiter to use, default is 1/4</param>
        /// <returns>Milliseconds converted to beats per minute</returns>
        public static double MillisecondsToBPM(double ms, int delimiter = 4) => 60000.0 / (ms * delimiter);

        /// <summary>
        /// Calculates a S-shaped logistic function (https://en.wikipedia.org/wiki/Logistic_function)
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        /// <param name="midpointOffset">How much the function midpoint is offset from zero <paramref name="x"/></param>
        /// <param name="multiplier">Growth rate of the function</param>
        /// <param name="maxValue">Maximum value returnable by the function</param>
        /// <returns>The output of logistic function of <paramref name="x"/></returns>
        public static double Logistic(double x, double midpointOffset, double multiplier, double maxValue = 1) => maxValue / (1 + Math.Exp(multiplier * (midpointOffset - x)));

        /// <summary>
        /// Returns the <i>p</i>-norm of an <i>n</i>-dimensional vector (https://en.wikipedia.org/wiki/Norm_(mathematics))
        /// </summary>
        /// <param name="p">The value of <i>p</i> to calculate the norm for.</param>
        /// <param name="values">The coefficients of the vector.</param>
        /// <returns>The <i>p</i>-norm of the vector.</returns>
        public static double Norm(double p, params double[] values)
        {
            double sum = 0;

            foreach (double x in values)
                sum += Pow(x, p);

            return Pow(sum, 1.0 / p);
        }

        // In actual debug testing it's very rare for a (double, double) call to end up with a rounded int value in the first place.
        // Making an explicit overload is slightly faster than running the `switch` in such cases.
        public static double Pow(double x, double exponent) => Math.Pow(x, exponent);

        public static double Pow(double x, int exponent) => exponent switch
        {
            0 => 1,
            1 => x,
            2 => x * x,
            3 => x * x * x,
            4 => x * x * x * x,
            5 => x * x * x * x * x, // This is the largest value used in diffcalc right now.
            _ => Math.Pow(x, exponent)
        };

        /// <summary>
        /// Error function (https://en.wikipedia.org/wiki/Error_function)
        /// </summary>
        /// <remarks>Abramowitz and Stegun formula 7.1.26 approximation.</remarks>
        public static double Erf(double x)
        {
            if (x == 0)
                return 0;

            if (double.IsPositiveInfinity(x))
                return 1;

            if (double.IsNegativeInfinity(x))
                return -1;

            if (double.IsNaN(x))
                return double.NaN;

            double t = 1.0 / (1.0 + 0.3275911 * Math.Abs(x));
            double tau = t * (0.254829592
                              + t * (-0.284496736
                                     + t * (1.421413741
                                            + t * (-1.453152027
                                                   + t * 1.061405429))));

            double erf = 1.0 - tau * Math.Exp(-x * x);

            return x >= 0 ? erf : -erf;
        }

        /// <summary>
        /// Complementary error function (https://en.wikipedia.org/wiki/Error_function)
        /// </summary>
        public static double Erfc(double x) => 1 - Erf(x);
    }
}
