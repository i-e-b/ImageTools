using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

// ReSharper disable SuggestBaseTypeForParameter

namespace ImageTools
{
    public class ColorCellEncoding
    {
        /// <summary>
        /// Encode an image as a byte array.
        /// The output is always in blocks of 48 bits (6 bytes).
        /// Has a fixed compression ratio of 3bpp output.
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public static byte[] EncodeImage2D(Bitmap src)
        {
            if (src == null) return null;
            BitmapTools.ImageToPlanesf(src, ColorSpace.RGBToYCoCg, out var Y, out var U, out var V);
            int width = src.Width;
            int height = src.Height;

            // TODO: boundary condition for non-multiple-4 images
            // TODO: Better color reproduction

            var block = new TripleFloat[16];

            // Plan - 16 bit color for high and low - 844 YUV
            //        16 bit 4x4 bitmap between high and low

            var outp = new MemoryStream();
            var w = new BinaryWriter(outp);

            // write width & height
            w.Write((ushort)width);
            w.Write((ushort)height);

            for (int by = 0; by < height; by += 4) // block y axis
            {
                for (int bx = 0; bx < width; bx += 4) // block x axis
                {
                    // pick 4x4 block into an array
                    PickBlock(by, width, bx, block, Y, U, V);
                    // calculate the upper and lower colors, and the bit pattern
                    AveBlock(block, out var upper, out var lower, out var bits);

                    // encode colors
                    var encUpper = YUV_to_YUV844(upper);
                    var encLower = YUV_to_YUV844(lower);

                    // write data
                    w.Write((ushort)encUpper);
                    w.Write((ushort)encLower);
                    w.Write((ushort)bits);
                }
            }

            outp.Seek(0, SeekOrigin.Begin);
            return outp.ToArray();
        }


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

            
            for (int by = 0; by < height; by += 4) // block y axis
            {
                for (int bx = 0; bx < width; bx += 4) // block x axis
                {
                    var encUpper = r.ReadUInt16();
                    var encLower = r.ReadUInt16();
                    var bits = r.ReadUInt16();

                    var upper = YUV844_to_YUV(encUpper);
                    var lower = YUV844_to_YUV(encLower);

                    // write pixel colors
                    for (int y = 0; y < 4; y++)
                    {
                        var yo = (by + y) * width;
                        for (int x = 0; x < 4; x++)
                        {
                            var xo = x + bx;

                            if ((bits & 1) > 0) {
                                Y[yo + xo] = upper.Y;
                                U[yo + xo] = upper.U;
                                V[yo + xo] = upper.V;
                            } else {
                                Y[yo + xo] = lower.Y;
                                U[yo + xo] = lower.U;
                                V[yo + xo] = lower.V;
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

        private static TripleFloat YUV844_to_YUV(ushort encoded)
        {
            return new TripleFloat{
                Y = encoded >> 8,
                U = encoded & 0xf0,
                V = (encoded & 0x0f) << 4
            };
        }
        
        private static int YUV_to_YUV844(TripleFloat upper)
        {
            var y = (ColorSpace.clip(upper.Y) << 8) & 0xff00;
            var u =  ColorSpace.clip(upper.U)       & 0x00f0;
            var v = (ColorSpace.clip(upper.V) >> 4) & 0x000f;
            return y | u | v;
        }

        private static void AveBlock(TripleFloat[] block, out TripleFloat upper, out TripleFloat lower, out int bits)
        {
            // calculate average brightness
            float aveY = 0.0f;
            for (int i = 0; i < 16; i++) { aveY += block[i].Y; }
            aveY /= 16;

            // set outputs to starting condition
            upper.Y = 0;
            upper.U = 0;
            upper.V = 0;

            lower.Y = 0;
            lower.U = 0;
            lower.V = 0;

            bits = 0;

            int countUpper = 0;
            int countLower = 0;

            // Separate colors either side of the average
            for (int i = 0; i < 16; i++)
            {
                var sample = block[i];
                if (sample.Y >= aveY) {
                    countUpper++;
                    upper.Y += sample.Y;
                    upper.U += sample.U;
                    upper.V += sample.V;
                    bits |= 1 << i;
                } else {
                    countLower++;
                    lower.Y += sample.Y;
                    lower.U += sample.U;
                    lower.V += sample.V;
                }
            }

            if (countLower > 0) {
                lower.Y /= countLower;
                lower.U /= countLower;
                lower.V /= countLower;
            }

            if (countUpper > 0) {
                upper.Y /= countUpper;
                upper.U /= countUpper;
                upper.V /= countUpper;
            }
        }

        private static void PickBlock(int by, int width, int bx, TripleFloat[] block, float[] Y, float[] U, float[] V)
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
    }

    public struct TripleFloat
    {
        public float Y, U, V;
    }
}
