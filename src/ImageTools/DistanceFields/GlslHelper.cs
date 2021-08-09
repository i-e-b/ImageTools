using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using ImageTools.GeneralTypes;
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBeProtected.Global

namespace ImageTools.DistanceFields
{
    public abstract class GlslHelper
    {
        protected static void RenderDistanceFunction(ByteImage img, int minY, int maxY, int minX, int maxX, Func<Vector2, double> distanceFunc, byte b, byte g, byte r)
        {
            // Scan through the bounds, skipping where possible
            // filling area based on distance to edge (internal in negative)
            for (int y = minY; y < maxY; y++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    var s = new Vector2(x, y);
                    var d = distanceFunc!(s);

                    // TODO: when we jump, have an expected next distance
                    //       if we're far off that, back-track and try again
                    //       this should let us use 'approximate' distance
                    //       functions with much better output.
                    
                    if (d > 1) // outside the iso-surface
                    {
                        x += (int) (d - 1); // jump if distance is big enough to save calculations, but don't get too close or we break anti-aliasing.
                        continue;
                    }

                    if (d < -1) // Inside the iso-surface
                    {
                        var id = (int) -d;

                        id = Math.Min(maxX - x, id - 1/*allow for some error*/);// don't run off the edge
                        img!.SetSpan(y, x, x + id, r, g, b);

                        x += id;
                        continue;
                    }

                    // Not outside or inside -- we're within 1 pixel of the edge
                    // so draw a single pixel with blending and advance a single pixel

                    var f = (d > 0) ? 1 - d : 1; // convert distance to blend
                    f = Clamp(f, 0, 1);
                    var blend = (byte)(f * 255);

                    img!.BlendPixel(x,y,blend, r,g,b);
                }
            }
        }

        protected static void SplitColor(uint color, out byte r, out byte g, out byte b)
        {
            r = (byte) ((color >> 16) & 0xff);
            g = (byte) ((color >> 8)  & 0xff);
            b = (byte) ( color        & 0xff);
        }

        protected static void ReduceMinMaxToBounds(ByteImage img, ref int minX, ref int maxX, ref int minY, ref int maxY)
        {
            minX = Math.Max(img!.Bounds.Left, minX);
            maxX = Math.Min(img.Bounds.Right, maxX);
            minY = Math.Max(img.Bounds.Top, minY);
            maxY = Math.Min(img.Bounds.Bottom, maxY);
        }

        protected static void ExpandRange(PointF p, ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            minX = Math.Min(minX, (int) (p.X - 2));
            minY = Math.Min(minY, (int) (p.Y - 2));
            maxX = Math.Max(maxX, (int) (p.X + 2));
            maxY = Math.Max(maxY, (int) (p.Y + 2));
        }
        
        protected static void ExpandRange(Vector2 p, ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            minX = Math.Min(minX, (int) (p.X - 2));
            minY = Math.Min(minY, (int) (p.Y - 2));
            maxX = Math.Max(maxX, (int) (p.X + 2));
            maxY = Math.Max(maxY, (int) (p.Y + 2));
        }
        
        protected static void ExpandRange(Vector2 p, Vector2 r, ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            var drp = max(abs(p-abs(r)) * 1.6);
            minX = Math.Min(minX, (int) (p.X - drp - 3));
            minY = Math.Min(minY, (int) (p.Y - drp - 3));
            maxX = Math.Max(maxX, (int) (p.X + drp + 4));
            maxY = Math.Max(maxY, (int) (p.Y + drp + 4));
        }
        
        protected static void ExpandRange(double x, double y, ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            minX = Math.Min(minX, (int) (x - 2));
            minY = Math.Min(minY, (int) (y - 2));
            maxX = Math.Max(maxX, (int) (x + 2));
            maxY = Math.Max(maxY, (int) (y + 2));
        }
        
        protected static void ExpandRange(Vector3 p, ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            minX = Math.Min(minX, (int)(p.X - p.Z)-2);
            maxX = Math.Max(maxX, (int)(p.X + p.Z)+2);
            minY = Math.Min(minY, (int)(p.Y - p.Z)-2);
            maxY = Math.Max(maxY, (int)(p.Y + p.Z)+2);
        }

        protected static void MinMaxRange(out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = int.MaxValue;
            minY = int.MaxValue;
            maxX = int.MinValue;
            maxY = int.MinValue;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static double Clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
        
        // helpers when converting from glsl to C#
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static double clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static Vector2 clamp(Vector2 v, double min, double max) => new Vector2(clamp(v.X,min,max),clamp(v.Y,min,max));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static double dot(Vector2 a, Vector2 b) => Vector2.Dot(a,b);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static Vector2 vec2(double x, double y) => new Vector2(x,y);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static Vector2 min(Vector2 a, Vector2 b) => Vector2.ComponentMin(a,b);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static double min(double a, double b) => Math.Min(a,b);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static Vector2 max(Vector2 a, Vector2 b) => Vector2.ComponentMax(a,b);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static double max(Vector2 a) => Math.Max(a.X,a.Y);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static double max(double a, double b) => Math.Max(a,b);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static double sign(double d) => Math.Sign(d);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static double sqrt(double d) => d >= 0 ? Math.Sqrt(d) : 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static double length(Vector2 v) => v.Length();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static Vector2 abs(Vector2 v) => v.Abs();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static double abs(double v) => Math.Abs(v);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static Vector2 normalize(Vector2 v) => v.Normalized();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static double acos(double d) => Math.Acos(d);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static double cos(double d) => Math.Cos(d);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static double sin(double d) => Math.Sin(d);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static double pow(double d, double e) => Math.Pow(d,e);
    }
}