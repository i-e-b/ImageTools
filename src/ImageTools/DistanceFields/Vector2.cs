using System;

namespace ImageTools.DistanceFields
{
    public struct Vector2
    {
        public double Dx, Dy;

        public Vector2(double x, double y)
        {
            Dx = x; Dy = y;
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
    }
}