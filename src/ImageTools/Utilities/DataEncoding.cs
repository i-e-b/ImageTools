using System;
using System.Collections.Generic;

namespace ImageTools.Utilities
{
    /// <summary>
    /// Tools to convert arrays to different encodings
    /// </summary>
    public static class DataEncoding
    {
        public static byte[] DoubleToByte_EncodeBytes(double[] buffer)
        {
            var b = new byte[buffer.Length];
            for (int i = 0; i < buffer.Length; i++)
            {
                b[i] = (byte)Saturate(buffer[i]);
            }
            return b;
        }
        
        public static byte[] DoubleToShort_EncodeBytes(double[] buffer)
        {
            var b = new byte[buffer.Length * 2];
            for (int i = 0; i < buffer.Length; i++)
            {
                var s = (short)buffer[i];
                var j = i*2;
                b[j] = (byte)(s >> 8);
                b[j+1] = (byte)(s & 0xff);
            }
            return b;
        }

        public static void ByteToDouble_DecodeBytes(byte[] src, double[] buffer)
        {
            for (int i = 0; i < src.Length; i++)
            {
                buffer[i] = src[i];
            }
        }

        public static void ShortToDouble_DecodeBytes(byte[] src, double[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                var j = i * 2;
                short s = (short)((src[j] << 8) | (src[j+1]));
                buffer[i] = s;
            }
        }
        
        /// <summary>
        /// Reverse of RLZ_Encode
        /// </summary>
        private static void RLZ_Decode(byte[] packedSource, double[] buffer)
        {
            int DC = 0;
            var p = 0;
            var lim = buffer.Length - 1;
            bool err = false;
            for (int i = 0; i < packedSource.Length; i++)
            {
                if (p > lim) { err = true; break; }
                var value = packedSource[i];

                if (value == DC) {
                    if (i >= packedSource.Length) { err = true; break; }
                    // next byte is run length
                    for (int j = 0; j < packedSource[i+1]; j++)
                    {
                        buffer[p++] = value;
                        if (p > lim) { err = true; break; }
                    }
                    i++;
                    continue;
                }
                buffer[p++] = value;
            }
            if (err) Console.WriteLine("RLZ Decode did not line up correctly");
            else Console.WriteLine("RLZ A-OK");
        }

        /// <summary>
        /// Run length of zeros
        /// </summary>
        private static byte[] RLZ_Encode(double[] buffer)
        {
            var samples = DataEncoding.DoubleToByte_EncodeBytes(buffer);
            byte DC = 0;

            // the run length encoding is ONLY for zeros...
            // [data] ?[runlength] in bytes.
            //

            var runs = new List<byte>();
            var dcLength = 0;

            for (int i = 0; i < samples.Length; i++)
            {
                var samp = samples[i];
                if (samp == DC && dcLength < 252)
                {
                    dcLength++;
                    continue;
                }

                // non zero sample...
                if (dcLength > 0)
                {
                    runs.Add(DC);
                    runs.Add((byte)dcLength);
                }
                dcLength = 0;
                runs.Add(samp);
            }

            if (dcLength > 0)
            {
                runs.Add(DC);
                runs.Add((byte)dcLength);
            }

            return runs.ToArray();
        }

        /// <summary>
        /// Reverse UnsignedFibEncode()
        /// If byteLimit > 0, a limited amount of data will be loaded
        /// </summary>
        public static uint[] UnsignedFibDecode(byte[] data) {
            //  Make a bit queue from the byte data
            var q = new Queue<byte>(data.Length * 8);

            for (int Byte = data.Length - 1; Byte >= 0; Byte--)
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    q.Enqueue((byte) ((data[Byte] >> bit) & 0x01));
                }
            }

            var result = new Stack<uint>();
            while (q.Count > 0)  {
                var num = FibDecodeNum(q);
                if (num > 0) result.Push(num - 1); // this -1 matches a +1 in the encode
            }

            return result.ToArray();
        }

        /// <summary>
        /// Encode data as a sequence of fibonacci codes
        /// </summary>
        public static byte[] UnsignedFibEncode(uint[] data) {
            var q = new Stack<byte>();
            for (int i = 0; i < data.Length; i++)
            {
                q = FibEncodeNum(data[i] + 1, q); // this +1 matches a -1 in the decode
            }

            var result = new byte[(q.Count / 8) + 1];

            int bit = 0, Byte = result.Length - 1;

            while (q.Count > 0) {
                var v = q.Pop();
                result[Byte] |= (byte)(v << bit);
                bit++;
                if (bit > 7) {
                    bit = 0;
                    Byte--;
                }
            }

            return result;
        }

        /// <summary>
        /// take a single number and return an array encoding of the fibonacci code. Returns empty array on error.
        /// Results are 1 or 0 encoded in a byte
        /// </summary>
        public static Stack<byte> FibEncodeNum(uint n, Stack<byte> previous) {
            if (n < 1) return new Stack<byte>(0);

            var res = previous ?? new Stack<byte>(20);
            res.Push(1);

            // find the smallest fibonacci number greater than `n`
            uint f = 1, k = 1;
            while (f <= n) {f = fibonacci(++k);}

            // decompose back through the sequence
            while(--k > 1) {
                f = fibonacci(k);
                if (f <= n) {
                    res.Push(1);
                    n -= f;
                } else {
                    res.Push(0);
                }
            }
            res.Push(0); // stuff 1 bit -- this is only needed for error recovery
            return res;
        }
        
        /// <summary>
        /// Read a single number from a bit queue and return a single unsigned number.
        /// This reverses FibEncodeNum();
        /// </summary>
        public static uint FibDecodeNum(Queue<byte> bitArray) {
            bool lastWas1 = false;
            uint accum = 0;
            uint pos = 0;

            while (bitArray.Count > 0) {
                var f = bitArray.Dequeue();
                if (f > 0) {
                    if (lastWas1) break;
                    lastWas1 = true;
                } else lastWas1 = false;

                accum += f * fibonacci(pos + 1);
                pos++;
            }

            return accum;
        }

        // Cache of fib sequence
        private static readonly uint[] fibonacciSeq = {0,1,1,2,3,5,8,13,21,34,55,89,144,233,377,610,987,1597,2584,4181,6765,10946,17711,28657,46368,75025,121393,196418 };

        private static uint fibonacci (uint n) {
            if (fibonacciSeq.Length > n) { return fibonacciSeq[n]; }
            return 0; // out of bounds
        }

        private static int Saturate(double value)
        {
            if (value < 0) return 0;
            if (value > 255) return 255;
            return (int)value;
        }

        /// <summary>
        /// Lossless convert to positive numbers only
        /// </summary>
        public static uint[] SignedToUnsigned(int[] input)
        {
            var outp = new uint[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] >= 0) outp[i] = (uint)(input[i] * 2); // positive becomes even
                else outp[i] = (uint)(input[i] * -2) - 1;          // negative becomes odd
            }
            return outp;
        }

        public static int[] UnsignedToSigned(uint[] input)
        {
            var outp = new int[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                if ((input[i] % 2) == 0) outp[i] = (int) (input[i] >> 1);
                else outp[i] = (int) (((input[i] + 1) >> 1) * -1);
            }
            return outp;
        }
    }
}