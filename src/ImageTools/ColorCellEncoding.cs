using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ImageTools.ImageDataFormats;
using ImageTools.Utilities;

// ReSharper disable SuggestBaseTypeForParameter

namespace ImageTools
{
    /// <summary>
    /// Color cell compression is simple, and gives a very reliable output.
    /// It's best suited to noisy natural images. It performs poorly with
    /// subtle gradients and antialiased synthetic images.
    /// </summary>
    /// <remarks>
    /// This compression type is very simple. It requires minimal working memory
    /// and needs very little of the image to be held in memory during encoding.
    /// Typically only 4 lines of input need to be held at once.
    /// This makes it a good choice for highly constrained embedded systems.
    /// The poor encoding efficiency and image quality makes it unsuitable for
    /// large scale general imagery.
    /// </remarks>
    public class ColorCellEncoding
    {
        /// <summary>
        /// Encode an image as a byte array.
        /// The output is always in blocks of 48 bits (6 bytes).
        /// Has a fixed compression ratio of 3bpp output.
        /// </summary>
        public static byte[] EncodeImage2D(Bitmap src)
        {
            if (src == null) return null;
            BitmapTools.ImageToPlanesf(src, ColorSpace.RGBToExp, out var Y, out var U, out var V);
            int width = src.Width;
            int height = src.Height;

            var block = new TripleFloat[16];

            // Plan - 24 bit color for high and low - 888 YUV
            //        There is only one color per 4x4 block, U is encoded with high Y, V with low Y
            //        16 bit 4x4 bitmap between high and low
            //        This results in 6 bytes per 16 pixel block (3bpp)

            var outp = new MemoryStream();
            var w = new BinaryWriter(outp);

            // write width & height
            w.Write((ushort)width);
            w.Write((ushort)height);

            var blockH = height - (height % 4);
            var blockW = width - (width % 4);

            for (int by = 0; by < blockH; by += 4) // block y axis
            {
                for (int bx = 0; bx < blockW; bx += 4) // block x axis
                {
                    // pick 4x4 block into an array
                    PickBlock16(by, width, bx, block, Y, U, V);
                    // calculate the upper and lower colors, and the bit pattern
                    AveBlock16(block, out var upperY, out var lowerY, out var aveU, out var aveV, out var bits);

                    // encode colors
                    // we share the color across the two (better color repo, less spatial)
                    var encUpper = YUV_to_YC88(upperY, aveU);
                    var encLower = YUV_to_YC88(lowerY, aveV);

                    // write data
                    w.Write((ushort)encUpper);
                    w.Write((ushort)encLower);
                    w.Write((ushort)bits);
                }
            }

            outp.Seek(0, SeekOrigin.Begin);
            return outp.ToArray();
        }

        /// <summary>
        /// Restore a bitmap from a byte array created by `EncodeImage2D`
        /// </summary>
        public static Bitmap DecodeImage2D(byte[] encoded)
        {
            var ms = new MemoryStream(encoded);
            var r = new BinaryReader(ms);

            var width = r.ReadUInt16();
            var height = r.ReadUInt16();
            var sampleCount = width * height;

            var Y = new float[sampleCount];
            var U = new float[sampleCount];
            var V = new float[sampleCount];

            var blockH = height - (height % 4);
            var blockW = width - (width % 4);

            for (int by = 0; by < blockH; by += 4) // block y axis
            {
                for (int bx = 0; bx < blockW; bx += 4) // block x axis
                {
                    var encUpper = r.ReadUInt16();
                    var encLower = r.ReadUInt16();
                    var bits = r.ReadUInt16();

                    YC88_to_YC(encUpper, out var upper, out var aveU);
                    YC88_to_YC(encLower, out var lower, out var aveV);

                    // write pixel colors
                    for (int y = 0; y < 4; y++)
                    {
                        var yo = (by + y) * width;
                        for (int x = 0; x < 4; x++)
                        {
                            var xo = x + bx;

                            if ((bits & 1) > 0) {
                                Y[yo + xo] = upper;
                                U[yo + xo] = aveU;
                                V[yo + xo] = aveV;
                            } else {
                                Y[yo + xo] = lower;
                                U[yo + xo] = aveU;
                                V[yo + xo] = aveV;
                            }
                            bits >>= 1;
                        }
                    }
                }
            }

            var dst = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapTools.PlanesToImage_f(dst, ColorSpace.ExpToRGB, 0, Y, U, V);
            return dst;
        }


