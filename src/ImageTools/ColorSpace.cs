
using System.Linq;

namespace ImageTools
{
    /// <summary>
    /// RGB colour components
    /// </summary>
    public struct ColorRGB {
        public int R;
        public int G;
        public int B;

        public static ColorRGB FromARGB32(uint c) {
            ColorSpace.RGB32ToComponent(c, out var r,out var g, out var b);
            return new ColorRGB { R = r, G = g, B = b };
        }

    }
    
    /// <summary>
    /// YUV colour components (this is high-precision YUV, not Ycbcr)
    /// </summary>
    public struct ColorYUV {
        public double Y;
        public double U;
        public double V;

        public static ColorYUV FromYUV32(uint c)
        {
            ColorSpace.RGB32ToComponent(c, out var y, out var u, out var v);
            return new ColorYUV { Y = y, U = u, V = v };
        }
    }

    /// <summary>
    /// Tools for reading and converting color spaces
    /// </summary>
    public static class ColorSpace
    {
        /// <summary>
        /// Lossy conversion from RGB to Ycbcr (both 24 bit, stored as 32)
        /// This is approximately a (luma + blue/green + yellow/red) space
        /// </summary>
        public static uint RGB32_To_Ycbcr32(uint c)
        {
            var R = (c >> 16) & 0xff;
            var G = (c >>  8) & 0xff;
            var B = (c      ) & 0xff;
            var Y = clip(16 + (0.257 * R + 0.504 * G + 0.098 * B));
            var cb = clip(128 + (-0.148 * R + -0.291 * G + 0.439 * B));
            var cr = clip(128 + (0.439 * R + -0.368 * G + -0.071 * B));

            return (uint)((Y << 16) + (cb << 8) + (cr));
        }

        /// <summary>
        /// Lossy conversion from Ycbcr to RGB (both 24 bit, stored as 32)
        /// </summary>
        public static uint Ycbcr32_To_RGB32(uint c)
        {
            long Y =  (c >> 16) & 0xFF;
            long cb = (c >>  8) & 0xFF;
            long cr = (c      ) & 0xFF;
            
            var R = clip(1.164 * (Y - 16) + 0.0 * (cb - 128) + 1.596 * (cr - 128));
            var G = clip(1.164 * (Y - 16) + -0.392 * (cb - 128) + -0.813 * (cr - 128));
            var B = clip(1.164 * (Y - 16) + 2.017 * (cb - 128) + 0.0 * (cr - 128));

            return (uint)((R << 16) + (G << 8) + B);
        }
        
        /// <summary>
        /// Very lossy conversion to 16 bit Ycbcr
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static ushort RGB32_To_Ycbcr16(uint c)
        {
            var R = (c >> 16) & 0xff;
            var G = (c >>  8) & 0xff;
            var B = (c      ) & 0xff;
            var Y = clip(16 + (0.257 * R + 0.504 * G + 0.098 * B)) >> 2;     // 6 bps
            var cb = clip(128 + (-0.148 * R + -0.291 * G + 0.439 * B)) >> 3; // 5 bps
            var cr = clip(128 + (0.439 * R + -0.368 * G + -0.071 * B)) >> 3; // 5 bps

            return (ushort)((Y << 10) + (cb << 5) + (cr));
        }
        
        /// <summary>
        /// Very lossy conversion from 16 bit Ycbcr
        /// </summary>
        public static uint Ycbcr16_To_RGB32(uint c)
        {
            long Y =  ((c >> 10) & 0xFF) << 2;
            long cb = ((c >>  5) & 0x1F) << 3;
            long cr = ((c      ) & 0x1F) << 3;
            
            var R = clip(1.164 * (Y - 16) + 0.0 * (cb - 128) + 1.596 * (cr - 128));
            var G = clip(1.164 * (Y - 16) + -0.392 * (cb - 128) + -0.813 * (cr - 128));
            var B = clip(1.164 * (Y - 16) + 2.017 * (cb - 128) + 0.0 * (cr - 128));
            
            return (uint)((R << 16) + (G << 8) + B);
        }

        /// <summary>
        /// Lossy conversion from RGB to YCoCg (both 24 bit, stored as 32).
        /// This is a (luma + blue/orange + purple/green) space
        /// </summary>
        public static uint RGB32_To_Ycocg32(uint c)
        {
            int R = (int) ((c >> 16) & 0xff);
            int G = (int) ((c >>  8) & 0xff);
            int B = (int) ((c      ) & 0xff);

            var Co  = R - B;
            var tmp = B + (Co >> 1);
            var Cg  = G - tmp;
            var Y   = tmp + (Cg >> 1);

            // if you don't do this step, it's a lossless transform,
            // but you need 2 extra bits to store the color data
            Co = (Co >> 1) + 127;
            Cg = (Cg >> 1) + 127;
            
            return (uint)((clip(Y) << 16) + (clip(Co) << 8) + (clip(Cg)));
        }

        /// <summary>
        /// Lossy conversion from YCoCg to RGB (both 24 bit, stored as 32).
        /// This is a (luma + blue/orange + purple/green) space
        /// </summary>
        public static uint Ycocg32_To_RGB32(uint c)
        {
            int Y  = (int) ((c >> 16) & 0xff);
            int Co = (int) ((c >>  8) & 0xff);
            int Cg = (int) ((c      ) & 0xff);
            
            Co = (Co - 127) << 1;
            Cg = (Cg - 127) << 1;

            var tmp = Y - (Cg >> 1);
            var G   = Cg + tmp;
            var B   = tmp - (Co >> 1);
            var R   = B + Co;

            return (uint)((clip(R) << 16) + (clip(G) << 8) + clip(B));
        }

