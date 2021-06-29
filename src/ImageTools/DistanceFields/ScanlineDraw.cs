using System;

namespace ImageTools.DistanceFields
{
    /// <summary>
    /// Does the same stuff as SdfDraw, for comparison.
    /// This tends to be around 10x faster, and about 5x worse looking
    /// </summary>
    public static class ScanlineDraw
    {
#region Tables
        private static readonly byte[] _gammaAdjust = new byte[256];
        /// <summary>
        /// Set up tables
        /// </summary>
        static ScanlineDraw()
        {
            SetGamma(2.2);
        }

        public static void SetGamma(double gamma)
        {
            var m = 0.0;
            var n = 255 / Math.Pow(255, 1/gamma);
            for (int i = 0; i < 256; i++)
            {
                var x = Math.Pow(i, 1/gamma) * n;
                m = Math.Max(x,m);
                if (x > 255) x = 255;
                _gammaAdjust![i] = (byte)x;
            }
            Console.WriteLine($"Max = {m}, n = {n}");
        }
#endregion

        /// <summary>
        /// Coverage based anti-aliased line fixed to 1-pixel total.
        /// This gives a very rough effect, but is quite fast.
        /// </summary>
        public static void DrawLine(ByteImage img, int x1, int y1, int x2, int y2, uint color)
        {
            if (img == null) return;

            byte r = (byte) ((color >> 16) & 0xff);
            byte g = (byte) ((color >> 8) & 0xff);
            byte b = (byte) ((color) & 0xff);
            CoverageLine(img, x1, y1, x2, y2, r, g, b);
        }

        /// <summary>
        /// Single pixel, scanline based anti-aliased line
        /// </summary>
        private static void CoverageLine(
            ByteImage img, // target buffer
            int x0, int y0, int x1, int y1, // line coords
            byte r, byte g, byte b // draw color
        )
        {
            if (img?.PixelBytes == null || img.RowBytes < 1) return;
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

            int pairOffset = (dx > dy ? img.RowBytes : 4); // paired pixel for AA, as byte offset from main pixel.

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
                var leftCover = 128 + v; // 'left' coverage,  0..255
                var rightCover = 128 - v; // 'right' coverage, 0..255

                // Gamma adjust (not doing this results in really ugly lines)
                leftCover = _gammaAdjust![leftCover];
                var antiLeft = 255 - leftCover;
                rightCover = _gammaAdjust![rightCover];
                var antiRight = 255 - leftCover;
                
                // set primary pixel, mixing original colour with target colour
                var pixelOffset = (y0 * img.RowBytes) + (x0 * 4); // target pixel as byte offset from base

                //                                          [              existing colour mix               ]   [   line colour mix   ]
                img.PixelBytes[pixelOffset + 0] = (byte) (((img.PixelBytes[pixelOffset + 0] * antiRight) >> 8) + ((b * rightCover) >> 8));
                img.PixelBytes[pixelOffset + 1] = (byte) (((img.PixelBytes[pixelOffset + 1] * antiRight) >> 8) + ((g * rightCover) >> 8));
                img.PixelBytes[pixelOffset + 2] = (byte) (((img.PixelBytes[pixelOffset + 2] * antiRight) >> 8) + ((r * rightCover) >> 8));

                pixelOffset += pairOffset; // switch to the 'other' pixel

                img.PixelBytes[pixelOffset + 0] = (byte) (((img.PixelBytes[pixelOffset + 0] * antiLeft) >> 8) + ((b * leftCover) >> 8));
                img.PixelBytes[pixelOffset + 1] = (byte) (((img.PixelBytes[pixelOffset + 1] * antiLeft) >> 8) + ((g * leftCover) >> 8));
                img.PixelBytes[pixelOffset + 2] = (byte) (((img.PixelBytes[pixelOffset + 2] * antiLeft) >> 8) + ((r * leftCover) >> 8));


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