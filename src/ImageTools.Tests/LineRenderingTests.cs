using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class LineRenderingTests
    {
        [Test]
        public void rendering_single_anti_aliased_line()
        {
            using (var bmp = new Bitmap(512,512, PixelFormat.Format32bppArgb))
            {
                Draw.LineOnBitmap(bmp, x1: 25, y1: 50, x2: 500, y2: 460, color: 0xffAA5577);
                Draw.LineOnBitmap(bmp, x1: 25, y1: 50, x2: 500, y2: 220, color: 0xffEEDDCC);
                Draw.LineOnBitmap(bmp, x1: 25, y1: 50, x2: 150, y2: 500, color: 0xffFFffFF);
                bmp.SaveBmp("./outputs/draw-aa-single.bmp");
            }
            Assert.That(Load.FileExists("./outputs/draw-aa-single.bmp"));
        }

        [Test]
        public void render_a_single_anti_aliased_line_with_thickness()
        {
            using (var bmp = new Bitmap(512,512, PixelFormat.Format32bppArgb))
            {
                Draw.LineOnBitmap(bmp, thickness:0.1, x1: 25, y1: 50, x2: 500, y2: 220, color: 0xffEEDDCC);
                Draw.LineOnBitmap(bmp, thickness:1.0, x1: 25, y1: 50, x2: 500, y2: 460, color: 0xffAA5577);
                Draw.LineOnBitmap(bmp, thickness:3.0, x1: 25, y1: 50, x2: 150, y2: 500, color: 0xffFFffFF);
                bmp.SaveBmp("./outputs/draw-aa-thick.bmp");
            }
            Assert.That(Load.FileExists("./outputs/draw-aa-thick.bmp"));
        }
    }

    public static class Draw
    {
        public static void LineOnBitmap(Bitmap bmp, double thickness, double x1, double y1, double x2, double y2, uint color)
        {
            if (bmp == null) return;
            var rect = RectOf(bmp);
            var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var bytes = new byte[data.Stride*data.Height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                
                byte r = (byte) ((color>>16) & 0xff);
                byte g = (byte) ((color>>8) & 0xff);
                byte b = (byte) ((color) & 0xff);
                DistanceLine(bytes, data.Stride, thickness, x1,y1,x2,y2, r,g,b, rect);
                
                Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }


        public static void LineOnBitmap(Bitmap bmp, int x1, int y1, int x2, int y2, uint color)
        {
            if (bmp == null) return;
            var data = bmp.LockBits(RectOf(bmp), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var bytes = new byte[data.Stride*data.Height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                
                byte r = (byte) ((color>>16) & 0xff);
                byte g = (byte) ((color>>8) & 0xff);
                byte b = (byte) ((color) & 0xff);
                CoverageLine(bytes, data.Stride, x1,y1,x2,y2, r,g,b);
                
                Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        private static Rectangle RectOf(Image bmp) => new Rectangle(0,0, bmp?.Width ?? 0, bmp?.Height ?? 0);

        /// <summary>
        /// Distance based anti-aliased line with thickness
        /// </summary>
        private static void DistanceLine(byte[] data, int rowBytes,
            double thickness, // line thickness
            double x1, double y1, double x2, double y2, // endpoints
            byte cr, byte cg, byte cb, // color
            Rectangle rectangle) // bounds of image
        {
            if (data == null || rowBytes < 1) return;
            
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
            
            minX = Math.Max(rectangle.Left, minX);
            maxX = Math.Min(rectangle.Right, maxX);
            minY = Math.Max(rectangle.Top, minY);
            maxY = Math.Min(rectangle.Bottom, maxY);
            
            for (int y = minY; y < maxY; y++)
            {
                var minD = 1000.0;
                var rowOffset = y * rowBytes;
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
                    
                    data[pixelOffset + 0] = (byte) (data[pixelOffset + 0] * d + cb * f);
                    data[pixelOffset + 1] = (byte) (data[pixelOffset + 1] * d + cg * f);
                    data[pixelOffset + 2] = (byte) (data[pixelOffset + 2] * d + cr * f);
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

        /// <summary>
        /// Single pixel, scanline based anti-aliased line
        /// </summary>
        private static void CoverageLine(
            byte[] data, int rowBytes, // target buffer
            int x0, int y0, int x1, int y1, // line coords
            byte r, byte g, byte b // draw color
        )
        {
            if (data == null || rowBytes < 1) return;
            int dx = x1 - x0, sx = (dx < 0) ? -1 : 1;
            int dy = y1 - y0, sy = (dy < 0) ? -1 : 1;

            // dx and dy always positive. sx & sy hold the sign
            if (dx < 0) dx = -dx;
            if (dy < 0) dy = -dy;

            // adjust for the 1-pixel offset our paired-pixels approximation gives
            if (sy < 0)
            {
                y0++;
                y1++;
            }

            if (sx < 0)
            {
                x0++;
                x1++;
            }

            int pairOffset = (dx > dy ? rowBytes : 4); // paired pixel for AA, as byte offset from main pixel.

            int coverAdj = (dx + dy) / 2; // to adjust `err` so it's centred over 0
            int err = 0; // running error
            int ds = (dx >= dy ? -sy : sx); // error sign
            int errOff = (dx > dy ? dx + dy : 0); // error adjustment

            for (;;)
            {
                // rough approximation of coverage, based on error
                int v = (err + coverAdj - errOff) * ds;
                if (v > 127) v = 127;
                if (v < -127) v = -127;
                var lv = 128 + v; // 'left' coverage,  0..255
                var rv = 128 - v; // 'right' coverage, 0..255

                // set primary pixel, mixing original colour with target colour
                var pixelOffset = (y0 * rowBytes) + (x0 * 4); // target pixel as byte offset from base

                //                         [ existing colour mix        ]   [ line colour mix ]
                data[pixelOffset + 0] = (byte) (((data[pixelOffset + 0] * lv) >> 8) + ((b * rv) >> 8));
                data[pixelOffset + 1] = (byte) (((data[pixelOffset + 1] * lv) >> 8) + ((g * rv) >> 8));
                data[pixelOffset + 2] = (byte) (((data[pixelOffset + 2] * lv) >> 8) + ((r * rv) >> 8));

                pixelOffset += pairOffset; // switch to the 'other' pixel

                data[pixelOffset + 0] = (byte) (((data[pixelOffset + 0] * rv) >> 8) + ((b * lv) >> 8));
                data[pixelOffset + 1] = (byte) (((data[pixelOffset + 1] * rv) >> 8) + ((g * lv) >> 8));
                data[pixelOffset + 2] = (byte) (((data[pixelOffset + 2] * rv) >> 8) + ((r * lv) >> 8));


                // end of line check
                if (x0 == x1 && y0 == y1) break;

                int e2 = err;
                if (e2 > -dx)
                {
                    err -= dy;
                    x0 += sx;
                }

                if (e2 < dy)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }
    }
}