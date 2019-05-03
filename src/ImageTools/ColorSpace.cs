
namespace ImageTools
{
    public static class ColorSpace
    {
        /// <summary>
        /// Lossy conversion from RGB to Ycbcr (both 24 bit, stored as 32)
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

        
        public static int clip(double v) {
            if (v > 255) return 255;
            if (v < 0) return 0;
            return (int)v;
        }

        public static int clip(long v) {
            if (v > 255) return 255;
            if (v < 0) return 0;
            return (int)v;
        }
    }
}