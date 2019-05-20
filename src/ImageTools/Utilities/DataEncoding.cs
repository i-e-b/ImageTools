using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
        public static void RLZ_Decode(byte[] packedSource, double[] buffer)
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
        public static byte[] RLZ_Encode(double[] buffer)
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
        /// Decode a stream of byte values into an array of doubles
        /// </summary>
        /// <param name="input">Readable stream for input</param>
        public static double[] FibonacciDecode(Stream input)
        {
            // Read a byte, scan through bits building up a number until we hit `b11`
            // Then move on to the next

            int bv;
            var output = new List<double>();

            bool lastWas1 = false;
            uint accum = 0;
            uint pos = 0;
            var bytePos = 0;

            while ((bv = input.ReadByte()) >= 0) {

                while (bytePos++ < 8) {
                    uint f = (uint)((bv >> (8 - bytePos)) & 0x01);
                    Console.Write(f);
                    if (f > 0) {
                        if (lastWas1) {
                            // convert back to signed, add to list
                            if (accum > 0) {
                                //Console.Write($"!{accum};");
                                Console.Write(".");
                                //long n = accum - 1L;
                                //if ((n % 2) == 0) output.Add((int)(n >> 1));
                                //else output.Add((int)(((n + 1) >> 1) * -1));
                                output.Add(accum);
                            } else Console.Write("?");
                            // `b11`; reset, move to next number
                            accum = 0;
                            pos = 0;
                            lastWas1 = false;
                            continue;
                        }
                        lastWas1 = true;
                    } else lastWas1 = false;

                    accum += f * fseq[pos + 1];
                    pos++;
                }
                
                Console.Write(",");
                bytePos = 0;
            }

            return output.ToArray();
        }
        
        private static readonly uint[] fseq = {0,1,1,2,3,5,8,13,21,34,55,89,144,233,377,610,987,1597,2584,4181,6765,10946,17711,28657,46368,75025,121393,196418 };


        /// <summary>
        /// Encode an array of integer values into a byte stream.
        /// The input of double values are truncated during encoding.
        /// </summary>
        /// <param name="buffer">Input buffer. Values will be truncated and must be in the range +- 196418</param>
        /// <param name="output">Writable stream for output</param>
        public static void FibonacciEncode(double[] buffer, Stream output)
        {
            var bf = new byte[8]; // if each bit is set. Value is 0xFF or 0x00
            var v = new byte[]{ 1<<7, 1<<6, 1<<5, 1<<4, 1<<3, 1<<2, 1<<1, 1 }; // values of the flag
            var bytePos = 0;
            var termBits = 0;

            // for each number, build up the fib code.
            // any time we exceed a byte we write it out and reset
            // Negative numbers are handled by the same process as `SignedToUnsigned`
            // this streams out numbers MSB-first (?)

            foreach (var inValue in buffer)
            {
                // Signed to unsigned
                var n = (long)inValue;//(inValue >= 0) ? (uint)(inValue * 2) : (uint)(inValue * -2) - 1; // value to be encoded
                //n += 1; // always greater than zero

                // Fibonacci encode
                ulong res = 0UL;
                var maxidx = -1;

                var i = 2;
                while (fseq[i] < n) i++;

                while (n > 0)
                {
                    if (fseq[i] <= n)
                    {
                        res |= 1UL << (i - 2);
                        n -= (int)fseq[i];
                        if (maxidx < i) maxidx = i;
                    }
                    i--;
                }
                res |= 1UL << (maxidx - 1);

                // output to stream
                for (int boc = 0; boc < maxidx; boc++)
                {
                    bf[bytePos] = (byte)(0xFF * ((res >> (boc)) & 1));
                    Console.Write(bf[bytePos]&1);
                    bytePos++;

                    if (bytePos > 7)
                    { // completed a byte (same as above)
                        int bv = (bf[0] & v[0]) | (bf[1] & v[1]) | (bf[2] & v[2]) | (bf[3] & v[3]) | (bf[4] & v[4]) | (bf[5] & v[5]) | (bf[6] & v[6]) | (bf[7] & v[7]);
                        output.WriteByte((byte)bv);
                        bf[0] = bf[1] = bf[2] = bf[3] = bf[4] = bf[5] = bf[6] = bf[7] = 0;
                        bytePos = 0;
                        Console.Write(",");
                    }
                }
            }

            // If we didn't land on a byte boundary, push the last one out here
            if (bytePos != 0) { // completed a byte (slightly different to the others above)
                int bv = (bf[0] & v[0]) | (bf[1] & v[1]) | (bf[2] & v[2]) | (bf[3] & v[3]) | (bf[4] & v[4]) | (bf[5] & v[5]) | (bf[6] & v[6]) | (bf[7] & v[7]);
                output.WriteByte((byte)bv);
                Console.Write(",");
            }
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