using System.Drawing;
using System.Drawing.Imaging;
using ImageTools.AnalyticalTransforms;
using ImageTools.Utilities;

namespace ImageTools
{
    /// <summary>
    /// Scale an image using bicubic spline interpolation and box filtering.
    /// This gives downscaling a slight sharpening effect.
    /// </summary>
    public class BiCubicBoxScale
    {
        /// <summary>
        /// Create a rescaled copy of an image.
        /// Image aspect ratio is always maintained
        /// </summary>
        public static Bitmap MaintainAspect(Bitmap src, int maxWidth, int maxHeight)
        {
            float xScale = Math.Min(1.0f, (float)(maxWidth) / src.Width);
            float yScale = Math.Min(1.0f, (float)(maxHeight) / src.Height);

            float scale = Math.Min(xScale, yScale);
            var w = (int)(src.Width * scale);
            var h = (int)(src.Height * scale);

            return DisregardAspect(src, w, h);
        }

        public static Bitmap DisregardAspect(Bitmap src, int dstWidth, int dstHeight)
        {
            // TODO: if any dimension is going *down* more than 50%, box filter it until it's less.
            
            // This is a pretty dumb way to do it, which calculates way more than it needs to.
            var srcWidth = src.Width;
            var srcHeight = src.Height;
            BitmapTools.ArgbImageToYUVPlanes(src, out var sY, out var sU, out var sV);
            
            var dY = new double[dstWidth*dstHeight];
            var dU = new double[dstWidth*dstHeight];
            var dV = new double[dstWidth*dstHeight];

            var dx = (double)srcWidth / dstWidth;
            var dy = (double)srcHeight / dstHeight;
            var samples = new double[4,4];
            
            for (int y = 0; y < dstHeight; y++)
            {
                var oy = y * dstWidth;
                for (int x = 0; x < dstWidth; x++)
                {
                    // TODO: read plane into the samples array, pick a centroid point, write back
                    // The code below is total crap.
                    var left = ((int)(x*dx - 1.5)).Pin(0, srcWidth-4);
                    var top = ((int)(y*dy - 1.5)).Pin(0, srcHeight-4);
                    
                    var sx = 1.0 - (x*dx % 1.0);
                    var sy = 1.0 - (y*dy % 1.0);
                    
                    LoadSamples(sY, srcWidth, samples, left,  top);
                    var s = CubicSplines.SampleInterpolate2D(samples, sx,sy);
                    dY[oy+x] = s;
                    
                    LoadSamples(sU, srcWidth, samples, left,  top);
                    s = CubicSplines.SampleInterpolate2D(samples, sx, sy);
                    dU[oy+x] = s;
                    
                    LoadSamples(sV, srcWidth, samples, left,  top);
                    s = CubicSplines.SampleInterpolate2D(samples, sx, sy);
                    dV[oy+x] = s;
                }
            }
            
            var dest = new Bitmap(dstWidth, dstHeight, PixelFormat.Format32bppArgb);
            BitmapTools.YUVPlanes_To_ArgbImage(dest, 0, dY, dU, dV);
            return dest;
        }

        private static void LoadSamples(double[] src, int srcWidth, double[,] samples, int left, int top)
        {
            for (int y = 0; y < 4; y++)
            {
                var oy = (y+top) * srcWidth;
                for (int x = 0; x < 4; x++)
                {
                    var ox = x+left;
                    samples[x,y] = src[oy+ox];
                }
            }
        }
    }
}