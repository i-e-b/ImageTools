using System;
using System.Collections.Generic;

namespace ImageTools.ImageDataFormats
{
    public static class TinyFloat
    {
        private const int ExponentOffset = -1020;
        private const int FractionPrecision = 46; // between 0 (max) and 52 (min)
        
        public static byte[] Encode(double d)
        {
            var raw = BitConverter.DoubleToInt64Bits(d);
            
            if (d == 0.0) return new byte[]{0,1,1};

            var sign = raw >> 63; // 1 bit
            var exp = ((raw >> 52) & 0x7FF) + ExponentOffset; // 11 bit, with offset (normally should be 1023, but we 'optimise')
            var frac = raw & 0xFFFFFFFFFFFFF; // 52 bit
            if (exp == ExponentOffset) exp = 0;

            var uExp = DataEncoding.SignedToUnsigned((int) exp);
            var lFrac = (ulong)(frac >> FractionPrecision);
            var uFrac = (uint)lFrac;

            var fE = DataEncoding.FibonacciEncodeOne(uExp);
            var fF = DataEncoding.FibonacciEncodeOne(uFrac);

            var outp = new List<byte>();
            outp.Add((byte) (sign == 0 ? 1 : 0));
            outp.AddRange(fE);
            outp.AddRange(fF);
            return outp.ToArray();
        }

        public static double Decode(byte[] bits)
        {
            var q = new Queue<byte>(bits);
            var sign = (int)q.Dequeue();
            var fE = (uint)DataEncoding.FibonacciDecodeOne(q);
            
            if (sign == 0 && fE == 0) return 0.0;
            
            var fF = DataEncoding.FibonacciDecodeOne(q);
            
            if (fE == 0 && fF == 0) return 0.0;
            
            var lFrac = fF;
            long exp = (DataEncoding.UnsignedToSigned(fE) - ExponentOffset) & 0x7FF;
            
            var frac = (long)lFrac << FractionPrecision;
            var raw = sign == 0 ? 1L<<63 : 0L;
            raw |= exp << 52;
            raw |= frac;

            return BitConverter.Int64BitsToDouble(raw);
        }
    }
}