using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using ImageTools.DataCompression.Experimental;
using ImageTools.ImageDataFormats;
using ImageTools.Utilities;
using ImageTools.WaveletTransforms;

namespace ImageTools.ImageStorageFileFormats
{
    // TODO: this lot needs major cleanup
    
    public static class WaveletImageFormat
    {
        public static void SaveWaveletImageFormat(this Bitmap bmp, string outputPath)
        {
            var fileData = WaveletCompress.Compress(bmp);
            using var fileStream = File.OpenWrite(outputPath!);
            fileData!.WriteToStream(fileStream);
        }

        public static bool IsWaveletFile(string path)
        {
            using var fileStream = File.OpenRead(path!);
            return VersionedInterleavedFile.IsValidFile(fileStream);
        }

        public static Bitmap LoadFile(string inputPath)
        {
            using var fileStream = File.OpenRead(inputPath!);
            var fileData = VersionedInterleavedFile.ReadFromVersionedStream(fileStream);
            WaveletCompress.Decompress(fileData, 1, out var y, out var u, out var v, out var width);
            
            var dst = new Bitmap(fileData!.Width, fileData.Height);
            BitmapTools.PlanesToImage_Slice(dst,ColorSpace.YiqToRGB, 0, width, y,u,v);
            return dst;
        }
    }

    /// <summary>
    /// A low-loss image compression format.
    /// It gives nearly the same quality as PNG at lower sizes.
    /// This also allows us to load scaled-down versions of images at reduced processing / loading costs.
    /// </summary>
    public class WaveletCompress
    {
        // This set of coefficients will be used for new and edited tiles
        // TODO: have a range (low, medium, high)
        public static readonly double[] HighQualityYQuants = {2, 1};
        public static readonly double[] HighQualityCQuants = {4, 2, 1};

        public static readonly double[] StandardYQuants = {12, 9, 4, 2.3, 1.5};
        public static readonly double[] StandardCQuants = {15, 10, 2};
        
        public static VersionedInterleavedFile Compress(Bitmap bmp)
        {
            if (bmp == null) throw new Exception("Invalid image");
            var imgWidth = bmp.Width;
            var imgHeight = bmp.Height;
            BitmapTools.ImageToPlanes_ForcePower2(bmp, ColorSpace.RGBToYiq, out var Y, out var U, out var V,  out var width, out var height);

            return WaveletDecomposePlanar2(Y, U, V, width, height, imgWidth, imgHeight);
        }

        public static void Decompress(VersionedInterleavedFile file, byte scale, out float[] Y, out float[] U, out float[] V, out int width)
        {
            width = WaveletRestorePlanar2(file, scale, out Y, out U, out V);
        }

        // This controls the overall size and quality of the output
        private static void QuantisePlanar2(float[] buffer, int ch, int packedLength, QuantiseType mode, double[] fYs, double[] fCs)
        {
            if (packedLength < buffer.Length) packedLength = buffer.Length;
            // Planar two splits in half, starting with top/bottom, and alternating between
            // vertical and horizontal

            // Fibonacci coding strongly prefers small numbers
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


        /// <summary>
        /// Compress an image to a byte stream
        /// </summary>
        /// <param name="Y">Luminance plane</param>
        /// <param name="U">color plane</param>
        /// <param name="V">color plane</param>
        /// <param name="planeWidth">Width of the YUV planes, in samples. This must be a power-of-two</param>
        /// <param name="planeHeight">Height of the YUV planes, in samples. This must be a power-of-two</param>
        /// <param name="imgWidth">Width of the image region of interest. This must be less-or-equal to the plane width. Does not need to be a power of two</param>
        /// <param name="imgHeight">Height of the image region of interest. This must be less-or-equal to the plane height. Does not need to be a power of two</param>
        
        private static VersionedInterleavedFile WaveletDecomposePlanar2(float[] Y, float[] U, float[] V, int planeWidth, int planeHeight, int imgWidth, int imgHeight)
        {
            int rounds = (int)Math.Log(planeWidth, 2);

            var p2Height = (int)NextPow2((uint)planeHeight);
            var p2Width = (int)NextPow2((uint)planeWidth);
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
                QuantisePlanar2(buffer, ch, packedLength, QuantiseType.Reduce, StandardYQuants, StandardCQuants);

                // Write output
                using (var tmp = new MemoryStream(buffer.Length))
                {   // byte-by-byte writing to DeflateStream is *very* slow, so we buffer
                    DataEncoding.FibonacciEncode(buffer, 0, tmp);
                    tmp.Seek(0, SeekOrigin.Begin);
                    var coder = new TruncatableEncoder();
                    coder.CompressStream(tmp, ms);
                }
            }

            // interleave the files:
            msY.Seek(0, SeekOrigin.Begin);
            msU.Seek(0, SeekOrigin.Begin);
            msV.Seek(0, SeekOrigin.Begin);
            var container = new VersionedInterleavedFile((ushort)imgWidth, (ushort)imgHeight, 1, StandardYQuants, StandardCQuants,
                msY.ToArray(), msU.ToArray(), msV.ToArray());

            msY.Dispose();
            msU.Dispose();
            msV.Dispose();
            return container;
        }

