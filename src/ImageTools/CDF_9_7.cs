using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace ImageTools
{
    public class CDF_9_7
    {
        /// <summary>
        /// Returns a 64 bit-per-pixel image
        /// containing the gradients of a 32-bpp image
        /// </summary>
        public static unsafe Bitmap Gradients(Bitmap src)
        {
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format64bppArgb);

            Bitmangle.RunKernel(src, dst, GradientKernel);

            return dst;
        }

        /**
         *  fwt97 - Forward biorthogonal 9/7 wavelet transform (lifting implementation)
         *
         *  x is an input signal, which will be replaced by its output transform.
         *  n is the length of the signal, and must be a power of 2.
         *
         *  The first half part of the output signal contains the approximation coefficients.
         *  The second half part contains the detail coefficients (aka. the wavelets coefficients).
         *
         *  See also iwt97.
         */
        private static void fwt97(double[] x, int n)
        {
            double a;
            int i;

            // Predict 1
            a = -1.586134342;
            for (i = 1; i < n - 2; i += 2)
            {
                x[i] += a * (x[i - 1] + x[i + 1]);
            }
            x[n - 1] += 2 * a * x[n - 2];

            // Update 1
            a = -0.05298011854;
            for (i = 2; i < n; i += 2)
            {
                x[i] += a * (x[i - 1] + x[i + 1]);
            }
            x[0] += 2 * a * x[1];

            // Predict 2
            a = 0.8829110762;
            for (i = 1; i < n - 2; i += 2)
            {
                x[i] += a * (x[i - 1] + x[i + 1]);
            }
            x[n - 1] += 2 * a * x[n - 2];

            // Update 2
            a = 0.4435068522;
            for (i = 2; i < n; i += 2)
            {
                x[i] += a * (x[i - 1] + x[i + 1]);
            }
            x[0] += 2 * a * x[1];

            // Scale
            a = 1 / 1.149604398;
            for (i = 0; i < n; i++)
            {
                if ((i % 2) == 0) x[i] *= a;
                else x[i] /= a;
            }

            // Pack
            var tempbank = new double[n];
            for (i = 0; i < n; i++)
            {
                if (i % 2 == 0) tempbank[i / 2] = x[i];
                else tempbank[n / 2 + i / 2] = x[i];
            }
            for (i = 0; i < n; i++) x[i] = tempbank[i];
        }

        static unsafe void GradientKernel(byte* s, ushort* d, BitmapData si, BitmapData di)
        {
            var bytePerPix = si.Stride / si.Width;
            var buffer = new double[si.Width];

            double lowest = 0;
            double highest = 0;

            double scale = 8192 / 2.6;

            // width wise
            for (int y = 0; y < si.Height; y++) // each row
            {
                var yo = y * si.Stride;
                for (int ch = 0; ch < bytePerPix; ch++) // each channel
                {
                    for (int x = 0; x < si.Width; x++) // each pixel (read cycle)
                    {
                        var sptr = yo + (x * bytePerPix) + ch;
                        buffer[x] = s[sptr] / 255.0;
                    }

                    int len = buffer.Length >> 1;
                    fwt97(buffer, len << 1);

                    lowest = Math.Min(lowest, buffer.Min());
                    highest = Math.Max(highest, buffer.Max());

                    for (int x = 0; x < si.Width; x++) // each pixel (write cycle)
                    {
                        var dptr = yo + (x * bytePerPix) + ch;
                        d[dptr] = (ushort)((1.3 + buffer[x]) * scale); // TODO: what are the limits on CDF97? I've seen -1.3..1.3
                    }
                }
            }

            
            Console.WriteLine("Min cdf value = " + lowest);
            Console.WriteLine("Max cdf value = " + highest);
        }
    }
}