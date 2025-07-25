﻿using System.Drawing;
using System.Drawing.Imaging;
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
        /// <para></para>
        /// This version uses integer calculations internally
        /// </summary>
        public static byte[] EncodeImage2D_int(Bitmap src)
        {
            if (src == null) return null;
            BitmapTools.ArgbImageToYUVPlanes_int(src, out var Y, out var U, out var V);
            int width = src.Width;
            int height = src.Height;

            var block_Y = new int[16];
            var block_U = new int[16];
            var block_V = new int[16];

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
                    PickBlock16(by, width, bx, block_Y, block_U, block_V, Y, U, V);
                    // calculate the upper and lower colors, and the bit pattern
                    AveBlock16(block_Y, block_U, block_V, out var upperY, out var lowerY, out var aveU, out var aveV, out var bits);

                    // encode colors
                    // we share the color across the two (better color repo, less spatial)
                    var encUpper = YUV_to_YC88(upperY, aveU);
                    var encLower = YUV_to_YC88(lowerY, aveV);

                    // write data
                    w.Write(encUpper);
                    w.Write(encLower);
                    w.Write(bits);
                }
            }

            outp.Seek(0, SeekOrigin.Begin);
            return outp.ToArray();
        }

        /// <summary>
        /// Restore a bitmap from a byte array created by `EncodeImage2D`
        /// <para></para>
        /// This version uses integer calculations internally
        /// </summary>
        public static Bitmap DecodeImage2D_int(byte[] encoded)
        {
            var ms = new MemoryStream(encoded);
            var r = new BinaryReader(ms);

            var width = r.ReadUInt16();
            var height = r.ReadUInt16();
            var sampleCount = width * height;

            var Y = new int[sampleCount];
            var U = new int[sampleCount];
            var V = new int[sampleCount];

            var blockH = height - (height % 4);
            var blockW = width - (width % 4);

            for (int by = 0; by < blockH; by += 4) // block y axis
            {
                for (int bx = 0; bx < blockW; bx += 4) // block x axis
                {
                    var encUpper = r.ReadUInt16();
                    var encLower = r.ReadUInt16();
                    var bits = r.ReadUInt16();

                    YC88_to_YC_int(encUpper, out var upper, out var aveU);
                    YC88_to_YC_int(encLower, out var lower, out var aveV);

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
            BitmapTools.YUVPlanes_To_ArgbImage_int(dst, 0,Y,U,V);
            return dst;
        }


        /// <summary>
        /// Encode an image as a byte array.
        /// The output is always in blocks of 48 bits (6 bytes).
        /// Has a fixed compression ratio of 3bpp output.
        /// <para></para>
        /// This version uses floating-point calculations internally
        /// </summary>
        public static byte[] EncodeImage2D(Bitmap src)
        {
            if (src == null) return null;
            BitmapTools.ImageToPlanesf(src, ColorSpace.RGBToYiq, out var Y, out var U, out var V);
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
            BitmapTools.PlanesToImage_f(dst, ColorSpace.YiqToRGB, 0, Y, U, V);
            return dst;
        }


        /// <summary>
        /// Encode an image as a byte array.
        /// The output is far more constrained than `EncodeImage2D`,
        /// resulting in smaller file sizes, but worse image quality
        /// </summary>
        public static byte[] EncodeImage2D_Tight(Bitmap src, bool dither) {
            if (src == null) return null;
            BitmapTools.ImageToPlanesf(src, ColorSpace.RGBToYiq, out var Y, out var U, out var V);
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
                    // pick 8x8 block into an array
                    PickBlock64(by, width, bx, block, Y, U, V);
                    // calculate the upper and lower colors, and the bit pattern
                    AveBlock64(block, dither, out var upperY, out var lowerY, out var aveU, out var aveV, out var bits);

                    // alter green/purple balance (to compensate for bit loss)
                    aveV = ColorSpace.clip(aveV + 12);


                    // encode color and brightness difference
                    var encLower = YUV_to_YC664(lowerY, aveU, aveV);
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

                    YC664_to_YUV(encLower, out var lower, out var aveU, out var aveV);
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
            BitmapTools.PlanesToImage_f(dst, ColorSpace.YiqToRGB, 0, Y, U, V);
            return dst;
        }

        private static void YC88_to_YC(ushort encoded, out float Y, out float C)
        {
            Y = encoded >> 8;
            C = encoded & 0xff;
        }

        private static void YC88_to_YC_int(ushort encoded, out int Y, out int C)
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
        
        private static ushort YUV_to_YC88(int Y, int C)
        {
            return (ushort)(((Y & 0xff) << 8) | (C & 0xff));
        }

        private static int YUV_to_YC664(float Y, float U, float V)
        {
            var y = (ColorSpace.clip(Y) & 0xfc) << 8;
            var u = (ColorSpace.clip(U) & 0xfc) << 2;
            var v = (ColorSpace.clip(V) & 0xf0) >> 4;

            return y | u | v;
        }
        private static void YC664_to_YUV(ushort encoded, out float Y, out float U, out float V)
        {
            Y = (encoded >> 8) & 0xfc;
            U = (encoded >> 2) & 0xF8;
            V = (encoded << 4) & 0xF0;
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

        
        private static void AveBlock16(int[] block_Y,int[] block_U,int[] block_V, out int upperY, out int lowerY, out int U, out int V, out ushort bits)
        {
            // set outputs to starting condition
            upperY = 0;
            lowerY = 0;
            bits = 0;
            U = 0;
            V = 0;

            // calculate average brightness
            var aveY = 0;
            for (int i = 0; i < 16; i++) { 
                aveY += block_Y[i];
                U += block_U[i];
                V += block_V[i];
            }
            aveY >>= 4;
            U >>= 4;
            V >>= 4;

            int countUpper = 0;
            int countLower = 0;

            // Separate colors either side of the average
            for (int i = 0; i < 16; i++)
            {
                var sample = block_Y[i];
                if (sample >= aveY) {
                    countUpper++;
                    upperY += sample;
                    bits |= (ushort)(1u << i);
                } else {
                    countLower++;
                    lowerY += sample;
                }
            }

            if (countLower > 0) {
                lowerY /= countLower;
            }

            if (countUpper > 0) {
                upperY /= countUpper;
            }
        }

        
        private static void AveBlock64(TripleFloat[] block, bool dither, out float upperY, out float lowerY, out float U, out float V, out ulong bits)
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

            // calculate the upper and lower
            for (int i = 0; i < 64; i++)
            {
                var sample = block[i];
                if (sample.Y >= aveY) {
                    countUpper++;
                    upperY += sample.Y;
                } else {
                    countLower++;
                    lowerY += sample.Y;
                }
            }

            if (countLower > 0) { lowerY /= countLower; }
            if (countUpper > 0) { upperY /= countUpper; }
            
            // Separate colors either side of the average
            float error = 0;
            for (int i = 0; i < 64; i++)
            {
                if ((i % 8) == 0) error = 0; // prevent error diffusion wrapping
                var sample = block[i];
                if (sample.Y >= aveY - error) {
                    if (dither) error += sample.Y - upperY;
                    bits |= 1UL << i;
                } else {
                    if (dither) error += sample.Y - lowerY;
                }

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
        
        private static void PickBlock16(int by, int width, int bx, int[] block_Y, int[] block_U, int[] block_V, int[] Y, int[] U, int[] V)
        {
            for (int y = 0; y < 4; y++)
            {
                var yo = (by + y) * width;
                for (int x = 0; x < 4; x++)
                {
                    var xo = x + bx;
                    block_Y[x + (y * 4)] = Y[yo + xo];
                    block_U[x + (y * 4)] = U[yo + xo];
                    block_V[x + (y * 4)] = V[yo + xo];
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
