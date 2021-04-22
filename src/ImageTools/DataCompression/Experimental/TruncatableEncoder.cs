﻿using System;
using System.IO;
using ImageTools.DataCompression.Encoding;
using ImageTools.ImageDataFormats;
// ReSharper disable InconsistentNaming
// ReSharper disable PossibleNullReferenceException

namespace ImageTools.DataCompression.Experimental
{
    /// <summary>
    /// A combination encoder that supports decoding truncated data streams.
    /// </summary>
    /// <remarks>
    /// This is based on the Arithmetic encoder, with a lot of defaults rolled in, and not using a termination symbol.
    /// We interleave a block-wise checksum value into the encoded data, and read this back during decode.
    /// If a block fails its checksum during decode, we end the decode early
    /// </remarks>
    public class TruncatableEncoder
    {
        /*
         Notes (assuming scale=4)
            
            Fib encode           -> 248.07kb
            Fib encode checksum  -> 266.43kb (256 sample)
            Fib encode checksum  -> 287.08kb (64 sample)
            
            Short byte     -> 271.23kb 
            Short byte cs  -> 289.35kb (256 sample)
            Short byte cs  -> 311.00kb (64 sample)
            Short byte cav -> 273.95kb

            Fib with cs in coder -> 270.04kb (64 sample)
            Fib with cs in coder -> 254.57kb (256 sample)             <-- using this one ( 2.6% overhead )
            Fib with cs in coder -> 255.06kb (256 sample, no update)
            
            */

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
        private const int CHECKSUM_BLOCK_SIZE = 256; // insert a checksum symbol after this many symbols
        
        private const int MAP_SIZE = 258; // Size of 'map' array
        private const int COUNT_ENTRY = 257; // Entry index for max count
        private const int END_SYMBOL = 256; // Symbol for end-of-data

        // 2nd order probability model:
        private ulong[,] map; // [from,to]
        private int lastSymbol;
        private bool[] frozen;
        public const ulong ProbabilityScale = 4; // how aggressively we grow the symbol probabilities

        /// <summary>
        /// Decode a stream to a target. Returns true if the end symbol was found, false if the stream was truncated
        /// </summary>
        public bool DecompressStream(Stream source, Stream destination) {
            if (source == null || source.CanRead == false) throw new Exception("Invalid input stream passed to encoder");
            if (destination == null || destination.CanWrite == false) throw new Exception("Invalid output stream passed to encoder");
            ResetModel();

            var src = new BitwiseStreamWrapper(source, BIT_SIZE * 2);
            var buffer = new byte[CHECKSUM_BLOCK_SIZE + 2];

            ulong high = MAX_CODE;
            ulong low = 0;
            ulong value = 0;
            var blockCount = 0;
            var checksum = 0;
            var ready = 0;

            // prime probability
            for ( int i = 0 ; i < CODE_VALUE_BITS ; i++ ) {
                value <<= 1;
                value += (ulong)src.ReadBit();
            }

            while (src.CanRead()) { // data loop

                // Decode probability to symbol
                var range = high - low + 1;
                var scale = map[lastSymbol, COUNT_ENTRY];
                var scaled_value = ((value - low + 1) * scale - 1) / range;
                int symbol = 0;
                var p = DecodeSymbol(scaled_value, ref symbol);
                if (p.terminates) break;

                // Do check-block work
                blockCount++;
                if (blockCount > CHECKSUM_BLOCK_SIZE) { // this is a checksum
                    var expected = checksum & 0xff;
                    if (symbol == expected) { // checksum is OK. Write buffer out
                        destination.Write(buffer, 0, ready);
                    } else { // truncation (we shouldn't write buffer)
                        return false;
                    }

                    // reset counts
                    blockCount = 0; checksum = 0; ready = 0;
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
            
            destination.Write(buffer, 0, ready); // this didn't get check-summed, but we hit the end-symbol ok.
            destination.Flush();
            return true;
        }


        public void CompressStream(Stream source, Stream destination)
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
            var moreData = true;

            while (moreData) // data loop
            {
                int c;

                SymbolProbability p;
                if (blockCount >= CHECKSUM_BLOCK_SIZE) {
                    // encode a check symbol
                    blockCount = 0;
                    c = checksum & 0xff;
                    checksum = 0;
                    p = EncodeSymbol(c);
                } else {
                    // encode an input symbol
                    c = source.ReadByte();
                    if (c < 0) // end of data, write termination
                    {
                        moreData = false;
                        p = EncodeTerminationSymbol();
                    }
                    else // normal data
                    {
                        p = EncodeSymbol(c);
                        checksum += c;
                        blockCount++;
                    }
                }

                // convert symbol to probability
                //var p = EncodeSymbol(c);
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
        
        private void ResetModel()
        {
            lastSymbol = 0;
            map = new ulong[MAP_SIZE,MAP_SIZE];
            frozen = new bool[MAP_SIZE];
            for (int i = 0; i < MAP_SIZE; i++)
            {
                frozen[i] = false;
                for (uint j = 0; j < MAP_SIZE; j++)
                {
                    map[i,j] = j;
                }
            }
        }
        
        private SymbolProbability EncodeTerminationSymbol()
        {
            var p = new SymbolProbability
            {
                low = map[lastSymbol,END_SYMBOL],
                high = map[lastSymbol,END_SYMBOL+1],
                count = map[lastSymbol, COUNT_ENTRY]
            };
            lastSymbol = END_SYMBOL;
            return p;
        }
        
        private SymbolProbability EncodeSymbol(int symbol)
        {
            var p = new SymbolProbability
            {
                low = map[lastSymbol,symbol],
                high = map[lastSymbol,symbol + 1],
                count = map[lastSymbol, COUNT_ENTRY]
            };
            UpdateModel(lastSymbol,symbol);
            lastSymbol = symbol;
            return p;
        }

        private void UpdateModel(int prev, int next)
        {
            if (frozen[prev]) return;

            const ulong max = ArithmeticEncode.MAX_FREQ / 3;
            if (map[prev, COUNT_ENTRY] > max) {
                frozen[prev] = true;
                return;
            }

            // TODO: this is currently the bottleneck of encode/decode
            // replace this with a sum-tree
            for (int i = next + 1; i < MAP_SIZE; i++) map[prev, i] += ProbabilityScale;
        }

        private SymbolProbability DecodeSymbol(ulong scaledValue, ref int decodedSymbol)
        {
            // Find the symbol with the highest value less-than-or-equal-to the scaled value.
            
            // Check range
            if (scaledValue > map[lastSymbol, COUNT_ENTRY]) return new SymbolProbability { terminates = true };
            
            // Binary search to find likely symbol
            var stride = 128;
            var idx = 128;
            while (stride > 0)
            {
                var cmp = map[lastSymbol, idx];
                if (scaledValue == cmp) {idx++; break; }

                if (scaledValue > cmp) idx += stride;
                else idx -= stride;
                stride >>= 1;
            }
            idx--;
            
            // double check
            if (scaledValue >= map[lastSymbol, idx + 1]) idx++;
            
            // update model and return symbol
            decodedSymbol = idx;
            if (idx == END_SYMBOL)  return new SymbolProbability { terminates = true }; 
            var p = new SymbolProbability
            {
                low = map[lastSymbol, idx],
                high = map[lastSymbol, idx + 1],
                count = map[lastSymbol, COUNT_ENTRY]
            };
            UpdateModel(lastSymbol, decodedSymbol);
            lastSymbol = decodedSymbol;
            return p;
        }
    }
}
