using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using ImageTools.DataCompression.LZMA;
using ImageTools.Utilities;

namespace ImageTools
{
    public class CDF_9_7
    {
        public static unsafe Bitmap HorizontalGradients(Bitmap src)
        {
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);

            Bitmangle.RunKernel(src, dst, HorizonalWaveletTest);

            return dst;
        }
        
        public static unsafe Bitmap VerticalGradients(Bitmap src)
        {
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);

            Bitmangle.RunKernel(src, dst, VerticalWaveletTest);

            return dst;
        }

        public static unsafe Bitmap MortonReduceImage(Bitmap src)
        {
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);

            Bitmangle.RunKernel(src, dst, WaveletDecomposeMortonOrder);

            return dst;
        }

        public static unsafe Bitmap Planar3ReduceImage(Bitmap src)
        {
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);

            Bitmangle.RunKernel(src, dst, WaveletDecomposePlanar3);

            return dst;
        }

        public static unsafe Bitmap Planar2ReduceImage(Bitmap src)
        {
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);

            Bitmangle.RunKernel(src, dst, WaveletDecomposePlanar2);

            return dst;
        }


        public static void ReduceImage3D(Image3d img3d)
        {
            // Step 1: Do a first reduction in 3 dimensions
            // We could use morton order, or 3D-planar.
            // Morton order requires cubic images, so we ignore it for now.
            // This is doing overlapping coefficients (like planar-3 in 2D, but this results in 6 sets of coefficients per round)

            // DC to AC
            for (int i = 0; i < img3d.Y.Length; i++)
            {
                img3d.Y[i] -= 127.5;
                img3d.Cg[i] -= 127.5;
                img3d.Co[i] -= 127.5;
            }

            var quantise = 1.0;

            for (int ch = 0; ch < 3; ch++)
            {
                double[] buffer = null;
                switch(ch) {
                    case 0: buffer = img3d.Y; break;
                    case 1: buffer = img3d.Co; break; // not entirely sure if orange or green deserves more bits
                    case 2: buffer = img3d.Cg; break;
                }
                int rounds = (int)Math.Log(img3d.MinDimension, 2);

                // Decompose Transform
                for (int i = 0; i < rounds; i++)
                {
                    var height = img3d.Height >> i;
                    var width = img3d.Width >> i;
                    var depth = img3d.Depth >> i;

                    // Try different orderings of XY and Z once compressed output is going
                    //  Z,Y,X = 218(@7); = 11.8(@64)     
                    //  Y,X,Z = 218    ; = 11.8(@64)
                    // Looks like there's nothing much in it in 6-Planar
                    // Trying 4-planar (2-planar, plus z across all)
                    //  Z,Y,X = 204(@7); = 12.1(@64)
                    //  Y,X,Z = 204    ; = 12.1
                    // 4-planar it is. Choosing Y,X,Z for personal preference

                    // Reduce each plane independently
                    for (int z = 0; z < depth; z++)
                    {
                        var zo = z * img3d.zspan;

                        // Wavelet decompose vertical
                        for (int x = 0; x < width; x++) // each column
                        {
                            Fwt97(buffer, height, zo + x, img3d.Width);
                        }

                        // Wavelet decompose horizontal
                        for (int y = 0; y < height >> 1; y++) // each row
                        {
                            var yo = (y * img3d.Width);
                            Fwt97(buffer, width, zo + yo, 1);
                        }
                    }

                    // decompose through depth
                    for (int xy = 0; xy < img3d.zspan; xy++)
                    {
                        Fwt97(buffer, depth, xy, img3d.zspan);
                    }
                }

                // Quantise, compress, write
                var tmp = ToMortonOrder3D(buffer, img3d);
                for (int i = 0; i < buffer.Length; i++) { buffer[i] = tmp[i]; }
                Quantise3D(buffer, QuantiseType.Reduce, rounds, ch);

                WriteToFileFibonacci(buffer, ch, "3D");
                for (int i = 0; i < buffer.Length; i++) { buffer[i] = 0; } // prove we're reading from the file
                ReadFromFileFibonacci(buffer, ch, "3D");

                // De-quantise
                Quantise3D(buffer, QuantiseType.Expand, rounds, ch);
                tmp = FromMortonOrder3D(buffer, img3d);
                for (int i = 0; i < buffer.Length; i++) { buffer[i] = tmp[i]; }


                // Restore
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var height = img3d.Height >> i;
                    var width = img3d.Width >> i;
                    var depth = img3d.Depth >> i;

                    // Order here must be exact reverse of above

                    // restore through depth
                    var dz = img3d.zspan;
                    for (int xy = 0; xy < img3d.zspan; xy++)
                    {
                        Iwt97(buffer, depth, xy, dz);
                    }

                    // Restore each plane independently
                    for (int z = 0; z < depth; z++)
                    {
                        var zo = z * img3d.zspan;

                        // Wavelet restore horizontal
                        for (int y = 0; y < height >> 1; y++) // each row
                        {
                            var yo = (y * img3d.Width);
                            Iwt97(buffer, width, zo + yo, 1);
                        }

                        // Wavelet restore vertical
                        for (int x = 0; x < width; x++) // each column
                        {
                            Iwt97(buffer, height, zo + x, img3d.Width);
                        }
                    }
                }
            }

            // AC to DC
            for (int i = 0; i < img3d.Y.Length; i++)
            {
                img3d.Y[i] += 127.5;
                img3d.Cg[i] += 127.5;
                img3d.Co[i] += 127.5;
            }
        }

        private static double[] FromMortonOrder3D(double[] buffer, Image3d img3d)
        {
            var limit = (int)Math.Pow(img3d.MaxDimension, 3);
            var mx = img3d.Width - 1;
            var my = img3d.Height - 1;
            var mz = img3d.Depth - 1;

            var swap = new double[buffer.Length];
            int o = 0;

            for (uint i = 0; i < limit; i++)
            {
                Morton.DecodeMorton3(i, out var x, out var y, out var z);
                if (x > mx || y > my || z > mz) continue;

                var si = x + (y * img3d.yspan) + (z * img3d.zspan);
                swap[si] = buffer[o++];
            }
            return swap;
        }

        private static double[] ToMortonOrder3D(double[] buffer, Image3d img3d)
        {
            var limit = (int)Math.Pow(img3d.MaxDimension, 3);
            var mx = img3d.Width - 1;
            var my = img3d.Height - 1;
            var mz = img3d.Depth - 1;

            var swap = new double[buffer.Length];
            int o = 0;

            for (uint i = 0; i < limit; i++)
            {
                Morton.DecodeMorton3(i, out var x, out var y, out var z);
                if (x > mx || y > my || z > mz) continue;

                var si = x + (y * img3d.yspan) + (z * img3d.zspan);
                swap[o++] = buffer[si];
            }
            return swap;
        }

        /// <summary>
        ///  fwt97 - Forward biorthogonal 9/7 wavelet transform (lifting implementation)
        ///<para></para><list type="bullet">
        ///  <item><description>x is an input signal, which will be replaced by its output transform.</description></item>
        ///  <item><description>n is the length of the signal, and must be a power of 2.</description></item>
        ///  <item><description>s is the stride across the signal (for multi dimensional signals)</description></item>
        /// </list>
        ///<para></para>
        ///  The first half part of the output signal contains the approximation coefficients.
        ///  The second half part contains the detail coefficients (aka. the wavelets coefficients).
        ///<para></para>
        ///  See also iwt97.
        /// </summary>
        public static void Fwt97(double[] buf, int n, int offset, int stride)
        {
            double a;
            int i;

            // pick out stride data
            var x = new double[n];
            for (i = 0; i < n; i++) { x[i] = buf[i * stride + offset]; }

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
            // The raw output is like [DC][AC][DC][AC]...
            // we want it as          [DC][DC]...[AC][AC]
            var tempbank = new double[n];
            for (i = 0; i < n; i++)
            {
                if (i % 2 == 0) tempbank[i / 2] = x[i];
                else tempbank[n / 2 + i / 2] = x[i];
            }

            // pick out stride data
            for (i = 0; i < n; i++) { buf[i * stride + offset] = tempbank[i]; }
        }

        /// <summary>
        /// iwt97 - Inverse biorthogonal 9/7 wavelet transform
        /// <para></para>
        /// This is the inverse of fwt97 so that iwt97(fwt97(x,n),n)=x for every signal x of length n.
        /// <para></para>
        /// See also fwt97.
        /// </summary>
        public static void Iwt97(double[] buf, int n, int offset, int stride)
        {
            double a;
            int i;
            
            // pick out stride data
            var x = new double[n];
            for (i = 0; i < n; i++) { x[i] = buf[i * stride + offset]; }

            // Unpack
            // The raw input is like [DC][DC]...[AC][AC]
            // we want it as         [DC][AC][DC][AC]...
            var tempbank = new double[n];
            for (i = 0; i < n / 2; i++)
            {
                tempbank[i * 2] = x[i];
                tempbank[i * 2 + 1] = x[i + n / 2];
            }
            for (i = 0; i < n; i++) x[i] = tempbank[i];

            // Undo scale
            a = 1.149604398;
            for (i = 0; i < n; i++)
            {
                if ((i % 2) == 0) x[i] *= a;
                else x[i] /= a;
            }

            // Undo update 2
            a = -0.4435068522;
            for (i = 2; i < n; i += 2)
            {
                x[i] += a * (x[i - 1] + x[i + 1]);
            }
            x[0] += 2 * a * x[1];

            // Undo predict 2
            a = -0.8829110762;
            for (i = 1; i < n - 2; i += 2)
            {
                x[i] += a * (x[i - 1] + x[i + 1]);
            }
            x[n - 1] += 2 * a * x[n - 2];

            // Undo update 1
            a = 0.05298011854;
            for (i = 2; i < n; i += 2)
            {
                x[i] += a * (x[i - 1] + x[i + 1]);
            }
            x[0] += 2 * a * x[1];

            // Undo predict 1
            a = 1.586134342;
            for (i = 1; i < n - 2; i += 2)
            {
                x[i] += a * (x[i - 1] + x[i + 1]);
            }
            x[n - 1] += 2 * a * x[n - 2];
            

            // pick out stride data
            for (i = 0; i < n; i++) { buf[i * stride + offset] = x[i]; }
        }
        
        /// <summary>
        /// Separate scales into 3 sets of coefficients
        /// </summary>
        static unsafe void WaveletDecomposePlanar3(byte* s, byte* d, BitmapData si, BitmapData di)
        {
            // Change color space
            var bufferSize = To_YCxCx_ColorSpace(s, si);


            int rounds = (int)Math.Log(si.Width, 2) - 1;
            Console.WriteLine($"Decomposing with {rounds} rounds");

            for (int ch = 0; ch < 3; ch++)
            {
                var buffer = ReadPlane(s, si, ch);

                // DC to AC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] -= 127.5; }

                // Transform
                PlanarDecompose(si, buffer, rounds);


                // Test of quantisation:
               // var quality = (ch + 1) * 2; // bias quality by color channel. Assumes 2=Y
                //buffer = QuantiseByIndependentRound(si, buffer, ch, rounds, quality);
                //buffer = QuantiseByEnergyBalance(si, buffer, ch, rounds, quality);

                WriteToFileFibonacci(buffer, ch, "planar");

                
                ReadFromFileFibonacci(buffer, ch, "planar");

                // Restore
                PlanarRestore(si, buffer, rounds);

                // AC to DC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5; }

                // Write back to image
                WritePlane(buffer, d, di, ch);
            }
            
            // Restore color space
            To_RGB_ColorSpace(d, bufferSize);
        }

        /// <summary>
        /// This version is a hybrid between Morton (1 set of Coeffs per round) and Planar (3 sets of Coeffs per round)
        /// </summary>
        static unsafe void WaveletDecomposePlanar2(byte* s, byte* d, BitmapData si, BitmapData di)
        {
            // Change color space
            var bufferSize = To_YCxCx_ColorSpace(s, si);

            int rounds = (int)Math.Log(si.Width, 2) - 1;
            Console.WriteLine($"Decomposing with {rounds} rounds");

            for (int ch = 0; ch < 3; ch++)
            {
                var buffer = ReadPlane(s, si, ch);

                // DC to AC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] -= 127.5; }

                // Transform
                for (int i = 0; i < rounds; i++)
                {
                    var height = si.Height >> i;
                    var width = si.Width >> i;

                    // Wavelet decompose vertical
                    for (int x = 0; x < width; x++) // each column
                    {
                        Fwt97(buffer, height, x, si.Width);
                    }
                    
                    // Wavelet decompose HALF horizontal
                    for (int y = 0; y < height / 2; y++) // each row
                    {
                        Fwt97(buffer, width, y * si.Width, 1);
                    }
                }

                // Quantise and reduce co-efficients
                QuantisePlanar2(si, buffer, ch, rounds, QuantiseType.Reduce);

                // Write output
                WriteToFileFibonacci(buffer, ch, "p_2");

                // Prove reading is good:
                for (int i = 0; i < buffer.Length; i++) { buffer[i] = 0.0; }

                // read output
                ReadFromFileFibonacci(buffer, ch, "p_2");

                // Re-expand co-efficients
                QuantisePlanar2(si, buffer, ch, rounds, QuantiseType.Expand);

                // Restore
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var height = si.Height >> i;
                    var width = si.Width >> i;

                    // Wavelet restore HALF horizontal
                    for (int y = 0; y < height / 2; y++) // each row
                    {
                        Iwt97(buffer, width, y * si.Width, 1);
                    }

                    // Wavelet restore vertical
                    for (int x = 0; x < width; x++) // each column
                    {
                        Iwt97(buffer, height, x, si.Width);
                    }
                }

                // AC to DC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5; }

                // Write back to image
                WritePlane(buffer, d, di, ch);
            }
            
            // Restore color space
            To_RGB_ColorSpace(d, bufferSize);
        }

        private static void Quantise3D(double[] buffer, QuantiseType mode, int rounds, int ch)
        {
            // ReSharper disable JoinDeclarationAndInitializer
            double[] fYs, fCs;   
            // ReSharper restore JoinDeclarationAndInitializer

            //              |< spacial blur
            //                               motion blur >|

            // Test MJPEG = 1,864kb

            // Good quality (test = 529kb) (morton = 517kb)
            //fYs = new double[]{  5,  4,  3, 2, 1, 1, 1 };
            //fCs = new double[]{ 24, 15, 10, 7, 5, 3, 2 };

            // Normal compression (test = 224kb) (morton = 195kb)
            fYs = new double[]{ 24, 12, 7,  5, 3, 2, 1 };
            fCs = new double[]{ 50, 24, 12, 7, 5, 3, 2 };

            // Strong compression (test = 145kb) (morton = 124kb)
            //fYs = new double[]{ 50,  35, 17, 10, 5, 3, 1 };
            //fCs = new double[]{200, 100, 50, 10, 5, 3, 2 };
            
            // Very strong compression (test = 95.3kb) (morton = 81.4kb)
            //fYs = new double[]{200,  80,  60,  40, 10,  5,  4 };
            //fCs = new double[]{999, 999, 400, 200, 80, 40, 20 };
            
            for (int r = 0; r < rounds; r++)
            {
                double factor = (ch == 0) ? fYs[r] : fCs[r];
                if (mode == QuantiseType.Reduce) factor = 1 / factor;

                var len = buffer.Length >> r;
                for (int i = len / 2; i < len; i++)
                {
                    buffer[i] *= factor;
                }
            }
        }

        private static void QuantisePlanar2(BitmapData si, double[] buffer, int ch, int rounds, QuantiseType mode)
        {
            // Planar two splits in half, starting with top/bottom, and alternating between
            // vertical and horizontal

            // Fibonacci coding strongly prefers small numbers

            // pretty good:
            var fYs = new[]{9, 4, 2.3, 1.5, 1, 1, 1, 1, 1 };
            var fCs = new[]{15, 10, 2, 1, 1, 1, 1, 1, 1 };
            
            // heavily crushed
            //var fYs = new[]{ 80, 50, 20, 10, 3.5, 1, 1, 1, 1};
            //var fCs = new[]{200, 100, 50, 10, 3.5, 2, 1, 1, 1};

            // about the same as 100% JPEG
            //var fYs = new[]{ 1, 1, 1, 1, 1, 1, 1, 1, 1};
            //var fCs = new[]{10000, 2, 1, 1, 1, 1, 1, 1, 1};

            for (int r = 0; r < rounds; r++)
            {
                double factor = (ch == 2) ? fYs[r] : fCs[r];

                if (mode == QuantiseType.Reduce) factor = 1 / factor;
                var width = si.Width >> r;
                var height = si.Height >> r;
                // vertical
                for (int y = height / 2; y < height; y++)
                {
                    var yo = y * si.Width;
                    for (int x = 0; x < width; x++)
                    {
                        buffer[yo+x] *= factor;
                    }
                }

                // half-horizontal
                for (int y = 0; y < height / 2; y++)
                {
                    var yo = y * si.Width;
                    for (int x = width / 2; x < width; x++)
                    {
                        buffer[yo+x] *= factor;
                    }
                }
            }
        }

        /// <summary>
        /// Separate scales into 1 set of coefficients using a spacefilling curve
        /// </summary>
        static unsafe void WaveletDecomposeMortonOrder(byte* s, byte* d, BitmapData si, BitmapData di)
        {
            // Change color space
            var bufferSize = To_YCxCx_ColorSpace(s, si);

            int rounds = (int)Math.Log(bufferSize, 2) - 1;
            Console.WriteLine($"Decomposing with {rounds} rounds");

            for (int ch = 0; ch < 3; ch++)
            {
                var buffer = ReadPlane(s, si, ch);

                // DC to AC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] -= 127.5; }

                // Transform
                buffer = ToMortonOrder(buffer, si.Width, si.Height);
                for (int i = 0; i < rounds; i++)
                {
                    var length = bufferSize >> i;
                    Fwt97(buffer, length, 0, 1);
                }

                WriteToFileFibonacci(buffer, ch, "morton");
                
                // read output
                ReadFromFileFibonacci(buffer, ch, "morton");

                // Restore
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var length = bufferSize >> i;
                    Iwt97(buffer, length, 0, 1);
                }

                buffer = FromMortonOrder(buffer, si.Width, si.Height);

                // AC to DC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5; }

                // Write back to image
                WritePlane(buffer, d, di, ch);
            }
            
            // Restore color space
            To_RGB_ColorSpace(d, bufferSize);
        }

        private static unsafe void To_RGB_ColorSpace(byte* d, int bufferSize)
        {
            var pixelBuf2 = (uint*) (d);
            for (int i = 0; i < bufferSize; i++)
            {
                //pixelBuf2[i] = ColorSpace.Ycbcr32_To_RGB32(pixelBuf2[i]);
                pixelBuf2[i] = ColorSpace.Ycocg32_To_RGB32(pixelBuf2[i]);
                // TODO: Chroma from luma estimation?
            }
        }

        private static unsafe int To_YCxCx_ColorSpace(byte* s, BitmapData si)
        {
            var pixelBuf = (uint*) (s);
            var bufferSize = si.Width * si.Height;
            for (int i = 0; i < bufferSize; i++)
            {
                //pixelBuf[i] = ColorSpace.RGB32_To_Ycbcr32(pixelBuf[i]);
                pixelBuf[i] = ColorSpace.RGB32_To_Ycocg32(pixelBuf[i]);
                // TODO: Chroma from luma estimation?
            }

            return bufferSize;
        }

        private static double[] FromMortonOrder(double[] src, int width, int height)
        {
            var dst = new double[width*height];
            var planeSize = width * height;
            for (uint i = 0; i < planeSize; i++) // each pixel (read cycle)
            {
                Morton.DecodeMorton2(i, out var x, out var y);
                //Hilbert.d2xy(si.Width, (int)i, out var x, out var y);

                var row = (int)y * height;
                dst[row + (int)x] = src[i];
            }
            return dst;
        }

        private static double[] ToMortonOrder(double[] src, int width, int height)
        {
            var dst = new double[width*height];
            for (uint y = 0; y < height; y++)
            {
                var row = y * height;
                for (uint x = 0; x < width; x++)
                {
                    var i = Morton.EncodeMorton2(x, y);
                    //var dst = Hilbert.xy2d(si.Width, (int)x, (int)y);

                    dst[i] = src[row + x];
                }
            }
            return dst;
        }

        private static void PlanarDecompose(BitmapData si, double[] buffer, int rounds)
        {
            for (int i = 0; i < rounds; i++)
            {
                var height = si.Height >> i;
                var width = si.Width >> i;
                // Wavelet decompose vertical
                for (int x = 0; x < width; x++) // each column
                {
                    Fwt97(buffer, height, x, si.Width);
                }

                // Wavelet decompose horizontal
                for (int y = 0; y < height; y++) // each row
                {
                    Fwt97(buffer, width, y * si.Width, 1);
                }
            }
        }

        private static void PlanarRestore(BitmapData si, double[] buffer, int rounds)
        {
            for (int i = rounds - 1; i >= 0; i--)
            {
                var height = si.Height >> i;
                var width = si.Width >> i;

                // Wavelet restore horizontal
                for (int y = 0; y < height; y++) // each row
                {
                    Iwt97(buffer, width, y * si.Width, 1);
                }

                // Wavelet restore vertical
                for (int x = 0; x < width; x++) // each column
                {
                    Iwt97(buffer, height, x, si.Width);
                }
            }
        }

        private static void ReadFromFileFibonacci(double[] buffer, int ch, string name)
        {
            var ms = new MemoryStream();
            var testpath = @"C:\gits\ImageTools\src\ImageTools.Tests\bin\Debug\outputs\"+name+"_fib_test_"+ch+".dat";


            // GZIP
            using (var fs = File.Open(testpath, FileMode.Open))
            {
                // switch comment to demonstrate shortened files
                //var trunc_sim = new TruncatedStream(fs, (int)(fs.Length * 0.1));
                var trunc_sim = fs;
                using (var gs = new DeflateStream(trunc_sim, CompressionMode.Decompress))
                {
                    DataEncoding.FibonacciDecode(gs, buffer);
                }
            }

            // LZMA (slower, slightly better compression)
            /*var raw = File.ReadAllBytes(testpath.Replace(".dat", ".lzma"));
            var instream = new MemoryStream(raw);
            CompressionUtility.Decompress(instream, ms, null);
            */
        }

        private static void WriteToFileFibonacci(double[] buffer, int ch, string name)
        {
            var testpath = @"C:\gits\ImageTools\src\ImageTools.Tests\bin\Debug\outputs\"+name+"_fib_test_"+ch+".dat";
            if (File.Exists(testpath)) File.Delete(testpath);

            // Common packing
            //var usig = DataEncoding.SignedToUnsigned(buffer.Select(d => (int)d).ToArray());
            //var bytes = DataEncoding.UnsignedFibEncode(usig);

            // LZMA (better compression, but a lot slower)
            /*var instream = new MemoryStream(bytes);
            using (var fs2 = File.Open(testpath.Replace(".dat", ".lzma"), FileMode.Create))
            {
                CompressionUtility.Compress(instream, fs2, null);
                fs2.Flush();
            }*/


            // GZIP
            using (var fs = File.Open(testpath, FileMode.Create))
            using (var gs = new DeflateStream(fs, CompressionMode.Compress))
            {
                DataEncoding.FibonacciEncode(buffer, gs);
                gs.Flush();
                fs.Flush();
            }
        }


        private static int Saturate(double value)
        {
            if (value < 0) return 0;
            if (value > 255) return 255;
            return (int)value;
        }


        private static unsafe double[] ReadPlane(byte* src, BitmapData si, int channelNumber) {
            var bytePerPix = si.Stride / si.Width;
            var samples = si.Width * si.Height;
            var limit = si.Stride * si.Height;

            var dst = new double[samples];
            var j = 0;
            for (int i = channelNumber; i < limit; i+= bytePerPix)
            {
                dst[j++] = src[i];
            }
            
            return dst;
        }
        
        private static unsafe void WritePlane(double[] src, byte* dst, BitmapData di, int channelNumber) {
            var bytePerPix = di.Stride / di.Width;
            var limit = di.Stride * di.Height;

            var j = 0;
            for (int i = channelNumber; i < limit; i+= bytePerPix)
            {
                dst[i] = (byte)Saturate(src[j++]);
            }
        }

        static unsafe void HorizonalWaveletTest(byte* s, byte* d, BitmapData si, BitmapData di)
        {
            for (int ch = 0; ch < 3; ch++)
            {
                var buffer = ReadPlane(s, si, ch);

                // DC to AC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] -= 127.5; }

                // Wavelet decompose
                for (int y = 0; y < si.Height; y++) // each row
                {
                    Fwt97(buffer, si.Width, y * si.Width, 1);
                }
                
                // Wavelet restore (half image)
                for (int y = 0; y < si.Height / 2; y++) // each row
                {
                    Iwt97(buffer, si.Width, y * si.Width, 1);
                }

                // AC to DC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5; }

                // Write back to image
                WritePlane(buffer, d, di, ch);
            }
        }
        
        static unsafe void VerticalWaveletTest(byte* s, byte* d, BitmapData si, BitmapData di)
        {
            for (int ch = 0; ch < 3; ch++)
            {
                var buffer = ReadPlane(s, si, ch);

                // DC to AC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] -= 127.5; }

                // Wavelet decompose
                for (int x = 0; x < si.Width; x++) // each column
                {
                    Fwt97(buffer, si.Height, x, si.Width);
                }

                
                // Wavelet restore (half image)
                for (int x = 0; x < si.Width / 2; x++) // each column
                {
                    Iwt97(buffer, si.Height, x, si.Width);
                }

                // AC to DC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5; }

                // Write back to image
                WritePlane(buffer, d, di, ch);
            }
        }

    }
}