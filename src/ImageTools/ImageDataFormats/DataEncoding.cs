using System;
using System.Collections.Generic;
using System.IO;

namespace ImageTools.ImageDataFormats
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
        /// Decode a stream of byte values into a new array of doubles
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

                    if (f > 0) {
                        if (lastWas1) {
                            // convert back to signed, add to list
                            if (accum > 0) {
                                long n = accum - 1L;
                                if ((n % 2) == 0) output.Add((int)(n >> 1));
                                else output.Add((int)(((n + 1) >> 1) * -1));
                            } // else damaged data
                            // `b11`; reset, move to next number
                            accum = 0;
                            pos = 0;
                            lastWas1 = false;
                            continue;
                        }
                        lastWas1 = true;
                    } else lastWas1 = false;

                    accum += f * fseq[pos + 2];
                    pos++;
                }
                
                bytePos = 0;
            }

            return output.ToArray();
        }


        /// <summary>
        /// Decode a stream of byte values into an existing array of doubles
        /// </summary>
        /// <param name="input">Readable stream for input</param>
        /// <param name="output">An existing value buffer. If there is more input
        /// than buffer space, the end of the input will be truncated</param>
        public static void FibonacciDecode(Stream input, float[] output)
        {
            // Read a byte, scan through bits building up a number until we hit `b11`
            // Then move on to the next

            int bv;

            bool lastWas1 = false;
            uint accum = 0;
            uint pos = 0;
            var bytePos = 0;
            int outidx = 0;
            int outlimit = output.Length;

            while ((bv = input.ReadByte()) >= 0) {

                while (bytePos++ < 8) {
                    if (outidx >= outlimit) return; // end of buffer
                    uint f = (uint)((bv >> (8 - bytePos)) & 0x01);

                    if (f > 0) {
                        if (lastWas1) {
                            // convert back to signed, add to list
                            if (accum > 0) {
                                long n = accum - 1L;
                                if ((n & 1) == 0) output[outidx++] = (int)(n >> 1);
                                else output[outidx++] = (int)(((n + 1) >> 1) * -1);
                            } // else damaged data


                            // `b11`; reset, move to next number
                            accum = 0;
                            pos = 0;
                            lastWas1 = false;
                            continue;
                        }
                        lastWas1 = true;
                    } else lastWas1 = false;

                    if (pos >= fseq.Length - 3) {
                        //pos = 0; // Option 1: reset value and continue -- this works best for transient errors
                        return; // Option 2: Break sequence -- this works best for truncation
                    }
                    accum += f * fseq[pos + 2];
                    pos++;
                }
                
                bytePos = 0;
            }
        }
        
        private static readonly uint[] fseq = {0,1,1,2,3,5,8,13,21,34,55,89,144,233,377,610,987,1597,
            2584,4181,6765,10946,17711,28657,46368,75025,121393,196418,317811,514229  };

        public static void FibonacciEncode(int[] buffer, int length, Stream output)
        {
            var bf = new byte[8]; // if each bit is set. Value is 0xFF or 0x00
            var v = new byte[]{ 1<<7, 1<<6, 1<<5, 1<<4, 1<<3, 1<<2, 1<<1, 1 }; // values of the flag
            var bytePos = 0;

            if (length <= 0) length = buffer.Length;

            // for each number, build up the fib code.
            // any time we exceed a byte we write it out and reset
            // Negative numbers are handled by the same process as `SignedToUnsigned`
            // this streams out numbers MSB-first (?)

            for (var idx = 0; idx < length; idx++)
            {
                var n = buffer[idx];

                if (n < 1 || n > 514229) throw new Exception($"Value out of bounds: {n} at index {idx}");

                // Fibonacci encode
                ulong res = 0UL;
                var maxidx = -1;

                // find starting position
                var i = 2;
                while (fseq[i] < n) { i++; }

                // scan backwards marking value bits
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
                    bytePos++;

                    if (bytePos > 7)
                    { // completed a byte (same as above)
                        int bv = (bf[0] & v[0]) | (bf[1] & v[1]) | (bf[2] & v[2]) | (bf[3] & v[3]) | (bf[4] & v[4]) | (bf[5] & v[5]) | (bf[6] & v[6]) | (bf[7] & v[7]);
                        output.WriteByte((byte)bv);
                        bf[0] = bf[1] = bf[2] = bf[3] = bf[4] = bf[5] = bf[6] = bf[7] = 0;
                        bytePos = 0;
                    }
                }
            }

            // If we didn't land on a byte boundary, push the last one out here
            if (bytePos != 0) { // completed a byte (slightly different to the others above)
                int bv = (bf[0] & v[0]) | (bf[1] & v[1]) | (bf[2] & v[2]) | (bf[3] & v[3]) | (bf[4] & v[4]) | (bf[5] & v[5]) | (bf[6] & v[6]) | (bf[7] & v[7]);
                output.WriteByte((byte)bv);
            }
        }

        /// <summary>
        /// Encode an array of integer values into a byte stream.
        /// The input of double values are truncated during encoding.
        /// </summary>
        /// <param name="buffer">Input buffer. Values will be truncated and must be in the range +- 196418</param>
        /// <param name="length">Number of samples to encode. Must be equal-or-less than buffer length. To encode entire buffer, pass zero.</param>
        /// <param name="output">Writable stream for output</param>
        public static void FibonacciEncode(float[] buffer, int length, Stream output)
        {
            var bf = new byte[8]; // if each bit is set. Value is 0xFF or 0x00
            var v = new byte[]{ 1<<7, 1<<6, 1<<5, 1<<4, 1<<3, 1<<2, 1<<1, 1 }; // values of the flag
            var bytePos = 0;

            if (length <= 0) length = buffer.Length;

            // for each number, build up the fib code.
            // any time we exceed a byte we write it out and reset
            // Negative numbers are handled by the same process as `SignedToUnsigned`
            // this streams out numbers MSB-first (?)

            for (var idx = 0; idx < length; idx++)
            {
                var inValue = buffer[idx];

                // Signed to unsigned
                int n = (int)inValue;
                n = (n >= 0) ? (n * 2) : (n * -2) - 1; // value to be encoded
                n += 1; // always greater than zero

                if (n > 514229) throw new Exception($"Value out of bounds: {inValue} at index {idx}");

                // Fibonacci encode
                ulong res = 0UL;
                var maxidx = -1;

                // find starting position
                var i = 2;
                while (fseq[i] < n) { i++; }

                // scan backwards marking value bits
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
                    bytePos++;

                    if (bytePos > 7)
                    { // completed a byte (same as above)
                        int bv = (bf[0] & v[0]) | (bf[1] & v[1]) | (bf[2] & v[2]) | (bf[3] & v[3]) | (bf[4] & v[4]) | (bf[5] & v[5]) | (bf[6] & v[6]) | (bf[7] & v[7]);
                        output.WriteByte((byte)bv);
                        bf[0] = bf[1] = bf[2] = bf[3] = bf[4] = bf[5] = bf[6] = bf[7] = 0;
                        bytePos = 0;
                    }
                }
            }

            // If we didn't land on a byte boundary, push the last one out here
            if (bytePos != 0) { // completed a byte (slightly different to the others above)
                int bv = (bf[0] & v[0]) | (bf[1] & v[1]) | (bf[2] & v[2]) | (bf[3] & v[3]) | (bf[4] & v[4]) | (bf[5] & v[5]) | (bf[6] & v[6]) | (bf[7] & v[7]);
                output.WriteByte((byte)bv);
            }
        }

        /// <summary>
        /// Encode a single value to an open bitstream
        /// </summary>
        public static void FibonacciEncodeOne(uint value, BitwiseStreamWrapper output) {
            var n = value + 1;

            var res = new Stack<byte>(20);
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

            //res.Push(0); // add this to make a recoverable stream; and change `pos+2` to `pos+1` in FibonacciDecodeOne
            while (res.Count > 0) {
                output.WriteBit(res.Pop());
            }
        }

        /// <summary>
        /// Decode a single value from an open bitstream
        /// </summary>
        public static uint FibonacciDecodeOne(BitwiseStreamWrapper input) {
            bool lastWas1 = false;
            uint accum = 0;
            uint pos = 0;

            while (!input.IsEmpty()) {
                if (!input.TryReadBit(out var f)) break;
                if (f > 0) {
                    if (lastWas1) break;
                    lastWas1 = true;
                } else lastWas1 = false;

                accum += (uint)f * fibonacci(pos + 2);
                pos++;
            }

            return accum - 1;
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
        public static int[] SignedToUnsigned(float[] input)
        {
            var outp = new int[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                var n = (int)input[i];
                if (n >= 0) outp[i] = n * 2; // positive becomes even
                else outp[i] = (n * -2) - 1; // negative becomes odd
            }
            return outp;
        }

        public static int[] UnsignedToSigned(int[] input)
        {
            var outp = new int[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                if ((input[i] & 1) == 0) outp[i] = (int) (input[i] >> 1);
                else outp[i] = (int) (((input[i] + 1) >> 1) * -1);
            }
            return outp;
        }
        
        public static float[] UnsignedToSignedFloat(int[] input)
        {
            var outp = new float[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                if ((input[i] & 1) == 0) outp[i] = input[i] >> 1;
                else outp[i] = ((input[i] + 1) >> 1) * -1;
            }
            return outp;
        }

        public static bool EliasOmegaTryDecodeOne(BitwiseStreamWrapper src, out uint dest)
        {
            dest = 1;
            while (src.TryReadBit(out var b))
            {
                if (b == 0) {
                    dest--; // so we can encode zero
                    return true;
                }
                uint len = dest;
                dest = 1;

                for (int i = 0; i < len; i++)
                {
                    dest <<= 1;
                    var ok = src.TryReadBit(out b);
                    if (!ok) return false;
                    dest = dest | (uint)b;
                }
            }
            return false;
        }

        public static void EliasOmegaEncodeOne(uint src, BitwiseStreamWrapper dest)
        {
            src++; // so we can encode zero
            ulong stack = 0;
            var scount = 0;
            while (src > 1)
            {
                uint len = 0;
                for (uint tmp = src; tmp > 0; tmp >>= 1) len++; // 1 + floor(log₂(data))

                for (int i = 0; i < len; i++)
                {
                    stack |= ((src >> i) & 1) << scount;
                    scount++;
                }

                src = len - 1;
            }

            scount--;
            while (scount >= 0)
            {
                dest.WriteBit((int)((stack >> (scount)) & 1));
                scount--;
            }
            dest.WriteBit(0);
        }


        public static bool ByteBlockTryDecodeOne(BitwiseStreamWrapper src, out uint value)
        {
            value = 0;
            var ok = src.TryReadBit(out var b);
            if (!ok) return false;

            if (b == 0) { // one byte (7 bit data)
                for (int i = 0; i < 7; i++) {
                    value |= (uint)(src.ReadBit() << i);
                }
                return true;
            }
            
            ok = src.TryReadBit(out b);
            if (!ok) return false;
            if (b == 0) { // two byte (14 bits data)
                for (int i = 0; i < 14; i++) {
                    value |= (uint)(src.ReadBit() << i);
                }
                value += 127;
                return true;
            }
            
            //3 bytes (22 bit data)
            for (int i = 0; i < 22; i++) {
                value |= (uint)(src.ReadBit() << i);
            }
            value += 16384 + 127;
            return true;
        }

        public static void ByteBlockEncodeOne(uint value, BitwiseStreamWrapper dest)
        {
            if (value < 127) { // one byte (7 bits data)
                dest.WriteBit(0);
                for (int i = 0; i < 7; i++) {
                    dest.WriteBit((int) ((value >> i) & 1));
                }
                return;
            }

            value -= 127;

            if (value < 16384) { // two bytes (14 bits data)
                dest.WriteBit(1);
                dest.WriteBit(0);
                for (int i = 0; i < 14; i++) {
                    dest.WriteBit((int) ((value >> i) & 1));
                }
                return;
            }

            value -= 16384;

            // Otherwise 3 bytes (22 bit data)
            dest.WriteBit(1);
            dest.WriteBit(1);
            for (int i = 0; i < 22; i++)
            {
                dest.WriteBit((int)((value >> i) & 1));
            }
        }

        
        public static void ShortByteBlockEncodeOne(uint value, BitwiseStreamWrapper dest)
        {
            if (value == 0) {
                dest.WriteBit(0);
                return;
            }

            value--; // implied minimum size

            if (value < 32) { // one byte (6 bits data)
                dest.WriteBit(1);
                dest.WriteBit(0);
                for (int i = 0; i < 6; i++) {
                    dest.WriteBit((int) ((value >> i) & 1));
                }
                return;
            }

            value -= 31;

            if (value < 8192) { // two bytes (13 bits data)
                dest.WriteBit(1);
                dest.WriteBit(1);
                dest.WriteBit(0);
                for (int i = 0; i < 13; i++) {
                    dest.WriteBit((int) ((value >> i) & 1));
                }
                return;
            }

            value -= 8191;

            // Otherwise 3 bytes (21 bit data -- MAX)
            dest.WriteBit(1);
            dest.WriteBit(1);
            dest.WriteBit(1);
            for (int i = 0; i < 21; i++)
            {
                dest.WriteBit((int)((value >> i) & 1));
            }
        }
        
        public static bool ShortByteBlockTryDecodeOne(BitwiseStreamWrapper src, out uint value)
        {
            value = 0;
            var ok = src.TryReadBit(out var b);
            if (!ok) return false;
            
            if (b == 0) { // zero value
                return true;
            }

            ok = src.TryReadBit(out b);
            if (!ok) return false;

            if (b == 0) {  // one byte (6 bits data)
                for (int i = 0; i < 6; i++) {
                    value |= (uint)(src.ReadBit() << i);
                }
                value += 1;
                return true;
            }
            
            ok = src.TryReadBit(out b);
            if (!ok) return false;

            if (b == 0) { // two byte (13 bits data)
                for (int i = 0; i < 13; i++) {
                    value |= (uint)(src.ReadBit() << i);
                }
                value += 31 + 1;
                return true;
            }
            
            //3 bytes (21 bit data)
            for (int i = 0; i < 21; i++) {
                value |= (uint)(src.ReadBit() << i);
            }
            value += 8191 + 31 + 1;
            return true;
        }


        public static void ShortByteEncode(float[] input, int length, Stream output){
            if (output == null || input == null) throw new Exception("Null param");
            if (length < 1) length += input.Length;

            var dest = new BitwiseStreamWrapper(output, 1);
            for (int i = 0; i < length; i++)
            {
                // Signed to unsigned
                int n = (int)input[i];
                n = (n >= 0) ? (n * 2) : (n * -2) - 1; // value to be encoded

                ShortByteBlockEncodeOne((uint) n, dest);
            }
            dest.Flush();
        }

        public static void ShortByteDecode(Stream input, float[] output) {
            if (output == null || input == null) throw new Exception("Null param");
            var src = new BitwiseStreamWrapper(input, 32);
            var outidx = 0;

            while (src.CanRead()) {
                var ok = ShortByteBlockTryDecodeOne(src, out var n);
                if (!ok) {
                    Console.WriteLine($"Decode failed at step {outidx}");
                    break;
                }
                
                // unsigned to signed
                if ((n % 2) == 0) output[outidx++] = (int)(n >> 1);
                else output[outidx++] = (int)(((n + 1) >> 1) * -1);

                if (outidx >= output.Length) {
                    Console.WriteLine($"Overran output at step {outidx}");
                    break;
                }
            }
        }

        private const int CHECK_BLOCK_LENGTH = 256;

        /// <summary>
        /// Lossy encode floating point input (truncates to ints)
        /// Stores result in check-sum blocks
        /// </summary>
        public static void CheckBlockEncode(float[] input, int length, Stream output){
            if (output == null || input == null) throw new Exception("Null param");
            if (length < 1) length += input.Length;

            // we use a simple sum, so that long runs of zeros stay that way

            var dest = new BitwiseStreamWrapper(output, 1);
            uint sum = 0;
            var count = 0;
            for (int i = 0; i < length; i++)
            {
                // Signed to unsigned
                int v = (int)input[i];
                uint n = (uint)((v >= 0) ? (v * 2) : (v * -2) - 1); // value to be encoded

                sum += n;
                ShortByteBlockEncodeOne(n, dest);

                if (count++ >= CHECK_BLOCK_LENGTH) {
                    count = 0;

                    ShortByteBlockEncodeOne(sum, dest);
                    sum = 0;
                }
            }
            dest.Flush();
        }

        /// <summary>
        /// Lossy decode floating point input (truncates to ints)
        /// Any blocks that fail checksum are zeroed
        /// </summary>
        public static void CheckBlockDecode(Stream input, float[] output) {
            if (output == null || input == null) throw new Exception("Null param");
            var src = new BitwiseStreamWrapper(input, 32);
            var outidx = 0;

            // need to buffer blocks of output
            var buffer = new float[CHECK_BLOCK_LENGTH+2];

            uint sum = 0;
            var count = 0;
            while (src.CanRead()) {
                var ok = ShortByteBlockTryDecodeOne(src, out var n);
                if (!ok) { n = 0; }

                sum += n;

                // unsigned to signed
                if ((n % 2) == 0) buffer[count] = (int)(n >> 1);
                else buffer[count] = (int)(((n + 1) >> 1) * -1);
                
                if (count++ >= CHECK_BLOCK_LENGTH) {
                    count = 0;
                    
                    ok = ShortByteBlockTryDecodeOne(src, out var s); // don't output this one

                    if (ok && s == sum) { // checksum is good, copy values over
                        for (int i = 0; i <= CHECK_BLOCK_LENGTH; i++)
                        {
                            output[outidx++] = buffer[i];
                        }
                    } else { // failed check -- write zeros
                        for (int i = 0; i <= CHECK_BLOCK_LENGTH; i++)
                        {
                            output[outidx++] = 0;
                        }
                    }

                    sum = 0;
                }


                if (outidx + CHECK_BLOCK_LENGTH >= output.Length)
                {
                    Console.WriteLine($"Overran output at step {outidx}");
                    break;
                }
            }
        }

    }
}