using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace ImageTools.GeneralTypes
{
    public class VecSegment2
    {
        public Vector2 A { get; set; }
        public Vector2 B { get; set; }
    }

    public struct Vector2
    {
        public double Dx, Dy;

        public Vector2(double x, double y)
        {
            Dx = x; Dy = y;
        }

        public Vector2(PointF p)
        {
            Dx = p.X; Dy = p.Y;
        }

        /// <summary>
        /// Helper for creating arrays of vectors
        /// </summary>
        public static Vector2[] Set(double scale, double dx, double dy, params double[] p)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < p!.Length - 1; i+=2)
            {
                result.Add(new Vector2(p[i]*scale + dx, p[i+1]*scale + dy));
            }
            return result.ToArray();
        }
        
        /// <summary>
        /// Helper for creating arrays of vectors
        /// </summary>
        public static Vector2[] Set(params double[] p)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < p!.Length - 1; i+=2)
            {
                result.Add(new Vector2(p[i], p[i+1]));
            }
            return result.ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator- (Vector2 a, Vector2 b) {
            return new Vector2{ Dx = a.Dx - b.Dx, Dy = a.Dy - b.Dy};
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator+ (Vector2 a, Vector2 b) {
            return new Vector2{ Dx = a.Dx + b.Dx, Dy = a.Dy + b.Dy};
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator/ (Vector2 a, double b) {
            return new Vector2{ Dx = a.Dx / b, Dy = a.Dy / b};
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator* (Vector2 a, double b) {
            return new Vector2{ Dx = a.Dx * b, Dy = a.Dy * b};
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Length()
        {
            return Math.Sqrt(Dx*Dx + Dy*Dy);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 Abs()
        {
            return new Vector2{Dx = Math.Abs(Dx), Dy = Math.Abs(Dy)};
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 Max(double v)
        {
            return new Vector2{Dx = Math.Max(Dx,v), Dy = Math.Max(Dy,v)};
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Dot(Vector2 a, Vector2 b)
        {
            return a.Dx * b.Dx + a.Dy * b.Dy;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Cross(Vector2 v0, Vector2 v1) {
            return v0.Dx * v1.Dy - v0.Dy * v1.Dx;
        }
    }
}