        /// <summary>
        /// Encode an image as a byte array.
        /// The output is far more constrained than `EncodeImage2D`,
        /// resulting in smaller file sizes, but worse image quality
        /// </summary>
        public static byte[] EncodeImage2D_Tight(Bitmap src) {
            if (src == null) return null;
            BitmapTools.ImageToPlanesf(src, ColorSpace.RGBToYCoCg, out var Y, out var U, out var V);
            int width = src.Width;
            int height = src.Height;

            var block = new TripleFloat[64];

            // Plan - 16 bit color for "low" - 844 YUV
            //        8-bit Y value for (high-low)
            //        64 bit 8x8 bitmap between high and low
            //        11 bytes per 8x8 block (1.375 bpp)

            var outp = new MemoryStream();
            var w = new BinaryWriter(outp);

            // write width & height
            w.Write((ushort)width);
            w.Write((ushort)height);

            var blockH = height - (height % 8);
            var blockW = width - (width % 8);

            for (int by = 0; by < blockH; by += 8) // block y axis
            {
                for (int bx = 0; bx < blockW; bx += 8) // block x axis
                {
                    // pick 4x4 block into an array
                    PickBlock64(by, width, bx, block, Y, U, V);
                    // calculate the upper and lower colors, and the bit pattern
                    AveBlock64(block, out var upperY, out var lowerY, out var aveU, out var aveV, out var bits);

                    // encode color and brightness difference
                    var encLower = YUV_to_YC655(lowerY, aveU, aveV);
                    var encDiff = ColorSpace.clip(upperY - lowerY);

                    // write data
                    w.Write((ushort)encLower);
                    w.Write((byte)encDiff);
                    w.Write((ulong)bits);
                }
            }

            outp.Seek(0, SeekOrigin.Begin);
            return outp.ToArray();
        }

        
        /// <summary>
        /// Restore a bitmap from a byte array created by `EncodeImage2D`
        /// </summary>
        public static Bitmap DecodeImage2D_Tight(byte[] encoded)
        {
            var ms = new MemoryStream(encoded);
            var r = new BinaryReader(ms);

            var width = r.ReadUInt16();
            var height = r.ReadUInt16();
            var sampleCount = width * height;

            var Y = new float[sampleCount];
            var U = new float[sampleCount];
            var V = new float[sampleCount];

            var blockH = height - (height % 8);
            var blockW = width - (width % 8);

            for (int by = 0; by < blockH; by += 8) // block y axis
            {
                for (int bx = 0; bx < blockW; bx += 8) // block x axis
                {
                    var encLower = r.ReadUInt16();
                    var encDiff = r.ReadByte();
                    var bits = r.ReadUInt64();

                    YC655_to_YUV(encLower, out var lower, out var aveU, out var aveV);
                    var upper = lower + encDiff;

                    // write pixel colors
                    for (int y = 0; y < 8; y++)
                    {
                        var yo = (by + y) * width;
                        for (int x = 0; x < 8; x++)
                        {
                            var xo = x + bx;

                            if ((bits & 1) > 0) {
                                Y[yo + xo] = upper;
                                U[yo + xo] = aveU;
                                V[yo + xo] = aveV;
                            } else {
                                Y[yo + xo] = lower;
                                U[yo + xo] = aveU;
                                V[yo + xo] = aveV;
                            }
                            bits >>= 1;
                        }
                    }
                }
            }

            var dst = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapTools.PlanesToImage_f(dst, ColorSpace.YCoCgToRGB, 0, Y, U, V);
            return dst;
        }

