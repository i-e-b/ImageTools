using System;
using System.Collections.Generic;

namespace ImageTools.ImageDataFormats
{
    public static class TinyFloat
    {
        public static byte[] Encode(double d)
        {
            var raw = BitConverter.DoubleToInt64Bits(d);

            var sign = raw >> 63; // 1 bit
            var exp = ((raw >> 52) & 0x7FF) - 1020; // 11 bit, with offset (normally should be 1023, but we 'optimise')
            var frac = raw & 0xFFFFFFFFFFFFF; // 52 bit
            if (exp == -1020) exp = 0;

            var uExp = DataEncoding.SignedToUnsigned((int) exp);
            var lFrac = (int)(frac >> 48);
            if (sign != 0) lFrac = lFrac == 0 ? -1 : -lFrac;
            var uFrac = DataEncoding.SignedToUnsigned(lFrac);

            var fE = DataEncoding.FibonacciEncodeOne(uExp);
            var fF = DataEncoding.FibonacciEncodeOne(uFrac);

            var outp = new List<byte>();
            outp.AddRange(fE);
            outp.AddRange(fF);
            return outp.ToArray();
        }

        public static double Decode(byte[] bits)
        {
            var q = new Queue<byte>(bits);
            var fE = DataEncoding.FibonacciDecodeOne(q);
            var fF = DataEncoding.FibonacciDecodeOne(q);
            
            if (fE == 0 && fF == 0) return 0.0;
            
            var sign = 0L;
            var lFrac = DataEncoding.UnsignedToSigned(fF);
            long exp = (DataEncoding.UnsignedToSigned(fE)+1020) & 0x7FF;
            if (lFrac < 0)
            {
                lFrac = -lFrac;
                sign = -1;
            }
            var frac = (long)lFrac << 48;
            var raw = sign < 0 ? 1L<<63 : 0L;
            raw |= exp << 52;
            raw |= frac;

            return BitConverter.Int64BitsToDouble(raw);
        }
    }
}