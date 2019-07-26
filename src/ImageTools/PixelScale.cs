using System;
using System.Drawing;
using System.Drawing.Imaging;
using ImageTools.ImageDataFormats;
using ImageTools.Utilities;

namespace ImageTools
{
    /// <summary>
    /// Special scaling for pixel art
    /// </summary>
    public static class PixelScale {

        /// <summary>
        /// Use Eric's Pixel Expander to double the image size (also known as AdvMAME2x and Scale2x).
        /// Interpolation is done in YUV space.
        /// </summary>
        public static Bitmap EPX_2x(Bitmap src)
        {
            if (src == null) return null;
            BitmapTools.ImageToPlanesf(src, ColorSpace.RGBToYUV, out var srcY, out var srcU, out var srcV);

            var dstY = new float[srcY.Length*4];
            var dstU = new float[srcU.Length*4];
            var dstV = new float[srcV.Length*4];

            var srcWidth = src.Width;
            var srcHeight = src.Height;
            var dstWidth = srcWidth * 2;

            var dy = dstWidth;
            for (int plane = 0; plane < 3; plane++)
            {
                var small = Pick(plane, srcY, srcU, srcV);
                var large = Pick(plane, dstY, dstU, dstV);

                for (int y = 0; y < srcHeight; y++)
                {
                    var dyo = 2 * y * dstWidth;
                    var syo = y * srcWidth;
                    var row = (y == 0 || y == srcHeight - 1) ? 0 : srcWidth;

                    for (int x = 0; x < srcWidth; x++)
                    {
                        var dx = 2 * x;
                        var col = (x == 0 || x == srcWidth - 1) ? 0 : 1;

                        var _1 = dyo + dx;
                        var _2 = dyo + dx + 1;
                        var _3 = dyo + dy + dx;
                        var _4 = dyo + dy + dx + 1;

                        var P = (int)small[syo + x];
                        var A = (int)small[syo + x - row];
                        var C = (int)small[syo + x - col];
                        var B = (int)small[syo + x + col];
                        var D = (int)small[syo + x + row];

                        large[_1] = P;
                        large[_2] = P;
                        large[_3] = P;
                        large[_4] = P;

                        if (C == A && C != D && A != B) { large[_1] = A; }
                        if (A == B && A != C && B != D) { large[_2] = B; }
                        if (D == C && D != B && C != A) { large[_3] = C; }
                        if (B == D && B != A && D != C) { large[_4] = D; }
                    }
                }
            }

            var dst = new Bitmap(src.Width * 2, src.Height * 2, PixelFormat.Format32bppArgb);
            BitmapTools.PlanesToImage_f(dst, ColorSpace.YUVToRGB, 0, dstY, dstU, dstV);
            return dst;
        }

        private static T Pick<T>(int i, params T[] stuff)
        {
            return stuff[i];
        }

        /// <summary>
        /// Use Eric's Pixel Expander algorithm with a threshold rather than
        /// exact value matching
        /// </summary>
        public static Bitmap EPXT_2x(Bitmap src, int maxDifference)
        {
            if (src == null) return null;
            BitmapTools.ImageToPlanesf(src, ColorSpace.RGBToYUV, out var srcY, out var srcU, out var srcV);

            var dstY = new float[srcY.Length*4];
            var dstU = new float[srcU.Length*4];
            var dstV = new float[srcV.Length*4];

            var srcWidth = src.Width;
            var srcHeight = src.Height;
            var dstWidth = srcWidth * 2;

            var dy = dstWidth;
            for (int plane = 0; plane < 3; plane++)
            {
                var small = Pick(plane, srcY, srcU, srcV);
                var large = Pick(plane, dstY, dstU, dstV);

                for (int y = 0; y < srcHeight; y++)
                {
                    var dyo = 2 * y * dstWidth;
                    var syo = y * srcWidth;
                    var row = (y == 0 || y == srcHeight - 1) ? 0 : srcWidth;

                    for (int x = 0; x < srcWidth; x++)
                    {
                        var dx = 2 * x;
                        var col = (x == 0 || x == srcWidth - 1) ? 0 : 1;

                        var _1 = dyo + dx;
                        var _2 = dyo + dx + 1;
                        var _3 = dyo + dy + dx;
                        var _4 = dyo + dy + dx + 1;

                        var P = (int)small[syo + x];
                        var A = (int)(small[syo + x - row] / maxDifference);
                        var C = (int)(small[syo + x - col] / maxDifference);
                        var B = (int)(small[syo + x + col] / maxDifference);
                        var D = (int)(small[syo + x + row] / maxDifference);

                        large[_1] = P;
                        large[_2] = P;
                        large[_3] = P;
                        large[_4] = P;

                        if (C == A && C != D && A != B) { large[_1] = small[syo + x - row]; }
                        if (A == B && A != C && B != D) { large[_2] = small[syo + x + col]; }
                        if (D == C && D != B && C != A) { large[_3] = small[syo + x - col]; }
                        if (B == D && B != A && D != C) { large[_4] = small[syo + x + row]; }
                    }
                }
            }

            var dst = new Bitmap(src.Width * 2, src.Height * 2, PixelFormat.Format32bppArgb);
            BitmapTools.PlanesToImage_f(dst, ColorSpace.YUVToRGB, 0, dstY, dstU, dstV);
            return dst;
        }
    }
}