using System;
using System.IO;
using ImageTools.DataCompression.Encoding;
using ImageTools.ImageDataFormats;
// ReSharper disable InconsistentNaming
// ReSharper disable PossibleNullReferenceException

namespace ImageTools.DataCompression.Experimental
{
    /// <summary>
    /// Arithmetic encoder with probability modelling
    /// </summary>
    public class ArithmeticEncoder2
    {
        // Arithmetic encoder values
        public const int BIT_SIZE = sizeof(long) * 8;
        public const int CODE_VALUE_BITS = BIT_SIZE / 2;
        public const int FREQUENCY_BITS = BIT_SIZE - CODE_VALUE_BITS;
        public const ulong MAX_CODE = (1ul << CODE_VALUE_BITS) - 1;
        public const ulong MAX_FREQ = (1ul << FREQUENCY_BITS) - 1;
        public const ulong ONE_QUARTER = 1ul << (CODE_VALUE_BITS - 2);
        public const ulong ONE_HALF = 2 * ONE_QUARTER;
        public const ulong THREE_QUARTERS = 3 * ONE_QUARTER;

        // Checksum block values
        private const int CHECKSUM_BLOCK_SIZE = 256; // insert a checksum symbol after this many symbols

        // 2nd order probability model:
        private readonly IProbabilityModel2 _model;
        private int _lastSymbol;

        /// <summary>
        /// Start a new AE2 transcoder.
        /// </summary>
        /// <param name="model">Probability model</param>
        public ArithmeticEncoder2(IProbabilityModel2 model)
        {
            _model = model;
        }
        
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
                var scale = _model.SymbolProbability(_lastSymbol).Total(); //map[lastSymbol].Total();
                var scaled_value = ((value - low + 1) * scale - 1) / range;
                var p = DecodeSymbol(scaled_value);
                if (p.terminates) break;
                var symbol = p.symbol;

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
                        p = EncodeSymbol(_model.EndSymbol());
                    }
                    else // normal data
                    {
                        p = EncodeSymbol(c);
                        checksum += c;
                        blockCount++;
                    }
                }

                // convert symbol to probability
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

        private static void output_bit_plus_pending(BitwiseStreamWrapper target, int bit, ref int pending_bits)
        {
            target.WriteBit( bit == 1 );
            while ( pending_bits-- > 0 ) target.WriteBit( bit == 0 );
            pending_bits = 0;
        }
        
        private void ResetModel()
        {
            _lastSymbol = 0;
            _model.Reset();
        }
        
        private SymbolProbability EncodeSymbol(int symbol)
        {
            var point = _model.SymbolProbability(_lastSymbol); //map[lastSymbol];
            
            var prob = point.EncodeSymbol(symbol);
            UpdateModel(_lastSymbol,symbol);
            _lastSymbol = symbol;
            return prob;
        }

        private void UpdateModel(int prev, int next)
        {
            const ulong max = MAX_FREQ / 3;
            
            _model.UpdateModel(prev, next, max);
        }

        private SymbolProbability DecodeSymbol(ulong scaledValue)
        {
            var prob = _model.SymbolProbability(_lastSymbol);//map[lastSymbol];
            
            if (scaledValue > prob.Total()) return new SymbolProbability { terminates = true };
            
            var sym = prob.FindSymbol(scaledValue);
            UpdateModel(_lastSymbol, sym.symbol);
            _lastSymbol = sym.symbol;
            return sym;
        }
    }

    /// <summary>
    /// Model for ArithmeticEncoder2
    /// </summary>
    public interface IProbabilityModel2
    {
        /// <summary>
        /// Feed incoming data. Symbol 'next' has followed symbol 'prev'
        /// </summary>
        public void UpdateModel(int prev, int next, ulong max);
        
        /// <summary>
        /// Return a probability tree appropriate for the given previous symbol
        /// </summary>
        public ISumTree SymbolProbability(int symbol);
        
        /// <summary>
        /// Create, or reset, internal settings
        /// </summary>
        void Reset();

        /// <summary>
        /// Symbol to represent end-of-stream
        /// </summary>
        int EndSymbol();
    }

    public class Markov2D_v2: IProbabilityModel2
    {
        private readonly int _aggressiveness;
        private readonly ISumTree[] _map;
        private readonly bool[] _frozen;
        
        private readonly int _mapSize; // Size of 'map' array
        private readonly int _countEntry; // Entry index for max count
        private readonly int _endSymbol; // Symbol for end-of-data

        public Markov2D_v2(int symbolCount, int aggressiveness)
        {
            _endSymbol = symbolCount + 1;
            _countEntry = _endSymbol + 1;
            _mapSize = _countEntry + 1;
            
            _map = new ISumTree[_mapSize];
            _frozen = new bool[_mapSize];
            _aggressiveness = aggressiveness;
        }
        
        public void UpdateModel(int prev, int next, ulong max)
        {
            if (_frozen[prev]) return;

            var prob = _map[prev];
            if (prob.Total() > max) {
                _frozen[prev] = true;
                return;
            }

            prob.IncrementSymbol(next, (ulong)_aggressiveness);
        }

        public ISumTree SymbolProbability(int symbol)
        {
            return _map[symbol];
        }

        public void Reset()
        {
            for (int i = 0; i < _mapSize; i++)
            {
                _frozen[i] = false;
                _map[i] = new FenwickTree(_countEntry, _endSymbol);

            }
            // Assume runs of zeros
            _map[0].IncrementSymbol(0, 1000);
        }

        public int EndSymbol()
        {
            return _endSymbol;
        }
    }
}
