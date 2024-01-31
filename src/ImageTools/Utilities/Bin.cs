using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace ImageTools.Utilities
{
    public static class Bin
    {

        /// <summary>
        /// Return the smalles number that is a power-of-two
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

        /// <summary>
        /// Return the smalles number that is a power-of-two
        /// greater than or equal to the input
        /// </summary>
        public static int NextPow2(int c)
        {
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
            var prefix = new[] { "b", "kb", "mb", "gb", "tb", "pb" };
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
            var head = BitConverter.DoubleToInt64Bits(b);
            var bitwise = (long)(e * (head - 4606921280493453312L)) + 4606921280493453312L;
            return BitConverter.Int64BitsToDouble(bitwise);
        }
        /*public static double pow(final double a, final double b) {
            final long tmp = Double.doubleToLongBits(a);
            final long tmp2 = (long)(b * (tmp - 4606921280493453312L)) + 4606921280493453312L;
            return Double.longBitsToDouble(tmp2);
        }*/

        /// <summary>
        /// Output hex string from byte array
        /// </summary>
        public static string HexString(IEnumerable<byte> input)
        {
            var sb = new StringBuilder();
            foreach (var b in input)
            {
                var u = (b & 0xF0) >> 4;
                var l = b & 0x0F;
                
                if (u < 0x0A) sb.Append((char)('0'+u));
                else sb.Append((char)('A'+(u-0x0A)));
                if (l < 0x0A) sb.Append((char)('0'+l));
                else sb.Append((char)('A'+(l-0x0A)));
            }
            return sb.ToString();
        }
        
        /// <summary>
        /// Output binary string from byte array
        /// </summary>
        public static string BinString(IEnumerable<byte> input)
        {
            var sb = new StringBuilder();
            foreach (var b in input)
            {
                var m = 0b1000_0000u;
                for (int i = 0; i < 8; i++)
                {
                    if ((b&m) > 0) sb.Append('1');
                    else sb.Append('0');
                    m >>= 1;
                }
            }
            return sb.ToString();
        }
    }

}