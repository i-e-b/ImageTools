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


        // Reducing image by equal rounds
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
                img3d.V[i] -= 127.5;
                img3d.U[i] -= 127.5;
            }

            var quantise = 1.0;

            for (int ch = 0; ch < 3; ch++)
            {
                double[] buffer = null;
                switch(ch) {
                    case 0: buffer = img3d.Y; break;
                    case 1: buffer = img3d.U; break; // not entirely sure if orange or green deserves more bits
                    case 2: buffer = img3d.V; break;
                }
                int rounds = (int)Math.Log(img3d.MinDimension, 2);

                // Decompose Transform
                for (int i = 0; i < rounds; i++)
                {
                    var height = img3d.Height >> i;
                    var width = img3d.Width >> i;
                    var depth = img3d.Depth >> i;

                    var hx = new double[height];
                    var yx = new double[width];
                    var dx = new double[depth];

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
                            Fwt97(buffer, hx, zo + x, img3d.Width);
                        }

                        // Wavelet decompose horizontal
                        for (int y = 0; y < height >> 1; y++) // each row
                        {
                            var yo = (y * img3d.Width);
                            Fwt97(buffer, yx, zo + yo, 1);
                        }
                    }

                    // decompose through depth
                    for (int xy = 0; xy < img3d.zspan; xy++)
                    {
                        Fwt97(buffer, dx, xy, img3d.zspan);
                    }
                }

                // Quantise, compress, write
                var storage = ToMortonOrder3D(buffer, img3d);
                Quantise3D(storage, QuantiseType.Reduce, rounds, ch);

                WriteToFileFibonacci(storage, ch, "3D");

                ReadFromFileFibonacci(storage, ch, "3D");

                // De-quantise
                Quantise3D(storage, QuantiseType.Expand, rounds, ch);
                FromMortonOrder3D(storage, buffer, img3d);


                // Restore
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var height = img3d.Height >> i;
                    var width = img3d.Width >> i;
                    var depth = img3d.Depth >> i;

                    var hx = new double[height];
                    var yx = new double[width];
                    var dx = new double[depth];

                    // Order here must be exact reverse of above

                    // restore through depth
                    var dz = img3d.zspan;
                    for (int xy = 0; xy < img3d.zspan; xy++)
                    {
                        Iwt97(buffer, dx, xy, dz);
                    }

                    // Restore each plane independently
                    for (int z = 0; z < depth; z++)
                    {
                        var zo = z * img3d.zspan;

                        // Wavelet restore horizontal
                        for (int y = 0; y < height >> 1; y++) // each row
                        {
                            var yo = (y * img3d.Width);
                            Iwt97(buffer, yx, zo + yo, 1);
                        }

                        // Wavelet restore vertical
                        for (int x = 0; x < width; x++) // each column
                        {
                            Iwt97(buffer, hx, zo + x, img3d.Width);
                        }
                    }
                }
            }

            // AC to DC
            for (int i = 0; i < img3d.Y.Length; i++)
            {
                img3d.Y[i] += 127.5;
                img3d.V[i] += 127.5;
                img3d.U[i] += 127.5;
            }
        }

        
        // Reducing image in XY first then by Z
        public static void ReduceImage3D_2(Image3d img3d)
        {
            // DC to AC
            for (int i = 0; i < img3d.Y.Length; i++)
            {
                img3d.Y[i] -= 127.5;
                img3d.V[i] -= 127.5;
                img3d.U[i] -= 127.5;
            }

            var quantise = 1.0;
            int rounds;

            for (int ch = 0; ch < 3; ch++)
            {
                double[] buffer = null;
                switch(ch) {
                    case 0: buffer = img3d.Y; break;
                    case 1: buffer = img3d.U; break; // not entirely sure if orange or green deserves more bits
                    case 2: buffer = img3d.V; break;
                }

                // Note: The compression is the same regardless of depth-first or frame-first,
                //       but doing frame-first is much better for live capture, as each frame
                //       can be processed as it comes in, then the depth and packing done with
                //       a complete frameset on a separate processor.

                // Reduce each plane independently
                rounds = (int)Math.Log(img3d.Width, 2);
                for (int i = 0; i < rounds; i++)
                {
                    var height = img3d.Height >> i;
                    var width = img3d.Width >> i;
                    var depth = img3d.Depth;

                    var hx = new double[height];
                    var yx = new double[width];

                    for (int z = 0; z < depth; z++)
                    {
                        var zo = z * img3d.zspan;

                        // Wavelet decompose vertical
                        for (int x = 0; x < width; x++) // each column
                        {
                            Fwt97(buffer, hx, zo + x, img3d.Width);
                        }

                        // Wavelet decompose horizontal
                        for (int y = 0; y < height >> 1; y++) // each row
                        {
                            var yo = (y * img3d.Width);
                            Fwt97(buffer, yx, zo + yo, 1);
                        }
                    }
                }
                // decompose through depth
                rounds = (int)Math.Log(img3d.Depth, 2);
                for (int i = 0; i < rounds; i++)
                {
                    var depth = img3d.Depth >> i;
                    var dx = new double[depth];
                    for (int xy = 0; xy < img3d.zspan; xy++)
                    {
                        Fwt97(buffer, dx, xy, img3d.zspan);
                    }
                }


                // Reorder, quantise, write
                ToStorageOrder3D(buffer, img3d, (int)Math.Log(img3d.Width, 2));
                Quantise3D(buffer, QuantiseType.Reduce, (int)Math.Log(img3d.MaxDimension, 2), ch);
                WriteToFileFibonacci(buffer, ch, "3D");

                // clear buffer:
                for (int i = 0; i < buffer.Length; i++) { buffer[i] = 0.0; }

                // Read, De-quantise, reorder
                ReadFromFileFibonacci(buffer, ch, "3D");
                Quantise3D(buffer, QuantiseType.Expand, (int)Math.Log(img3d.MaxDimension, 2), ch);
                FromStorageOrder3D(buffer, img3d, (int)Math.Log(img3d.Width, 2));
                
                
                // Restore
                // through depth
                rounds = (int)Math.Log(img3d.Depth, 2);
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var depth = img3d.Depth >> i;
                    var dx = new double[depth];
                    for (int xy = 0; xy < img3d.zspan; xy++)
                    {
                        Iwt97(buffer, dx, xy, img3d.zspan);
                    }
                }
                // each plane independently
                rounds = (int)Math.Log(img3d.Width, 2);
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var height = img3d.Height >> i;
                    var width = img3d.Width >> i;
                    var depth = img3d.Depth;

                    var hx = new double[height];
                    var yx = new double[width];

                    for (int z = 0; z < depth; z++)
                    {
                        var zo = z * img3d.zspan;

                        // horizontal
                        for (int y = 0; y < height >> 1; y++) // each row
                        {
                            var yo = (y * img3d.Width);
                            Iwt97(buffer, yx, zo + yo, 1);
                        }

                        // vertical
                        for (int x = 0; x < width; x++) // each column
                        {
                            Iwt97(buffer, hx, zo + x, img3d.Width);
                        }
                    }
                }
            }

            // AC to DC
            for (int i = 0; i < img3d.Y.Length; i++)
            {
                img3d.Y[i] += 127.5;
                img3d.V[i] += 127.5;
                img3d.U[i] += 127.5;
            }
        }

        private static void CubeOrder(Image3d img3d, Action<int,int,int> wc) {
            for (int i = 0; i < img3d.MaxDimension; i++)
            {
                // corner
                wc(i,i,i);

                for (int a = 0; a < i; a++)
                {
                    // faces
                    for (int b = 0; b < i; b++)
                    {
                        wc(a,b,i);
                        wc(i,a,b);
                        wc(b,i,a);
                    }
                    // axis (excl corner)
                    wc(a,i,i); // x
                    wc(i,a,i); // y
                    wc(i,i,a); // z
                }
            }
        }

        // An attempt at reordering specifically for 3D CDF
        private static void FromStorageOrder3D(double[] buffer, Image3d img3d, int rounds)
        {
            var storage = new double[buffer.Length];

            var depth = img3d.Depth;

            // Plan: Do like the CDF reductions, but put all depths together before the next scale.

            int incrPos = 0;


            // first, any unreduced value
            for (int z = 0; z < depth; z++)
            {
                var zo = z * img3d.zspan;
                var height = img3d.Height >> rounds;
                var width = img3d.Width >> rounds;

                for (int y = 0; y < height; y++)
                {
                    var yo = y * img3d.yspan;
                    for (int x = 0; x < width; x++)
                    {
                        storage[zo + yo + x] = buffer[incrPos++];
                    }
                }
            }

            // now the reduced coefficients in order from most to least significant
            for (int i = rounds - 1; i >= 0; i--)
            {
                var height = img3d.Height >> i;
                var width = img3d.Width >> i;
                var left = width >> 1;
                var top = height >> 1;

                for (int z = 0; z < depth; z++)
                {
                    var zo = z * img3d.zspan;

                    // vertical block
                    // from top to the height of the horz block,
                    // from left=(right most of prev) to right
                    for (int x = left; x < width; x++) // each column
                    {
                        for (int y = 0; y < top; y++)
                        {
                            var yo = y * img3d.yspan;
                            storage[zo + yo + x] = buffer[incrPos++];
                        }
                    }

                    // horizontal block
                    for (int y = top; y < height; y++) // each row
                    {
                        var yo = y * img3d.yspan;
                        for (int x = 0; x < width; x++)
                        {
                            storage[zo + yo + x] = buffer[incrPos++];
                        }
                    }
                }
            }

            // copy back to buffer
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = storage[i];
            }
        }

        // An attempt at reordering specifically for 3D CDF
        private static void ToStorageOrder3D(double[] buffer, Image3d img3d, int rounds)
        {
            var storage = new double[buffer.Length];

            var depth = img3d.Depth;

            // Plan: Do like the CDF reductions, but put all depths together before the next scale.

            int incrPos = 0;


            // first, any unreduced value
            for (int z = 0; z < depth; z++)
            {
                var zo = z * img3d.zspan;
                var height = img3d.Height >> rounds;
                var width = img3d.Width >> rounds;

                for (int y = 0; y < height; y++)
                {
                    var yo = y * img3d.yspan;
                    for (int x = 0; x < width; x++)
                    {
                        storage[incrPos++] = buffer[zo + yo + x];
                    }
                }
            }

            // now the reduced coefficients in order from most to least significant
            for (int i = rounds - 1; i >= 0; i--)
            {
                var height = img3d.Height >> i;
                var width = img3d.Width >> i;
                var left = width >> 1;
                var top = height >> 1;

                for (int z = 0; z < depth; z++)
                {
                    var zo = z * img3d.zspan;

                    // vertical block
                    // from top to the height of the horz block,
                    // from left=(right most of prev) to right
                    for (int x = left; x < width; x++) // each column
                    {
                        for (int y = 0; y < top; y++)
                        {
                            var yo = y * img3d.yspan;
                            storage[incrPos++] = buffer[zo + yo + x];
                            //buffer[zo + yo + x] = -127;
                        }
                    }

                    // horizontal block
                    for (int y = top; y < height; y++) // each row
                    {
                        var yo = y * img3d.yspan;
                        for (int x = 0; x < width; x++)
                        {
                            storage[incrPos++] = buffer[zo + yo + x];
                        }
                    }
                }
            }

            // copy back to buffer
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = storage[i];
            }
        }

        

        private static void FromStorageOrder2D(double[] buffer, BitmapData si, int rounds)
        {
            var storage = new double[buffer.Length];

            // Plan: Do like the CDF reductions, but put all depths together before the next scale.
            int incrPos = 0;

            // first, any unreduced value
            var height = si.Height >> rounds;
            var width = si.Width >> rounds;

            for (int y = 0; y < height; y++)
            {
                var yo = y * si.Width;
                for (int x = 0; x < width; x++)
                {
                    storage[yo + x] = buffer[incrPos++];
                }
            }

            // now the reduced coefficients in order from most to least significant
            for (int i = rounds - 1; i >= 0; i--)
            {
                height = si.Height >> i;
                width = si.Width >> i;
                var left = width >> 1;
                var top = height >> 1;


                    // vertical block
                    // from top to the height of the horz block,
                    // from left=(right most of prev) to right
                    for (int x = left; x < width; x++) // each column
                    {
                        for (int y = 0; y < top; y++)
                        {
                            var yo = y * si.Width;
                            storage[yo + x] = buffer[incrPos++];
                        }
                    }

                    // horizontal block
                    for (int y = top; y < height; y++) // each row
                    {
                        var yo = y * si.Width;
                        for (int x = 0; x < width; x++)
                        {
                            storage[yo + x] = buffer[incrPos++];
                        }
                }
            }

            // copy back to buffer
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = storage[i];
            }
        }

        private static void ToStorageOrder2D(double[] buffer, BitmapData si, int rounds)
        {
            var storage = new double[buffer.Length];

            // Plan: Do like the CDF reductions, but put all depths together before the next scale.
            int incrPos = 0;

            // first, any unreduced value
            var height = si.Height >> rounds;
            var width = si.Width >> rounds;

            for (int y = 0; y < height; y++)
            {
                var yo = y * si.Width;
                for (int x = 0; x < width; x++)
                {
                    storage[incrPos++] = buffer[yo + x];
                }
            }

            // now the reduced coefficients in order from most to least significant
            for (int i = rounds - 1; i >= 0; i--)
            {
                height = si.Height >> i;
                width = si.Width >> i;
                var left = width >> 1;
                var top = height >> 1;


                    // vertical block
                    // from top to the height of the horz block,
                    // from left=(right most of prev) to right
                    for (int x = left; x < width; x++) // each column
                    {
                        for (int y = 0; y < top; y++)
                        {
                            var yo = y * si.Width;
                            storage[incrPos++] = buffer[yo + x];
                        }
                    }

                    // horizontal block
                    for (int y = top; y < height; y++) // each row
                    {
                        var yo = y * si.Width;
                        for (int x = 0; x < width; x++)
                        {
                            storage[incrPos++] = buffer[yo + x];
                        }
                }
            }

            // copy back to buffer
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = storage[i];
            }
        }


        private static void FromMortonOrder3D(double[] input, double[] output, Image3d img3d)
        {
            var limit = (int)Math.Pow(img3d.MaxDimension, 3);
            var mx = img3d.Width - 1;
            var my = img3d.Height - 1;
            var mz = img3d.Depth - 1;

            int o = 0;

            for (uint i = 0; i < limit; i++)
            {
                Morton.DecodeMorton3(i, out var x, out var y, out var z);
                if (x > mx || y > my || z > mz) continue;

                var si = x + (y * img3d.yspan) + (z * img3d.zspan);
                output[si] = input[o++];
            }
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
        public static void Fwt97(double[] buf, double[] x, int offset, int stride)
        {
            double a;
            int i;

            // pick out stride data
            var n = x.Length;
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
            var b = 1.149604398;
            for (i = 0; i < n; i+=2)
            {
                x[i] *= a;
                x[i+1] *= b;
            }

            // Pack into buffer (using stride and offset)
            // The raw output is like [DC][AC][DC][AC]...
            // we want it as          [DC][DC]...[AC][AC]
            var hn = n/2;
            for (i = 0; i < hn; i++) {
                buf[i * stride + offset] = x[i*2];
                buf[(i + hn) * stride + offset] = x[1 + i * 2];
            }
        }

        /// <summary>
        /// iwt97 - Inverse biorthogonal 9/7 wavelet transform
        /// <para></para>
        /// This is the inverse of fwt97 so that iwt97(fwt97(x,n),n)=x for every signal x of length n.
        /// <para></para>
        /// See also fwt97.
        /// </summary>
        public static void Iwt97(double[] buf, double[] x, int offset, int stride)
        {
            double a;
            int i;
                        
            // Unpack from stride into working buffer
            // The raw input is like [DC][DC]...[AC][AC]
            // we want it as         [DC][AC][DC][AC]...
            var n = x.Length;
            var hn = n/2;
            for (i = 0; i < hn; i++) {
                x[i*2] = buf[i * stride + offset];
                x[1 + i * 2] = buf[(i + hn) * stride + offset];
            }

            // Undo scale
            a = 1.149604398;
            var b = 1 / 1.149604398;
            for (i = 0; i < n; i+=2)
            {
                x[i] *= a;
                x[i+1] *= b;
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
            

            // write back stride data
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

            // Current best: 259kb; 253kb

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

                    var hx = new double[height];
                    var yx = new double[width];

                    // Wavelet decompose vertical
                    for (int x = 0; x < width; x++) // each column
                    {
                        Fwt97(buffer, hx, x, si.Width);
                    }
                    
                    // Wavelet decompose HALF horizontal
                    for (int y = 0; y < height / 2; y++) // each row
                    {
                        Fwt97(buffer, yx, y * si.Width, 1);
                    }
                }

                // Reorder, Quantise and reduce co-efficients
                ToStorageOrder2D(buffer, si, rounds);
                QuantisePlanar2(si, buffer, ch, rounds, QuantiseType.Reduce);

                // Write output
                WriteToFileFibonacci(buffer, ch, "p_2");

                // Prove reading is good:
                for (int i = 0; i < buffer.Length; i++) { buffer[i] = 0.0; }

                // read output
                ReadFromFileFibonacci(buffer, ch, "p_2");

                // Re-expand co-efficients
                QuantisePlanar2(si, buffer, ch, rounds, QuantiseType.Expand);
                FromStorageOrder2D(buffer, si, rounds);

                // Restore
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var height = si.Height >> i;
                    var width = si.Width >> i;

                    var hx = new double[height];
                    var yx = new double[width];

                    // Wavelet restore HALF horizontal
                    for (int y = 0; y < height / 2; y++) // each row
                    {
                        Iwt97(buffer, yx, y * si.Width, 1);
                    }

                    // Wavelet restore vertical
                    for (int x = 0; x < width; x++) // each column
                    {
                        Iwt97(buffer, hx, x, si.Width);
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

            // MJPEG 100% equivalent (zsep = 1,270kb) (lzma = 1,170kb) (cdf-ord = 1,210kb)
            //fYs = new double[]{ 1 };
            //fCs = new double[]{ 2 };

            // Good quality (test = 529kb) (morton = 477kb) (cbcr = 400kb) (zsep = 378kb)
            //              (lzma = 325kb) (cube = 362kb) (flat-morton: 401kb)
            //              (cdf-ord = 369kb)
            //fYs = new double[]{  5,  4,  3, 2, 1 };
            //fCs = new double[]{ 24, 15, 10, 7, 5, 3, 2 };

            // Medium compression (test = 224kb) (morton = 177kb) (cbcr = 151kb) (zsep = 131kb)
            //                    (lzma = 115kb) (cube = 162kb) (cdf-ord = 128kb/110kb)
            fYs = new double[]{ 24, 12, 7,  5, 3, 2, 1 };
            fCs = new double[]{ 50, 24, 12, 7, 5, 3, 2 };
            
            // Flat compression (cbcr = 116kb) (zsep = 95.1kb) (lzma 80.3kb)
            //fYs = new double[]{ 14, 14, 14, 14, 8, 4, 1 };
            //fCs = new double[]{ 400, 200, 100, 100, 90, 40, 20 };

            // Strong compression (test = 145kb) (morton = 113kb) (cbcr = 95.8kb) (zsep = 81.3kb)
            //fYs = new double[]{ 50,  35, 17, 10, 5, 3, 1 };
            //fCs = new double[]{200, 100, 50, 10, 5, 3, 2 };
            
            // Very strong compression (test = 95.3kb) (morton = 72.4kb) (cbcr = 64.4kb)
            //                         (zsep = 35.3kb) (lzma = 31.5kb) (ring = 57.6)
            //fYs = new double[]{200,  80,  60,  40, 10,  5,  4 };
            //fCs = new double[]{999, 999, 400, 200, 80, 40, 20 };

            
            // Very strong compression (lzma = 15.1kb) (gzip = 19.1kb) (cube=17.8kb) (cdf-ord = 11.6kb)
            //fYs = new double[]{200 };
            //fCs = new double[]{400 };
            
            // 'dish' compressed (morton = 69.0kb); This gives small file sizes and
            //                                      reasonable still-image detail, but
            //                                      at a cost of strong motion artefacts.
            //fYs = new double[]{200, 50,  20,   10,  20,  50, 200 };
            //fCs = new double[]{999, 999, 600, 400, 200, 200, 200 };

            // reverse dish (95kb) ... this one is quite weird
            //fYs = new double[]{10, 20,  50,   80,  50,  20, 10 };
            //fCs = new double[]{999, 999, 999, 999, 999, 999, 999 };
            
            
            // sigmoid-ish compression (zs morton = 111 kb) (ring = 135kb) (cube = 140kb) (cdf-ord = 104kb)
            //fYs = new double[]{ 15, 11,  7,  7,  7,  7, 4, 1.5 };
            //fCs = new double[]{ 200,50, 24, 15, 15, 10, 6, 4 };

            for (int r = 0; r < rounds; r++)
            {
                var factors = (ch == 0) ? fYs : fCs;
                var factor = (r >= factors.Length) ? factors[factors.Length - 1] : factors[r];
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
            // ReSharper disable JoinDeclarationAndInitializer
            double[] fYs, fCs;   
            // ReSharper restore JoinDeclarationAndInitializer

            // Planar two splits in half, starting with top/bottom, and alternating between
            // vertical and horizontal

            // Fibonacci coding strongly prefers small numbers

            // pretty good:
            fYs = new double[]{12, 9, 4, 2.3, 1.5 };
            fCs = new double[]{15, 10, 2 };
            
            // heavily crushed
            //fYs = new[]{ 80, 50, 20, 10, 3.5, 1, 1, 1, 1};
            //fCs = new[]{200, 100, 50, 10, 3.5, 2, 1, 1, 1};

            // about the same as 100% JPEG
            //fYs = new double[]{ 1, 1, 1, 1, 1, 1, 1, 1, 1};
            //fCs = new double[]{10000, 2, 1, 1, 1, 1, 1, 1, 1};

            
            for (int r = 0; r < rounds; r++)
            {
                var factors = (ch == 2) ? fYs : fCs;
                var factor = (r >= factors.Length) ? factors[factors.Length - 1] : factors[r];
                if (mode == QuantiseType.Reduce) factor = 1 / factor;

                var len = buffer.Length >> r;
                for (int i = len / 2; i < len; i++)
                {
                    buffer[i] *= factor;
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
                    var work = new double[length];
                    Fwt97(buffer, work, 0, 1);
                }

                WriteToFileFibonacci(buffer, ch, "morton");
                
                // read output
                ReadFromFileFibonacci(buffer, ch, "morton");

                // Restore
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var length = bufferSize >> i;
                    var work = new double[length];
                    Iwt97(buffer, work, 0, 1);
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

                var hx = new double[height];
                var wx = new double[width];

                // Wavelet decompose vertical
                for (int x = 0; x < width; x++) // each column
                {
                    Fwt97(buffer, hx, x, si.Width);
                }

                // Wavelet decompose horizontal
                for (int y = 0; y < height; y++) // each row
                {
                    Fwt97(buffer, wx, y * si.Width, 1);
                }
            }
        }

        private static void PlanarRestore(BitmapData si, double[] buffer, int rounds)
        {
            for (int i = rounds - 1; i >= 0; i--)
            {
                var height = si.Height >> i;
                var width = si.Width >> i;

                var hx = new double[height];
                var wx = new double[width];

                // Wavelet restore horizontal
                for (int y = 0; y < height; y++) // each row
                {
                    Iwt97(buffer, wx, y * si.Width, 1);
                }

                // Wavelet restore vertical
                for (int x = 0; x < width; x++) // each column
                {
                    Iwt97(buffer, hx, x, si.Width);
                }
            }
        }

        private static void ReadFromFileFibonacci(double[] buffer, int ch, string name)
        {
            var testpath = @"C:\gits\ImageTools\src\ImageTools.Tests\bin\Debug\outputs\" + name + "_fib_test_" + ch + ".dat";

            if (false)
            {
                // LZMA (slower, slightly better compression)
                var raw = File.ReadAllBytes(testpath.Replace(".dat", ".lzma"));
                using (var instream = new MemoryStream(raw))
                using (var ms = new MemoryStream())
                {
                    CompressionUtility.Decompress(instream, ms, null);
                    ms.Seek(0, SeekOrigin.Begin);
                    DataEncoding.FibonacciDecode(ms, buffer);
                }
            }
            else
            {
                // GZIP
                using (var fs = File.Open(testpath, FileMode.Open))
                {
                    // reduce factor to demonstrate shortened files
                    var length = (int)(fs.Length * 1.0);
                    Console.WriteLine($"Reading {length} bytes of a total {fs.Length}");
                    var trunc_sim = new TruncatedStream(fs, length);

                    using (var gs = new DeflateStream(trunc_sim, CompressionMode.Decompress))
                    {
                        DataEncoding.FibonacciDecode(gs, buffer);
                    }
                }
            }
        }

        private static void WriteToFileFibonacci(double[] buffer, int ch, string name)
        {
            var testpath = @"C:\gits\ImageTools\src\ImageTools.Tests\bin\Debug\outputs\"+name+"_fib_test_"+ch+".dat";
            if (File.Exists(testpath)) File.Delete(testpath);

            if (false)
            {
                // LZMA (better compression, but a lot slower)
                using (var ms = new MemoryStream())
                {
                    DataEncoding.FibonacciEncode(buffer, ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    using (var fs2 = File.Open(testpath.Replace(".dat", ".lzma"), FileMode.Create))
                    {
                        CompressionUtility.Compress(ms, fs2, null);
                        fs2.Flush();
                    }
                }
            }
            else
            {
                // GZIP
                using (var ms = new MemoryStream())
                {
                    DataEncoding.FibonacciEncode(buffer, ms);
                    ms.Seek(0, SeekOrigin.Begin);

                    using (var fs = File.Open(testpath, FileMode.Create))
                    using (var gs = new DeflateStream(fs, CompressionMode.Compress))
                    {
                        ms.CopyTo(gs);
                        gs.Flush();
                        fs.Flush();
                    }
                }
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
            var work = new double[si.Width];
            for (int ch = 0; ch < 3; ch++)
            {
                var buffer = ReadPlane(s, si, ch);

                // DC to AC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] -= 127.5; }

                // Wavelet decompose
                for (int y = 0; y < si.Height; y++) // each row
                {
                    Fwt97(buffer, work, y * si.Width, 1);
                }
                
                // Wavelet restore (half image)
                for (int y = 0; y < si.Height / 2; y++) // each row
                {
                    Iwt97(buffer, work, y * si.Width, 1);
                }

                // AC to DC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5; }

                // Write back to image
                WritePlane(buffer, d, di, ch);
            }
        }
        
        static unsafe void VerticalWaveletTest(byte* s, byte* d, BitmapData si, BitmapData di)
        {
            var work = new double[si.Height];
            for (int ch = 0; ch < 3; ch++)
            {
                var buffer = ReadPlane(s, si, ch);

                // DC to AC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] -= 127.5; }

                // Wavelet decompose
                for (int x = 0; x < si.Width; x++) // each column
                {
                    Fwt97(buffer, work, x, si.Width);
                }

                
                // Wavelet restore (half image)
                for (int x = 0; x < si.Width / 2; x++) // each column
                {
                    Iwt97(buffer, work, x, si.Width);
                }

                // AC to DC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5; }

                // Write back to image
                WritePlane(buffer, d, di, ch);
            }
        }

    }
}