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

namespace osuTK
{
    /// <summary>Represents a 2D vector using two single-precision floating-point numbers.</summary>
    /// <remarks>
    /// The Vector2 structure is suitable for interoperation with unmanaged code requiring two consecutive floats.
    /// </remarks>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Vector2 : IEquatable<Vector2>
    {
        /// <summary>
        /// The X component of the Vector2.
        /// </summary>
        public float X;

        /// <summary>
        /// The Y component of the Vector2.
        /// </summary>
        public float Y;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="value">The value that will initialize this instance.</param>
        public Vector2(float value)
        {
            X = value;
            Y = value;
        }

        /// <summary>
        /// Constructs a new Vector2.
        /// </summary>
        /// <param name="x">The x coordinate of the net Vector2.</param>
        /// <param name="y">The y coordinate of the net Vector2.</param>
        public Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Gets the length (magnitude) of the vector.
        /// </summary>
        public float Length
        {
            get
            {
                return (float)System.Math.Sqrt(X * X + Y * Y);
            }
        }

        /// <summary>
        /// Gets the square of the vector length (magnitude).
        /// </summary>
        /// <remarks>
        /// This property avoids the costly square root operation required by the Length property. This makes it more suitable
        /// for comparisons.
        /// </remarks>
        public float LengthSquared
        {
            get
            {
                return X * X + Y * Y;
            }
        }

        /// <summary>
        /// Returns a copy of the Vector2 scaled to unit length.
        /// </summary>
        /// <returns></returns>
        public Vector2 Normalized()
        {
            Vector2 v = this;
            v.Normalize();
            return v;
        }
        /// <summary>
        /// Scales the Vector2 to unit length.
        /// </summary>
        public void Normalize()
        {
            float scale = 1.0f / this.Length;
            X *= scale;
            Y *= scale;
        }

        /// <summary>
        /// Defines a zero-length Vector2.
        /// </summary>
        public static readonly Vector2 Zero = new Vector2(0, 0);

        /// <summary>
        /// Compute the euclidean distance between two vectors.
        /// </summary>
        /// <param name="vec1">The first vector</param>
        /// <param name="vec2">The second vector</param>
        /// <returns>The distance</returns>
        public static float Distance(Vector2 vec1, Vector2 vec2)
        {
            float result;
            Distance(ref vec1, ref vec2, out result);
            return result;
        }

        /// <summary>
        /// Compute the euclidean distance between two vectors.
        /// </summary>
        /// <param name="vec1">The first vector</param>
        /// <param name="vec2">The second vector</param>
        /// <param name="result">The distance</param>
        public static void Distance(ref Vector2 vec1, ref Vector2 vec2, out float result)
        {
            result = (float)Math.Sqrt((vec2.X - vec1.X) * (vec2.X - vec1.X) + (vec2.Y - vec1.Y) * (vec2.Y - vec1.Y));
        }

        /// <summary>
        /// Calculate the dot (scalar) product of two vectors
        /// </summary>
        /// <param name="left">First operand</param>
        /// <param name="right">Second operand</param>
        /// <returns>The dot product of the two inputs</returns>
        public static float Dot(Vector2 left, Vector2 right)
        {
            return left.X * right.X + left.Y * right.Y;
        }

        /// <summary>
        /// Adds the specified instances.
        /// </summary>
        /// <param name="left">Left operand.</param>
        /// <param name="right">Right operand.</param>
        /// <returns>Result of addition.</returns>
        public static Vector2 operator +(Vector2 left, Vector2 right)
        {
            left.X += right.X;
            left.Y += right.Y;
            return left;
        }

        /// <summary>
        /// Subtracts the specified instances.
        /// </summary>
        /// <param name="left">Left operand.</param>
        /// <param name="right">Right operand.</param>
        /// <returns>Result of subtraction.</returns>
        public static Vector2 operator -(Vector2 left, Vector2 right)
        {
            left.X -= right.X;
            left.Y -= right.Y;
            return left;
        }

        /// <summary>
        /// Negates the specified instance.
        /// </summary>
        /// <param name="vec">Operand.</param>
        /// <returns>Result of negation.</returns>
        public static Vector2 operator -(Vector2 vec)
        {
            vec.X = -vec.X;
            vec.Y = -vec.Y;
            return vec;
        }

        /// <summary>
        /// Multiplies the specified instance by a scalar.
        /// </summary>
        /// <param name="vec">Left operand.</param>
        /// <param name="scale">Right operand.</param>
        /// <returns>Result of multiplication.</returns>
        public static Vector2 operator *(Vector2 vec, float scale)
        {
            vec.X *= scale;
            vec.Y *= scale;
            return vec;
        }

        /// <summary>
        /// Multiplies the specified instance by a scalar.
        /// </summary>
        /// <param name="scale">Left operand.</param>
        /// <param name="vec">Right operand.</param>
        /// <returns>Result of multiplication.</returns>
        public static Vector2 operator *(float scale, Vector2 vec)
        {
            vec.X *= scale;
            vec.Y *= scale;
            return vec;
        }

        /// <summary>
        /// Divides the specified instance by a scalar.
        /// </summary>
        /// <param name="vec">Left operand</param>
        /// <param name="scale">Right operand</param>
        /// <returns>Result of the division.</returns>
        public static Vector2 operator /(Vector2 vec, float scale)
        {
            vec.X /= scale;
            vec.Y /= scale;
            return vec;
        }

        /// <summary>
        /// Compares the specified instances for equality.
        /// </summary>
        /// <param name="left">Left operand.</param>
        /// <param name="right">Right operand.</param>
        /// <returns>True if both instances are equal; false otherwise.</returns>
        public static bool operator ==(Vector2 left, Vector2 right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares the specified instances for inequality.
        /// </summary>
        /// <param name="left">Left operand.</param>
        /// <param name="right">Right operand.</param>
        /// <returns>True if both instances are not equal; false otherwise.</returns>
        public static bool operator !=(Vector2 left, Vector2 right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Returns the hashcode for this instance.
        /// </summary>
        /// <returns>A System.Int32 containing the unique hashcode for this instance.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return (this.X.GetHashCode() * 397) ^ this.Y.GetHashCode();
            }
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>True if the instances are equal; false otherwise.</returns>
        public override bool Equals(object obj)
        {
            if (!(obj is Vector2))
            {
                return false;
            }

            return this.Equals((Vector2)obj);
        }

        /// <summary>Indicates whether the current vector is equal to another vector.</summary>
        /// <param name="other">A vector to compare with this vector.</param>
        /// <returns>true if the current vector is equal to the vector parameter; otherwise, false.</returns>
        public bool Equals(Vector2 other)
        {
            return
                X == other.X &&
                Y == other.Y;
        }
        public static Vector2 CatmullRom(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float alpha)
        {
            float a = alpha * alpha;
            float b = alpha * a;
            Vector2 result;
            result.X = 0.5f * (2f * p2.X + (-p1.X + p3.X) * alpha + (2f * p1.X - 5f * p2.X + 4f * p3.X - p4.X) * a + (-p1.X + 3f * p2.X - 3f * p3.X + p4.X) * b);
            result.Y = 0.5f * (2f * p2.Y + (-p1.Y + p3.Y) * alpha + (2f * p1.Y - 5f * p2.Y + 4f * p3.Y - p4.Y) * a + (-p1.Y + 3f * p2.Y - 3f * p3.Y + p4.Y) * b);
            return result;
        }

        public static Vector2 Normalize(Vector2 v)
        {
            float lengthSquared = v.X * v.X + v.Y * v.Y;
            float lengthInverted = 1f / (float)Math.Sqrt((double)lengthSquared);
            Vector2 result;
            result.X = v.X * lengthInverted;
            result.Y = v.Y * lengthInverted;
            return result;
        }
        public static void Lerp(ref Vector2 p1, ref Vector2 p2, float x, out Vector2 result)
        {
            result.X = p1.X + (p2.X - p1.X) * x;
            result.Y = p1.Y + (p2.Y - p1.Y) * x;
        }

    }
}
