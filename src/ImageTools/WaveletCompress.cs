using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using ImageTools.DataCompression.LZMA;
using ImageTools.Tests;
using ImageTools.Utilities;

namespace ImageTools
{
    /// <summary>
    /// Image compression and decompression using the CDF97 wavelet transform
    /// </summary>
    public class WaveletCompress
    {
        public static unsafe Bitmap HorizontalGradients(Bitmap src)
        {
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);

            BitmapTools.RunKernel(src, dst, HorizonalWaveletTest);

            return dst;
        }
        
        public static unsafe Bitmap VerticalGradients(Bitmap src)
        {
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);

            BitmapTools.RunKernel(src, dst, VerticalWaveletTest);

            return dst;
        }

        public static unsafe Bitmap MortonReduceImage(Bitmap src)
        {
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);

            BitmapTools.RunKernel(src, dst, WaveletDecomposeMortonOrder);

            return dst;
        }

        public static unsafe Bitmap Planar3ReduceImage(Bitmap src)
        {
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);

            BitmapTools.RunKernel(src, dst, WaveletDecomposePlanar3);

            return dst;
        }

        public static unsafe Bitmap Planar2ReduceImage(Bitmap src)
        {
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);

            BitmapTools.RunKernel(src, dst, WaveletDecomposePlanar2);

            return dst;
        }


        /// <summary>
        /// Compress a 3D image to a single file. This can be restored by `RestoreImage3D_FromFile`
        /// </summary>
        public static void ReduceImage3D_ToFile(Image3d img3d, string filePath) {
            // this is the first half of `ReduceImage3D_2`
            // DC to AC
            for (int i = 0; i < img3d.Y.Length; i++)
            {
                img3d.Y[i] -= 127.5f;
                img3d.V[i] -= 127.5f;
                img3d.U[i] -= 127.5f;
            }

            var quantise = 1.0;
            int rounds;

            var msY = new MemoryStream();
            var msU = new MemoryStream();
            var msV = new MemoryStream();

            for (int ch = 0; ch < 3; ch++)
            {
                float[] buffer = null;
                MemoryStream ms = null;
                switch(ch) {
                    case 0:
                        buffer = img3d.Y;
                        ms = msY;
                        break;
                    case 1:
                        buffer = img3d.U;
                        ms = msU;
                        break;
                    case 2:
                        buffer = img3d.V;
                        ms = msV;
                        break;
                }

                // Reduce each plane independently
                rounds = (int)Math.Log(img3d.Width, 2);
                for (int i = 0; i < rounds; i++)
                {
                    var height = img3d.Height >> i;
                    var width = img3d.Width >> i;
                    var depth = img3d.Depth;

                    var hx = new float[height];
                    var yx = new float[width];

                    for (int z = 0; z < depth; z++)
                    {
                        var zo = z * img3d.zspan;

                        // Wavelet decompose vertical
                        for (int x = 0; x < width; x++) // each column
                        {
                            CDF.Fwt97(buffer, hx, zo + x, img3d.Width);
                        }

                        // Wavelet decompose horizontal
                        for (int y = 0; y < height >> 1; y++) // each row
                        {
                            var yo = (y * img3d.Width);
                            CDF.Fwt97(buffer, yx, zo + yo, 1);
                        }
                    }
                }
                // decompose through depth
                rounds = (int)Math.Log(img3d.Depth, 2);
                for (int i = 0; i < rounds; i++)
                {
                    var depth = img3d.Depth >> i;
                    var dx = new float[depth];
                    for (int xy = 0; xy < img3d.zspan; xy++)
                    {
                        CDF.Fwt97(buffer, dx, xy, img3d.zspan);
                    }
                }


                // Reorder, quantise, encode
                ToStorageOrder3D(buffer, img3d, (int)Math.Log(img3d.Width, 2));
                Quantise3D(buffer, QuantiseType.Reduce, (int)Math.Log(img3d.MaxDimension, 2), ch);

                using (var tmp = new MemoryStream(buffer.Length))
                {   // byte-by-byte writing to DeflateStream is *very* slow, so we buffer
                    DataEncoding.FibonacciEncode(buffer, tmp);
                    using (var gs = new DeflateStream(ms, CompressionLevel.Optimal, true))
                    {
                        tmp.WriteTo(gs);
                        gs.Flush();
                    }
                }
            }
        
            // Individual stream sum    = 128kb
            // Interleave then compress = 160kb
            // Compress then interleave = 128kb

            // equivalent bpp: 0.062 (263146 bits)/(4194304 pixels)

            // interleave the files:
            msY.Seek(0, SeekOrigin.Begin);
            msU.Seek(0, SeekOrigin.Begin);
            msV.Seek(0, SeekOrigin.Begin);
            var container = new InterleavedFile((ushort)img3d.Width, (ushort)img3d.Height, (ushort)img3d.Depth, msY.ToArray(), msU.ToArray(), msV.ToArray());

            using (var fs = File.Open(filePath, FileMode.Create))
            {
                container.WriteToStream(fs);
                fs.Flush();
            }
        }

        /// <summary>
        /// Restore a 3D image from a single file (as created by `ReduceImage3D_ToFile`)
        /// </summary>
        public static Image3d RestoreImage3D_FromFile(string targetPath)
        {
            // Load raw data out of the container file
            InterleavedFile container;
            using (var fs = File.Open(targetPath, FileMode.Open))
            {
                // reduce factor to demonstrate shortened files
                var length = (int)(fs.Length * 1.0);
                Console.WriteLine($"Reading {length} bytes of a total {fs.Length}");
                var trunc_sim = new TruncatedStream(fs, length);

                container = InterleavedFile.ReadFromStream(trunc_sim);
            }
            if (container == null) return null;

            var img3d = new Image3d(container.Width, container.Height, container.Depth);
            int rounds;
            MemoryStream storedData = null;

            // restore into the image.
            // this MUST exactly match the reduce method, but with transforms in reverse order
            for (int ch = 0; ch < 3; ch++)
            {
                float[] buffer = null;
                switch(ch) {
                    case 0: 
                        buffer = img3d.Y;
                        storedData = new MemoryStream(container.Planes[0]);
                        break;
                    case 1:
                        buffer = img3d.U;
                        storedData =  new MemoryStream(container.Planes[1]);
                        break; // not entirely sure if orange or green deserves more bits
                    case 2:
                        buffer = img3d.V;
                        storedData = new MemoryStream(container.Planes[2]);
                        break;
                }

                // Read, De-quantise, reorder
                using (var gs = new DeflateStream(storedData, CompressionMode.Decompress))
                {
                    DataEncoding.FibonacciDecode(gs, buffer);
                }
                Quantise3D(buffer, QuantiseType.Expand, (int)Math.Log(img3d.MaxDimension, 2), ch);
                FromStorageOrder3D(buffer, img3d, (int)Math.Log(img3d.Width, 2));
                
                
                // Restore
                // through depth
                rounds = (int)Math.Log(img3d.Depth, 2);
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var depth = img3d.Depth >> i;
                    var dx = new float[depth];
                    for (int xy = 0; xy < img3d.zspan; xy++)
                    {
                        CDF.Iwt97(buffer, dx, xy, img3d.zspan);
                    }
                }
                // each plane independently
                rounds = (int)Math.Log(img3d.Width, 2);
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var height = img3d.Height >> i;
                    var width = img3d.Width >> i;
                    var depth = img3d.Depth;

                    var hx = new float[height];
                    var yx = new float[width];

                    for (int z = 0; z < depth; z++)
                    {
                        var zo = z * img3d.zspan;

                        // horizontal
                        for (int y = 0; y < height >> 1; y++) // each row
                        {
                            var yo = (y * img3d.Width);
                            CDF.Iwt97(buffer, yx, zo + yo, 1);
                        }

                        // vertical
                        for (int x = 0; x < width; x++) // each column
                        {
                            CDF.Iwt97(buffer, hx, zo + x, img3d.Width);
                        }
                    }
                }
            }

            // AC to DC
            for (int i = 0; i < img3d.Y.Length; i++)
            {
                img3d.Y[i] += 127.5f;
                img3d.V[i] += 127.5f;
                img3d.U[i] += 127.5f;
            }

            return img3d;
        }

        private static void Quantise3D(float[] buffer, QuantiseType mode, int rounds, int ch)
        {
            // ReSharper disable JoinDeclarationAndInitializer
            double[] fYs, fCs;   
            // ReSharper restore JoinDeclarationAndInitializer

            //              |< spacial blur
            //                               motion blur >|

            // Test MJPEG = 1,864kb

            // MJPEG 100% equivalent (zsep = 1,270kb) (lzma = 1,170kb) (cdf-ord = 1,210kb)
            //fYs = new double[]{ 1 };
            //fCs = new double[]{ 999,2 };

            // Good quality (test = 529kb) (morton = 477kb) (cbcr = 400kb) (zsep = 378kb)
            //              (lzma = 325kb) (cube = 362kb) (flat-morton: 401kb)
            //              (cdf-ord = 369kb)
            //fYs = new double[]{  5,  4,  3, 2, 1 };
            //fCs = new double[]{ 24, 15, 10, 7, 5, 3, 2 };

            // Medium compression (test = 224kb) (morton = 177kb) (cbcr = 151kb) (zsep = 131kb)
            //                    (lzma = 115kb) (cube = 162kb) (cdf-ord = 128kb/110kb)
            //fYs = new double[]{ 24, 12, 7,  5, 3, 2, 1 };
            //fCs = new double[]{ 50, 24, 12, 7, 5, 3, 2 };
            
            // Flat compression (cbcr = 116kb) (zsep = 95.1kb) (lzma 80.3kb)
            //fYs = new double[]{ 14, 14, 14, 14, 8, 4, 1 };
            //fCs = new double[]{ 400, 200, 100, 100, 90, 40, 20 };

            // Strong compression (test = 145kb) (morton = 113kb) (cbcr = 95.8kb) (zsep = 81.3kb)
            //fYs = new double[]{ 50,  35, 17, 10, 5, 3, 1 };
            //fCs = new double[]{200, 100, 50, 10, 5, 3, 2 };
            
            // Very strong compression (test = 95.3kb) (morton = 72.4kb) (cbcr = 64.4kb)
            //                         (zsep = 35.3kb) (lzma = 31.5kb) (ring = 57.6)
            //                         (cdf-ord = 30.0kb)
            //fYs = new double[]{200,  80,  60,  40, 10,  5,  4 };
            //fCs = new double[]{999, 999, 400, 200, 80, 40, 20 };
            
            // sigmoid-ish compression, with extra high-freq.
            // (cdf-ord = 169kb)
            fYs = new double[]{ 4.5, 14, 12,  7,  7,  7,  7, 4, 1.5 };
            fCs = new double[]{ 255, 50, 24, 15, 15, 10, 6, 3.5 };

            for (int r = 0; r < rounds; r++)
            {
                var factors = (ch == 0) ? fYs : fCs;
                float factor = (float)((r >= factors.Length) ? factors[factors.Length - 1] : factors[r]);
                if (mode == QuantiseType.Reduce) factor = 1 / factor;

                var len = buffer.Length >> r;
                for (int i = len / 2; i < len; i++)
                {
                    buffer[i] *= factor;
                }
            }
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
                img3d.Y[i] -= 127.5f;
                img3d.V[i] -= 127.5f;
                img3d.U[i] -= 127.5f;
            }

            var quantise = 1.0;

            for (int ch = 0; ch < 3; ch++)
            {
                float[] buffer = null;
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

                    var hx = new float[height];
                    var yx = new float[width];
                    var dx = new float[depth];

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
                            CDF.Fwt97(buffer, hx, zo + x, img3d.Width);
                        }

                        // Wavelet decompose horizontal
                        for (int y = 0; y < height >> 1; y++) // each row
                        {
                            var yo = (y * img3d.Width);
                            CDF.Fwt97(buffer, yx, zo + yo, 1);
                        }
                    }

                    // decompose through depth
                    for (int xy = 0; xy < img3d.zspan; xy++)
                    {
                        CDF.Fwt97(buffer, dx, xy, img3d.zspan);
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

                    var hx = new float[height];
                    var yx = new float[width];
                    var dx = new float[depth];

                    // Order here must be exact reverse of above

                    // restore through depth
                    var dz = img3d.zspan;
                    for (int xy = 0; xy < img3d.zspan; xy++)
                    {
                        CDF.Iwt97(buffer, dx, xy, dz);
                    }

                    // Restore each plane independently
                    for (int z = 0; z < depth; z++)
                    {
                        var zo = z * img3d.zspan;

                        // Wavelet restore horizontal
                        for (int y = 0; y < height >> 1; y++) // each row
                        {
                            var yo = (y * img3d.Width);
                            CDF.Iwt97(buffer, yx, zo + yo, 1);
                        }

                        // Wavelet restore vertical
                        for (int x = 0; x < width; x++) // each column
                        {
                            CDF.Iwt97(buffer, hx, zo + x, img3d.Width);
                        }
                    }
                }
            }

            // AC to DC
            for (int i = 0; i < img3d.Y.Length; i++)
            {
                img3d.Y[i] += 127.5f;
                img3d.V[i] += 127.5f;
                img3d.U[i] += 127.5f;
            }
        }
        
        // Reducing image in XY first then by Z
        public static void ReduceImage3D_2(Image3d img3d)
        {
            // DC to AC
            for (int i = 0; i < img3d.Y.Length; i++)
            {
                img3d.Y[i] -= 127.5f;
                img3d.V[i] -= 127.5f;
                img3d.U[i] -= 127.5f;
            }

            int rounds;

            for (int ch = 0; ch < 3; ch++)
            {
                float[] buffer = null;
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

                    var hx = new float[height];
                    var yx = new float[width];

                    for (int z = 0; z < depth; z++)
                    {
                        var zo = z * img3d.zspan;

                        // Wavelet decompose vertical
                        for (int x = 0; x < width; x++) // each column
                        {
                            CDF.Fwt97(buffer, hx, zo + x, img3d.Width);
                        }

                        // Wavelet decompose horizontal
                        for (int y = 0; y < height >> 1; y++) // each row
                        {
                            var yo = (y * img3d.Width);
                            CDF.Fwt97(buffer, yx, zo + yo, 1);
                        }
                    }
                }
                // decompose through depth
                rounds = (int)Math.Log(img3d.Depth, 2);
                for (int i = 0; i < rounds; i++)
                {
                    var depth = img3d.Depth >> i;
                    var dx = new float[depth];
                    for (int xy = 0; xy < img3d.zspan; xy++)
                    {
                        CDF.Fwt97(buffer, dx, xy, img3d.zspan);
                    }
                }


                // Reorder, quantise, write
                ToStorageOrder3D(buffer, img3d, (int)Math.Log(img3d.Width, 2));
                Quantise3D(buffer, QuantiseType.Reduce, (int)Math.Log(img3d.MaxDimension, 2), ch);
                WriteToFileFibonacci(buffer, ch, "3D");

                // clear buffer:
                for (int i = 0; i < buffer.Length; i++) { buffer[i] = 0.0f; }

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
                    var dx = new float[depth];
                    for (int xy = 0; xy < img3d.zspan; xy++)
                    {
                        CDF.Iwt97(buffer, dx, xy, img3d.zspan);
                    }
                }
                // each plane independently
                rounds = (int)Math.Log(img3d.Width, 2);
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var height = img3d.Height >> i;
                    var width = img3d.Width >> i;
                    var depth = img3d.Depth;

                    var hx = new float[height];
                    var yx = new float[width];

                    for (int z = 0; z < depth; z++)
                    {
                        var zo = z * img3d.zspan;

                        // horizontal
                        for (int y = 0; y < height >> 1; y++) // each row
                        {
                            var yo = (y * img3d.Width);
                            CDF.Iwt97(buffer, yx, zo + yo, 1);
                        }

                        // vertical
                        for (int x = 0; x < width; x++) // each column
                        {
                            CDF.Iwt97(buffer, hx, zo + x, img3d.Width);
                        }
                    }
                }
            }

            // AC to DC
            for (int i = 0; i < img3d.Y.Length; i++)
            {
                img3d.Y[i] += 127.5f;
                img3d.V[i] += 127.5f;
                img3d.U[i] += 127.5f;
            }
        }

        
        /// <summary>
        /// Separate scales into 3 sets of coefficients
        /// </summary>
        public static unsafe void WaveletDecomposePlanar3(byte* s, byte* d, BitmapData si, BitmapData di)
        {
            // Change color space
            var bufferSize = To_YCxCx_ColorSpace(s, si);


            int rounds = (int)Math.Log(si.Width, 2) - 1;
            Console.WriteLine($"Decomposing with {rounds} rounds");

            for (int ch = 0; ch < 3; ch++)
            {
                var buffer = ReadPlane(s, si, ch);

                // DC to AC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] -= 127.5f; }

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
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5f; }

                // Write back to image
                WritePlane(buffer, d, di, ch);
            }
            
            // Restore color space
            To_RGB_ColorSpace(d, bufferSize);
        }

        /// <summary>
        /// This version is a hybrid between Morton (1 set of Coeffs per round) and Planar (3 sets of Coeffs per round)
        /// </summary>
        public static unsafe void WaveletDecomposePlanar2(byte* s, byte* d, BitmapData si, BitmapData di)
        {
            // Change color space
            var bufferSize = To_YCxCx_ColorSpace(s, si);

            // Current best: 265kb

            int rounds = (int)Math.Log(si.Width, 2) - 1;
            Console.WriteLine($"Decomposing with {rounds} rounds");

            for (int ch = 0; ch < 3; ch++)
            {
                var buffer = ReadPlane(s, si, ch);

                // DC to AC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] -= 127.5f; }

                // Transform
                for (int i = 0; i < rounds; i++)
                {
                    var height = si.Height >> i;
                    var width = si.Width >> i;

                    var hx = new float[height];
                    var yx = new float[width];

                    // Wavelet decompose vertical
                    for (int x = 0; x < width; x++) // each column
                    {
                        CDF.Fwt97(buffer, hx, x, si.Width);
                    }
                    
                    // Wavelet decompose HALF horizontal
                    for (int y = 0; y < height / 2; y++) // each row
                    {
                        CDF.Fwt97(buffer, yx, y * si.Width, 1);
                    }
                }

                // Unquantised: native: 708kb; Ordered:  705kb
                // Reorder, Quantise and reduce co-efficients
                ToStorageOrder2D(buffer, si.Width, si.Height, rounds);
                QuantisePlanar2(buffer, ch, rounds, QuantiseType.Reduce);

                // Write output
                WriteToFileFibonacci(buffer, ch, "p_2");

                // Prove reading is good:
                for (int i = 0; i < buffer.Length; i++) { buffer[i] = 0.0f; }

                // read output
                ReadFromFileFibonacci(buffer, ch, "p_2");

                // Re-expand co-efficients
                QuantisePlanar2(buffer, ch, rounds, QuantiseType.Expand);
                FromStorageOrder2D(buffer, si.Width, si.Height, rounds);

                // Restore
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var height = si.Height >> i;
                    var width = si.Width >> i;

                    var hx = new float[height];
                    var yx = new float[width];

                    // Wavelet restore HALF horizontal
                    for (int y = 0; y < height / 2; y++) // each row
                    {
                        CDF.Iwt97(buffer, yx, y * si.Width, 1);
                    }

                    // Wavelet restore vertical
                    for (int x = 0; x < width; x++) // each column
                    {
                        CDF.Iwt97(buffer, hx, x, si.Width);
                    }
                }
                
                // AC to DC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5f; }

                // Write back to image
                WritePlane(buffer, d, di, ch);
            }
            
            // Restore color space
            To_RGB_ColorSpace(d, bufferSize);
        }

        /// <summary>
        /// Separate scales into 1 set of coefficients using a spacefilling curve
        /// </summary>
        public static unsafe void WaveletDecomposeMortonOrder(byte* s, byte* d, BitmapData si, BitmapData di)
        {
            // Change color space
            var bufferSize = To_YCxCx_ColorSpace(s, si);

            int rounds = (int)Math.Log(bufferSize, 2) - 1;
            Console.WriteLine($"Decomposing with {rounds} rounds");

            for (int ch = 0; ch < 3; ch++)
            {
                var buffer = ReadPlane(s, si, ch);

                // DC to AC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] -= 127.5f; }

                // Transform
                buffer = ToMortonOrder(buffer, si.Width, si.Height);
                for (int i = 0; i < rounds; i++)
                {
                    var length = bufferSize >> i;
                    var work = new float[length];
                    CDF.Fwt97(buffer, work, 0, 1);
                }

                WriteToFileFibonacci(buffer, ch, "morton");
                
                // read output
                ReadFromFileFibonacci(buffer, ch, "morton");

                // Restore
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var length = bufferSize >> i;
                    var work = new float[length];
                    CDF.Iwt97(buffer, work, 0, 1);
                }

                buffer = FromMortonOrder(buffer, si.Width, si.Height);

                // AC to DC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5f; }

                // Write back to image
                WritePlane(buffer, d, di, ch);
            }
            
            // Restore color space
            To_RGB_ColorSpace(d, bufferSize);
        }

        // An attempt at reordering specifically for 3D CDF
        public static void FromStorageOrder3D(float[] buffer, Image3d img3d, int rounds)
        {
            var storage = new float[buffer.Length];

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
        public static void ToStorageOrder3D(float[] buffer, Image3d img3d, int rounds)
        {
            var storage = new float[buffer.Length];

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

        public static void FromStorageOrder2D(float[] buffer, int srcWidth, int srcHeight, int rounds)
        {
            var storage = new float[buffer.Length];

            // Plan: Do like the CDF reductions, but put all depths together before the next scale.
            int incrPos = 0;

            // first, any unreduced value
            var height = srcHeight >> rounds;
            var width = srcWidth >> rounds;

            for (int y = 0; y < height; y++)
            {
                var yo = y * srcWidth;
                for (int x = 0; x < width; x++)
                {
                    storage[yo + x] = buffer[incrPos++];
                }
            }

            // now the reduced coefficients in order from most to least significant
            for (int i = rounds - 1; i >= 0; i--)
            {
                height = srcHeight >> i;
                width = srcWidth >> i;
                var left = width >> 1;
                var top = height >> 1;


                    // vertical block
                    // from top to the height of the horz block,
                    // from left=(right most of prev) to right
                    for (int x = left; x < width; x++) // each column
                    {
                        for (int y = 0; y < top; y++)
                        {
                            var yo = y * srcWidth;
                            storage[yo + x] = buffer[incrPos++];
                        }
                    }

                    // horizontal block
                    for (int y = top; y < height; y++) // each row
                    {
                        var yo = y * srcWidth;
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

        public static void ToStorageOrder2D(float[] buffer, int srcWidth, int srcHeight, int rounds)
        {
            var storage = new float[buffer.Length];

            // Plan: Do like the CDF reductions, but put all depths together before the next scale.
            int incrPos = 0;

            // first, any unreduced value
            var height = srcHeight >> rounds;
            var width = srcWidth >> rounds;

            for (int y = 0; y < height; y++)
            {
                var yo = y * srcWidth;
                for (int x = 0; x < width; x++)
                {
                    storage[incrPos++] = buffer[yo + x];
                }
            }

            // now the reduced coefficients in order from most to least significant
            for (int i = rounds - 1; i >= 0; i--)
            {
                height = srcHeight >> i;
                width = srcWidth >> i;
                var left = width >> 1;
                var top = height >> 1;


                    // vertical block
                    // from top to the height of the horz block,
                    // from left=(right most of prev) to right
                    for (int x = left; x < width; x++) // each column
                    {
                        for (int y = 0; y < top; y++)
                        {
                            var yo = y * srcWidth;
                            storage[incrPos++] = buffer[yo + x];
                        }
                    }

                    // horizontal block
                    for (int y = top; y < height; y++) // each row
                    {
                        var yo = y * srcWidth;
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


        private static void FromMortonOrder3D(float[] input, float[] output, Image3d img3d)
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

        private static float[] ToMortonOrder3D(float[] buffer, Image3d img3d)
        {
            var limit = (int)Math.Pow(img3d.MaxDimension, 3);
            var mx = img3d.Width - 1;
            var my = img3d.Height - 1;
            var mz = img3d.Depth - 1;

            var swap = new float[buffer.Length];
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

        private static void QuantisePlanar2(float[] buffer, int ch, int rounds, QuantiseType mode)
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
            //fYs = new double[]{ 60, 60, 40, 20, 10, 6.5, 3.5, 1.5 };
            //fCs = new double[]{1000, 200, 100, 50, 20, 10, 4};

            // about the same as 100% JPEG
            //fYs = new double[]{ 1, 1, 1, 1, 1, 1, 1, 1, 1};
            //fCs = new double[]{10000, 2, 1, 1, 1, 1, 1, 1, 1};

            
            for (int r = 0; r < rounds; r++)
            {
                var factors = (ch == 2) ? fYs : fCs;
                float factor = (float)((r >= factors.Length) ? factors[factors.Length - 1] : factors[r]);
                if (mode == QuantiseType.Reduce) factor = 1 / factor;

                var len = buffer.Length >> r;
                for (int i = len >> 1; i < len; i++)
                {
                    buffer[i] *= factor;
                }
            }
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

        private static float[] FromMortonOrder(float[] src, int width, int height)
        {
            var dst = new float[width*height];
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

        private static float[] ToMortonOrder(float[] src, int width, int height)
        {
            var dst = new float[width*height];
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

        private static void PlanarDecompose(BitmapData si, float[] buffer, int rounds)
        {
            for (int i = 0; i < rounds; i++)
            {
                var height = si.Height >> i;
                var width = si.Width >> i;

                var hx = new float[height];
                var wx = new float[width];

                // Wavelet decompose vertical
                for (int x = 0; x < width; x++) // each column
                {
                    CDF.Fwt97(buffer, hx, x, si.Width);
                }

                // Wavelet decompose horizontal
                for (int y = 0; y < height; y++) // each row
                {
                    CDF.Fwt97(buffer, wx, y * si.Width, 1);
                }
            }
        }

        private static void PlanarRestore(BitmapData si, float[] buffer, int rounds)
        {
            for (int i = rounds - 1; i >= 0; i--)
            {
                var height = si.Height >> i;
                var width = si.Width >> i;

                var hx = new float[height];
                var wx = new float[width];

                // Wavelet restore horizontal
                for (int y = 0; y < height; y++) // each row
                {
                    CDF.Iwt97(buffer, wx, y * si.Width, 1);
                }

                // Wavelet restore vertical
                for (int x = 0; x < width; x++) // each column
                {
                    CDF.Iwt97(buffer, hx, x, si.Width);
                }
            }
        }

        private static void ReadFromFileFibonacci(float[] buffer, int ch, string name)
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
                // Deflate
                using (var ms = new MemoryStream())
                {   // byte-by-byte reading from Deflate stream is *very* slow, so we buffer it
                    using (var fs = File.Open(testpath, FileMode.Open))
                    {
                        // reduce factor to demonstrate shortened files
                        var length = (int)(fs.Length * 1.0);
                        Console.WriteLine($"Reading {length} bytes of a total {fs.Length}");
                        var trunc_sim = new TruncatedStream(fs, length);

                        using (var gs = new DeflateStream(trunc_sim, CompressionMode.Decompress))
                        {
                            gs.CopyTo(ms);
                        }
                    }

                    // now the actual decode:
                    ms.Seek(0, SeekOrigin.Begin);
                    DataEncoding.FibonacciDecode(ms, buffer);
                }
            }
        }

        private static void WriteToFileFibonacci(float[] buffer, int ch, string name)
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
                // Deflate
                using (var ms = new MemoryStream(buffer.Length)) // this is bytes/8
                {   // byte-by-byte input to deflate stream is *very* slow, so we buffer it first
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

        private static int Saturate(float value)
        {
            if (value < 0) return 0;
            if (value > 255) return 255;
            return (int)value;
        }

        private static unsafe float[] ReadPlane(byte* src, BitmapData si, int channelNumber) {
            var bytePerPix = si.Stride / si.Width;
            var samples = si.Width * si.Height;
            var limit = si.Stride * si.Height;

            var dst = new float[samples];
            var j = 0;
            for (int i = channelNumber; i < limit; i+= bytePerPix)
            {
                dst[j++] = src[i];
            }
            
            return dst;
        }
        
        private static unsafe void WritePlane(float[] src, byte* dst, BitmapData di, int channelNumber) {
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
            var work = new float[si.Width];
            for (int ch = 0; ch < 3; ch++)
            {
                var buffer = ReadPlane(s, si, ch);

                // DC to AC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] -= 127.5f; }

                // Wavelet decompose
                for (int y = 0; y < si.Height; y++) // each row
                {
                    CDF.Fwt97(buffer, work, y * si.Width, 1);
                }
                
                // Wavelet restore (half image)
                for (int y = 0; y < si.Height / 2; y++) // each row
                {
                    CDF.Iwt97(buffer, work, y * si.Width, 1);
                }

                // AC to DC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5f; }

                // Write back to image
                WritePlane(buffer, d, di, ch);
            }
        }
        
        static unsafe void VerticalWaveletTest(byte* s, byte* d, BitmapData si, BitmapData di)
        {
            var work = new float[si.Height];
            for (int ch = 0; ch < 3; ch++)
            {
                var buffer = ReadPlane(s, si, ch);

                // DC to AC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] -= 127.5f; }

                // Wavelet decompose
                for (int x = 0; x < si.Width; x++) // each column
                {
                    CDF.Fwt97(buffer, work, x, si.Width);
                }

                
                // Wavelet restore (half image)
                for (int x = 0; x < si.Width / 2; x++) // each column
                {
                    CDF.Iwt97(buffer, work, x, si.Width);
                }

                // AC to DC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5f; }

                // Write back to image
                WritePlane(buffer, d, di, ch);
            }
        }

    }
}