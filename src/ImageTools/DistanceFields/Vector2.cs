using System;
using System.Drawing;

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

        public static Vector2 operator- (Vector2 a, Vector2 b) {
            return new Vector2{ Dx = a.Dx - b.Dx, Dy = a.Dy - b.Dy};
        }
        public static Vector2 operator+ (Vector2 a, Vector2 b) {
            return new Vector2{ Dx = a.Dx + b.Dx, Dy = a.Dy + b.Dy};
        }
        
        public static Vector2 operator/ (Vector2 a, double b) {
            return new Vector2{ Dx = a.Dx / b, Dy = a.Dy / b};
        }
        public static Vector2 operator* (Vector2 a, double b) {
            return new Vector2{ Dx = a.Dx * b, Dy = a.Dy * b};
        }

        public double Length()
        {
            return Math.Sqrt(Dx*Dx + Dy*Dy);
        }

        public Vector2 Abs()
        {
            return new Vector2{Dx = Math.Abs(Dx), Dy = Math.Abs(Dy)};
        }

        public Vector2 Max(double v)
        {
            return new Vector2{Dx = Math.Max(Dx,v), Dy = Math.Max(Dy,v)};
        }

        public static double Dot(Vector2 a, Vector2 b)
        {
            return a.Dx * b.Dx + a.Dy * b.Dy;
        }
        
        public static double Cross(Vector2 v0, Vector2 v1) {
            return v0.Dx * v1.Dy - v0.Dy * v1.Dx;
        }
    }
}