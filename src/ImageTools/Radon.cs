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
        /// <param name="source">input bitmap</param>
        /// <param name="highestEnergyRotation">rotation in radians that will put the strongest line signals to vertical alignment</param>
        public static Bitmap? BuildRadon(Bitmap? source, out double highestEnergyRotation)
        {
            highestEnergyRotation = 0.0;
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

            // TODO: maybe check difference between sequential samples -- a contrast check?

            FillGutterWithRowEnergy(scanWidth, gutter, quarterTurn, dY, dU, dV, ref highestEnergyRotation);

            // pack back into bitmap
            BitmapTools.PlanesToImage(dest, ColorSpace.RGB_To_RGB, 0, dY, dU, dV);

            return dest;
        }

        private static void FillGutterWithRowEnergy(int scanWidth, int gutter, double quarterTurn, double[] dY, double[] dU, double[] dV, ref double highestEnergyRotation)
        {
            // Measure the peakiness of each row by Y channel, and mark the image at the right edge.
            int    realWidth  = scanWidth - gutter;
            int    realHeight = (int)quarterTurn;
            double maxEnergy  = 0.0;
            int    bestRow    = 0;
            for (int y = 0; y < realHeight; y++)
            {
                int    yOff    = y * scanWidth;
                double average = 0.0;
                double energy  = 0.0;

                for (int x = 0; x < realWidth; x++) { average += dY[yOff + x]; }

                average /= realWidth;

                for (int x = 0; x < realWidth; x++) {
                    var diff = average - dY[yOff + x];
                    energy += diff * diff;
                }

                energy /= realWidth;

                // Write RMS into the gutter
                for (int x = 0; x < gutter; x++)
                {
                    dY[yOff + x + realWidth] = energy;
                    dU[yOff + x + realWidth] = energy;
                    dV[yOff + x + realWidth] = energy;
                }

                if (energy > maxEnergy)
                {
                    maxEnergy = energy;
                    bestRow = y;
                }
            }

            var energyOffset = maxEnergy - 255.0;
            // Shift gutter down to under peak value
            for (int y = 0; y < realHeight; y++)
            {
                int    yOff    = y * scanWidth;
                for (int x = 0; x < gutter; x++)
                {
                    dY[yOff + x + realWidth] -= energyOffset;
                    dU[yOff + x + realWidth] -= energyOffset;
                    dV[yOff + x + realWidth] -= energyOffset;
                }
            }

            highestEnergyRotation = Math.PI * 0.5 * (bestRow / quarterTurn);
        }

        private static double Fractional(double real) => real - Math.Truncate(real);

        /// <summary>
        /// sample 4 input points, and add to a single output point
        /// </summary>
        private static void SumSubsampled(int sw, int sh, double sx, double sy, int dw, double dx, double dy, double[] sY, double[] sU, double[] sV, double[] dY, double[] dU, double[] dV)
        {
            // work out the sample weights
            var fx2 = Fractional(sx);
            var fx1 = 1.0 - fx2;
            var fy2 = Fractional(sy);
            var fy1 = 1.0 - fy2;

            var f0 = fx1 * fy1;
            var f1 = fx2 * fy1;
            var f2 = fx1 * fy2;
            var f3 = fx2 * fy2;

            var ox = sx < sw-1 ? 1 : 0;
            var oy = sy < sh-1 ? sw : 0;

            var sm0 = sw * (int)sy + (int)sx;
            var sm1 = sm0 + ox;
            var sm2 = sm0 + oy;
            var sm3 = sm2 + ox;

            var max = sY.Length - 1;
            if (sm0 < 0) sm0 = 0;
            if (sm1 < 0) sm1 = 0;
            if (sm2 < 0) sm2 = 0;
            if (sm3 < 0) sm3 = 0;
            if (sm0 > max) sm0 = max;
            if (sm1 > max) sm1 = max;
            if (sm2 > max) sm2 = max;
            if (sm3 > max) sm3 = max;

            var dm = dw*(int)dy + (int)dx;
            if (dm < 0 || dm >= dY.Length) return;

            dY[dm] += sY[sm0] * f0 + sY[sm1] * f1 + sY[sm2] * f2 + sY[sm3] * f3;
            dU[dm] += sU[sm0] * f0 + sU[sm1] * f1 + sU[sm2] * f2 + sU[sm3] * f3;
            dV[dm] += sV[sm0] * f0 + sV[sm1] * f1 + sV[sm2] * f2 + sV[sm3] * f3;
        }
    }
}