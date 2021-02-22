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
            // TODO: also do this in a linear & planar color space
            using (var bmp = new Bitmap(512,512, PixelFormat.Format32bppArgb))
            {
                Draw.LineOnBitmap(bmp, x1: 25, y1: 50, x2: 500, y2: 460, color: 0xffAA5577);
                Draw.LineOnBitmap(bmp, x1: 25, y1: 50, x2: 500, y2: 220, color: 0xffEEDDCC);
                bmp.SaveBmp("./outputs/draw-aa-single.bmp");
            }
            Assert.That(Load.FileExists("./outputs/draw-aa-single.bmp"));
        }

        [Test]
        [TestCase(0.5)]
        [TestCase(1.0)]
        [TestCase(1.5)]
        [TestCase(3.0)]
        [TestCase(10.0)]
        public void render_a_single_anti_aliased_line_with_thickness(double thickness)
        {
            Assert.Fail("Not yet implemented");
        }
    }

    public static class Draw
    {
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