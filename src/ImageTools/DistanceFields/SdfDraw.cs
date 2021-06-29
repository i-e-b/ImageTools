using System;

namespace ImageTools.DistanceFields
{
    public static class SdfDraw
    {
        /// <summary>
        /// Distance based anti-aliased line with thickness.
        /// This gives a much nicer result than coverage-based, but is slower
        /// </summary>
        public static void LineOnBitmap(ByteImage img, double thickness, double x1, double y1, double x2, double y2, uint color)
        {
            if (img == null) return;
            byte r = (byte) ((color >> 16) & 0xff);
            byte g = (byte) ((color >> 8) & 0xff);
            byte b = (byte) ((color) & 0xff);
            DistanceLine(img, thickness, x1, y1, x2, y2, r, g, b);
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
            
            int i = 0;

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
                    i++;
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
            
            Console.WriteLine($"{i} point calculations");
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
    }
}