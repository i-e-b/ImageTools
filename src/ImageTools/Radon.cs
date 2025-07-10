using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using ImageTools.GeneralTypes;
using ImageTools.ImageDataFormats;
using ImageTools.Utilities;

namespace ImageTools
{
    /// <summary>
    /// Experimental radon transform.
    /// https://en.wikipedia.org/wiki/Radon_transform
    /// </summary>
    public static class Radon
    {
        /// <summary>
        /// Generate a radon transform of the source image
        /// </summary>
        public static Bitmap? BuildRadon(Bitmap? source)
        {
            if (source == null) return null;

            // We use the smaller dimension of the source for rotation count,
            // and the larger dimension as the sensor.
            // Square images can choose either way.

            double rotateCount = 1;
            double quarterTurn = Math.Min(source.Width, source.Height);

            int gutter    = 10;
            int scanWidth = gutter + Math.Max(source.Width, source.Height);


            // Calculate how big the output image has to be
            var width = (double)source.Width;
            var height = (double)source.Height;

            // create an output image, and get source planes
            var dest = new Bitmap(scanWidth, (int)quarterTurn, PixelFormat.Format32bppArgb);
            BitmapTools.ImageToPlanes(source, ColorSpace.RGB_To_RGB, out var sY, out var sU, out var sV); // decompose input
            BitmapTools.ImageToPlanes(dest, ColorSpace.RGB_To_RGB, out var dY, out var dU, out var dV);   // build output arrays


            var sw = new Stopwatch();
            sw.Start();

            // For each scan width, sum up the values along each scan line, and divide by `scanWidth`.
            // Repeat this until rotateCount > quarterTurn

            int outY = 0;
            while (rotateCount <= quarterTurn)
            {
                // Build rotation matrix for this step
                var radians = Math.PI * 0.5 * (rotateCount / quarterTurn);
                var rot = new Matrix2(
                    Math.Cos(radians), -Math.Sin(radians),
                    Math.Sin(radians),  Math.Cos(radians)
                );

                // Invert the matrix, and look up a source location for each output pixel
                var halfHeight = height / 2.0;
                var halfWidth  = width / 2.0;
                var inverse    = rot.Inverse();
                for (var dy = -halfHeight; dy < halfHeight; dy++)
                {
                    for (var dx = -halfWidth; dx < halfWidth; dx++)
                    {
                        var sp = new Vector2(dx, dy) * inverse;
                        var sx = sp.X + halfWidth;
                        var sy = sp.Y + halfHeight;

                        // sample multiple points blended by sx/sy fractions
                        SumSubsampled( /*from*/ (int)width, (int)height, sx, sy, /* to */ scanWidth, dx + halfWidth, outY, /* buffers */ sY, sU, sV, dY, dU, dV);
                    }
                }

                outY++;
                rotateCount += 1.0;
            }

            sw.Stop();
            Console.WriteLine($"Core Radon transform took {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");

            // Reduce output samples to range
            for (int i = 0; i < dY.Length; i++)
            {
                dY[i] = (float)(dY[i] / quarterTurn);
                dU[i] = (float)(dU[i] / quarterTurn);
                dV[i] = (float)(dV[i] / quarterTurn);
            }

            // Measure the peakiness of each row, and mark the image at the right edge.
            /*int realWidth  = scanWidth - gutter;
            int realHeight = (int)quarterTurn;
            for (int y = 0; y < realHeight; y++)
            {
                double average = 0.0;
                double peak    = 0.0;
                for (int x = 0; x < realWidth; x++)
                {


                }
            }*/

            // pack back into bitmap
            BitmapTools.PlanesToImage(dest, ColorSpace.RGB_To_RGB, 0, dY, dU, dV);

            return dest;
        }

        private static double Fractional(double real) => real - Math.Floor(real);

        /// <summary>
        /// sample 4 input points, and add to a single output point
        /// </summary>
        private static void SumSubsampled(int sw, int sh, double sx, double sy, int dw, double dx, double dy, double[] sY, double[] sU, double[] sV, double[] dY, double[] dU, double[] dV)
        {
            // bounds check
            if (sy < 0 || sy > sw) return; // TODO: default to top corner color if we are out of bounds
            if (sx < 0 || sx > sh) return;

            // work out the sample weights
            var fx2 = Fractional(sx);
            var fx1 = 1.0 - fx2;
            var fy2 = Fractional(sy);
            var fy1 = 1.0 - fy2;

            var f0 = fx1 * fy1;
            var f1 = fx2 * fy1;
            var f2 = fx1 * fy2;
            var f3 = fx2 * fy2;

            var ox = sx < (sw-1) ? 1 : 0;
            var oy = sy < (sh-1) ? sw : 0;

            var sm0 = sw*(int)sy + (int)sx;
            if (sm0 < 0) sm0 = 0;
            var sm1 = sm0 + ox;
            var sm2 = sm0 + oy;
            var sm3 = sm2 + ox;

            var dm = dw*(int)dy + (int)dx;
            if (dm < 0 || dm >= dY.Length) return;

            // TODO: default to top corner color if we are out of bounds
            dY[dm] += Smpl(sY, sm0) * f0 + Smpl(sY, sm1) * f1 + Smpl(sY, sm2) * f2 + Smpl(sY, sm3) * f3;
            dU[dm] += Smpl(sU, sm0) * f0 + Smpl(sU, sm1) * f1 + Smpl(sU, sm2) * f2 + Smpl(sU, sm3) * f3;
            dV[dm] += Smpl(sV, sm0) * f0 + Smpl(sV, sm1) * f1 + Smpl(sV, sm2) * f2 + Smpl(sV, sm3) * f3;
        }

        private static double Smpl(double[] src, int idx)
        {
            if (idx < 0 || idx >= src.Length) return 0.0;
            return src[idx];
        }
    }
}