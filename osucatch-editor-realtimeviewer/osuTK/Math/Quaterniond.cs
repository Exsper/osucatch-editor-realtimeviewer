/*
Copyright (c) 2006 - 2008 The Open Toolkit library.

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace osuTK
{
    /// <summary>
    /// Represents a double-precision Quaternion.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Quaterniond
    {
        /// <summary>
        /// The X, Y and Z components of this instance.
        /// </summary>
        public Vector3d Xyz;

        /// <summary>
        /// The W component of this instance.
        /// </summary>
        public double W;

        /// <summary>
        /// Construct a new Quaterniond from vector and w components
        /// </summary>
        /// <param name="v">The vector part</param>
        /// <param name="w">The w part</param>
        public Quaterniond(Vector3d v, double w)
        {
            Xyz = v;
            W = w;
        }

        /// <summary>
        /// Construct a new Quaterniond
        /// </summary>
        /// <param name="x">The x component</param>
        /// <param name="y">The y component</param>
        /// <param name="z">The z component</param>
        /// <param name="w">The w component</param>
        public Quaterniond(double x, double y, double z, double w)
            : this(new Vector3d(x, y, z), w)
        { }

        /// <summary>
        /// Gets the square of the Quaterniond length (magnitude).
        /// </summary>
        public double LengthSquared
        {
            get
            {
                return W * W + Xyz.LengthSquared;
            }
        }

        /// <summary>
        /// Defines the identity quaternion.
        /// </summary>
        public readonly static Quaterniond Identity = new Quaterniond(0, 0, 0, 1);
    }
}
