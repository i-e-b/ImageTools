using System;
using System.Linq;

namespace ImageTools.ImageDataFormats
{
    /// <summary>
    /// RGB colour components
    /// </summary>
    public struct ColorRGB {
        public int R;
        public int G;
        public int B;

        public static ColorRGB FromARGB32(uint c) {
            ColorSpace.CompoundToComponent(c, out _, out var r, out var g, out var b);
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
            ColorSpace.CompoundToComponent(c, out _, out var y, out var u, out var v);
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
        
        public static void YCoCgToRGB(double Y, double Co, double Cg, out double R, out double G, out double B) {
            Co = (Co - 127) * 2;
            Cg = (Cg - 127) * 2;

            var tmp = Y - (Cg / 2);
            G   = Cg + tmp;
            B   = tmp - (Co / 2);
            R   = B + Co;
        }
        public static void RGBToYCoCg(double R, double G, double B, out double Y, out double Co, out double Cg) {
            Co = R - B;
            var tmp = B + (Co / 2);
            Cg = G - tmp;
            Y = tmp + (Cg / 2);

            // if you don't do this step, it's a lossless transform,
            // but you need 2 extra bits to store the color data
            Co = (Co / 2) + 127;
            Cg = (Cg / 2) + 127;
        }

        /// <summary>
        /// Approximation of NTSC/FCC YIQ space.
        /// This expects `i` to be stored/transmitted with twice the bandwidth of q
        /// </summary>
        public static void YiqToRGB(double Y, double i, double q, out double R, out double G, out double B) {
            i = (i - 127) * 1.1914;
            q = (q - 127) * 1.0452;

            R = Y + (0.9469 * i) + (0.6236 * q);
            G = Y - (0.2748 * i) - (0.6357 * q);
            B = Y - (1.1000 * i) + (1.7000 * q);
        }
        
        /// <summary>
        /// Approximation of NTSC/FCC YIQ space.
        /// This expects `i` to be stored/transmitted with twice the bandwidth of q
        /// </summary>
        public static void RGBToYiq(double R, double G, double B, out double Y, out double i, out double q){
            Y = (0.30 * R) + (0.59 * G) + (0.11 * B);
            i = (-0.27 * (B - Y)) + (0.74 * (R - Y));
            q = (0.41 * (B - Y)) + (0.48 * (R - Y));

            i = (i * 0.8393) + 127;
            q = (q * 0.9567) + 127;
        }

        public static uint Yiq_To_RGB32(int Y, int i, int q)
        {
            YiqToRGB(Y, i, q, out var R, out var G, out var B);

            return (uint)((clip(R) << 16) + (clip(G) << 8) + clip(B));
        }

        /// <summary>
        /// Lossy conversion from RGB to YCoCg (both 24 bit, stored as 32).
        /// This is a (luma + blue/orange + purple/green) space
        /// </summary>
        public static void RGB32_To_Ycocg(uint c, out int Y, out int Co, out int Cg)
        {
            int R = (int) ((c >> 16) & 0xff);
            int G = (int) ((c >>  8) & 0xff);
            int B = (int) ((c      ) & 0xff);

            Co  = R - B;
            var tmp = B + (Co >> 1);
            Cg  = G - tmp;
            Y   = tmp + (Cg >> 1);

            // if you don't do this step, it's a lossless transform,
            // but you need 2 extra bits to store the color data
            Co = (Co >> 1) + 127;
            Cg = (Cg >> 1) + 127;
        }
        
        public static void RGB32_To_Ycbcr(uint c, out int Y, out int Cb, out int Cr)
        {
            int R = (int) ((c >> 16) & 0xff);
            int G = (int) ((c >>  8) & 0xff);
            int B = (int) ((c      ) & 0xff);

            Y = clip(16 + (0.257 * R + 0.504 * G + 0.098 * B));
            Cb = clip(128 + (-0.148 * R + -0.291 * G + 0.439 * B));
            Cr = clip(128 + (0.439 * R + -0.368 * G + -0.071 * B));
        }
        
        /// <summary>
        /// Lossy conversion from YCoCg to RGB (both 24 bit, stored as 32).
        /// This is a (luma + blue/orange + purple/green) space
        /// </summary>
        public static uint Ycocg_To_RGB32(int Y, int Co, int Cg)
        {
            Co = (Co - 127) << 1;
            Cg = (Cg - 127) << 1;

            var tmp = Y - (Cg >> 1);
            var G   = Cg + tmp;
            var B   = tmp - (Co >> 1);
            var R   = B + Co;

            return (uint)((clip(R) << 16) + (clip(G) << 8) + clip(B));
        }
        
        /// <summary>
        /// Lossy conversion from YCoCg to RGB (both 24 bit, stored as 32).
        /// This is a (luma + blue/orange + purple/green) space
        /// </summary>
        public static uint Ycocg_To_RGB32(double Y, double Co, double Cg)
        {
            var vY = clip(Y);
            var vCo = clip(Co - 127) << 1;
            var vCg = clip(Cg - 127) << 1;

            var tmp = vY - (vCg >> 1);
            var G   = vCg + tmp;
            var B   = tmp - (vCo >> 1);
            var R   = B + vCo;

            return (uint)((clip(R) << 16) + (clip(G) << 8) + clip(B));
        }

        public static uint Ycbcr_To_RGB32(double Y, double Cb, double Cr)
        {
            var R = clip(1.164 * (Y - 16) + 0.0 * (Cb - 128) + 1.596 * (Cr - 128));
            var G = clip(1.164 * (Y - 16) + -0.392 * (Cb - 128) + -0.813 * (Cr - 128));
            var B = clip(1.164 * (Y - 16) + 2.017 * (Cb - 128) + 0.0 * (Cr - 128));

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
        /// Force a double into the integral range [0..255]
        /// </summary>
        public static int clip(float v) {
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
        /// Clip and convert 4 channels of 0..255
        /// to a packed 32 bit int
        /// </summary>
        public static uint ComponentToCompound(int A, int B, int C, int D)
        {
            return (uint)((clip(A) << 24) | (clip(B) << 16) | (clip(C) << 8) | clip(D));
        }

        
        /// <summary>
        /// Clip and convert 4 channels of 0..255
        /// to a packed 32 bit int
        /// </summary>
        public static uint ComponentToCompound(double A, double B, double C, double D)
        {
            return (uint)((clip(A) << 24) | (clip(B) << 16) | (clip(C) << 8) | clip(D));
        }
        
        /// <summary>
        /// Convert a packed 32 bit int
        /// to 4 channels of 0..255
        /// </summary>
        public static void CompoundToComponent(uint c, out int A, out int B, out int C, out int D)
        {
            A = (int) ((c >> 24) & 0xff);
            B = (int) ((c >> 16) & 0xff);
            C = (int) ((c >>  8) & 0xff);
            D = (int) ((c      ) & 0xff);
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
            CompoundToComponent(c, out _, out var R, out var G, out var B);
            var Y = 16 + (0.257 * R + 0.504 * G + 0.098 * B);
            var U = 128 + (-0.148 * R + -0.291 * G + 0.439 * B);
            var V = 128 + (0.439 * R + -0.368 * G + -0.071 * B);

            return new ColorYUV { Y = Y, U = U, V = V };
        }

        public static void RGBToYUV(double R, double G, double B, out double Y, out double U, out double V)
        {
            Y = 16 + (0.257 * R + 0.504 * G + 0.098 * B);
            U = 128 + (-0.148 * R + -0.291 * G + 0.439 * B);
            V = 128 + (0.439 * R + -0.368 * G + -0.071 * B);
        }
        
        /// <summary>
        /// Lossless conversion to YUV
        /// </summary>
        public static void RGB32_To_YUV(uint c, out double Y, out double U, out double V)
        {
            CompoundToComponent(c, out _, out var R, out var G, out var B);
            Y = 16 + (0.257 * R + 0.504 * G + 0.098 * B);
            U = 128 + (-0.148 * R + -0.291 * G + 0.439 * B);
            V = 128 + (0.439 * R + -0.368 * G + -0.071 * B);
        }

        /// <summary>
        /// Lossless conversion from Ycbcr 
        /// </summary>
        public static uint YUV_To_RGB32(double Y, double U, double V)
        {
            var R = 1.164 * (Y - 16) + 0.0 * (U - 128) + 1.596 * (V - 128);
            var G = 1.164 * (Y - 16) + -0.392 * (U - 128) + -0.813 * (V - 128);
            var B = 1.164 * (Y - 16) + 2.017 * (U - 128) + 0.0 * (V - 128);

            return (uint)((clip(R) << 16) | (clip(G) << 8) | clip(B));
        }

        
        /// <summary>
        /// Experimental color space.
        /// This is biased to allow more error in red and blue, less in green.
        /// </summary>
        public static void YUVToRGB(double Y, double U, double V, out double R, out double G, out double B)
        {
            R = 1.164 * (Y - 16) + 0.0 * (U - 128) + 1.596 * (V - 128);
            G = 1.164 * (Y - 16) + -0.392 * (U - 128) + -0.813 * (V - 128);
            B = 1.164 * (Y - 16) + 2.017 * (U - 128) + 0.0 * (V - 128);
        }

        /// <summary>
        /// Capture 4x4 array of brightness into a linear list of 16 elements
        /// </summary>
        public static byte[] CalcBrightness(ColorRGB[] input)
        {
            return input.Select(Brightness).ToArray();
        }

        /// <summary>
        /// YUV brightness
        /// </summary>
        public static byte Brightness(ColorRGB c){
            return (byte)(16+(0.257 * c.R + 0.504 * c.G + 0.098 * c.B));
        }

        private const double Pr = 0.299;
        private const double Pg = 0.587;
        private const double Pb = 0.114;

        /// <summary>
        /// Experimental color space.
        /// This is biased to allow more error in red and blue, less in green.
        /// </summary>
        public static void RGBToExp(double R, double G, double B, out double c1, out double c2, out double c3)
        {
            var X = 0.0638407 * B + 0.291612 * G + 0.144547 * R + 64;
            var Y = 0.435098 * B - 0.270198 * G - 0.1649 * R + 128;
            var Z = -0.0527391 * B - 0.382359 * G + 0.435098 * R + 128;

            c1 = clip(X);
            c2 = clip(Y);
            c3 = clip(Z);
        }

        /// <summary>
        /// Experimental color space.
        /// This is biased to allow more error in red and blue, less in green.
        /// </summary>
        public static void ExpToRGB(double x, double y, double z, out double R, out double G, out double B)
        {
            R = 2 * (x - 64) -  0.1   * (y - 128) +  1.596 * (z - 128);
            G = 2 * (x - 64) + -0.392 * (y - 128) + -0.813 * (z - 128);
            B = 2 * (x - 64) +  2.017 * (y - 128) +  0.1   * (z - 128);

            R = clip(R);
            G = clip(G);
            B = clip(B);
        }

        /// <summary>
        /// Experimental color space
        /// </summary>
        public static uint ExpToRGB32(double X, double Y, double Z)
        {
            ExpToRGB(X,Y,Z, out var R, out var G, out var B);
            return (uint)((clip(R) << 16) + (clip(G) << 8) + clip(B));
        }


        /// <summary>
        /// Convert a compound RGB to compound HSP ( see http://alienryderflex.com/hsp.html )
        /// </summary><remarks>
        /// The internal function expects the passed-in values to be on a scale
        ///  of 0 to 1, and uses that same scale for the return values.
        /// </remarks>
        public static uint RGB32_To_HSP32(uint c)
        {
            RGB32_To_HSP(c, out var H, out var S, out var P);
            return ComponentToCompound(0, clip(H), clip(S), clip(P));
        }
        
        
        /// <summary>
        /// Convert a compound HSP to compound RGB ( see http://alienryderflex.com/hsp.html )
        /// </summary><remarks>
        /// The internal function expects the passed-in values to be on a scale
        ///  of 0 to 1, and uses that same scale for the return values.
        ///
        ///  Note that some combinations of HSP, even if in the scale
        ///  0-1, may return RGB values that exceed a value of 1.  For
        ///  example, if you pass in the HSP color 0,1,1, the result
        ///  will be the RGB color 2.037,0,0.
        /// </remarks>
        public static uint HSP32_To_RGB32(uint c) {
            CompoundToComponent(c, out _, out var h, out var s, out var p);
            return HSP_To_RGB32(h, s, p);
        }

        /// <summary>
        /// Convert a component HSP to compound RGB ( see http://alienryderflex.com/hsp.html )
        /// </summary><remarks>
        /// The internal function expects the passed-in values to be on a scale
        ///  of 0 to 1, and uses that same scale for the return values.
        ///
        ///  Note that some combinations of HSP, even if in the scale
        ///  0-1, may return RGB values that exceed a value of 1.  For
        ///  example, if you pass in the HSP color 0,1,1, the result
        ///  will be the RGB color 2.037,0,0.
        /// </remarks>
        public static void HSP_To_RGB(double h, double s, double p, out double R, out double G, out double B) {
            double H = h / 255.0;
            double S = s / 255.0;
            double P = p / 255.0;
            
            double minOverMax = 1.0 - S;

            if (minOverMax > 0)
            {
                double part;
                if (H < 1.0 / 6.0)
                {   //  R>G>B
                    H = 6.0 * (H - 0.0 / 6.0); part = 1.0 + H * (1.0 / minOverMax - 1.0);
                    B = P / Math.Sqrt(Pr / minOverMax / minOverMax + Pg * part * part + Pb);
                    R = (B) / minOverMax; G = (B) + H * ((R) - (B));
                }
                else if (H < 2.0 / 6.0)
                {   //  G>R>B
                    H = 6.0 * (-H + 2.0 / 6.0); part = 1.0 + H * (1.0 / minOverMax - 1.0);
                    B = P / Math.Sqrt(Pg / minOverMax / minOverMax + Pr * part * part + Pb);
                    G = (B) / minOverMax; R = (B) + H * ((G) - (B));
                }
                else if (H < 3.0 / 6.0)
                {   //  G>B>R
                    H = 6.0 * (H - 2.0 / 6.0); part = 1.0 + H * (1.0 / minOverMax - 1.0);
                    R = P / Math.Sqrt(Pg / minOverMax / minOverMax + Pb * part * part + Pr);
                    G = (R) / minOverMax; B = (R) + H * ((G) - (R));
                }
                else if (H < 4.0 / 6.0)
                {   //  B>G>R
                    H = 6.0 * (-H + 4.0 / 6.0); part = 1.0 + H * (1.0 / minOverMax - 1.0);
                    R = P / Math.Sqrt(Pb / minOverMax / minOverMax + Pg * part * part + Pr);
                    B = (R) / minOverMax; G = (R) + H * ((B) - (R));
                }
                else if (H < 5.0 / 6.0)
                {   //  B>R>G
                    H = 6.0 * (H - 4.0 / 6.0); part = 1.0 + H * (1.0 / minOverMax - 1.0);
                    G = P / Math.Sqrt(Pb / minOverMax / minOverMax + Pr * part * part + Pg);
                    B = (G) / minOverMax; R = (G) + H * ((B) - (G));
                }
                else
                {   //  R>B>G
                    H = 6.0 * (-H + 6.0 / 6.0); part = 1.0 + H * (1.0 / minOverMax - 1.0);
                    G = P / Math.Sqrt(Pr / minOverMax / minOverMax + Pb * part * part + Pg);
                    R = (G) / minOverMax; B = (G) + H * ((R) - (G));
                }
            }
            else
            {
                if (H < 1.0 / 6.0)
                {   //  R>G>B
                    H = 6.0 * (H - 0.0 / 6.0); R = Math.Sqrt(P * P / (Pr + Pg * H * H)); G = (R) * H; B = 0.0;
                }
                else if (H < 2.0 / 6.0)
                {   //  G>R>B
                    H = 6.0 * (-H + 2.0 / 6.0); G = Math.Sqrt(P * P / (Pg + Pr * H * H)); R = (G) * H; B = 0.0;
                }
                else if (H < 3.0 / 6.0)
                {   //  G>B>R
                    H = 6.0 * (H - 2.0 / 6.0); G = Math.Sqrt(P * P / (Pg + Pb * H * H)); B = (G) * H; R = 0.0;
                }
                else if (H < 4.0 / 6.0)
                {   //  B>G>R
                    H = 6.0 * (-H + 4.0 / 6.0); B = Math.Sqrt(P * P / (Pb + Pg * H * H)); G = (B) * H; R = 0.0;
                }
                else if (H < 5.0 / 6.0)
                {   //  B>R>G
                    H = 6.0 * (H - 4.0 / 6.0); B = Math.Sqrt(P * P / (Pb + Pr * H * H)); R = (B) * H; G = 0.0;
                }
                else
                {   //  R>B>G
                    H = 6.0 * (-H + 6.0 / 6.0); R = Math.Sqrt(P * P / (Pr + Pb * H * H)); B = (R) * H; G = 0.0;
                }
            }

            R *= 255;
            G*=255;
            B *= 255;
        }

        public static uint HSP_To_RGB32(int h, int s, int p)
        {
            HSP_To_RGB(h, s, p, out var R, out var G, out var B);
            return ComponentToCompound(0, clip(R), clip(G), clip(B));
        }

        public static short YUV_To_RGB565(float Y, float U, float V)
        {
            if (Y > 220) return -1; // threshold to white

            var R = 1.164f * (Y - 16) + 0.0f * (U - 127.5f) + 1.596f * (V - 127.5f);
            var G = 1.164f * (Y - 16) + -0.392f * (U - 127.5f) + -0.813f * (V - 127.5f);
            var B = 1.164f * (Y - 16) + 2.017f * (U - 127.5f) + 0.0f * (V - 127.5f);
            int bits = ((clip(R) & 0xF8) << 8)
                       | ((clip(G) & 0xFC) << 3)
                       | ((clip(B) & 0xF8) >> 3);
            return (short)bits;
        }

        public static void RGB565_To_YUV(short c, out float Y, out float U, out float V)
        {
            int R = (c >> 8) & 0xF8;
            int G = (c >> 3) & 0xFC;
            int B = (c << 3) & 0xF8;

            Y = 16 + (0.257f * R + 0.504f * G + 0.098f * B);
            U = 127.5f + (-0.148f * R + -0.291f * G + 0.439f * B);
            V = 127.5f + (0.439f * R + -0.368f * G + -0.071f * B);
        }

        public static void RGB32_To_HSP(uint c, out double H, out double S, out double P){
            CompoundToComponent(c, out _, out var r, out var g, out var b);
            RGB_To_HSP(r, g, b, out H, out S, out P);
        }

        public static void RGB_To_HSP(double r, double g, double b, out double H, out double S, out double P)
        {
            const double NoiseFloor = 0.03;
            double R = r / 255.0;
            double G = g / 255.0;
            double B = b / 255.0;

            P = Math.Sqrt(R * R * Pr + G * G * Pg + B * B * Pb);

            H = 0;
            S = 0;

            // check for greys
            if (Math.Abs(r - g) < NoiseFloor && Math.Abs(r - b) < NoiseFloor) {
                P = clip(P * 255);
                return;
            }

            //  Calculate the Hue and Saturation.  (This part works
            //  the same way as in the HSV/B and HSL systems???.)
            if (R >= G && R >= B)
            {   //  R is largest
                if (B >= G)
                {
                    H = 6.0 / 6.0 - 1.0 / 6.0 * (B - G) / (R - G);
                    S = 1.0 - G / R;
                }
                else
                {
                    H = 0.0 / 6.0 + 1.0 / 6.0 * (G - B) / (R - B);
                    S = 1.0 - B / R;
                }
            }
            else if (G >= R && G >= B)
            {   //  G is largest
                if (R >= B)
                {
                    H = 2.0 / 6.0 - 1.0 / 6.0 * (R - B) / (G - B);
                    S = 1.0 - B / G;
                }
                else
                {
                    H = 2.0 / 6.0 + 1.0 / 6.0 * (B - R) / (G - R);
                    S = 1.0 - R / G;
                }
            }
            else
            {   //  B is largest
                if (G >= R)
                {
                    H = 4.0 / 6.0 - 1.0 / 6.0 * (G - R) / (B - R);
                    S = 1.0 - R / B;
                }
                else
                {
                    H = 4.0 / 6.0 + 1.0 / 6.0 * (R - G) / (B - G);
                    S = 1.0 - G / B;
                }
            }

            if (P < NoiseFloor) S = 0; // prevent Saturation noise at very low brightness
            if (S < NoiseFloor) H = 0; // prevent Hue noise at very low saturation

            H = clip(H * 255);
            S = clip(S * 255);
            P = clip(P * 255);
        }

        /// <summary>
        /// Pass through color space unchanged
        /// </summary>
        public static void Native(double i1, double i2, double i3, out double o1, out double o2, out double o3)
        {
            o1 = i1; o2 = i2; o3 = i3;
        }

        public static uint HCL_To_RGB32(int h, int c, int l)
        {
            HCL_To_RGB(h, c, l, out var R, out var G, out var B);
            return ComponentToCompound(0, clip(R), clip(G), clip(B));
        }

        public static void RGB_To_HCL(double r, double g, double b, out double H, out double C, out double L){
            const double tolerance = 0.0001;

            L = (0.30 * r + 0.59 * g + 0.11 * b) / 255.0;
            if (Math.Abs(r - g) < tolerance && Math.Abs(g - b) < tolerance)
            {
                H = 0; C = 0;
                return;
            }

            var M = r;
            var m = r;
            if (M < g) M = g;
            if (M < b) M = b;
            if (m > g) m = g;
            if (m > b) m = b;
            C = M - m;
            if (Math.Abs(M - r) < tolerance)
            {
                H = (g - b) / C;
                while (H < 0) H += 6;
                H %= 6;
            }
            else if (Math.Abs(M - g) < tolerance)
            {
                H = (b - r) / C + 2;
            }
            else
            {
                H = (r - g) / C + 4;
            }

            H *= 60.0;
            C /= 255.0;
        }

        public static void HCL_To_RGB(double hue, double chroma, double luma, out double R, out double G, out double B)
        {
            // 255-space adjustment
            chroma *= 1.0 / 255.0;
            luma *= 1.0 / 255.0;

            if (chroma <= 0)
            {
                R = G = B = luma * 255;
                return;
            }

            var hm = hue / 60;
            while (hm < 0)
            {
                hm += 6;
            }
            hm %= 6;
            var x = chroma * (1 - Math.Abs(hm % 2 - 1));

            double rm, gm, bm;
            if (hm < 1) { rm = chroma; gm = x; bm = 0; }
            else if (hm < 2) { rm = x; gm = chroma; bm = 0; }
            else if (hm < 3) { rm = 0; gm = chroma; bm = x; }
            else if (hm < 4) { rm = 0; gm = x; bm = chroma; }
            else if (hm < 5) { rm = x; gm = 0; bm = chroma; }
            else { rm = chroma; gm = 0; bm = x; }

            var m = luma - (0.30 * rm + 0.59 * gm + 0.11 * bm);


            R = clip(255 * (rm + m));
            G = clip(255 * (gm + m));
            B = clip(255 * (bm + m));
        }
    }
}