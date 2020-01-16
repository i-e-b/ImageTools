using System;
using System.Collections.Generic;
using System.IO;
using ImageTools.DataCompression.Encoding;
using ImageTools.ImageDataFormats;

namespace ImageTools.DataCompression.Experimental
{
    /// <summary>
    /// An integrated lossy encoder for float buffers
    /// </summary>
    /// <remarks>
    /// This converts floats to uints, then encodes them in sections
    /// </remarks>
    public class NybbleEncoder
    {

        // Arithmetic encoder values
        public const int BIT_SIZE = sizeof(long) * 8;
        public const int CODE_VALUE_BITS = BIT_SIZE / 2;
        public const int PRECISION = CODE_VALUE_BITS;
        public const int FREQUENCY_BITS = BIT_SIZE - CODE_VALUE_BITS;
        public const ulong MAX_CODE = (1ul << CODE_VALUE_BITS) - 1;
        public const ulong MAX_FREQ = (1ul << FREQUENCY_BITS) - 1;
        public const ulong ONE_QUARTER = 1ul << (CODE_VALUE_BITS - 2);
        public const ulong ONE_HALF = 2 * ONE_QUARTER;
        public const ulong THREE_QUARTERS = 3 * ONE_QUARTER;

        // Checksum block values
        private const int CHECKSUM_BLOCK_SIZE = 256; // insert a checksum symbol after this many symbols (NOT samples!)

        // 2nd order probability model:
        private ulong[,] map; // [from,to]
        private int lastSymbol;
        private bool[] frozen;

        private const int SampleEndSymbol = 16; // this is the end of the nybbles of a sample. A zero sample is *only* this.
        private const ulong ProbabilityScale = 4; // how agressively we grow the symbol probabilities


        private float UnsignedToFloat(int n) {
            if ((n & 1) == 0) return n >> 1;
            return ((n + 1) >> 1) * -1;
        }

        private int FloatToUnsigned(float f) { // up to 8 nybbles
            return (int)((f >= 0) ? (f * 2) : (f * -2) - 1); // value to be encoded
        }

        public void DecompressStream(Stream source, float[]  destination) {
            if (source == null || source.CanRead == false) throw new Exception("Invalid input stream passed to encoder");
            if (destination == null) throw new Exception("Invalid output passed to encoder");
            ResetModel();

            var src = new BitwiseStreamWrapper(source, BIT_SIZE * 2);
            var buffer = new byte[CHECKSUM_BLOCK_SIZE * 2]; // we might not empty this every block, so leave lots of space

            ulong high = MAX_CODE;
            ulong low = 0;
            ulong value = 0;
            var blockCount = 0;
            var checksum = 0;
            var ready = 0;
            var sampleIdx = 0;

            // prime probability
            for ( int i = 0 ; i < CODE_VALUE_BITS ; i++ ) {
                value <<= 1;
                value += (ulong)src.ReadBit();
            }

            while (src.CanRead()) { // data loop

                // Decode probability to symbol
                var range = high - low + 1;
                var scale = map[lastSymbol, 17];
                var scaled_value = ((value - low + 1) * scale - 1) / range;
                int symbol = 0;
                var p = DecodeSymbol(scaled_value, ref symbol);

                // Do check-block work
                blockCount++;
                if (blockCount > CHECKSUM_BLOCK_SIZE) { // this is a checksum
                    var expected = checksum & 0x0f;
                    if (symbol == expected) { // checksum is OK. Write buffer out
                        //destination.Write(buffer, 0, ready);
                        FeedSymbols(buffer, destination, ref ready, ref sampleIdx);
                    } else { // truncation (we shouldn't write buffer)
                        return;
                    }

                    // reset counts
                    blockCount = 0; checksum = 0;
                } else {
                    // this is a data symbol
                    buffer[ready++] = (byte)symbol; // delay writing until checksum
                    checksum += symbol;
                }


                // Decode next symbol probability
                high = low + (range * p.high) / p.count - 1;
                low = low + (range * p.low) / p.count;

                while (src.CanRead()) { // symbol decoding loop
                    if ( high < ONE_HALF ) {
                        //do nothing, bit is a zero
                    } else if ( low >= ONE_HALF ) {
                        value -= ONE_HALF;  //subtract one half from all three code values
                        low -= ONE_HALF;
                        high -= ONE_HALF;
                    } else if ( low >= ONE_QUARTER && high < THREE_QUARTERS ) {
                        value -= ONE_QUARTER;
                        low -= ONE_QUARTER;
                        high -= ONE_QUARTER;
                    } else {
                        break;
                    }

                    low <<= 1;
                    high <<= 1;
                    high++;
                    value <<= 1;
                    value += (ulong)src.ReadBit();

                } // end of symbol decoding loop
            } // end of data loop
        }


        public void CompressStream(float[] source, Stream dest) {

            CompressStreamInternal(ConvertToSymbols(source), dest);

        }

