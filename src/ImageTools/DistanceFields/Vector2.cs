using System;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace ImageTools.DistanceFields
{
    public struct BoolVec3
    {
        public bool A, B, C;

        public BoolVec3(bool a, bool b, bool c)
        {
            A = a;
            B = b;
            C = c;
        }

        public bool All() => A && B && C;

        public bool None() => !A && !B && !C;
    }

    public struct Vector3
    {
        public double Dx, Dy, Dz;

        public Vector3(double x, double y, double z) { Dx = x; Dy = y; Dz = z; }

        public Vector2 SplitXY_Z(out double z)
        {
            z = Dz;
            return new Vector2(Dx, Dy);
        }
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