﻿using System;
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

        public static Bitmap Planar3ReduceImage(Bitmap src)
        {
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            
            BitmapTools.ArgbImageToYUVPlanes_ForcePower2(src, out var Y, out var U, out var V, out var width, out var height);
            WaveletDecomposePlanar3(Y,U,V, width, height);
            BitmapTools.YUVPlanes_To_ArgbImage_Slice(dst, 0, width, Y, U, V);

            return dst;
        }

        public static Bitmap Planar2ReduceImage(Bitmap src)
        {
            BitmapTools.ArgbImageToYUVPlanes_ForcePower2(src, out var Y, out var U, out var V, out var width, out var height);
            WaveletDecomposePlanar2(Y,U,V, width, height, src.Width, src.Height);

            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            BitmapTools.YUVPlanes_To_ArgbImage_Slice(dst, 0, width, Y, U, V);

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
                    var wx = new float[width];

                    for (int z = 0; z < depth; z++)
                    {
                        var zo = z * img3d.zspan;

                        // Wavelet decompose vertical
                        for (int x = 0; x < width; x++) // each column
                        {
                            CDF.Fwt97(buffer, hx, height, zo + x, img3d.Width);
                        }

                        // Wavelet decompose horizontal
                        for (int y = 0; y < height >> 1; y++) // each row
                        {
                            var yo = (y * img3d.Width);
                            CDF.Fwt97(buffer, wx, width, zo + yo, 1);
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
                        CDF.Fwt97(buffer, dx, depth, xy, img3d.zspan);
                    }
                }


                // Reorder, quantise, encode
                ToStorageOrder3D(buffer, img3d, (int)Math.Log(img3d.Width, 2));
                Quantise3D(buffer, QuantiseType.Reduce, ch);

                using (var tmp = new MemoryStream(buffer.Length))
                {   // byte-by-byte writing to DeflateStream is *very* slow, so we buffer
                    DataEncoding.FibonacciEncode(buffer, 0, tmp);
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
                Console.WriteLine($"Reading {Bin.Human(length)} bytes of a total {Bin.Human(fs.Length)}");
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
                Quantise3D(buffer, QuantiseType.Expand, ch);
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
                        CDF.Iwt97(buffer, dx, depth, xy, img3d.zspan);
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
                    var wx = new float[width];

                    for (int z = 0; z < depth; z++)
                    {
                        var zo = z * img3d.zspan;

                        // horizontal
                        for (int y = 0; y < height >> 1; y++) // each row
                        {
                            var yo = (y * img3d.Width);
                            CDF.Iwt97(buffer, wx, width, zo + yo, 1);
                        }

                        // vertical
                        for (int x = 0; x < width; x++) // each column
                        {
                            CDF.Iwt97(buffer, hx, height, zo + x, img3d.Width);
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

        private static void Quantise3D(float[] buffer, QuantiseType mode, int ch)
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
            //              (cdf-ord = 369kb) (cdf-more-round = 367kb)
            //fYs = new double[]{  5,  4,  3, 2, 1 };
            //fCs = new double[]{ 24, 15, 10, 7, 5, 3, 2 };
            
            // Good quality,long tail (cdf-more-round = 351kb)
            fYs = new double[]{  5,  4,  3, 2, 1.5 };
            fCs = new double[]{ 24, 15, 10, 7, 5, 3, 2 };

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
            // sqrt 2nd: 27.4; no sqrt: 30.0; sqrt 1st: 15.2kb
            //fYs = new double[]{200,  80,  60,  40, 10,  5,  4 };
            //fCs = new double[]{999, 999, 400, 200, 80, 40, 20 };
            
            // sigmoid-ish compression, with extra high-freq.
            // (cdf-ord = 169kb), (cdf-more-rounds = 165kb)
            // no sqrt: 169; sqrt 2nd: 151   ...sqrt harms low freq quite badly. Sqrt alone = 1200. High freq dominates
            //fYs = new double[]{ 4.5, 14, 12,  7,  7,  7,  7, 4, 1.5 };
            //fCs = new double[]{ 255, 50, 24, 15, 15, 10, 6, 3.5 };

            
            var rounds = (int)Math.Log(buffer.Length, 2);
            for (int r = 0; r <  rounds; r++)
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


        /// <summary>
        /// Compress an image to a byte stream
        /// </summary>
        public static InterleavedFile ReduceImage2D_ToFile(Bitmap src)
        {
            if (src == null) return null;
            BitmapTools.ArgbImageToYUVPlanes_ForcePower2(src, out var Y, out var U, out var V, out var planeWidth, out var planeHeight);
            int imgWidth = src.Width;
            int imgHeight = src.Height;

            int rounds = (int)Math.Log(planeWidth, 2);

            var p2Height = (int)Bin.NextPow2((uint)planeHeight);
            var p2Width = (int)Bin.NextPow2((uint)planeWidth);
            var hx = new float[p2Height];
            var wx = new float[p2Width];

            var msY = new MemoryStream();
            var msU = new MemoryStream();
            var msV = new MemoryStream();

            for (int ch = 0; ch < 3; ch++)
            {
                var buffer = Pick(ch, Y, U, V);
                var ms = Pick(ch, msY, msU, msV);

                // DC to AC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] -= 127.5f; }

                // Transform
                for (int i = 0; i < rounds; i++)
                {
                    var height = p2Height >> i;
                    var width = p2Width >> i;

                    // Wavelet decompose vertical
                    for (int x = 0; x < width; x++) // each column
                    {
                        CDF.Fwt97(buffer, hx, height, x, planeWidth);
                    }

                    // Wavelet decompose HALF horizontal
                    for (int y = 0; y < height / 2; y++) // each row
                    {
                        CDF.Fwt97(buffer, wx, width, y * planeWidth, 1);
                    }
                }

                // Reorder, Quantise and reduce co-efficients
                var packedLength = ToStorageOrder2D(buffer, planeWidth, planeHeight, rounds, imgWidth, imgHeight);
                QuantisePlanar2(buffer, ch, packedLength, QuantiseType.Reduce);

                // Write output
                using (var tmp = new MemoryStream(buffer.Length))
                {   // byte-by-byte writing to DeflateStream is *very* slow, so we buffer
                    DataEncoding.FibonacciEncode(buffer, 0, tmp);
                    using (var gs = new DeflateStream(ms, CompressionLevel.Optimal, true))
                    {
                        tmp.WriteTo(gs);
                        gs.Flush();
                    }
                }
            }

            // interleave the files:
            msY.Seek(0, SeekOrigin.Begin);
            msU.Seek(0, SeekOrigin.Begin);
            msV.Seek(0, SeekOrigin.Begin);
            var container = new InterleavedFile((ushort)imgWidth, (ushort)imgHeight, 1,
                msY.ToArray(), msU.ToArray(), msV.ToArray());

            msY.Dispose();
            msU.Dispose();
            msV.Dispose();
            return container;
        }

        /// <summary>
        /// Restore an image from a byte stream
        /// </summary>
        /// <param name="container">Image container</param>
        /// <param name="scale">Optional: restore scaled down. 1 is natural size, 2 is half size, 3 is quarter size</param>
        public static Bitmap RestoreImage2D_FromFile(InterleavedFile container, byte scale = 1)
        {
            if (container == null) return null;
            var Ybytes = container.Planes?[0];
            var Ubytes = container.Planes?[1];
            var Vbytes = container.Planes?[2];

            if (Ybytes == null || Ubytes == null || Vbytes == null) throw new NullReferenceException("Planes were not read from image correctly");

            int imgWidth = container.Width;
            int imgHeight = container.Height;

            // the original image source's internal buffer size
            var packedLength = Bin.NextPow2(imgWidth) * Bin.NextPow2(imgHeight);

            // scale by a power of 2
            if (scale < 1) scale = 1;
            var scaleShift = scale - 1;
            if (scale > 1)
            {
                imgWidth >>= scaleShift;
                imgHeight >>= scaleShift;
            }

            var planeWidth = Bin.NextPow2(imgWidth);
            var planeHeight = Bin.NextPow2(imgHeight);

            var sampleCount = planeHeight * planeWidth;

            var Y = new float[sampleCount];
            var U = new float[sampleCount];
            var V = new float[sampleCount];

            var hx = new float[planeHeight];
            var wx = new float[planeWidth];

            int rounds = (int)Math.Log(planeWidth, 2);

            for (int ch = 0; ch < 3; ch++)
            {

                var buffer = Pick(ch, Y, U, V);
                if (buffer == null) continue;
                var storedData = new MemoryStream(Pick(ch, Ybytes, Ubytes, Vbytes));

                using (var gs = new DeflateStream(storedData, CompressionMode.Decompress))
                {
                    DataEncoding.FibonacciDecode(gs, buffer);
                }

                // Re-expand co-efficients
                QuantisePlanar2(buffer, ch, packedLength, QuantiseType.Expand);
                FromStorageOrder2D(buffer, planeWidth, planeHeight, rounds, imgWidth, imgHeight, scaleShift);

                // Restore
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var height = planeHeight >> i;
                    var width = planeWidth >> i;

                    // Wavelet restore HALF horizontal
                    for (int y = 0; y < height / 2; y++) // each row
                    {
                        CDF.Iwt97(buffer, wx, width, y * planeWidth, 1);
                    }

                    // Wavelet restore vertical
                    for (int x = 0; x < width; x++) // each column
                    {
                        CDF.Iwt97(buffer, hx, height, x, planeWidth);
                    }
                }

                // AC to DC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5f; }
            }

            var dst = new Bitmap(imgWidth, imgHeight, PixelFormat.Format32bppArgb);
            BitmapTools.YUVPlanes_To_ArgbImage_Slice(dst, 0, planeWidth, Y, U, V);
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
                img3d.Y[i] -= 127.5f;
                img3d.V[i] -= 127.5f;
                img3d.U[i] -= 127.5f;
            }

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
                    var wx = new float[width];
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
                            CDF.Fwt97(buffer, hx, height, zo + x, img3d.Width);
                        }

                        // Wavelet decompose horizontal
                        for (int y = 0; y < height >> 1; y++) // each row
                        {
                            var yo = (y * img3d.Width);
                            CDF.Fwt97(buffer, wx, width, zo + yo, 1);
                        }
                    }

                    // decompose through depth
                    for (int xy = 0; xy < img3d.zspan; xy++)
                    {
                        CDF.Fwt97(buffer, dx, depth, xy, img3d.zspan);
                    }
                }

                // Quantise, compress, write
                var storage = ToMortonOrder3D(buffer, img3d);
                Quantise3D(storage, QuantiseType.Reduce, ch);

                WriteToFileFibonacci(storage, ch, storage.Length, "3D");

                ReadFromFileFibonacci(storage, ch, "3D");

                // De-quantise
                Quantise3D(storage, QuantiseType.Expand, ch);
                FromMortonOrder3D(storage, buffer, img3d);


                // Restore
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var height = img3d.Height >> i;
                    var width = img3d.Width >> i;
                    var depth = img3d.Depth >> i;

                    var hx = new float[height];
                    var wx = new float[width];
                    var dx = new float[depth];

                    // Order here must be exact reverse of above

                    // restore through depth
                    var dz = img3d.zspan;
                    for (int xy = 0; xy < img3d.zspan; xy++)
                    {
                        CDF.Iwt97(buffer, dx, depth, xy, dz);
                    }

                    // Restore each plane independently
                    for (int z = 0; z < depth; z++)
                    {
                        var zo = z * img3d.zspan;

                        // Wavelet restore horizontal
                        for (int y = 0; y < height >> 1; y++) // each row
                        {
                            var yo = (y * img3d.Width);
                            CDF.Iwt97(buffer, wx, width, zo + yo, 1);
                        }

                        // Wavelet restore vertical
                        for (int x = 0; x < width; x++) // each column
                        {
                            CDF.Iwt97(buffer, hx, height, zo + x, img3d.Width);
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
                    var wx = new float[width];

                    for (int z = 0; z < depth; z++)
                    {
                        var zo = z * img3d.zspan;

                        // Wavelet decompose vertical
                        for (int x = 0; x < width; x++) // each column
                        {
                            CDF.Fwt97(buffer, hx, height, zo + x, img3d.Width);
                        }

                        // Wavelet decompose horizontal
                        for (int y = 0; y < height >> 1; y++) // each row
                        {
                            var yo = (y * img3d.Width);
                            CDF.Fwt97(buffer, wx, width, zo + yo, 1);
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
                        CDF.Fwt97(buffer, dx, depth, xy, img3d.zspan);
                    }
                }


                // Reorder, quantise, write
                ToStorageOrder3D(buffer, img3d, (int)Math.Log(img3d.Width, 2));
                Quantise3D(buffer, QuantiseType.Reduce, ch);
                WriteToFileFibonacci(buffer, ch, buffer.Length, "3D");

                // clear buffer:
                for (int i = 0; i < buffer.Length; i++) { buffer[i] = 0.0f; }

                // Read, De-quantise, reorder
                ReadFromFileFibonacci(buffer, ch, "3D");
                Quantise3D(buffer, QuantiseType.Expand, ch);
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
                        CDF.Iwt97(buffer, dx, depth, xy, img3d.zspan);
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
                    var wx = new float[width];

                    for (int z = 0; z < depth; z++)
                    {
                        var zo = z * img3d.zspan;

                        // horizontal
                        for (int y = 0; y < height >> 1; y++) // each row
                        {
                            var yo = (y * img3d.Width);
                            CDF.Iwt97(buffer, wx, width, zo + yo, 1);
                        }

                        // vertical
                        for (int x = 0; x < width; x++) // each column
                        {
                            CDF.Iwt97(buffer, hx, height, zo + x, img3d.Width);
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
        public static void WaveletDecomposePlanar3(float[] Y, float[] U, float[] V, int srcWidth, int srcHeight)
        {
            int rounds = (int)Math.Log(srcWidth, 2) - 1;
            Console.WriteLine($"Decomposing with {rounds} rounds");

            for (int ch = 0; ch < 3; ch++)
            {
                var buffer = Pick(ch, Y, U, V);

                // DC to AC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] -= 127.5f; }

                // Transform
                PlanarDecompose(srcWidth, srcHeight, buffer, rounds);


                // Test of quantisation:
               // var quality = (ch + 1) * 2; // bias quality by color channel. Assumes 2=Y
                //buffer = QuantiseByIndependentRound(si, buffer, ch, rounds, quality);
                //buffer = QuantiseByEnergyBalance(si, buffer, ch, rounds, quality);

                WriteToFileFibonacci(buffer, ch, buffer.Length, "planar");

                
                ReadFromFileFibonacci(buffer, ch, "planar");

                // Restore
                PlanarRestore(srcWidth, srcHeight, buffer, rounds);

                // AC to DC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5f; }
            }
        }

        /// <summary>
        /// This version is between Morton (1 set of Coeffs per round) and Planar (3 sets of Coeffs per round)
        /// </summary>
        /// <param name="Y">Luminence plane</param>
        /// <param name="U">color plane</param>
        /// <param name="V">color plane</param>
        /// <param name="planeWidth">Width of the YUV planes, in samples. This must be a power-of-two</param>
        /// <param name="planeHeight">Height of the YUV planes, in samples. This must be a power-of-two</param>
        /// <param name="imgWidth">Width of the image region of interest. This must be less-or-equal to the plane width. Does not need to be a power of two</param>
        /// <param name="imgHeight">Height of the image region of interest. This must be less-or-equal to the plane height. Does not need to be a power of two</param>
        public static void WaveletDecomposePlanar2(float[] Y, float[] U, float[] V, int planeWidth, int planeHeight, int imgWidth, int imgHeight)
        {
            // Current best: 265kb (using input 3.png)

            int rounds = (int)Math.Log(planeWidth, 2);
            Console.WriteLine($"Decomposing with {rounds} rounds");

            var p2Height = (int)Bin.NextPow2((uint) planeHeight);
            var p2Width = (int)Bin.NextPow2((uint) planeWidth);
            var hx = new float[p2Height];
            var wx = new float[p2Width];

            for (int ch = 0; ch < 3; ch++)
            {
                var buffer = Pick(ch, Y, U, V);

                // DC to AC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] -= 127.5f; }

                // Transform
                for (int i = 0; i < rounds; i++)
                {
                    var height = p2Height >> i;
                    var width = p2Width >> i;

                    // Wavelet decompose vertical
                    for (int x = 0; x < width; x++) // each column
                    {
                        CDF.Fwt97(buffer, hx, height, x, planeWidth);
                    }
                    
                    // Wavelet decompose HALF horizontal
                    for (int y = 0; y < height / 2; y++) // each row
                    {
                        CDF.Fwt97(buffer, wx, width, y * planeWidth, 1);
                    }
                }

                // Unquantised: native: 708kb; Ordered:  705kb
                // Reorder, Quantise and reduce co-efficients
                var packedLength = ToStorageOrder2D(buffer, planeWidth, planeHeight, rounds, imgWidth, imgHeight);
                QuantisePlanar2(buffer, ch, packedLength, QuantiseType.Reduce);

                // Write output
                WriteToFileFibonacci(buffer, ch, packedLength, "p_2");

                // Prove reading is good:
                for (int i = 0; i < buffer.Length; i++) { buffer[i] = 0.0f; }

                // read output
                ReadFromFileFibonacci(buffer, ch, "p_2");

                // Re-expand co-efficients
                QuantisePlanar2(buffer, ch, packedLength, QuantiseType.Expand);
                FromStorageOrder2D(buffer, planeWidth, planeHeight, rounds, imgWidth, imgHeight);

                // Restore
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var height = p2Height >> i;
                    var width = p2Width >> i;

                    // Wavelet restore HALF horizontal
                    for (int y = 0; y < height / 2; y++) // each row
                    {
                        CDF.Iwt97(buffer, wx, width,  y * planeWidth, 1);
                    }

                    // Wavelet restore vertical
                    for (int x = 0; x < width; x++) // each column
                    {
                        CDF.Iwt97(buffer, hx, height, x, planeWidth);
                    }
                }

                // AC to DC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5f; }
            }
        }

        private static T Pick<T>(int i, params T[] opts) { return opts[i]; }

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
                    CDF.Fwt97(buffer, work, length, 0, 1);
                }

                WriteToFileFibonacci(buffer, ch, buffer.Length, "morton");
                
                // read output
                ReadFromFileFibonacci(buffer, ch, "morton");

                // Restore
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var length = bufferSize >> i;
                    var work = new float[length];
                    CDF.Iwt97(buffer, work, length, 0, 1);
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

        public static void FromStorageOrder2D(float[] buffer, int srcWidth, int srcHeight, int rounds, int imgWidth, int imgHeight, int scale = 0)
        {
            var storage = new float[buffer.Length];

            // Do like the CDF reductions, but put all depths together before the next scale.
            int incrPos = 0;

            // first, any unreduced value
            var height = srcWidth >> rounds;
            var width = srcWidth >> rounds;

            for (int y = 0; y < height; y++)
            {
                var yo = y * srcWidth;
                for (int x = 0; x < width; x++)
                {
                    storage[yo + x] = buffer[incrPos++];
                }
            }

            var lowerDiff = (srcHeight - imgHeight) / 2;
            var eastDiff = (srcWidth - imgWidth) / 2;

            // prevent over-reading on non-power-two images:
            // this knocks-out the last two co-efficient blocks
            var limit = (imgHeight / 2) * (imgWidth / 2);
            if (scale < 1) limit = buffer.Length;


            // now the reduced coefficients in order from most to least significant
            for (int i = rounds - 1; i >= 0; i--)
            {
                height = srcHeight >> i;
                width = srcWidth >> i;
                var left = width >> 1;
                var top = height >> 1;
                var right = width - (eastDiff >> i);
                var lowerEnd = height - (lowerDiff >> i);
                var eastEnd = top - (lowerDiff >> i);

                if (incrPos > limit) { break; }

                // vertical block
                // from top to the height of the horz block,
                // from left=(right most of prev) to right
                for (int x = left; x < right; x++) // each column
                {
                    for (int y = 0; y < eastEnd ;y++)
                    {
                        var yo = y * srcWidth;
                        storage[yo + x] = buffer[incrPos++];
                    }
                }

                // horizontal block
                for (int y = top; y < lowerEnd; y++) // each row
                {
                    var yo = y * srcWidth;
                    for (int x = 0; x < right; x++)
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

        /// <summary>
        /// Returns total number of samples used (packed into lower range)
        /// </summary>
        public static int ToStorageOrder2D(float[] buffer, int srcWidth, int srcHeight, int rounds, int imgWidth, int imgHeight)
        {
            var storage = new float[buffer.Length];
 
            // midpoint(top) to lower;
            // lower is (bottom -  (srcHeight-imgHeight)/2)

            // Do like the CDF reductions, but put all depths together before the next scale.
            int incrPos = 0;

            // first, any unreduced value
            var height = srcWidth >> rounds;
            var width = srcWidth >> rounds;

            for (int y = 0; y < height; y++)
            {
                var yo = y * srcWidth;
                for (int x = 0; x < width; x++)
                {
                    storage[incrPos++] = buffer[yo + x];
                }
            }

            var lowerDiff = (srcHeight - imgHeight) / 2;
            var eastDiff = (srcWidth - imgWidth) / 2;

            // now the reduced coefficients in order from most to least significant
            for (int i = rounds - 1; i >= 0; i--)
            {
                height = srcHeight >> i;
                width = srcWidth >> i;
                var left = width >> 1;
                var top = height >> 1;
                var right = width - (eastDiff >> i);
                var lowerEnd = height - (lowerDiff >> i);
                var eastEnd = top - (lowerDiff >> i);

                // vertical block
                // from top to the height of the horz block,
                // from left=(right most of prev) to right
                for (int x = left; x < right; x++) // each column
                {
                    for (int y = 0; y < eastEnd ;y++)
                    {
                        var yo = y * srcWidth;
                        storage[incrPos++] = buffer[yo + x];
                    }
                }

                // horizontal block
                for (int y = top; y < lowerEnd; y++) // each row
                {
                    var yo = y * srcWidth;
                    for (int x = 0; x < right; x++)
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
            return incrPos;
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

        private static void QuantisePlanar2(float[] buffer, int ch, int packedLength, QuantiseType mode)
        {
            if (packedLength < buffer.Length) packedLength = buffer.Length;
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
            //fYs = new double[]{ 180, 150, 100, 40, 8, 5, 3.5, 1.5 };
            //fCs = new double[]{1000, 200, 200, 50, 20, 10, 4};

            // about the same as 100% JPEG
            //fYs = new double[]{ 1, 1, 1, 1, 1, 1, 1, 1, 1};
            //fCs = new double[]{10000, 2, 1, 1, 1, 1, 1, 1, 1};
                        
            var rounds = (int)Math.Log(packedLength, 2);
            for (int r = 0; r < rounds; r++)
            {
                var factors = (ch == 0) ? fYs : fCs;
                float factor = (float)((r >= factors.Length) ? factors[factors.Length - 1] : factors[r]);
                if (mode == QuantiseType.Reduce) factor = 1 / factor;

                var len = packedLength >> r;
                
                // handle scale reductions:
                if (len >> 1 >= buffer.Length) continue;

                // expand co-efficients
                if (len >= buffer.Length) len = buffer.Length - 1;
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
                pixelBuf2[i] = ColorSpace.Ycbcr32_To_RGB32(pixelBuf2[i]);
                //pixelBuf2[i] = ColorSpace.Ycocg32_To_RGB32(pixelBuf2[i]);
                // TODO: Chroma from luma estimation?
            }
        }

        private static unsafe int To_YCxCx_ColorSpace(byte* s, BitmapData si)
        {
            var pixelBuf = (uint*) (s);
            var bufferSize = si.Width * si.Height;
            for (int i = 0; i < bufferSize; i++)
            {
                pixelBuf[i] = ColorSpace.RGB32_To_Ycbcr32(pixelBuf[i]);
                //pixelBuf[i] = ColorSpace.RGB32_To_Ycocg32(pixelBuf[i]);
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

        private static void PlanarDecompose(int srcWidth, int srcHeight, float[] buffer, int rounds)
        {
            for (int i = 0; i < rounds; i++)
            {
                var height = srcHeight >> i;
                var width = srcWidth >> i;

                var hx = new float[height];
                var wx = new float[width];

                // Wavelet decompose vertical
                for (int x = 0; x < width; x++) // each column
                {
                    CDF.Fwt97(buffer, hx, height, x, srcWidth);
                }

                // Wavelet decompose horizontal
                for (int y = 0; y < height; y++) // each row
                {
                    CDF.Fwt97(buffer, wx, width, y * srcWidth, 1);
                }
            }
        }

        private static void PlanarRestore(int srcWidth, int srcHeight,  float[] buffer, int rounds)
        {
            for (int i = rounds - 1; i >= 0; i--)
            {
                var height = srcHeight >> i;
                var width = srcWidth >> i;

                var hx = new float[height];
                var wx = new float[width];

                // Wavelet restore horizontal
                for (int y = 0; y < height; y++) // each row
                {
                    CDF.Iwt97(buffer, wx, width, y * srcWidth, 1);
                }

                // Wavelet restore vertical
                for (int x = 0; x < width; x++) // each column
                {
                    CDF.Iwt97(buffer, hx, height, x, srcWidth);
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

        private static void WriteToFileFibonacci(float[] buffer, int ch, int packedLength, string name)
        {
            var testpath = @"C:\gits\ImageTools\src\ImageTools.Tests\bin\Debug\outputs\"+name+"_fib_test_"+ch+".dat";
            if (File.Exists(testpath)) File.Delete(testpath);

            if (false)
            {
                // LZMA (better compression, but a lot slower)
                using (var ms = new MemoryStream())
                {
                    DataEncoding.FibonacciEncode(buffer, packedLength, ms);
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
                    DataEncoding.FibonacciEncode(buffer, packedLength, ms);
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
                    CDF.Fwt97(buffer, work, work.Length, y * si.Width, 1);
                }
                
                // Wavelet restore (half image)
                for (int y = 0; y < si.Height / 2; y++) // each row
                {
                    CDF.Iwt97(buffer, work, work.Length, y * si.Width, 1);
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
                    CDF.Fwt97(buffer, work, work.Length, x, si.Width);
                }

                
                // Wavelet restore (half image)
                for (int x = 0; x < si.Width / 2; x++) // each column
                {
                    CDF.Iwt97(buffer, work, work.Length, x, si.Width);
                }

                // AC to DC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5f; }

                // Write back to image
                WritePlane(buffer, d, di, ch);
            }
        }

    }
}