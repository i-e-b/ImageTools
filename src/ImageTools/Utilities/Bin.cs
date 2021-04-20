using System;

namespace ImageTools.Utilities
{
    public static class Bin {
        
        /// <summary>
        /// Return the smalles number that is a power-of-two
        /// greater than or equal to the input
        /// </summary>
        public static uint NextPow2(uint c) {
            c--;
            c |= c >> 1;
            c |= c >> 2;
            c |= c >> 4;
            c |= c >> 8;
            c |= c >> 16;
            return ++c;
        }
        
        /// <summary>
        /// Return the smalles number that is a power-of-two
        /// greater than or equal to the input
        /// </summary>
        public static int NextPow2(int c) {
            return (int)NextPow2((uint)c);
        }

        /// <summary>
        /// Render a human-friendly string for a file size in bytes
        /// </summary>
        /// <param name="byteLength"></param>
        /// <returns></returns>
        public static string Human(long byteLength)
        {
            double size = byteLength;
            var prefix = new []{ "b", "kb", "mb", "gb", "tb", "pb" };
            int i;
            for (i = 0; i < prefix.Length; i++)
            {
                if (size < 1024) break;
                size /= 1024;
            }
            return size.ToString("#0.##") + prefix[i];
        }

        /// <summary>
        /// Pin number to range
        /// </summary>
        public static int Pin(this int v, int lower, int upper)
        {
            if (v < lower) return lower;
            if (v > upper) return upper;
            return v;
        }
        
        /// <summary>
        /// Pin number to range, with exclusive upper bound
        /// </summary>
        public static int PinXu(this int v, int lower, int exclusiveUpper)
        {
            if (v < lower) return lower;
            if (v >= exclusiveUpper) return exclusiveUpper - 1;
            return v;
        }
        
        /// <summary>
        /// Approximate Math.Pow using bitwise tricks
        /// </summary>
        /// <param name="b">base</param>
        /// <param name="e">exponent</param>
        /// <returns>Approximation of b^e</returns>
        public static double FPow(double b, double e)
        {
            var head = BitConverter.DoubleToInt64Bits(b) >> 32;
            var bitwise = (long)(e * (head - 1072632447) + 1072632447) << 32;
            return BitConverter.Int64BitsToDouble(bitwise);
        }
    }

}