using System;
using System.IO;
using ImageTools.DataCompression.Encoding;
using ImageTools.ImageDataFormats;
// ReSharper disable InconsistentNaming
// ReSharper disable PossibleNullReferenceException
// ReSharper disable UnusedType.Global

namespace ImageTools.DataCompression.Experimental
{
    /// <summary>
    /// Arithmetic encoder with probability modelling
    /// </summary>
    public class ArithmeticEncoder2
    {
        // Arithmetic encoder values
        private const int BIT_SIZE = sizeof(long) * 8;
        private const int CODE_VALUE_BITS = BIT_SIZE / 2;
        private const int FREQUENCY_BITS = BIT_SIZE - CODE_VALUE_BITS;
        private const ulong MAX_CODE = (1ul << CODE_VALUE_BITS) - 1;
        private const ulong MAX_FREQ = (1ul << FREQUENCY_BITS) - 1;
        private const ulong ONE_QUARTER = 1ul << (CODE_VALUE_BITS - 2);
        private const ulong ONE_HALF = 2 * ONE_QUARTER;
        private const ulong THREE_QUARTERS = 3 * ONE_QUARTER;

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
        public bool DecompressStream(Stream source, ISymbolStream destination) {
            if (source == null || source.CanRead == false) throw new Exception("Invalid input stream passed to encoder");
            if (destination == null) throw new Exception("Invalid output stream passed to encoder");
            ResetModel();

            var src = new BitwiseStreamWrapper(source, BIT_SIZE * 2);

            ulong high  = MAX_CODE;
            ulong low   = 0;
            ulong value = 0;
            int   index = 0;

            // prime probability
            for ( int i = 0 ; i < CODE_VALUE_BITS ; i++ ) {
                value <<= 1;
                value += (ulong)src.ReadBit();
            }

            while (src.CanRead()) { // data loop

                // Decode probability to symbol
                var range        = high - low + 1;
                var scale        = _model.SymbolProbability(_lastSymbol, index).Total();
                var scaled_value = ((value - low + 1) * scale - 1) / range;

                var p = DecodeSymbol(scaled_value, index);
                if (p.terminates) break;
                var symbol = p.symbol;

                destination.WriteSymbol(symbol);
                index++;

                // Decode next symbol probability
                high = low + (range * p.high) / p.count - 1;
                low += (range * p.low) / p.count;

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
            
            destination.Flush();
            return true;
        }

        public void CompressStream(ISymbolStream source, Stream destination)
        {
            if (source == null) throw new Exception("Invalid input stream passed to encoder");
            if (destination == null || destination.CanWrite == false) throw new Exception("Invalid output stream passed to encoder");
            ResetModel();

            var target = new BitwiseStreamWrapper(destination, BIT_SIZE);

            ulong high         = MAX_CODE;
            ulong low          = 0;
            int   pending_bits = 0;
            int   index        = 0;
            var   moreData     = true;

            while (moreData) // data loop
            {
                SymbolProbability p;
                
                // encode an input symbol
                var c = source.ReadSymbol();
                if (c < 0) // end of data, write termination
                {
                    moreData = false;
                    p = EncodeSymbol(_model.EndSymbol(), index);
                }
                else // normal data
                {
                    p = EncodeSymbol(c, index);
                }

                index++;

                // convert symbol to probability
                var range = high - low + 1;
                high = low + (range * p.high / p.count) - 1;
                low += (range * p.low / p.count);

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
        
        private SymbolProbability EncodeSymbol(int symbol, int position)
        {
            var point = _model.SymbolProbability(_lastSymbol, position);
            
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

        private SymbolProbability DecodeSymbol(ulong scaledValue, int position)
        {
            var prob = _model.SymbolProbability(_lastSymbol, position);
            
            if (scaledValue > prob.Total()) return new SymbolProbability { terminates = true };
            
            var sym = prob.FindSymbol(scaledValue);
            UpdateModel(_lastSymbol, sym.symbol);
            _lastSymbol = sym.symbol;
            return sym;
        }
    }

    /// <summary>
    /// Interface for receiving or providing uncompressed symbols
    /// </summary>
    public interface ISymbolStream
    {
        /// <summary>
        /// Write a decoded symbol into the stream
        /// </summary>
        void WriteSymbol(int symbol);
        
        /// <summary>
        /// Decoding is complete
        /// </summary>
        void Flush();
        
        /// <summary>
        /// Read a symbol from the stream.
        /// Valid symbols must be zero or positive.
        /// Negative values are interpreted as end-of-stream
        /// </summary>
        int ReadSymbol();
    }

    /// <summary>
    /// Byte-symbol access over a dotnet Stream
    /// </summary>
    public class ByteSymbolStream : ISymbolStream
    {
        private readonly Stream _stream;

        public ByteSymbolStream(Stream stream)
        {
            _stream = stream;
        }
        
        public void WriteSymbol(int symbol)
        {
            _stream.WriteByte((byte)symbol);
        }

        public void Flush()
        {
            _stream.Flush();
        }

        public int ReadSymbol()
        {
            return _stream.ReadByte();
        }
    }
    
    /// <summary>
    /// 4-bit access over a dotnet Stream
    /// </summary>
    public class  NybbleSymbolStream : ISymbolStream
    {
        private readonly Stream _stream;
        private byte _half;
        private bool _flick;

        public NybbleSymbolStream(Stream stream)
        {
            _flick = false;
            _stream = stream;
        }
        
        public void WriteSymbol(int symbol)
        {
            if (_flick)
            {
                var v = (_half & 0x0F) | ((symbol & 0x0F) << 4);
                _stream.WriteByte((byte)v);
                _flick = false;
            }
            else
            {
                _half = (byte)(symbol & 0x0F);
                _flick = true;
            }
        }

        public void Flush()
        {
            _stream.Flush();
        }

        public int ReadSymbol()
        {
            if (_flick)
            {
                _flick = false;
                return _half;
            }
            else
            {
                var v = _stream.ReadByte();
                if (v < 0) return -1;
                _half = (byte)(v >> 4);
                _flick = true;
                return (v & 0x0F);
            }
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
        /// <param name="symbol">Previous symbol</param>
        /// <param name="position">Position of new symbol</param>
        public ISumTree SymbolProbability(int symbol, int position);
        
        /// <summary>
        /// Create, or reset, internal settings
        /// </summary>
        void Reset();

        /// <summary>
        /// Symbol to represent end-of-stream
        /// </summary>
        int EndSymbol();
    }

    /// <summary>
    /// Non-model, same probability for all symbols
    /// </summary>
    public class FlatModel_v2: IProbabilityModel2
    {
        private ISumTree _map;
        
        private readonly int _countEntry; // Entry index for max count
        private readonly int _endSymbol; // Symbol for end-of-data

        public FlatModel_v2(int symbolCount)
        {
            _endSymbol = symbolCount;
            _countEntry = _endSymbol + 1;
            
            _map = new FenwickTree(_countEntry, _endSymbol);
        }
        
        public void UpdateModel(int prev, int next, ulong max)
        {
            // No updates
        }

        public ISumTree SymbolProbability(int symbol, int position)
        {
            return _map;
        }

        public void Reset()
        {
        }

        public int EndSymbol()
        {
            return _endSymbol;
        }
    }
    
    /// <summary>
    /// Single probability tree, learning from incoming data
    /// </summary>
    public class SimpleLearningModel_v2: IProbabilityModel2
    {
        private readonly int _aggressiveness;
        private ISumTree _map;
        private bool _frozen;
        
        private readonly int _countEntry; // Entry index for max count
        private readonly int _endSymbol; // Symbol for end-of-data

        public SimpleLearningModel_v2(int symbolCount, int aggressiveness)
        {
            _endSymbol = symbolCount;
            _countEntry = _endSymbol + 1;
            
            _frozen = false;
            _map = new FenwickTree(_countEntry, _endSymbol);
            _aggressiveness = aggressiveness;
        }
        
        public void UpdateModel(int prev, int next, ulong max)
        {
            if (_frozen) return;

            var prob = _map;
            if (prob.Total() > max) {
                _frozen = true;
                return;
            }

            prob.IncrementSymbol(next, (ulong)_aggressiveness);
        }

        public ISumTree SymbolProbability(int symbol, int position)
        {
            return _map;
        }

        public void Reset()
        {
            _frozen = false;
            _map = new FenwickTree(_countEntry, _endSymbol);
        }

        public int EndSymbol()
        {
            return _endSymbol;
        }
    }
    
    /// <summary>
    /// 4-bit symbol model, with prefix probability table
    /// </summary>
    public class NybblePreScanModel : IProbabilityModel2
    {
        private FenwickTree _tree;
        private readonly int[] _histogram;

        /// <summary>
        /// Scan source data, and output probability tables to the destination stream.
        /// Probabilities add 16 bytes
        /// </summary>
        public NybblePreScanModel(byte[] src, MemoryStream dst)
        {
            _tree = new FenwickTree(18, 17);
            _histogram = new int[16];
            foreach (var b in src)
            {
                var upper = (b >> 4) & 0x0F;
                var lower = b & 0x0F;
                
                _histogram[upper]++;
                _histogram[lower]++;
            }
            
            // TODO: actual stuff
        }

        public void UpdateModel(int prev, int next, ulong max)
        {
            // Nothing. Is a fixed model
        }

        public ISumTree SymbolProbability(int symbol, int position)
        {
            return _tree;
        }

        public void Reset()
        {
            _tree = new FenwickTree(18, 17);
            for (int i = 0; i < _histogram.Length; i++)
            {
                var val = _histogram[i];
                if (val < 1) val = 1;
                _tree.IncrementSymbol(i, (ulong)val );
            }
        }

        public int EndSymbol()
        {
            return 17;
        }
    }
    
    /// <summary>
    /// 4-bit symbol model, with prefix probability table
    /// </summary>
    public class BytePreScanModel : IProbabilityModel2
    {
        private FenwickTree _tree;
        private readonly int[] _histogram;

        /// <summary>
        /// Scan source data, and output probability tables to the destination stream.
        /// Probabilities add 16 bytes
        /// </summary>
        public BytePreScanModel(byte[] src, MemoryStream dst)
        {
            _tree = new FenwickTree(257, 256);
            _histogram = new int[256];
            foreach (var b in src)
            {
                _histogram[b]++;
            }
            Reset();
            // TODO: write histogram to dst
        }

        public void UpdateModel(int prev, int next, ulong max)
        {
            // Nothing. Is a fixed model
        }

        public ISumTree SymbolProbability(int symbol, int position)
        {
            return _tree;
        }

        public void Reset()
        {
            _tree = new FenwickTree(257, 256);
            for (int i = 0; i < _histogram.Length; i++)
            {
                var val = _histogram[i];
                if (val < 1) val = 1;
                _tree.IncrementSymbol(i, (ulong)val );
            }
        }

        public int EndSymbol()
        {
            return 256;
        }
    }
    
    /// <summary>
    /// 2-stage byte-wise Markov chain
    /// </summary>
    public class Markov2D_v2: IProbabilityModel2
    {
        private readonly int        _aggressiveness;
        private readonly ISumTree[] _map; // 1 back => predicted next
        private readonly bool[]     _frozen;
        
        private readonly int _mapSize; // Size of 'map' array
        private readonly int _countEntry; // Entry index for max count
        private readonly int _endSymbol; // Symbol for end-of-data

        public Markov2D_v2(int symbolCount, int aggressiveness)
        {
            _endSymbol = symbolCount; // assuming symbols are dense, and start at zero
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
                Console.WriteLine($"Saturated leading symbol {prev:X2}");
                _frozen[prev] = true;
                return;
            }

            prob.IncrementSymbol(next, (ulong)_aggressiveness);
        }

        public ISumTree SymbolProbability(int symbol, int position)
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
        }

        public int EndSymbol()
        {
            return _endSymbol;
        }
    }
    
    /// <summary>
    /// 3-stage byte-wise Markov chain
    /// </summary>
    public class Markov3D_v2: IProbabilityModel2
    {
        private readonly int _aggressiveness;
        private readonly ISumTree[,] _map; // 2 back, 1 back => predicted next
        private readonly bool[] _frozen;
        
        private readonly int _mapSize; // Size of 'map' array
        private readonly int _countEntry; // Entry index for max count
        private readonly int _endSymbol; // Symbol for end-of-data
        
        private int _doublePrev;

        public Markov3D_v2(int symbolCount, int aggressiveness)
        {
            _endSymbol = symbolCount; // assuming symbols are dense, and start at zero
            _countEntry = _endSymbol + 1;
            _mapSize = _countEntry + 1;
            
            _map = new ISumTree[_mapSize,_mapSize];
            _frozen = new bool[_mapSize];
            _aggressiveness = aggressiveness;
        }
        
        public void UpdateModel(int prev, int next, ulong max)
        {
            if (_frozen[prev]) return;

            var prob = _map[_doublePrev, prev];
            if (prob.Total() > max) {
                Console.WriteLine($"Saturated leading symbol {prev:X2}");
                _frozen[prev] = true;
                return;
            }

            _doublePrev = prev;
            prob.IncrementSymbol(next, (ulong)_aggressiveness);
        }

        public ISumTree SymbolProbability(int symbol, int position)
        {
            return _map[_doublePrev, symbol];
        }

        public void Reset()
        {
            _doublePrev = 0;
            for (int i = 0; i < _mapSize; i++)
            {
                for (int j = 0; j < _mapSize; j++)
                {
                    _frozen[i] = false;
                    _map[i,j] = new FenwickTree(_countEntry, _endSymbol);
                }
            }
        }

        public int EndSymbol()
        {
            return _endSymbol;
        }
    }

    /// <summary>
    /// 2-stage Markov chain with byte index in context
    /// </summary>
    public class MarkovPos_v2: IProbabilityModel2
    {
        private readonly int _aggressiveness;
        private readonly ISumTree[,] _map; // Index, 1 back => predicted next
        private readonly bool[] _frozen;

        private readonly int _mapSize; // Size of 'map' array
        private readonly int _countEntry; // Entry index for max count
        private readonly int _endSymbol; // Symbol for end-of-data

        private int _lastIndex;

        public MarkovPos_v2(int symbolCount, int aggressiveness)
        {
            _endSymbol = symbolCount; // assuming symbols are dense, and start at zero
            _countEntry = _endSymbol + 1;
            _mapSize = _countEntry + 1;

            _map = new ISumTree[_mapSize,_mapSize];
            _frozen = new bool[_mapSize];
            _aggressiveness = aggressiveness;
        }

        public void UpdateModel(int prev, int next, ulong max)
        {
            if (_frozen[prev]) return;

            var prob = _map[_lastIndex, prev];
            if (prob.Total() > max) {
                Console.WriteLine($"Saturated leading symbol {prev:X2}");
                _frozen[prev] = true;
                return;
            }

            prob.IncrementSymbol(next, (ulong)_aggressiveness);
        }

        public ISumTree SymbolProbability(int symbol, int position)
        {
            _lastIndex = position & 0x0F;
            return _map[_lastIndex, symbol];
        }

        public void Reset()
        {
            _lastIndex = 0;
            for (int i = 0; i < _mapSize; i++)
            {
                for (int j = 0; j < _mapSize; j++)
                {
                    _frozen[i] = false;
                    _map[i,j] = new FenwickTree(_countEntry, _endSymbol);
                }
            }
        }

        public int EndSymbol()
        {
            return _endSymbol;
        }
    }

    /// <summary>
    /// 2-stage Markov chain with byte index in context, and context folding
    /// </summary>
    public class MarkovFoldPos_v2: IProbabilityModel2
    {
        private readonly int         _aggressiveness;
        private readonly ISumTree[,] _map; // Index, 1 back => predicted next
        private readonly bool[]      _frozen;

        private readonly int _countEntry; // Entry index for max count
        private readonly int _endSymbol; // Symbol for end-of-data

        private       int _lastIndex;
        private const int fold = 0x0F;

        public MarkovFoldPos_v2(int symbolCount, int aggressiveness)
        {
            _endSymbol = symbolCount; // assuming symbols are dense, and start at zero
            _countEntry = _endSymbol + 1;

            // all context is folded to 0x7F
            _map = new ISumTree[fold+1, fold+1];
            _frozen = new bool[fold+1];
            _aggressiveness = aggressiveness;
        }

        public void UpdateModel(int prev, int next, ulong max)
        {
            if (_frozen[prev&fold]) return;

            var prob = _map[_lastIndex&fold, prev&fold];
            if (prob.Total() > max) {
                Console.WriteLine($"Saturated leading symbol {prev:X2}");
                _frozen[prev] = true;
                return;
            }

            prob.IncrementSymbol(next, (ulong)_aggressiveness);
        }

        public ISumTree SymbolProbability(int symbol, int position)
        {
            _lastIndex = position;
            return _map[_lastIndex&fold, symbol&fold];
        }

        public void Reset()
        {
            _lastIndex = 0;
            for (int i = 0; i <= fold; i++)
            {
                for (int j = 0; j <= fold; j++)
                {
                    _frozen[i] = false;
                    _map[i,j] = new FenwickTree(_countEntry, _endSymbol);
                }
            }
        }

        public int EndSymbol()
        {
            return _endSymbol;
        }
    }
    
    /// <summary>
    /// 4-stage byte-wise Markov chain
    /// </summary>
    public class Markov4D_v2: IProbabilityModel2
    {
        private readonly int _aggressiveness;
        private readonly ISumTree[,,] _map; // 3, 2, 1 back => predicted next
        
        private readonly int _mapSize; // Size of 'map' array
        private readonly int _countEntry; // Entry index for max count
        private readonly int _endSymbol; // Symbol for end-of-data
        
        private int _doublePrev;
        private int _triplePrev;

        public Markov4D_v2(int symbolCount, int aggressiveness)
        {
            _endSymbol = symbolCount; // assuming symbols are dense, and start at zero
            _countEntry = _endSymbol + 1;
            _mapSize = _countEntry + 1;
            
            _map = new ISumTree[_mapSize,_mapSize,_mapSize];
            _aggressiveness = aggressiveness;
        }
        
        public void UpdateModel(int prev, int next, ulong max)
        {

            var prob = _map[_triplePrev, _doublePrev, prev];

            _triplePrev = _doublePrev;
            _doublePrev = prev;
            prob.IncrementSymbol(next, (ulong)_aggressiveness);
        }

        public ISumTree SymbolProbability(int symbol, int position)
        {
            return _map[_triplePrev, _doublePrev, symbol];
        }

        public void Reset()
        {
            _triplePrev = 0;
            _doublePrev = 0;
            for (int i = 0; i < _mapSize; i++)
            {
                for (int j = 0; j < _mapSize; j++)
                {
                    for (int k = 0; k < _mapSize; k++)
                    {
                        _map[i, j, k] = new FenwickTree(_countEntry, _endSymbol);
                    }
                }
            }
        }

        public int EndSymbol()
        {
            return _endSymbol;
        }
    }


    /// <summary>
    /// 4-stage byte-wise Markov chain with context folding
    /// </summary>
    public class Markov4DFold_v2: IProbabilityModel2
    {
        private readonly int          _aggressiveness;
        private readonly ISumTree[,,] _map; // 3, 2, 1 back => predicted next

        private readonly int _countEntry; // Entry index for max count
        private readonly int _endSymbol; // Symbol for end-of-data

        private int _doublePrev;
        private int _triplePrev;

        private const int fold = 0x0F;

        public Markov4DFold_v2(int symbolCount, int aggressiveness)
        {
            _endSymbol = symbolCount; // assuming symbols are dense, and start at zero
            _countEntry = _endSymbol + 1;

            _map = new ISumTree[fold+1,fold+1,fold+1];
            _aggressiveness = aggressiveness;
        }

        public void UpdateModel(int prev, int next, ulong max)
        {
            var prob = _map[_triplePrev & fold, _doublePrev & fold, prev & fold];

            _triplePrev = _doublePrev;
            _doublePrev = prev;
            prob.IncrementSymbol(next, (ulong)_aggressiveness);
        }

        public ISumTree SymbolProbability(int symbol, int position)
        {
            return _map[_triplePrev & fold, _doublePrev & fold, symbol & fold];
        }

        public void Reset()
        {
            _triplePrev = 0;
            _doublePrev = 0;
            for (int i = 0; i <= fold; i++)
            {
                for (int j = 0; j <= fold; j++)
                {
                    for (int k = 0; k <= fold; k++)
                    {
                        _map[i, j, k] = new FenwickTree(_countEntry, _endSymbol);
                    }
                }
            }
        }

        public int EndSymbol()
        {
            return _endSymbol;
        }
    }
}
