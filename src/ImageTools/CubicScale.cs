using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using ImageTools.AnalyticalTransforms;
using ImageTools.Utilities;

namespace ImageTools
{
    /// <summary>
    /// Scale an image using cubic spline interpolation.
    /// This gives downscaling a slight sharpening effect.
    /// </summary>
    public static class CubicScale
    {

        /// <summary>
        /// Create a rescaled copy of an image.
        /// Image aspect ratio is always maintained
        /// </summary>
        public static Bitmap MaintainAspect(Bitmap src, int maxWidth, int maxHeight)
        {
            if (src == null) return null;
            float xScale = Math.Min(1.0f, (float)(maxWidth) / src.Width);
            float yScale = Math.Min(1.0f, (float)(maxHeight) / src.Height);

            float scale = Math.Min(xScale, yScale);
            var w = (int)(src.Width * scale);
            var h = (int)(src.Height * scale);

            return DisregardAspect(src, w, h);
        }

        public static Bitmap DisregardAspect(Bitmap src, int targetWidth, int targetHeight)
        {
            if (src == null) return null;
            
            var planeWidth = Math.Max(targetWidth, src.Width);
            var planeHeight = Math.Max(targetHeight, src.Height);
            
            BitmapTools.ArgbImageToYUVPlanes_Overscale(src, planeWidth, planeHeight, out var Y, out var U, out var V);

            var maxInput = Math.Max(src.Width, src.Height);
            
            var planes = new []{Y,U,V};
            var input = new double[maxInput];
            
            var dx = src.Width / (double)targetWidth;
            var xSamples = Enumerable.Range(0, targetWidth).Select(i=> i*dx).ToArray();
            
            var dy = src.Width / (double)targetWidth;
            var ySamples = Enumerable.Range(0, targetHeight).Select(i=> i*dy).ToArray();
            
            // compact rows
            foreach (var plane in planes)
            {
                for (int y = 0; y < src.Height; y++)
                {
                    var oy = y * planeWidth;
                    for (int x = 0; x < src.Width; x++) { input[x] = plane[oy + x]; }
                    var output = CubicSplines.Resample1D(input, xSamples);
                    for (int x = 0; x < targetWidth; x++) { plane[oy + x] = output[x]; }
                }
            }
            
            // compact columns
            foreach (var plane in planes)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    for (int y = 0; y < src.Height; y++) { input[y] = plane[(y*planeWidth) + x]; }
                    var output = CubicSplines.Resample1D(input, ySamples);
                    for (int y = 0; y < targetHeight; y++) { plane[(y*planeWidth) + x] = output[y]; }
                }
            }

            var dest = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
            BitmapTools.YUVPlanes_To_ArgbImage_Slice(dest, 0, planeWidth, Y, U, V);
            return dest;
        }

    }
}