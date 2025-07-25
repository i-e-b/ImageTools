﻿namespace ImageTools.DataCompression.Encoding
{
    /// <summary>
    /// Container for various Arithmetic Encoder models
    /// </summary>
    public static class ProbabilityModels
    {
        /// <summary>
        /// Model probability based on most recent symbols
        /// </summary>
        public class PushToFrontModel : IProbabilityModel
        {
            private readonly int[] _symbols;
            private readonly ulong[] _cumlFreq;
            private readonly ulong _total;

            public PushToFrontModel(int fallOff = 3)
            {
                _symbols = new int[257]; // the closer to the front, the higher the expected probability
                _cumlFreq = new ulong[258]; // cumulative frequencies for the positions

                uint sum = 0;
                uint prob = 0x7000;
                for (int i = 0; i < 258; i++)
                {
                    _cumlFreq[i] = sum;
                    sum += prob;
                    prob = (prob >> fallOff) | 1;
                }
                _total = _cumlFreq[257];

                Reset();
            }

            /// <inheritdoc />
            public SymbolProbability GetCurrentProbability(int symbol)
            {
                // build probability from *current* state
                // update expected array (push to front)

                for (int i = 0; i < 257; i++)
                {
                    if (symbol != _symbols[i]) continue;

                    var p = new SymbolProbability
                    {
                        low = _cumlFreq[i],
                        high = _cumlFreq[i + 1],
                        count = _total
                    };
                    Update(i);
                    return p;
                }
                throw new Exception("Encode model could not encode symbol value = " + symbol);
            }

            private void Update(int i)
            {
                // pull value at `i` to the front, push other values back
                if (i == 0) return; // already at front.

                var tmp = _symbols[i];
                for (int j = i - 1; j >= 0; j--) { _symbols[j + 1] = _symbols[j]; } // shift right
                _symbols[0] = tmp; // set at head
            }

            /// <inheritdoc />
            public SymbolProbability GetChar(ulong scaledValue, ref int decodedSymbol)
            {

                for (int i = 0; i < 257; i++)
                {
                    if (scaledValue >= _cumlFreq[i + 1]) continue;
                    decodedSymbol = _symbols[i];
                    var p = new SymbolProbability
                    {
                        low = _cumlFreq[i],
                        high = _cumlFreq[i + 1],
                        count = _total
                    };
                    Update(i);
                    return p;
                }
                throw new Exception("Decode model could not find symbol for value = " + scaledValue);
            }

            /// <inheritdoc />
            public void Reset()
            {
                // initial state
                for (int i = 0; i < 257; i++) { _symbols[i] = i; }
            }

            /// <inheritdoc />
            public ulong GetCount() { return _total; }

            /// <inheritdoc />
            public int RequiredSymbolBits() { return 13; }

            /// <inheritdoc />
            public byte[] Preamble()
            {
                return Array.Empty<byte>();
            }

            /// <inheritdoc />
            public void WritePreamble(Stream dest) { }

            /// <inheritdoc />
            public void ReadPreamble(Stream src) { }
        }

        /// <summary>
        /// Model based on histogram of data seen so far
        /// </summary>
        public class SimpleLearningModel : IProbabilityModel
        {
            private readonly ulong _gain;
            private readonly ulong[] cumulative_frequency;
            private bool _frozen;

            public SimpleLearningModel(ulong gain = 2)
            {
                _gain = gain;
                cumulative_frequency = new ulong[258];
                Reset();
            }

            /// <inheritdoc />
            public SymbolProbability GetCurrentProbability(int symbol)
            {
                var p = new SymbolProbability
                {
                    low = cumulative_frequency[symbol],
                    high = cumulative_frequency[symbol + 1],
                    count = cumulative_frequency[257]
                };
                Update(symbol);
                return p;
            }

            private void Update(int c)
            {
                if (_frozen) return; // model is saturated

                for (int i = c + 1; i < 258; i++) cumulative_frequency[i] += _gain;

                if (cumulative_frequency[257] < (ArithmeticEncode.MAX_FREQ - 258)) return;

                Console.WriteLine("Ran out of model precision. Will freeze probabilities.");
                _frozen = true;
            }

            /// <inheritdoc />
            public SymbolProbability GetChar(ulong scaledValue, ref int decodedSymbol)
            {
                for (int i = 0; i < 257; i++)
                    if (scaledValue < cumulative_frequency[i + 1])
                    {
                        decodedSymbol = i;
                        var p = new SymbolProbability
                        {
                            low = cumulative_frequency[i],
                            high = cumulative_frequency[i + 1],
                            count = cumulative_frequency[257]
                        };
                        Update(decodedSymbol);
                        return p;
                    }
                throw new Exception("Decoder model found no symbol range for scaled value = " + scaledValue);
            }

            /// <inheritdoc />
            public void Reset()
            {
                // start with all code points equally likely
                for (uint i = 0; i < 258; i++) cumulative_frequency[i] = i;
                _frozen = false;
            }

            /// <inheritdoc />
            public ulong GetCount()
            {
                return cumulative_frequency[257];
            }

            /// <inheritdoc />
            public int RequiredSymbolBits() { return 9; }

            /// <inheritdoc />
            public byte[] Preamble()
            {
                return Array.Empty<byte>();
            }

            /// <inheritdoc />
            public void WritePreamble(Stream dest) { }

            /// <inheritdoc />
            public void ReadPreamble(Stream src) { }
        }
        
        /// <summary>
        /// Model based on histogram of data seen so far,
        /// supporting varying count of symbols 
        /// </summary>
        public class LearningModel : IProbabilityModel
        {
            private readonly int _symbolCount;
            private readonly int _countIdx;
            private readonly int _freqCount;
            
            private bool _frozen;
            
            private readonly ulong _gain;
            private readonly ulong[] cumulative_frequency;

            public LearningModel(int symbolCount, ulong gain)
            {
                _symbolCount = symbolCount;
                _countIdx = _symbolCount + 1;
                _freqCount = _symbolCount + 2;
                _gain = gain;
                cumulative_frequency = new ulong[_freqCount];
                Reset();
            }

            /// <inheritdoc />
            public SymbolProbability GetCurrentProbability(int symbol)
            {
                var p = new SymbolProbability
                {
                    low = cumulative_frequency[symbol],
                    high = cumulative_frequency[symbol + 1],
                    count = cumulative_frequency[_countIdx]
                };
                Update(symbol);
                return p;
            }

            private void Update(int c)
            {
                if (_frozen) return; // model is saturated

                for (int i = c + 1; i < _freqCount; i++) cumulative_frequency[i] += _gain;

                if (cumulative_frequency[_countIdx] < (ArithmeticEncode.MAX_FREQ - (ulong)_freqCount)) return;

                Console.WriteLine("Ran out of model precision. Will freeze probabilities.");
                _frozen = true;
            }

            /// <inheritdoc />
            public SymbolProbability GetChar(ulong scaledValue, ref int decodedSymbol)
            {
                for (int i = 0; i < _countIdx; i++)
                    if (scaledValue < cumulative_frequency[i + 1])
                    {
                        decodedSymbol = i;
                        var p = new SymbolProbability
                        {
                            low = cumulative_frequency[i],
                            high = cumulative_frequency[i + 1],
                            count = cumulative_frequency[_countIdx]
                        };
                        Update(decodedSymbol);
                        return p;
                    }
                throw new Exception("Decoder model found no symbol range for scaled value = " + scaledValue);
            }

            /// <inheritdoc />
            public void Reset()
            {
                // start with all code points equally likely
                for (uint i = 0; i < _freqCount; i++) cumulative_frequency[i] = i;
                _frozen = false;
            }

            /// <inheritdoc />
            public ulong GetCount()
            {
                return cumulative_frequency[_countIdx];
            }

            /// <inheritdoc />
            public int RequiredSymbolBits() { return 16; }

            /// <inheritdoc />
            public byte[] Preamble()
            {
                return Array.Empty<byte>();
            }

            /// <inheritdoc />
            public void WritePreamble(Stream dest) { }

            /// <inheritdoc />
            public void ReadPreamble(Stream src) { }
        }

        /// <summary>
        /// A model that never updates, and treats 0 as the most likely symbol
        /// </summary>
        public class BraindeadModel : IProbabilityModel
        {
            private readonly ulong[] cumulative_frequency;

            public BraindeadModel()
            {
                cumulative_frequency = new ulong[258];
                Reset();
            }

            /// <inheritdoc />
            public SymbolProbability GetCurrentProbability(int symbol)
            {
                var p = new SymbolProbability
                {
                    low = cumulative_frequency[symbol],
                    high = cumulative_frequency[symbol + 1],
                    count = cumulative_frequency[257]
                };
                return p;
            }

            /// <inheritdoc />
            public SymbolProbability GetChar(ulong scaledValue, ref int decodedSymbol)
            {
                for (int i = 0; i < 257; i++)
                    if (scaledValue < cumulative_frequency[i + 1])
                    {
                        decodedSymbol = i;
                        var p = new SymbolProbability
                        {
                            low = cumulative_frequency[i],
                            high = cumulative_frequency[i + 1],
                            count = cumulative_frequency[257]
                        };
                        return p;
                    }
                throw new Exception("Decoder model found no symbol range for scaled value = " + scaledValue);
            }

            /// <inheritdoc />
            public void Reset()
            {
                cumulative_frequency[0] = 0;
                for (uint i = 1; i < 258; i++) cumulative_frequency[i] = i + 128;
            }

            /// <inheritdoc />
            public ulong GetCount()
            {
                return cumulative_frequency[257];
            }

            /// <inheritdoc />
            public int RequiredSymbolBits() { return 9; }

            /// <inheritdoc />
            public byte[] Preamble()
            {
                return Array.Empty<byte>();
            }

            /// <inheritdoc />
            public void WritePreamble(Stream dest) { }

            /// <inheritdoc />
            public void ReadPreamble(Stream src) { }

            // 8 bits for values, 1 for stop
        }

        /// <summary>
        /// A simple table-based markov predictor. Predicts based on current value and 1 previous value
        /// </summary>
        public class LearningMarkov_2D : IProbabilityModel
        {
            private readonly int _leadInBytes;
            private readonly ulong _gain;

            /// <summary>
            /// the map is treated as an independent set of cumulative probabilities.
            /// </summary>
            private ulong[,] map; // [from,to]
            private int lastSymbol;
            private bool[] frozen;
            private int leadIn;

            /// <summary>
            /// Create a new order-2 model
            /// </summary>
            /// <param name="leadInBytes">If greater than zero, this many bytes are ignored for learning at the start of the data. This prevents preamble poisoning.</param>
            public LearningMarkov_2D(int leadInBytes = 0, ulong gain = 2)
            {
                map = new ulong[258,258];
                frozen = new bool[258];
                _leadInBytes = leadInBytes;
                _gain = gain;
                Reset();
            }

            /// <inheritdoc />
            public SymbolProbability GetCurrentProbability(int symbol)
            {
                var p = new SymbolProbability
                {
                    low = map[lastSymbol,symbol],
                    high = map[lastSymbol,symbol + 1],
                    count = map[lastSymbol, 257]
                };
                Update(lastSymbol,symbol);
                lastSymbol = symbol;
                return p;
            }

            private void Update(int prev, int next)
            {
                if (frozen[prev]) return;
                if (leadIn-- > 0) return;

                const ulong max = ArithmeticEncode.MAX_FREQ / 3;
                if (map[prev, 257] > max) {
                    frozen[prev] = true;
                    return;
                }

                for (int i = next + 1; i < 258; i++) map[prev, i] += _gain;
            }

            /// <inheritdoc />
            public SymbolProbability GetChar(ulong scaledValue, ref int decodedSymbol)
            {
                for (int i = 0; i < 257; i++)
                    if (scaledValue < map[lastSymbol, i + 1])
                    {
                        decodedSymbol = i;
                        var p = new SymbolProbability
                        {
                            low = map[lastSymbol, i],
                            high = map[lastSymbol, i + 1],
                            count = map[lastSymbol, 257]
                        };
                        Update(lastSymbol, decodedSymbol);
                        lastSymbol = decodedSymbol;
                        return p;
                    }
                throw new Exception("Decoder model found no symbol range for scaled value = " + scaledValue);
            }

            /// <inheritdoc />
            public void Reset()
            {
                lastSymbol = 0;
                leadIn = _leadInBytes;
                for (int i = 0; i < 258; i++)
                {
                    frozen[i] = false;
                    for (uint j = 0; j < 258; j++)
                    {
                        map[i,j] = j;
                    }
                }
            }

            /// <inheritdoc />
            public ulong GetCount()
            {
                return map[lastSymbol, 257];
            }

            /// <inheritdoc />
            public int RequiredSymbolBits() { return 11; }

            /// <inheritdoc />
            public byte[] Preamble() { return Array.Empty<byte>(); }

            /// <inheritdoc />
            public void WritePreamble(Stream dest) { }

            /// <inheritdoc />
            public void ReadPreamble(Stream src) { }
        }

        /// <summary>
        /// A simple table-based markov predictor. Predicts based on a rolling hash of previous 4 values
        /// </summary>
        public class LearningMarkov_2DHn : IProbabilityModel
        {
            private readonly int _leadInBytes;
            private readonly int _gain;

            /// <summary>
            /// the map is treated as an independent set of cumulative probabilities.
            /// </summary>
            private ulong[,] map; // [from,to]
            private int lastSymbol;
            private int[] hashEntries;
            private bool[] frozen;
            private int leadIn;

            /// <summary>
            /// Create a new order-2 model
            /// </summary>
            /// <param name="leadInBytes">If greater than zero, this many bytes are ignored for learning at the start of the data. This prevents preamble poisoning.</param>
            /// <param name="predictionWidth">Number of symbols to include in prediction. Try 1..8</param>
            public LearningMarkov_2DHn(int leadInBytes, int predictionWidth, int gain)
            {
                map = new ulong[258,258];
                frozen = new bool[258];
                hashEntries = new int[predictionWidth]; // relies on being set to all zeros
                _leadInBytes = leadInBytes;
                _gain = gain;
                Reset();
            }

            /// <inheritdoc />
            public SymbolProbability GetCurrentProbability(int symbol)
            {
                var p = new SymbolProbability
                {
                    low = map[lastSymbol,symbol],
                    high = map[lastSymbol,symbol + 1],
                    count = map[lastSymbol, 257]
                };
                Update(lastSymbol,symbol);
                lastSymbol = UpdateHash(symbol);
                return p;
            }

            /// <summary>
            /// Roll the hash
            /// </summary>
            private int UpdateHash(int symbol)
            {
                var result = symbol;
                var end = hashEntries.Length-1;
                for (int i = 1; i < end; i++)
                {
                    result ^= hashEntries[i];
                    hashEntries[i] = hashEntries[i+1];
                }
                hashEntries[end] = symbol;
                return result;
            }

            /// <summary>
            /// update probability map based on data
            /// </summary>
            private void Update(int prev, int next)
            {
                if (frozen[prev]) return;
                if (leadIn-- > 0) return;

                const ulong max = ArithmeticEncode.MAX_FREQ / 3;
                const ulong high = max / 3;
                if (map[prev, 257] > max) {
                    frozen[prev] = true;
                    return;
                }

                var gain = (ulong)_gain;
                if (map[prev, 257] > high) { gain = 1; }

                for (int i = next + 1; i < 258; i++) map[prev, i] += gain;
            }

            /// <inheritdoc />
            public SymbolProbability GetChar(ulong scaledValue, ref int decodedSymbol)
            {
                for (int i = 0; i < 257; i++)
                    if (scaledValue < map[lastSymbol, i + 1])
                    {
                        decodedSymbol = i;
                        var p = new SymbolProbability
                        {
                            low = map[lastSymbol, i],
                            high = map[lastSymbol, i + 1],
                            count = map[lastSymbol, 257]
                        };
                        Update(lastSymbol, decodedSymbol);
                        lastSymbol = UpdateHash(decodedSymbol);
                        return p;
                    }
                throw new Exception("Decoder model found no symbol range for scaled value = " + scaledValue);
            }

            /// <inheritdoc />
            public void Reset()
            {
                lastSymbol = 0;
                leadIn = _leadInBytes;
                for (int i = 0; i < hashEntries.Length; i++) { hashEntries[i] = 0; }
                
                for (int i = 0; i < 258; i++)
                {
                    frozen[i] = false;
                    for (uint j = 0; j < 258; j++)
                    {
                        map[i,j] = j;
                    }
                }
            }

            /// <inheritdoc />
            public ulong GetCount()
            {
                return map[lastSymbol, 257];
            }

            /// <inheritdoc />
            public int RequiredSymbolBits() { return 11; }

            /// <inheritdoc />
            public byte[] Preamble() { return Array.Empty<byte>(); }

            /// <inheritdoc />
            public void WritePreamble(Stream dest) { }

            /// <inheritdoc />
            public void ReadPreamble(Stream src) { }
        }
        
        /// <summary>
        /// A table-based markov predictor. Predicts based on 2 previous values
        /// </summary>
        public class LearningMarkov_3D : IProbabilityModel
        {
            private readonly int _leadInBytes;

            /// <summary>
            /// the map is treated as an independent set of cumulative probabilities.
            /// </summary>
            private ulong[,,]? map; // [lsA, lsB, cuml prob]
            private int lsA, lsB;
            private bool[,]? frozen;
            private int leadIn;

            /// <summary>
            /// Create a new order-3 model
            /// </summary>
            /// <param name="leadInBytes">If greater than zero, this many bytes are ignored for learning at the start of the data. This prevents preamble poisoning.</param>
            public LearningMarkov_3D(int leadInBytes = 0)
            {
                _leadInBytes = leadInBytes;
                Reset();
            }

            /// <inheritdoc />
            public SymbolProbability GetCurrentProbability(int symbol)
            {
                var p = new SymbolProbability
                {
                    low = map[lsA,lsB,symbol],
                    high = map[lsA,lsB,symbol + 1],
                    count = map[lsA,lsB, 257]
                };
                Update(lsA, lsB, symbol);
                lsA = lsB;
                lsB = symbol;
                return p;
            }

            private void Update(int prev1, int prev2, int next)
            {
                if (frozen[prev1, prev2]) return;
                if (leadIn-- > 0) return;

                const ulong max = ArithmeticEncode.MAX_FREQ / 3;
                if (map[prev1, prev2, 257] > max) {
                    frozen[prev1, prev2] = true;
                    return;
                }

                for (int i = next + 1; i < 258; i++) map[prev1, prev2, i]++;
            }

            /// <inheritdoc />
            public SymbolProbability GetChar(ulong scaledValue, ref int decodedSymbol)
            {
                for (int i = 0; i < 257; i++)
                    if (scaledValue < map[lsA, lsB, i + 1])
                    {
                        decodedSymbol = i;
                        var p = new SymbolProbability
                        {
                            low = map[lsA, lsB, i],
                            high = map[lsA, lsB, i + 1],
                            count = map[lsA, lsB, 257]
                        };
                        Update(lsA, lsB, decodedSymbol);
                        lsA = lsB;
                        lsB = decodedSymbol;
                        return p;
                    }
                throw new Exception("Decoder model found no symbol range for scaled value = " + scaledValue);
            }

            /// <inheritdoc />
            public void Reset()
            {
                lsA = lsB = 0;
                leadIn = _leadInBytes;
                map = new ulong[258,258,258];
                frozen = new bool[258,258];
                for (int i = 0; i < 258; i++)
                for (int j = 0; j < 258; j++)
                {
                    frozen[i,j] = false;
                    for (uint p = 0; p < 258; p++)
                    {
                        map[i,j,p] = p;
                    }
                }
            }

            /// <inheritdoc />
            public ulong GetCount()
            {
                return map[lsA,lsB, 257];
            }

            /// <inheritdoc />
            public int RequiredSymbolBits() { return 11; }

            /// <inheritdoc />
            public byte[] Preamble() { return Array.Empty<byte>(); }

            /// <inheritdoc />
            public void WritePreamble(Stream dest) { }

            /// <inheritdoc />
            public void ReadPreamble(Stream src) { }
        }

        /// <summary>
        /// Uses a shift-and-mix hash model for simple probability
        /// </summary>
        public class MixShiftModel : IProbabilityModel
        {
            /// <summary>
            /// First dimension is input hash, output is NON-cumulative
            /// probability of symbols.
            /// </summary>
            private ulong[,] _table = new ulong[257, 257];
            private int _mixHash;
            
            public SymbolProbability GetCurrentProbability(int symbol)
            {
                // Calculate probability for this symbol
                ulong low = 0;
                for (int i = 0; i < symbol; i++)
                {
                    low += _table[_mixHash, i];
                }
                
                ulong high = low + _table[_mixHash, symbol];
                ulong count = high;

                for (int i = symbol + 1; i < 257; i++)
                {
                    count += _table[_mixHash, i];
                }
                
                UpdateModel(symbol);

                return new SymbolProbability
                {
                    low = low,
                    high = high,
                    count = count
                };
            }

            private void UpdateModel(int symbol)
            {
                // Update probabilities
                /*for (int i = 0; i < 256; i++)
                {
                    if (i == symbol)
                        _table[_mixHash, i] += 2;
                    else
                        if (_table[_mixHash, i] > 50) _table[_mixHash, i]--;
                }*/
                
                // Update prediction symbol
                _mixHash = symbol;
            }

            public SymbolProbability GetChar(ulong scaledValue, ref int decodedSymbol)
            {
                ulong high = 0;
                ulong low = 0;
                for (int i = 0; i < 257; i++)
                {
                    high += _table[_mixHash, i];

                    if (scaledValue < high)
                    {
                        decodedSymbol = i;
                        
                        ulong count = high;
                        for (int j = i+1; j < 257; j++)
                        {
                            count += _table[_mixHash, j];
                        }
                        
                        var p = new SymbolProbability
                        {
                            low = low,
                            high = high,
                            count = count
                        };
                        
                        UpdateModel(decodedSymbol);
                        
                        return p;
                    }
                    low = high;
                }

                throw new Exception("Invalid data");
            }

            public void Reset()
            {
                for (int i = 0; i < 257; i++)
                {
                    for (int j = 0; j < 257; j++)
                    {
                        _table[j, i] = 1;
                    }
                }
            }

            public ulong GetCount()
            {
                ulong count = 0;
                for (int i = 0; i < 257; i++)
                {
                    count += _table[_mixHash, i];
                }
                return count;
            }

            public int RequiredSymbolBits() { return 11; }

            public byte[] Preamble() { return Array.Empty<byte>(); }

            public void WritePreamble(Stream dest) { }
            public void ReadPreamble(Stream src) { }
        }
    }
}