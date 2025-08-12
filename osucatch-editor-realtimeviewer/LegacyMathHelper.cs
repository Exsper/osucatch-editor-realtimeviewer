﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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

            [DllImport("StableCompatLib.dll", EntryPoint = "flatJudge")]
            private static extern int FlatJudge0(float ax, float ay, float bx, float by, float cx, float cy);

            private static bool FlatJudge(Vector2 a, Vector2 b, Vector2 c)
                => FlatJudge0(a.X, a.Y, b.X, b.Y, c.X, c.Y) != 0;

            private static bool IsFlatEnough(Vector2[] controlPoints)
            {
                for (int i = 1; i < controlPoints.Length - 1; i++)
                    // if ((controlPoints[i - 1] - 2 * controlPoints[i] + controlPoints[i + 1]).LengthSquared > TOLERANCE_SQ)
                    if (FlatJudge(controlPoints[i - 1], controlPoints[i], controlPoints[i + 1]))
                        return false;

                return true;
            }

            [DllImport("StableCompatLib.dll", EntryPoint = "midpoint")]
            private static extern void Midpoint0(float ax, float ay, float bx, float by,
                out float x, out float y);

            private static Vector2 Midpoint(Vector2 a, Vector2 b)
            {
                Midpoint0(a.X, a.Y, b.X, b.Y, out float x, out float y);
                return new(x, y);
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
                        // midpoints[j] = (midpoints[j] + midpoints[j + 1]) / 2;
                        midpoints[j] = Midpoint(midpoints[j], midpoints[j + 1]);
                }
            }

            [DllImport("StableCompatLib.dll", EntryPoint = "bezierApproximate")]
            private static extern void BezierApproximate0(
                float ax, float ay, float bx, float by, float cx, float cy,
                out float x, out float y);
            
            private static Vector2 BezierApproximate(Vector2 a, Vector2 b, Vector2 c)
            {
                BezierApproximate0(a.X, a.Y, b.X, b.Y, c.X, c.Y, out float x, out float y);
                return new(x, y);
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
                    // Vector2 p = 0.25f * (l[index - 1] + 2 * l[index] + l[index + 1]);
                    Vector2 p = BezierApproximate(l[index - 1], l[index], l[index + 1]);
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

                while (toFlatten.Count > 0 && output.Count < 100000)
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
                        // Vector2.Lerp(ref working[i], ref working[i + 1], (float)iteration / points, out working[i]);
                        LerpStableCompat(working[i], working[i + 1], (float)iteration / points, out working[i]);
                output.Add(working[0]);
            }

            return output;
        }

        internal static bool IsStraightLine(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y) == 0.0f;
        }

        [DllImport("StableCompatLib.dll", EntryPoint = "isStraightLine")]
        internal static extern int IsStraightLineStableCompat0(float ax, float ay, float bx, float by, float cx, float cy);

        internal static bool IsStraightLineStableCompat(Vector2 a, Vector2 b, Vector2 c) =>
            IsStraightLineStableCompat0(a.X, a.Y, b.X, b.Y, c.X, c.Y) != 0;

        internal static double CircleTAt(Vector2 p, Vector2 center)
        {
            return Math.Atan2(p.Y - center.Y, p.X - center.X);
        }

        internal static Vector2 CirclePoint(Vector2 center, float radius, double t)
        {
            return new Vector2((float)(Math.Cos(t) * radius), (float)(Math.Sin(t) * radius)) + center;
        }

        [DllImport("StableCompatLib.dll", EntryPoint = "circlePoint")]
        internal static extern void CirclePointStableCompat0(
            float centerX, float centerY, float radius, double t,
            out float x, out float y);

        internal static Vector2 CirclePointStableCompat(Vector2 center, float radius, double t)
        {
            CirclePointStableCompat0(center.X, center.Y, radius, t, out float x, out float y);
            return new(x, y);
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

        [DllImport("StableCompatLib.dll", EntryPoint = "circleThroughPoints")]
        internal extern static void CircleThroughPointsStableCompat0(
            float ax, float ay, float bx, float by, float cx, float cy,
            out float centerx, out float centery, out float radius, out double startAngle, out double endAngle);

        internal static void CircleThroughPointsStableCompat(Vector2 a, Vector2 b, Vector2 c,
            out Vector2 center, out float radius, out double startAngle, out double endAngle)
        {
            CircleThroughPointsStableCompat0(a.X, a.Y, b.X, b.Y, c.X, c.Y, out float centerX, out float centerY, out radius, out startAngle, out endAngle);
            center = new(centerX, centerY);
        }

        [DllImport("StableCompatLib.dll", EntryPoint = "lerp")]
        public static extern void LerpStableCompat0(
            float ax, float ay, float bx, float by, float t,
            out float x, out float y);

        public static void LerpStableCompat(Vector2 p1, Vector2 p2, float x, out Vector2 result)
            => LerpStableCompat0(p1.X, p1.Y, p2.X, p2.Y, x, out result.X, out result.Y);

        [DllImport("StableCompatLib.dll", EntryPoint = "catmullRom")]
        public static extern Vector2 CatmullRomStableCompat0(
            float x1, float y1, float x2, float y2,
            float x3, float y3, float x4, float y4, float t,
            out float x, out float y);

        public static Vector2 CatmullRomStableCompat(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float t)
        {
            CatmullRomStableCompat0(p1.X, p1.Y, p2.X, p2.Y, p3.X, p3.Y, p4.X, p4.Y, t, out float x, out float y);
            return new(x, y);
        }
    }
}