        public static int WaveletRestorePlanar2(VersionedInterleavedFile container, byte scale, out float[] Y, out float[] U, out float[] V)
        {
            var Ybytes = container.Planes[0];
            var Ubytes = container.Planes[1];
            var Vbytes = container.Planes[2];

            var yQuants = container.QuantiserSettings_Y ?? StandardYQuants;
            var cQuants = container.QuantiserSettings_C ?? StandardCQuants;

            if (Ybytes == null || Ubytes == null || Vbytes == null) {
                Y = new float[0];
                U = new float[0];
                V = new float[0];
                return 0;//throw new NullReferenceException("Planes were not read from image correctly");
            }

            int imgWidth = container.Width;
            int imgHeight = container.Height;

            // the original image source's internal buffer size
            var packedLength = NextPow2(imgWidth) * NextPow2(imgHeight);

            // scale by a power of 2
            if (scale < 1) scale = 1;
            var scaleShift = scale - 1;
            if (scale > 1)
            {
                imgWidth >>= scaleShift;
                imgHeight >>= scaleShift;
            }
            var planeWidth = NextPow2(imgWidth);
            var planeHeight = NextPow2(imgHeight);

            var sampleCount = planeHeight * planeWidth;

            Y = new float[sampleCount];
            U = new float[sampleCount];
            V = new float[sampleCount];

            var hx = new float[planeHeight];
            var wx = new float[planeWidth];

            int rounds = (int)Math.Log(planeWidth, 2);

            for (int ch = 0; ch < 3; ch++)
            {

                var buffer = Pick(ch, Y, U, V);
                using (var storedData = new MemoryStream(Pick(ch, Ybytes, Ubytes, Vbytes)))
                {
                    using (var tmp = new MemoryStream())
                    {
                        var coder = new TruncatableEncoder();
                        coder.DecompressStream(storedData, tmp);
                        tmp.Seek(0, SeekOrigin.Begin);

                        DataEncoding.FibonacciDecode(tmp, buffer);
                    }
                }

                // Re-expand co-efficients
                QuantisePlanar2(buffer, ch, packedLength, QuantiseType.Expand, yQuants, cQuants);
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
            return planeWidth;
        }

        /// <summary>
        /// Return the smallest number that is a power-of-two
        /// greater than or equal to the input
        /// </summary>
        public static uint NextPow2(uint c)
        {
            c--;
            c |= c >> 1;
            c |= c >> 2;
            c |= c >> 4;
            c |= c >> 8;
            c |= c >> 16;
            return ++c;
        }

        private static int NextPow2(int c) => (int)NextPow2((uint)c);

        private static T Pick<T>(int i,  params T[] opts) => opts[i];

        /// <summary>
        /// Restore image byte order from storage format to image format
        /// </summary>
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
        /// Pack the image coefficients into an order that is good for progressive loading and compression
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
                    for (int y = 0; y < eastEnd; y++)
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
    }
}