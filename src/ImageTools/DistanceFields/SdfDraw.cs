﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.CompilerServices;
using ImageTools.GeneralTypes;

namespace ImageTools.DistanceFields
{
    public static class SdfDraw
    {
        /// <summary>
        /// Distance based anti-aliased line with thickness.
        /// This gives a much nicer result than coverage-based, but is slower
        /// </summary>
        public static void DrawLine(ByteImage img, double thickness, double x1, double y1, double x2, double y2, uint color)
        {
            if (img == null) return;
            byte r = (byte) ((color >> 16) & 0xff);
            byte g = (byte) ((color >> 8) & 0xff);
            byte b = (byte) ((color) & 0xff);
            DistanceLine(img, thickness, x1, y1, x2, y2, r, g, b);
        }

        /// <summary>
        /// Distance based fill of polygon
        /// </summary>
        public static void FillPolygon(ByteImage img, PointF[] points, uint color, FillMode mode)
        {
            // Basic setup
            if (img?.PixelBytes == null || points == null) return;
            if (points.Length < 3) return; // area is empty
            byte r = (byte) ((color >> 16) & 0xff);
            byte g = (byte) ((color >> 8) & 0xff);
            byte b = (byte) ((color) & 0xff);
            
            // Get bounds, and cast points to vectors
            MinMaxRange(out var minX, out var minY, out var maxX, out var maxY);
            var vectors = points.Select(p =>
            {
                ExpandRange(p, ref minX, ref minY, ref maxX, ref maxY);
                return new Vector2(p);
            }).ToArray();
            ReduceMinMaxToBounds(img, ref minX, ref maxX, ref minY, ref maxY);

            // Pick a distance function based on the fill rule
            Func<Vector2, double> distanceFunc = mode switch
            {
                FillMode.Alternate => p => PolygonDistanceAlternate(p, vectors),
                FillMode.Winding => p => PolygonDistanceWinding(p, vectors),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
            
            // Do a general rendering of the function
            RenderDistanceFunction(img, minY, maxY, minX, maxX, distanceFunc, b, g, r);
        }

        public static void FillPolygon(ByteImage img, Contour[] contours, uint color, FillMode mode)
        {
            // basic setup
            if (img?.PixelBytes == null || contours == null) return;
            if (contours.Length < 1) return; // area is empty
            byte r = (byte) ((color >> 16) & 0xff);
            byte g = (byte) ((color >> 8) & 0xff);
            byte b = (byte) ((color) & 0xff);
            
            // Get bounds, and extract point pairs
            MinMaxRange(out var minX, out var minY, out var maxX, out var maxY);
            var allPairs = new List<VecSegment2>();
            foreach (var contour in contours)
            {
                var pairCount = contour!.PairCount();
                for (int i = 0; i < pairCount; i++)
                {
                    var pair = contour.Pair(i);
                    allPairs.Add(pair);
                    
                    ExpandRange(pair!.A, ref minX, ref minY, ref maxX, ref maxY);
                    ExpandRange(pair!.B, ref minX, ref minY, ref maxX, ref maxY);
                }
            }
            ReduceMinMaxToBounds(img, ref minX, ref maxX, ref minY, ref maxY);
            var pairs = allPairs.ToArray();
            
            // Pick a distance function based on the fill rule
            Func<Vector2, double> distanceFunc = mode switch
            {
                FillMode.Alternate => p => PolygonDistanceAlternate(p, pairs),
                FillMode.Winding => p => PolygonDistanceWinding(p, pairs),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
            
            // Do a general rendering of the function
            RenderDistanceFunction(img, minY, maxY, minX, maxX, distanceFunc, b, g, r);
        }

        /// <summary>
        /// Draw a set of lines, using `z` co-ord as line radius
        /// </summary>
        public static void DrawPressureCurve(ByteImage img, uint color, Vector3[] curve)
        {
            // Basic setup
            if (img?.PixelBytes == null || curve == null) return;
            if (curve.Length < 2) return; // line is a point. TODO: handle this
            byte r = (byte) ((color >> 16) & 0xff);
            byte g = (byte) ((color >> 8) & 0xff);
            byte b = (byte) ((color) & 0xff);
            
            // Determine bounds
            MinMaxRange(out var minX, out var minY, out var maxX, out var maxY);
            foreach (var point in curve)
            {
                ExpandRange(point, ref minX, ref minY, ref maxX, ref maxY);
                minX = Math.Min(minX, (int)(point.Dx - point.Dz));
                maxX = Math.Max(maxX, (int)(point.Dx + point.Dz));
                minY = Math.Min(minY, (int)(point.Dy - point.Dz));
                maxY = Math.Max(maxY, (int)(point.Dy + point.Dz));
            }
            ReduceMinMaxToBounds(img, ref minX, ref maxX, ref minY, ref maxY);
            
            
            // Build a distance function (we do all the line segments
            // at once so the blending works correctly)
            double DistanceFunc(Vector2 p)
            {
                var d = double.MaxValue;
                var lim = curve.Length - 1;
                for (int i = 0; i < lim; i++)
                {
                    d = Math.Min(d, UnevenCapsule(p, curve[i], curve[i + 1])); // 'min' in sdf is equivalent to logical 'or' in bitmaps
                }

                return d;
            }

            // Do a general rendering of the function
            RenderDistanceFunction(img, minY, maxY, minX, maxX, DistanceFunc, b, g, r);
        }

        private static void RenderDistanceFunction(ByteImage img, int minY, int maxY, int minX, int maxX, Func<Vector2, double> distanceFunc, byte b, byte g, byte r)
        {
            // Scan through the bounds, skipping where possible
            // filling area based on distance to edge (internal in negative)
            for (int y = minY; y < maxY; y++)
            {
                var rowOffset = y * img!.RowBytes;
                for (int x = minX; x < maxX; x++)
                {
                    var s = new Vector2(x, y);
                    var d = distanceFunc!(s);

                    if (d >= 1) // outside the iso-surface
                    {
                        //img.PixelBytes![rowOffset + (x<<2)] = 255; // uncomment to get a debug view of jump points
                        x += (int) (d - 1);// jump if distance is big enough to save calculations, but don't get too close or we break anti-aliasing.
                        continue;
                    }

                    var pixelOffset = rowOffset + x * 4; // target pixel as byte offset from base

                    if (d < 0) // Inside the iso-surface
                    {
                        var id = (int) -d;

                        if (id >= 2) // if we're deep inside the polygon, draw big spans of pixels without shading
                        {
                            id = Math.Min(maxX - x, id);
                            for (int j = 0; j < id; j++)
                            {
                                img.PixelBytes![pixelOffset++] = b;
                                img.PixelBytes [pixelOffset++] = g;
                                img.PixelBytes [pixelOffset++] = r;
                                pixelOffset++; // skip alpha
                                x++;
                            }

                            x--;
                            continue;
                        }

                        d = 0;
                    }

                    // Not outside or inside -- we're within 1 pixel of the edge
                    // so draw a single pixel with blending and advance a single pixel
                    
                    var f = 1 - d; // inverse of the distance (how much fill color to blend in)

                    img.PixelBytes![pixelOffset + 0] = (byte) (img.PixelBytes[pixelOffset + 0] * d + b * f);
                    img.PixelBytes [pixelOffset + 1] = (byte) (img.PixelBytes[pixelOffset + 1] * d + g * f);
                    img.PixelBytes [pixelOffset + 2] = (byte) (img.PixelBytes[pixelOffset + 2] * d + r * f);
                }
            }
        }

        /// <summary>
        /// Distance based anti-aliased line with thickness
        /// </summary>
        private static void DistanceLine(ByteImage img,
            double thickness, // line thickness
            double x1, double y1, double x2, double y2, // endpoints
            byte cr, byte cg, byte cb) // color
        {
            if (img?.PixelBytes == null || img.RowBytes < 1) return;
            
            // For each pixel 'near' the line, we compute a signed distance to the line edge (negative is interior)
            // We start in a box slightly larger than our bounds, and a ray-marching algorithm to (slightly)
            // reduce the number of calculations.
            
            var a = new Vector2(x1, y1);
            var b = new Vector2(x2, y2);
            
            var minX = (int) (Math.Min(x1, x2) - thickness);
            var minY = (int) (Math.Min(y1, y2) - thickness);
            var maxX = (int) (Math.Max(x1, x2) + thickness);
            var maxY = (int) (Math.Max(y1, y2) + thickness);
            
            minX = Math.Max(img.Bounds.Left, minX);
            maxX = Math.Min(img.Bounds.Right, maxX);
            minY = Math.Max(img.Bounds.Top, minY);
            maxY = Math.Min(img.Bounds.Bottom, maxY);
            
            for (int y = minY; y < maxY; y++)
            {
                var minD = 1000.0;
                var rowOffset = y * img.RowBytes;
                for (int x = minX; x < maxX; x++)
                {
                    var s = new Vector2(x,y);
                    var d = OrientedBox(s, a, b, thickness);
                    
                    // jump if distance is big enough, to save calculations
                    if (d >= 2)
                    {
                        x += (int) (d - 1);
                        continue;
                    }

                    minD = Math.Min(minD, d);
                    if (d <= 0) d = 0; // we could optimise very thick lines here by filling a span size of -d
                    else if (d > 1)
                    {
                        if (d > minD) break; // we're moving further from the line, move to next scan
                        d = 1;
                    }

                    var f = 1 - d;
                    
                    var pixelOffset = rowOffset + x*4; // target pixel as byte offset from base
                    
                    img.PixelBytes[pixelOffset + 0] = (byte) (img.PixelBytes[pixelOffset + 0] * d + cb * f);
                    img.PixelBytes[pixelOffset + 1] = (byte) (img.PixelBytes[pixelOffset + 1] * d + cg * f);
                    img.PixelBytes[pixelOffset + 2] = (byte) (img.PixelBytes[pixelOffset + 2] * d + cr * f);
                }
            }
        }
        
        // square ends
        private static double OrientedBox(Vector2 samplePoint, Vector2 a, Vector2 b, double thickness)
        {
            var l = (b - a).Length();
            var d = (b - a) / l;
            var q = (samplePoint - (a + b) * 0.5);
            q = new Matrix2(d.Dx, -d.Dy, d.Dy, d.Dx) * q;
            q = q.Abs() - (new Vector2(l, thickness)) * 0.5;
            return q.Max(0.0).Length() + Math.Min(Math.Max(q.Dx, q.Dy), 0.0);    
        }
        
        // line with a linearly varying line width
        private static double UnevenCapsule(Vector2 samplePoint, Vector3 va, Vector3 vb)
        {
            var pa = va.SplitXY_Z(out var ra);
            var pb = vb.SplitXY_Z(out var rb);
            
            // rotate to standard form
            samplePoint  -= pa;
            pb -= pa;
            var h = Vector2.Dot(pb,pb);
            var q = new Vector2(Vector2.Dot(samplePoint, new Vector2(pb.Dy, -pb.Dx)), Vector2.Dot(samplePoint, pb)) / h;
    
            //-----------
    
            q.Dx = Math.Abs(q.Dx);
    
            var b = ra-rb;
            var c = new Vector2(Math.Sqrt(h-b*b),b);
    
            var k = Vector2.Cross(c,q);
            var m = Vector2.Dot(c,q);
            var n = Vector2.Dot(q,q);
    
            if( k < 0.0 )  return Math.Sqrt(h*(n             )) - ra;
            if( k > c.Dx ) return Math.Sqrt(h*(n+1.0-2.0*q.Dy)) - rb;
            return                     m                        - ra;
        }

        // https://www.shadertoy.com/view/wdBXRW
        private static double PolygonDistanceAlternate(Vector2 p, Vector2[] v)
        {
            var num = v!.Length;
            var d = Vector2.Dot(p - v[0], p - v[0]);
            var s = 1.0;
            for (int i = 0, j = num - 1; i < num; j = i, i++)
            {
                // distance
                var e = v[j] - v[i];
                var w = p - v[i];
                var b = w - e * Clamp(Vector2.Dot(w, e) / Vector2.Dot(e, e), 0.0, 1.0);
                d = Math.Min(d, Vector2.Dot(b, b));

                // winding number
                var cond = new BoolVec3(p.Dy >= v[i].Dy,
                    p.Dy < v[j].Dy,
                    e.Dx * w.Dy > e.Dy * w.Dx);
                if (cond.All() || cond.None()) s = -s;
            }

            return s * Math.Sqrt(d);
        }
        
        // Same as above, but input is disconnected line segments
        private static double PolygonDistanceAlternate(Vector2 p, VecSegment2[] v)
        {
            var num = v!.Length;
            var d = Vector2.Dot(p - v[0].A, p - v[0].A);
            var s = 1.0;
            for (int i = 0; i < num; i++)
            {
                // distance
                var e = v[i].B - v[i].A;
                var w = p - v[i].A;
                var b = w - e * Clamp(Vector2.Dot(w, e) / Vector2.Dot(e, e), 0.0, 1.0);
                d = Math.Min(d, Vector2.Dot(b, b));

                // winding number
                var cond = new BoolVec3(p.Dy >= v[i].A.Dy,
                    p.Dy < v[i].B.Dy,
                    e.Dx * w.Dy > e.Dy * w.Dx);
                if (cond.All() || cond.None()) s = -s;
            }

            return s * Math.Sqrt(d);
        }

        // https://www.shadertoy.com/view/WdSGRd
        private static double PolygonDistanceWinding(Vector2 p, Vector2[] poly)
        {
            var length = poly!.Length;
            var e = new Vector2[length];
            var v = new Vector2[length];
            var pq = new Vector2[length];
            
            // data
            for (int i = 0; i < length; i++)
            {
                int i2 = (int) ((float) (i + 1) % length); //i+1
                e[i] = poly[i2] - poly[i];
                v[i] = p - poly[i];
                pq[i] = v[i] - e[i] * Clamp(Vector2.Dot(v[i], e[i]) / Vector2.Dot(e[i], e[i]), 0.0, 1.0);
            }

            //distance
            var d = Vector2.Dot(pq[0], pq[0]);
            for (int i = 1; i < length; i++)
            {
                d = Math.Min(d, Vector2.Dot(pq[i], pq[i]));
            }

            //winding number
            // from http://geomalgorithms.com/a03-_inclusion.html
            var wn = 0;
            for (var i = 0; i < length; i++)
            {
                var i2 = (int) ((float) (i + 1) % length);
                var cond1 = 0.0 <= v[i].Dy;
                var cond2 = 0.0 > v[i2].Dy;
                var val3 = Vector2.Cross(e[i], v[i]); //isLeft
                wn += cond1 && cond2 && val3 > 0.0 ? 1 : 0; // have  a valid up intersect
                wn -= !cond1 && !cond2 && val3 < 0.0 ? 1 : 0; // have  a valid down intersect
            }

            var s = wn == 0 ? 1.0 : -1.0; // flip distance if we're inside
            return Math.Sqrt(d) * s;
        }
        
        // Same as above, but input is disconnected line segments
        private static double PolygonDistanceWinding(Vector2 p, VecSegment2[] poly)
        {
            var length = poly!.Length;
            var e = new Vector2[length];
            var v = new Vector2[length];
            var v2 = new Vector2[length];
            var pq = new Vector2[length];
            
            // data
            for (int i = 0; i < length; i++)
            {
                e[i] = poly[i].B - poly[i].A;
                v[i] = p - poly[i].A;
                v2[i] = p - poly[i].B;
                pq[i] = v[i] - e[i] * Clamp(Vector2.Dot(v[i], e[i]) / Vector2.Dot(e[i], e[i]), 0.0, 1.0);
            }

            //distance
            var d = Vector2.Dot(pq[0], pq[0]);
            for (int i = 1; i < length; i++)
            {
                d = Math.Min(d, Vector2.Dot(pq[i], pq[i]));
            }

            //winding number
            // from http://geomalgorithms.com/a03-_inclusion.html
            var wn = 0;
            for (int i = 0; i < length; i++)
            {
                var cond1 = 0.0 <= v[i].Dy;
                var cond2 = 0.0 > v2[i].Dy;
                var val3 = Vector2.Cross(e[i], v[i]); //isLeft
                wn += cond1 && cond2 && val3 > 0.0 ? 1 : 0; // have  a valid up intersect
                wn -= !cond1 && !cond2 && val3 < 0.0 ? 1 : 0; // have  a valid down intersect
            }

            var s = wn == 0 ? 1.0 : -1.0; // flip distance if we're inside
            return Math.Sqrt(d) * s;
        }

        
        #region Helpers
        private static void ReduceMinMaxToBounds(ByteImage img, ref int minX, ref int maxX, ref int minY, ref int maxY)
        {
            minX = Math.Max(img!.Bounds.Left, minX);
            maxX = Math.Min(img.Bounds.Right, maxX);
            minY = Math.Max(img.Bounds.Top, minY);
            maxY = Math.Min(img.Bounds.Bottom, maxY);
        }
        

        private static void ExpandRange(PointF p, ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            minX = Math.Min(minX, (int) (p.X - 2));
            minY = Math.Min(minY, (int) (p.Y - 2));
            maxX = Math.Max(maxX, (int) (p.X + 2));
            maxY = Math.Max(maxY, (int) (p.Y + 2));
        }
        
        private static void ExpandRange(Vector2 p, ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            minX = Math.Min(minX, (int) (p.Dx - 2));
            minY = Math.Min(minY, (int) (p.Dy - 2));
            maxX = Math.Max(maxX, (int) (p.Dx + 2));
            maxY = Math.Max(maxY, (int) (p.Dy + 2));
        }
        
        private static void ExpandRange(Vector3 p, ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            minX = Math.Min(minX, (int)(p.Dx - p.Dz)-2);
            maxX = Math.Max(maxX, (int)(p.Dx + p.Dz)+2);
            minY = Math.Min(minY, (int)(p.Dy - p.Dz)-2);
            maxY = Math.Max(maxY, (int)(p.Dy + p.Dz)+2);
        }

        private static void MinMaxRange(out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = int.MaxValue;
            minY = int.MaxValue;
            maxX = int.MinValue;
            maxY = int.MinValue;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
        #endregion
    }
}