        /// <summary>
        /// Force a double into the integral range [0..255]
        /// </summary>
        public static int clip(double v) {
            if (v > 255) return 255;
            if (v < 0) return 0;
            return (int)v;
        }
        
        /// <summary>
        /// Force a integer into the range [0..255]
        /// </summary>
        public static int clip(long v) {
            if (v > 255) return 255;
            if (v < 0) return 0;
            return (int)v;
        }

        /// <summary>
        /// Clip and convert 3 RGB channels of 0..255
        /// to a packed 32 bit int
        /// </summary>
        public static uint ComponentToRGB32(int R, int G, int B)
        {
            return (uint)((clip(R) << 16) + (clip(G) << 8) + clip(B));
        }
        
        /// <summary>
        /// Converta packed 32 bit int
        /// to 3 RGB channels of 0..255
        /// </summary>
        public static void RGB32ToComponent(uint c, out int R, out int G, out int B)
        {
            R = (int) ((c >> 16) & 0xff);
            G = (int) ((c >>  8) & 0xff);
            B = (int) ((c      ) & 0xff);
        }


        /// <summary>
        /// Pick a dark color by an RGB average-of-4
        /// </summary>
        public static ColorRGB LowerColor_AveOf4(ColorRGB[] cols)
        {
            long R = 0, G = 0, B = 0;
            for (int i = 0; i < 4; i++)
            {
                R += cols[i].R;
                G += cols[i].G;
                B += cols[i].B;
            }
            return new ColorRGB { R = (int)(R / 4), G = (int)(G / 4), B = (int)(B / 4) };
        }

        /// <summary>
        /// Pick dark color by YUV decaying
        /// </summary>
        public static ColorYUV LowerColor(ColorYUV[] cols)
        {
            const double fac = 2.59285714285714;
            double Y=0, cb=0, cr=0;
            for (int i = 0; i < 8; i++)
            {
                var x = cols[i];
                Y  += x.Y / (i+1);
                cb += x.U / (i+1);
                cr += x.V / (i+1);
            }
            return new ColorYUV { Y = Y / fac, U = cb / fac, V = cr / fac };
        }

        /// <summary>
        /// Pick a light color by average-of-4
        /// </summary>
        public static ColorRGB UpperColor_AveOf4(ColorRGB[] cols)
        {
            long R=0, G=0, B=0;
            for (int i = 12; i < 16; i++)
            {
                R+=cols[i].R;
                G+=cols[i].G;
                B+=cols[i].B;
            }
            return new ColorRGB { R = (int)(R / 4), G = (int)(G / 4), B = (int)(B / 4) };
        }
        
        /// <summary>
        /// Pick light color by YUV decaying
        /// </summary>
        public static ColorYUV UpperColor(ColorYUV[] cols)
        {
            const double fac = 2.59285714285714;
            double Y=0, cb=0, cr=0;
            for (int i = 0; i < 8; i++)
            {
                var x = cols[15 - i];
                Y  += x.Y / (i+1);
                cb += x.U / (i+1);
                cr += x.V / (i+1);
            }
            return new ColorYUV { Y = Y / fac, U = cb / fac, V = cr / fac };
        }
        

        /// <summary>
        /// Lossless conversion to YUV
        /// </summary>
        public static ColorYUV RGB32_To_YUV(uint c)
        {
            RGB32ToComponent(c, out var R, out var G, out var B);
            var Y = 16 + (0.257 * R + 0.504 * G + 0.098 * B);
            var U = 128 + (-0.148 * R + -0.291 * G + 0.439 * B);
            var V = 128 + (0.439 * R + -0.368 * G + -0.071 * B);

            return new ColorYUV { Y = Y, U = U, V = V };
        }

        /// <summary>
        /// Lossless conversion from Ycbcr 
        /// </summary>
        public static uint YUV_To_RGB32(double Y, double cb, double cr)
        {
            var R = clip(1.164 * (Y - 16) + 0.0 * (cb - 128) + 1.596 * (cr - 128));
            var G = clip(1.164 * (Y - 16) + -0.392 * (cb - 128) + -0.813 * (cr - 128));
            var B = clip(1.164 * (Y - 16) + 2.017 * (cb - 128) + 0.0 * (cr - 128));

            return ComponentToRGB32(R, G, B);
        }


        /// <summary>
        /// Capture 4x4 array of brightness into a linear list of 16 elements
        /// </summary>
        public static ColorRGB[] CaptureColors(uint[] input, int width, int x, int y)
        {
            return new ColorRGB[]{/*
                input.GetPixel(x, y),  input.GetPixel(x+1, y),  input.GetPixel(x+2, y),  input.GetPixel(x+3, y),
                input.GetPixel(x, y+1),input.GetPixel(x+1, y+1),input.GetPixel(x+2, y+1),input.GetPixel(x+3, y+1),
                input.GetPixel(x, y+2),input.GetPixel(x+1, y+2),input.GetPixel(x+2, y+2),input.GetPixel(x+3, y+2),
                input.GetPixel(x, y+3),input.GetPixel(x+1, y+3),input.GetPixel(x+2, y+3),input.GetPixel(x+3, y+3)*/
            };
        }

        /// <summary>
        /// Capture 4x4 array of brightness into a linear list of 16 elements
        /// </summary>
        public static byte[] CalcBrightness(ColorRGB[] input)
        {
            return input.Select(Brightness).ToArray();
        }

        /// <summary>
        /// YCrCb brightness
        /// </summary>
        public static byte Brightness(ColorRGB c){
            return (byte)(16+(0.257 * c.R + 0.504 * c.G + 0.098 * c.B));
        }
    }
}