using System;
using System.Drawing;
using System.Drawing.Imaging;
using ImageTools.ImageDataFormats;
using ImageTools.Utilities;

namespace ImageTools
{
    /// <summary>
    /// Scaling that combines nearest-neighbour and error diffusion
    /// to give an interesting looking upscale without changing color palette.
    /// This is intended for very large upscaling (4x and greater)
    /// </summary>
    public static class ErrorDiffusionScale {
        
        /// <summary>
        /// Scale up using error simple diffusion
        /// </summary>
        public static Bitmap Upscale(Bitmap src, float scale)
        {
            if (src == null) return null;
            BitmapTools.ImageToPlanesf(src, ColorSpace.RGBToYUV, out var srcY, out var srcU, out var srcV);

            var sqrScale = (int)Math.Ceiling(scale * scale);
            var dstY = new float[srcY.Length*sqrScale];
            var dstU = new float[srcU.Length*sqrScale];
            var dstV = new float[srcV.Length*sqrScale];

            var srcWidth = src.Width;
            var srcHeight = src.Height;
            var dstWidth = (int)(srcWidth * scale);
            var dstHeight = (int)(srcHeight * scale);

            var invScale = 1.0f / scale;

            for (int plane = 0; plane < 3; plane++)
            {
                var small = Pick(plane, srcY, srcU, srcV);
                var large = Pick(plane, dstY, dstU, dstV);

                var err = 0.0f;

                if (small == null || large == null) continue;
                for (int y = 0; y < dstHeight; y++)
                {
                    var sy = y * invScale;
                    err += sy - (int)sy;
                    //if (err >= 1) { sy+= 1; err -= 1.414f; }

                    for (int x = 0; x < dstWidth; x++)
                    {
                        var sx = x * invScale;

                        err += sx - (int)sx;
                        //if (err >= 1) { sx += 1; sy += 1; err -= 1; }
                        if (err >= 1) { sx += 1; err -= 1.414f; }

                        var isx = (int)sx;
                        var isy = (int)sy;
                        if (isx >= srcWidth) isx = srcWidth - 1;
                        if (isy >= srcHeight) isy = srcHeight - 1;

                        // pick the pixel
                        large[(y * dstWidth) + x] = small[(isy * srcWidth) + isx];
                    }
                }
            }

            var dst = new Bitmap(dstWidth, dstHeight, PixelFormat.Format32bppArgb);
            BitmapTools.PlanesToImage_f(dst, ColorSpace.YUVToRGB, 0, dstY, dstU, dstV);
            return dst;
        }

        private static T Pick<T>(int i, params T[] stuff)
        {
            return stuff == null ? default : stuff[i];
        }
    }
}