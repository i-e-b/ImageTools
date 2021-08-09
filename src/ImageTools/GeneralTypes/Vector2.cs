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
        public double X, Y;
        
        /// <summary> Return a new vector with X and Y exchanged </summary>
        // ReSharper disable InconsistentNaming
        // ReSharper disable IdentifierTypo
        public Vector2 YX => new Vector2(Y,X);
        public Vector4 XYYX => new Vector4(X,Y,Y,X);
        public Vector4 XXYY => new Vector4(X,X,Y,Y);
        // ReSharper restore InconsistentNaming
        // ReSharper restore IdentifierTypo

        public Vector2(double x, double y)
        {
            X = x; Y = y;
        }

        public Vector2(PointF p)
        {
            X = p.X; Y = p.Y;
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
            return new Vector2{ X = a.X - b.X, Y = a.Y - b.Y};
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator+ (Vector2 a, Vector2 b) {
            return new Vector2{ X = a.X + b.X, Y = a.Y + b.Y};
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator/ (Vector2 a, double b) {
            return new Vector2{ X = a.X / b, Y = a.Y / b};
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator/ (Vector2 a, Vector2 b) {
            return new Vector2{ X = a.X / b.X, Y = a.Y / b.Y};
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator/ (double a, Vector2 b) {
            return new Vector2{ X = a / b.X, Y = a / b.Y};
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator* (Vector2 a, double b) {
            return new Vector2{ X = a.X * b, Y = a.Y * b};
        }
        
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator* (Vector2 a, Vector2 b) {
            return new Vector2{ X = a.X * b.X, Y = a.Y * b.Y};
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Length()
        {
            return Math.Sqrt(X*X + Y*Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 Abs()
        {
            return new Vector2{X = Math.Abs(X), Y = Math.Abs(Y)};
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 Max(double v)
        {
            return new Vector2{X = Math.Max(X,v), Y = Math.Max(Y,v)};
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Dot(Vector2 a, Vector2 b)
        {
            return a.X * b.X + a.Y * b.Y;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Cross(Vector2 v0, Vector2 v1) {
            return v0.X * v1.Y - v0.Y * v1.X;
        }

        public PointF ToPointF() => new PointF((float)X, (float)Y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ComponentMin(Vector2 v0, Vector2 v1)
        {
            return new Vector2{X = Math.Min(v0.X,v1.X), Y = Math.Min(v0.Y,v1.Y)};
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ComponentMax(Vector2 v0, Vector2 v1)
        {
            return new Vector2{X = Math.Max(v0.X,v1.X), Y = Math.Max(v0.Y,v1.Y)};
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 Normalized()
        {
            var length = Math.Sqrt(X*X + Y*Y);
            if (length == 0.0) return new Vector2(0,0);
            return new Vector2(X/length,Y/length);
        }

        public static Vector2 Centre(double x1, double y1, double x2, double y2)
        {
            var l = Math.Min(x1, x2);
            var r = Math.Max(x1, x2);
            var t = Math.Min(y1, y2);
            var b = Math.Max(y1, y2);
            
            var hw = (r-l)/2.0;
            var hh = (b-t)/2.0;
            
            return new Vector2(l+hw, t+hh);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 Offset(double dx, double dy)
        {
            return new Vector2(X+dx, Y+dy);
        }

        public static Vector2 RectangleVector(double x1, double y1, double x2, double y2)
        {
            var l = Math.Min(x1, x2);
            var r = Math.Max(x1, x2);
            var t = Math.Min(y1, y2);
            var b = Math.Max(y1, y2);
            
            var hw = (r-l)/2.0;
            var hh = (b-t)/2.0;
            
            return new Vector2(hw, hh);
        }

        public static Vector2 LowestPair(double x1, double y1, double x2, double y2)
        {
            return new Vector2(Math.Min(x1,x2), Math.Min(y1,y2));
        }

        public static Vector2 HighestPair(double x1, double y1, double x2, double y2)
        {
            return new Vector2(Math.Max(x1,x2), Math.Max(y1,y2));
        }
    }

    public class Vector4
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double W { get; set; }

        public Vector4(double x, double y, double z, double w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
        public Vector4() { }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 operator* (Vector4 a, Vector4 b) {
            return new Vector4{ X = a.X * b.X, Y = a.Y * b.Y, Z = a.Z * b.Z, W = a.W * b.W};
        }
    }
}