        private static void YC88_to_YC(ushort encoded, out float Y, out float C)
        {
            Y = encoded >> 8;
            C = encoded & 0xff;
        }

        private static int YUV_to_YC88(float Y, float C)
        {
            var y = (ColorSpace.clip(Y) << 8) & 0xff00;
            var c =  ColorSpace.clip(C)       & 0x00ff;
            return y | c;
        }

        private static int YUV_to_YC655(float Y, float U, float V)
        {
            var y = (ColorSpace.clip(Y) & 0xfc) << 8;
            var u = (ColorSpace.clip(U) & 0xF8) << 2;
            var v = (ColorSpace.clip(V) & 0xF8) >> 3;

            return y | u | v;
        }
        private static void YC655_to_YUV(ushort encoded, out float Y, out float U, out float V)
        {
            Y = (encoded >> 8) & 0xfc;
            U = (encoded >> 2) & 0xF8;
            V = (encoded << 3) & 0xF8;
        }


        private static void AveBlock16(TripleFloat[] block, out float upperY, out float lowerY, out float U, out float V, out int bits)
        {
            // set outputs to starting condition
            upperY = 0;
            lowerY = 0;
            bits = 0;
            U = 0;
            V = 0;

            // calculate average brightness
            float aveY = 0.0f;
            for (int i = 0; i < 16; i++) { 
                aveY += block[i].Y;
                U += block[i].U;
                V += block[i].V;
            }
            aveY /= 16;
            U /= 16;
            V /= 16;

            int countUpper = 0;
            int countLower = 0;

            // Separate colors either side of the average
            for (int i = 0; i < 16; i++)
            {
                var sample = block[i];
                if (sample.Y >= aveY) {
                    countUpper++;
                    upperY += sample.Y;
                    bits |= 1 << i;
                } else {
                    countLower++;
                    lowerY += sample.Y;
                }
            }

            if (countLower > 0) {
                lowerY /= countLower;
            }

            if (countUpper > 0) {
                upperY /= countUpper;
            }
        }

        
        private static void AveBlock64(TripleFloat[] block, out float upperY, out float lowerY, out float U, out float V, out UInt64 bits)
        {
            // set outputs to starting condition
            upperY = 0;
            lowerY = 0;
            bits = 0;
            U = 0;
            V = 0;

            // calculate average brightness
            float aveY = 0.0f;
            for (int i = 0; i < 64; i++) { 
                aveY += block[i].Y;
                U += block[i].U;
                V += block[i].V;
            }
            aveY /= 64;
            U /= 64;
            V /= 64;

            int countUpper = 0;
            int countLower = 0;

            // Separate colors either side of the average
            for (int i = 0; i < 64; i++)
            {
                var sample = block[i];
                if (sample.Y >= aveY) {
                    countUpper++;
                    upperY += sample.Y;
                    bits |= 1UL << i;
                } else {
                    countLower++;
                    lowerY += sample.Y;
                }
            }

            if (countLower > 0) {
                lowerY /= countLower;
            }

            if (countUpper > 0) {
                upperY /= countUpper;
            }
        }

        private static void PickBlock16(int by, int width, int bx, TripleFloat[] block, float[] Y, float[] U, float[] V)
        {
            for (int y = 0; y < 4; y++)
            {
                var yo = (by + y) * width;
                for (int x = 0; x < 4; x++)
                {
                    var xo = x + bx;

                    block[x + (y * 4)] = new TripleFloat {Y = Y[yo + xo], U = U[yo + xo], V = V[yo + xo]};
                }
            }
        }
        
        private static void PickBlock64(int by, int width, int bx, TripleFloat[] block, float[] Y, float[] U, float[] V)
        {
            for (int y = 0; y < 8; y++)
            {
                var yo = (by + y) * width;
                for (int x = 0; x < 8; x++)
                {
                    var xo = x + bx;

                    block[x + (y * 8)] = new TripleFloat {Y = Y[yo + xo], U = U[yo + xo], V = V[yo + xo]};
                }
            }
        }
    }

    public struct TripleFloat
    {
        public float Y, U, V;
    }
}