        private void CompressStreamInternal(IEnumerable<int> source, Stream destination)
        {
            if (source == null) throw new Exception("Invalid input stream passed to encoder");
            if (destination == null || destination.CanWrite == false) throw new Exception("Invalid output stream passed to encoder");
            ResetModel();

            var target = new BitwiseStreamWrapper(destination, BIT_SIZE);

            ulong high = MAX_CODE;
            ulong low = 0;
            int pending_bits = 0;
            var blockCount = 0;
            var checksum = 0;

            using (var symbol = source.GetEnumerator())
            {
                while (symbol.MoveNext()) // data loop
                {
                    var c = 0;

                    if (blockCount >= CHECKSUM_BLOCK_SIZE)
                    {
                        // encode a check symbol
                        blockCount = 0;
                        c = checksum & 0x0f;
                        checksum = 0;
                    }
                    else
                    {
                        // encode an input symbol
                        c = symbol.Current;
                        checksum += c;
                        blockCount++;
                    }

                    // convert symbol to probability
                    var p = EncodeSymbol(c);
                    var range = high - low + 1;
                    high = low + (range * p.high / p.count) - 1;
                    low = low + (range * p.low / p.count);

                    while (true) // symbol encoding loop
                    {
                        if (high < ONE_HALF)
                        { // Converging
                            output_bit_plus_pending(target, 0, ref pending_bits);
                        }
                        else if (low >= ONE_HALF)
                        { // Converging
                            output_bit_plus_pending(target, 1, ref pending_bits);
                        }
                        else if (low >= ONE_QUARTER && high < THREE_QUARTERS)
                        { // Near converging
                            pending_bits++;
                            low -= ONE_QUARTER;
                            high -= ONE_QUARTER;
                        }
                        else
                        { // Not converging. Move to next input
                            break;
                        }
                        high <<= 1;
                        high++;
                        low <<= 1;
                        high &= MAX_CODE;
                        low &= MAX_CODE;
                    } // end of symbol encoding loop
                } // end of data loop
            }
            // Ensure we pump out enough bits to have an unambiguous result
            pending_bits++;
            if (low < ONE_QUARTER) output_bit_plus_pending(target, 0, ref pending_bits);
            else output_bit_plus_pending(target, 1, ref pending_bits);

            // Done. Write out final data
            target.Flush();
        }

        void output_bit_plus_pending(BitwiseStreamWrapper target, int bit, ref int pending_bits)
        {
            target.WriteBit( bit == 1 );
            while ( pending_bits-- > 0 ) target.WriteBit( bit == 0 );
            pending_bits = 0;
        }

        
        /// <summary>
        /// Float samples to the nybble symbols
        /// </summary>
        private IEnumerable<int> ConvertToSymbols(float[] source)
        {
            foreach (var f in source)
            {
                var u = FloatToUnsigned(f);
                while (u > 0) {
                    yield return u & 0x0f;
                    u >>= 4;
                }
                yield return SampleEndSymbol;
            }
        }
        
        /// <summary>
        /// Nybble symbols to float samples (reverses ConvertToSymbols)
        /// </summary>
        private void FeedSymbols(byte[] symbols, float[] samples, ref int symbolCount, ref int sampleIdx)
        {
            var unused = 0; // prevent drop-out across checksum boundaries
            var accum = 0;

            int i = 0;
            for (; i < symbolCount; i++)
            {
                if (symbols[i] == SampleEndSymbol) {
                    samples[sampleIdx] = accum;
                    sampleIdx++;
                    accum = 0;
                    unused = 0;
                } else {
                    accum = (accum << 4) + symbols[i];
                    unused++;
                }
            }

            symbolCount = unused;
            var offs = (i - 1) - unused;
            while (unused > 0) {
                // push back to start
                symbols[unused - 1] = symbols[unused + offs];
            }
        }
        
        private void ResetModel()
        {
            lastSymbol = 0;
            map = new ulong[18,18];
            frozen = new bool[18];
            for (int i = 0; i < 18; i++)
            {
                frozen[i] = false;
                for (uint j = 0; j < 18; j++)
                {
                    map[i,j] = j;
                }
            }
        }
        
        private SymbolProbability EncodeSymbol(int symbol)
        {
            if (symbol > 16) throw new Exception($"Symbol out of range. Should be 0..15, got {symbol}");
            var p = new SymbolProbability
            {
                low = map[lastSymbol,symbol],
                high = map[lastSymbol,symbol + 1],
                count = map[lastSymbol, 17]
            };
            UpdateModel(lastSymbol,symbol);
            lastSymbol = symbol;
            return p;
        }

        private void UpdateModel(int prev, int next)
        {
            if (frozen[prev]) return;

            const ulong max = ArithmeticEncode.MAX_FREQ / 3;
            if (map[prev, 17] > max) {
                frozen[prev] = true;
                return;
            }

            for (int i = next + 1; i < 18; i++) map[prev, i] += ProbabilityScale;
        }

        private SymbolProbability DecodeSymbol(ulong scaledValue, ref int decodedSymbol)
        {
            for (int i = 0; i < 17; i++)
                if (scaledValue < map[lastSymbol, i + 1])
                {
                    decodedSymbol = i;
                    var p = new SymbolProbability
                    {
                        low = map[lastSymbol, i],
                        high = map[lastSymbol, i + 1],
                        count = map[lastSymbol, 17]
                    };
                    UpdateModel(lastSymbol, decodedSymbol);
                    lastSymbol = decodedSymbol;
                    return p;
                }
            throw new Exception("Decoder model found no symbol range for scaled value = " + scaledValue);
        }
    }
}