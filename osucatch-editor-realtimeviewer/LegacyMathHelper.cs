using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using osuTK;

namespace osucatch_editor_realtimeviewer
{
    internal class LegacyMathHelper
    {
        public const float Pi = 3.14159274f;
        private class BezierApproximator
        {
            private int count;
            private List<Vector2> controlPoints;
            private Vector2[] subdivisionBuffer1;
            private Vector2[] subdivisionBuffer2;

            private const float TOLERANCE = 0.5f;
            private const float TOLERANCE_SQ = TOLERANCE * TOLERANCE;

            public BezierApproximator(List<Vector2> controlPoints)
            {
                this.controlPoints = controlPoints;
                count = controlPoints.Count;

                subdivisionBuffer1 = new Vector2[count];
                subdivisionBuffer2 = new Vector2[count * 2 - 1];
            }

            private static bool IsFlatEnough(Vector2[] controlPoints)
            {
                for (int i = 1; i < controlPoints.Length - 1; i++)
                    if ((controlPoints[i - 1] - 2 * controlPoints[i] + controlPoints[i + 1]).LengthSquared > TOLERANCE_SQ)
                        return false;

                return true;
            }
            
            private void Subdivide(Vector2[] controlPoints, Vector2[] l, Vector2[] r)
            {
                Vector2[] midpoints = subdivisionBuffer1;

                for (int i = 0; i < count; ++i)
                    midpoints[i] = controlPoints[i];

                for (int i = 0; i < count; i++)
                {
                    l[i] = midpoints[0];
                    r[count - i - 1] = midpoints[count - i - 1];

                    for (int j = 0; j < count - i - 1; j++)
                        midpoints[j] = (midpoints[j] + midpoints[j + 1]) / 2;
                }
            }

            private void Approximate(Vector2[] controlPoints, List<Vector2> output)
            {
                Vector2[] l = subdivisionBuffer2;
                Vector2[] r = subdivisionBuffer1;

                Subdivide(controlPoints, l, r);

                for (int i = 0; i < count - 1; ++i)
                    l[count + i] = r[i + 1];

                output.Add(controlPoints[0]);
                for (int i = 1; i < count - 1; ++i)
                {
                    int index = 2 * i;
                    Vector2 p = 0.25f * (l[index - 1] + 2 * l[index] + l[index + 1]);
                    output.Add(p);
                }
            }

            public List<Vector2> CreateBezier()
            {
                List<Vector2> output = new List<Vector2>();

                if (count == 0)
                    return output;

                Stack<Vector2[]> toFlatten = new Stack<Vector2[]>();
                Stack<Vector2[]> freeBuffers = new Stack<Vector2[]>();

                toFlatten.Push(controlPoints.ToArray());

                Vector2[] leftChild = subdivisionBuffer2;

                while (toFlatten.Count > 0)
                {
                    Vector2[] parent = toFlatten.Pop();
                    if (IsFlatEnough(parent))
                    {
                        Approximate(parent, output);
                        freeBuffers.Push(parent);
                        continue;
                    }
                    Vector2[] rightChild = freeBuffers.Count > 0 ? freeBuffers.Pop() : new Vector2[count];
                    Subdivide(parent, leftChild, rightChild);

                    for (int i = 0; i < count; ++i)
                        parent[i] = leftChild[i];

                    toFlatten.Push(rightChild);
                    toFlatten.Push(parent);
                }

                output.Add(controlPoints[count - 1]);
                return output;
            }
        }

        internal static List<Vector2> CreateBezier(List<Vector2> input)
        {
            BezierApproximator b = new BezierApproximator(input);
            return b.CreateBezier();
        }

        internal static List<Vector2> CreateBezierWrong(List<Vector2> input)
        {
            int count = input.Count;

            Vector2[] working = new Vector2[count];
            List<Vector2> output = new List<Vector2>();

            int points = 50 * count;

            for (int iteration = 0; iteration < points; iteration++)
            {
                for (int i = 0; i < count; i++)
                    working[i] = input[i];

                for (int level = 0; level < count; level++)
                    for (int i = 0; i < count - level - 1; i++)
                        Vector2.Lerp(ref working[i], ref working[i + 1], (float)iteration / points, out working[i]);
                output.Add(working[0]);
            }

            return output;
        }

        internal static bool IsStraightLine(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y) == 0.0f;
        }

        internal static double CircleTAt(Vector2 p, Vector2 center)
        {
            return Math.Atan2(p.Y - center.Y, p.X - center.X);
        }

        internal static Vector2 CirclePoint(Vector2 center, float radius, double t)
        {
            return new Vector2((float)(Math.Cos(t) * radius), (float)(Math.Sin(t) * radius)) + center;
        }

        internal static void CircleThroughPoints(Vector2 a, Vector2 b, Vector2 c,
            out Vector2 center, out float radius, out double startAngle, out double endAngle)
        {
            float D = 2 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
            float AMagSq = a.LengthSquared;
            float BMagSq = b.LengthSquared;
            float CMagSq = c.LengthSquared;
            center = new Vector2(
                (AMagSq * (b.Y - c.Y) + BMagSq * (c.Y - a.Y) + CMagSq * (a.Y - b.Y)) / D,
                (AMagSq * (c.X - b.X) + BMagSq * (a.X - c.X) + CMagSq * (b.X - a.X)) / D);
            radius = Vector2.Distance(center, a);

            startAngle = CircleTAt(a, center);
            double middleAngle = CircleTAt(b, center);
            endAngle = CircleTAt(c, center);

            while (middleAngle < startAngle) middleAngle += 2 * Pi;
            while (endAngle < startAngle) endAngle += 2 * Pi;
            if (middleAngle > endAngle)
            {
                endAngle -= 2 * Pi;
            }
        }

    }
}
