using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace ImageTools
{
    public class CDF_9_7
    {
        /// <summary>
        /// Returns a 32 bit-per-pixel image
        /// containing the compressed gradients of a 32-bpp image
        /// </summary>
        public static unsafe Bitmap HorizontalGradients(Bitmap src)
        {
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);

            Bitmangle.RunKernel(src, dst, HorizonalWaveletTest);

            return dst;
        }

        public static unsafe Bitmap ReduceImage(Bitmap src)
        {
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);

            Bitmangle.RunKernel(src, dst, WaveletDecompose);

            return dst;
        }

        /**
         *  fwt97 - Forward biorthogonal 9/7 wavelet transform (lifting implementation)
         *
         *  x is an input signal, which will be replaced by its output transform.
         *  n is the length of the signal, and must be a power of 2.
         *  s is the stride across the signal (for multi dimensional signals)
         *
         *  The first half part of the output signal contains the approximation coefficients.
         *  The second half part contains the detail coefficients (aka. the wavelets coefficients).
         *
         *  See also iwt97.
         */
        public static void fwt97(double[] x, int n)
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

        /**
         *  iwt97 - Inverse biorthogonal 9/7 wavelet transform
         *
         *  This is the inverse of fwt97 so that iwt97(fwt97(x,n),n)=x for every signal x of length n.
         *
         *  See also fwt97.
         */
        public static void iwt97(double[] x, int n)
        {
            double a;
            int i;

            // Unpack
            var tempbank = new double[n];
            for (i=0;i<n/2;i++) {
                tempbank[i*2]=x[i];
                tempbank[i*2+1]=x[i+n/2];
            }
            for (i=0;i<n;i++) x[i]=tempbank[i];

            // Undo scale
            a=1.149604398;
            for (i=0;i<n;i++) {
                if ((i % 2) == 0) x[i] *= a;
                else x[i]/=a;
            }

            // Undo update 2
            a=-0.4435068522;
            for (i=2;i<n;i+=2) {
                x[i]+=a*(x[i-1]+x[i+1]);
            }
            x[0]+=2*a*x[1];

            // Undo predict 2
            a=-0.8829110762;
            for (i=1;i<n-2;i+=2) {
                x[i]+=a*(x[i-1]+x[i+1]);
            }
            x[n-1]+=2*a*x[n-2];

            // Undo update 1
            a=0.05298011854;
            for (i=2;i<n;i+=2) {
                x[i]+=a*(x[i-1]+x[i+1]);
            }
            x[0]+=2*a*x[1];

            // Undo predict 1
            a=1.586134342;
            for (i=1;i<n-2;i+=2) {
                x[i]+=a*(x[i-1]+x[i+1]);
            } 
            x[n-1]+=2*a*x[n-2];
        }

        
        
        static unsafe void WaveletDecompose(byte* s, byte* d, BitmapData si, BitmapData di)
        {
            var bytePerPix = si.Stride / si.Width;
            var planeSize = si.Width * si.Height;
            var buffer = new double[planeSize];
            int i = 0;

            for (int ch = 0; ch < bytePerPix; ch++) // each channel
            {
                // load image as doubles
                for (i = 0; i < planeSize; i++) // each pixel (read cycle)
                {
                    buffer[i] = s[(i * bytePerPix) + ch];
                }


                // process rounds
                // ...


                // Normalise values
                // ...


                // Write output
                for (i = 0; i < planeSize; i++) // each pixel (read cycle)
                {
                    var value = buffer[i];
                    d[(i * bytePerPix) + ch] = (byte)Saturate(value);
                }

            }
        }

        static unsafe void HorizonalWaveletTest(byte* s, byte* d, BitmapData si, BitmapData di)
        {
            var bytePerPix = si.Stride / si.Width;
            var buffer = new double[si.Width];

            double lowest = 0;
            double highest = 0;

            // HACK: Test output size with RLE
            var testpath = @"C:\gits\ImageTools\src\ImageTools.Tests\bin\Debug\outputs\rle.dat";
            if (File.Exists(testpath)) File.Delete(testpath);

            using (var fs = File.Open(testpath, FileMode.Append)) {

            // width wise
            for (int y = 0; y < si.Height; y++) // each row
            {
                var yo = y * si.Stride;
                for (int ch = 0; ch < bytePerPix; ch++) // each channel
                {
                    for (int x = 0; x < si.Width; x++) // each pixel (read cycle)
                    {
                        var sptr = yo + (x * bytePerPix) + ch;
                        buffer[x] = s[sptr];
                    }

                    // lower = more compressed; Reasonable range is 0.1 to 0.8
                    // if this is set too high, you will get 'burn' artefacts
                    // if this is too low, you will get a blank image
                    double CompressionFactor = 0.76;
                    int rounds = 4; // more = more compression, more scale factors, more time
                    double factor = CompressionFactor;

                    // Compression phase
                    CompressLine(rounds, buffer, factor);

                    // EXPERIMENTS go here
                    // Thresholding co-effs
                    for (int i = buffer.Length >> rounds; i < buffer.Length - 1; i++)
                    {
                        if (Math.Abs(buffer[i]) < 10) // energy threshold. Play with this.
                        {
                            buffer[i] = 0; // remove coefficient
                        }
                    }

                    var rle = RLEncode(buffer);
                    fs.Write(rle, 0, rle.Length);

                    DCOffsetAndPinToRange(buffer, rounds);
                        

                    // Expansion phase
                    ExpandLine(buffer, rounds, factor);

                    for (int x = 0; x < si.Width; x++) // each pixel (write cycle)
                    {
                        var dptr = yo + (x * bytePerPix) + ch;
                        var value = buffer[x];
                        d[dptr] = (byte)Saturate(value);
                    }
                }
            }
            fs.Flush();
            }
            
            Console.WriteLine("Min cdf value = " + lowest);
            Console.WriteLine("Max cdf value = " + highest);
        }

        private static byte[] RLEncode(double[] buffer)
        {
            var samples = new byte[buffer.Length];
            for (int i = 0; i < buffer.Length; i++)
            {
                samples[i] = (byte)buffer[i];
            }

            // the run length encoding is ONLY for zeros...
            // [data] ?[runlength] in bytes.
            //

            var runs = new List<byte>();
            var zlength = 0;

            for (int i = 0; i < samples.Length; i++)
            {
                var samp = samples[i];
                if (samp == 0)
                {
                    zlength++;
                    continue;
                }

                // non zero sample...
                if (zlength > 0)
                {
                    runs.Add(0);
                    runs.Add((byte)zlength);
                }
                zlength = 0;
                runs.Add(samp);
            }

            if (zlength > 0)
            {
                runs.Add(0);
                runs.Add((byte)zlength);
            }

            return runs.ToArray();
        }

        private static void CompressLine(int rounds, double[] buffer, double factor)
        {
            for (int i = 0; i < rounds; i++)
            {
                fwt97(buffer, buffer.Length >> i); // decompose signal
                Compress(buffer, factor, i);
            }

            //DCOffsetAndPinToRange(buffer, rounds);
        }

        private static void ExpandLine(double[] buffer, int rounds, double factor)
        {
            DCRestore(buffer, rounds);
            for (int i = rounds - 1; i >= 0; i--)
            {
                Expand(buffer, factor, i);
                iwt97(buffer, buffer.Length >> i); // restore signal
            }
        }

        private static void Compress(double[] buffer, double factor, int round)
        {
            var start = 0; // buffer.Length >> (round + 1);
            var end = buffer.Length >> round;
            for (int i = start; i < end; i++)
            {
                buffer[i] = (int)(buffer[i] * factor);
            }
        }
        
        private static void Expand(double[] buffer, double factor, int round)
        {
            var expansion = 1 / factor;
            var start = 0; //buffer.Length >> (round + 1);
            var end = buffer.Length >> round;
            for (int i = start; i < end; i++)
            {
                buffer[i] *= expansion;
            }
        }

        // bias for coefficients. 127 is neutral, 0 is 100% positive, 255 is 100% negative
        const int DC_Bias = 127;

        private static void DCOffsetAndPinToRange(double[] buffer, int rounds)
        {
            var mid = buffer.Length >> rounds;
            var end = buffer.Length;
            for (int i = 0; i < mid; i++)
            {
                buffer[i] = Saturate(buffer[i]); // pin to byte range
            }
            for (int i = mid; i < end; i++)
            {
                buffer[i] = Saturate(buffer[i] + DC_Bias); // pin to byte range
            }
        }

        private static void DCRestore(double[] buffer, int rounds)
        {
            var mid = buffer.Length >> rounds;
            var end = buffer.Length;

            for (int i = mid; i < end; i++)
            {
                buffer[i] = (buffer[i]) - DC_Bias;
            }
        }

        private static int Saturate(double value)
        {
            if (value < 0) return 0;
            if (value > 255) return 255;
            return (int)value;
        }
    }
}