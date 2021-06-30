using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.CompilerServices;

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
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;
            var vectors = points.Select(p => {
                minX = Math.Min(minX, (int)(p.X-1));
                minY = Math.Min(minY, (int)(p.Y-1));
                maxX = Math.Max(maxX, (int)(p.X+1));
                maxY = Math.Max(maxY, (int)(p.Y+1));
                return new Vector2(p);
            }).ToArray();
            minX = Math.Max(img.Bounds.Left, minX);
            maxX = Math.Min(img.Bounds.Right, maxX);
            minY = Math.Max(img.Bounds.Top, minY);
            maxY = Math.Min(img.Bounds.Bottom, maxY);

            // Pick a distance function based on the fill rule
            Func<Vector2, Vector2[], double> distanceFunc = mode switch
            {
                FillMode.Alternate => PolygonDistanceAlternate,
                FillMode.Winding => PolygonDistanceWinding,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
            
            // Do a general rendering of the function
            RenderDistanceFunction(img, minY, maxY, minX, maxX, distanceFunc, vectors, b, g, r);
        }

        private static void RenderDistanceFunction(ByteImage img, int minY, int maxY, int minX, int maxX, Func<Vector2, Vector2[], double> distanceFunc, Vector2[] vectors, byte b, byte g, byte r)
        {
            // Scan through the bounds, skipping where possible
            // filling area based on distance to edge (internal in negative)
            for (int y = minY; y < maxY; y++)
            {
                var rowOffset = y * img!.RowBytes;
                for (int x = minX; x < maxX; x++)
                {
                    var s = new Vector2(x, y);
                    var d = distanceFunc!(s, vectors);

                    if (d >= 1) // outside the iso-surface
                    {
                        x += (int) (d - 1);// jump if distance is big enough, to save calculations
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}