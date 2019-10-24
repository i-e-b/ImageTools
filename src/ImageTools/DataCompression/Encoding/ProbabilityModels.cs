using System;
using System.IO;

namespace ImageTools.DataCompression.Encoding
{
    /// <summary>
    /// Container for various Arithmetic Encoder models
    /// </summary>
    public static class ProbabilityModels
    {
        /// <summary>
        /// A simple adaptive model that uses a rolling window
        /// </summary>
        public class RollingLearningModel : IProbabilityModel
        {
            private readonly uint _rescaleThreshold;
            private readonly uint[] cumulative_frequency;

            public RollingLearningModel(uint rescaleThreshold)
            {
                if (rescaleThreshold < 500) throw new Exception("Rescale threshold should be at least 500");
                _rescaleThreshold = rescaleThreshold;
                cumulative_frequency = new uint[258];
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
                for (int i = c + 1; i < 258; i++) cumulative_frequency[i]++;

                if (cumulative_frequency[257] >= _rescaleThreshold)
                {
                    Rescale();
                }

            }

            private void Rescale()
            {
                // We recover the individual probablities, half them, ensure a minimum of 1, then rebuild the cumulative probabilities
                var symProb = new uint[257];
                for (int i = 0; i < 257; i++)
                {
                    symProb[i] = ((cumulative_frequency[i + 1] - cumulative_frequency[i]) >> 1) | 1;
                }

                for (int i = 1; i < 257; i++)
                {
                    cumulative_frequency[i] = cumulative_frequency[i-1] + symProb[i];
                }
                cumulative_frequency[257] = cumulative_frequency[256] + 1;
            }

            /// <inheritdoc />
            public SymbolProbability GetChar(long scaledValue, ref int decodedSymbol)
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
            }

            /// <inheritdoc />
            public uint GetCount()
            {
                return cumulative_frequency[257];
            }

            /// <inheritdoc />
            public int RequiredSymbolBits() { return 9; }

            /// <inheritdoc />
            public byte[] Preamble()
            {
                return new byte[0];
            }

            /// <inheritdoc />
            public void WritePreamble(Stream dest) { }

            /// <inheritdoc />
            public void ReadPreamble(Stream src) { }
        }

        public class PushToFrontModel : IProbabilityModel
        {
            private readonly int[] _symbols;
            private readonly uint[] _cumlFreq;
            private readonly uint _total;

            public PushToFrontModel(int fallOff = 3)
            {
                _symbols = new int[257]; // the closer to the front, the higher the expected probability
                _cumlFreq = new uint[258]; // cumulative frequencies for the positions

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
            public SymbolProbability GetChar(long scaledValue, ref int decodedSymbol)
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
            public uint GetCount() { return _total; }

            /// <inheritdoc />
            public int RequiredSymbolBits() { return 13; }

            /// <inheritdoc />
            public byte[] Preamble()
            {
                return new byte[0];
            }

            /// <inheritdoc />
            public void WritePreamble(Stream dest) { }

            /// <inheritdoc />
            public void ReadPreamble(Stream src) { }
        }

        /// <summary>
        /// A really simple model for testing
        /// </summary>
        public class SimpleLearningModel : IProbabilityModel
        {
            private readonly uint[] cumulative_frequency;
            private bool _frozen;

            public SimpleLearningModel()
            {
                cumulative_frequency = new uint[258];
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
                for (int i = c + 1; i < 258; i++) cumulative_frequency[i]++;

                if (cumulative_frequency[257] >= ArithmeticEncode.MAX_FREQ)
                {
                    Console.WriteLine("Ran out of model precision. Will freeze probabilities.");
                    _frozen = true;
                }

            }

            /// <inheritdoc />
            public SymbolProbability GetChar(long scaledValue, ref int decodedSymbol)
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
            public uint GetCount()
            {
                return cumulative_frequency[257];
            }

            /// <inheritdoc />
            public int RequiredSymbolBits() { return 9; }

            /// <inheritdoc />
            public byte[] Preamble()
            {
                return new byte[0];
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
            private readonly uint[] cumulative_frequency;

            public BraindeadModel()
            {
                cumulative_frequency = new uint[258];
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
            public SymbolProbability GetChar(long scaledValue, ref int decodedSymbol)
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
            public uint GetCount()
            {
                return cumulative_frequency[257];
            }

            /// <inheritdoc />
            public int RequiredSymbolBits() { return 9; }

            /// <inheritdoc />
            public byte[] Preamble()
            {
                return new byte[0];
            }

            /// <inheritdoc />
            public void WritePreamble(Stream dest) { }

            /// <inheritdoc />
            public void ReadPreamble(Stream src) { }

            // 8 bits for values, 1 for stop
        }

        /// <summary>
        /// Scan the whole dataset first, and use a fixed model.
        /// This would require you to transmit the probability tables separately (256 bytes)
        /// </summary>
        public class PrescanModel : IProbabilityModel
        {
            private readonly byte[] preamble;
            private readonly uint[] cumulative_frequency;

            const int PreambleSize = 256;

            /// <summary>
            /// Create a model for known data.
            /// </summary>
            /// <remarks>Would need another that takes a known table</remarks>
            public PrescanModel(Stream targetData)
            {
                // count values
                preamble = new byte[PreambleSize];
                var countTable = new long[257];
                long len = 0;
                int b;
                while ((b = targetData.ReadByte()) >= 0)
                {
                    countTable[b]++;
                    len++;
                }

                long max = 0;
                for (int i = 0; i < countTable.Length; i++)
                {
                    max = Math.Max(max, countTable[i]);
                }

                // scale them to fit in a frequency table if required
                if (max > 255)
                {
                    var scale = max / 255;
                    for (int i = 0; i < countTable.Length; i++)
                    {
                        if (countTable[i] == 0) continue;
                        countTable[i] = (countTable[i] / scale) | 1;
                    }
                }

                // build the freq table
                cumulative_frequency = new uint[258];
                uint v = 0;
                for (int i = 0; i < 257; i++)
                {
                    cumulative_frequency[i] = v;
                    v += (uint)countTable[i];
                }
                cumulative_frequency[257] = v + 1; // `+1` for stop symbol

                // build preamble for decode
                for (int i = 0; i < 256; i++)
                {
                    preamble[i] = (byte)countTable[i];
                }
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
            public SymbolProbability GetChar(long scaledValue, ref int decodedSymbol)
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
            public void Reset() { }

            /// <inheritdoc />
            public uint GetCount()
            {
                return cumulative_frequency[257];
            }

            /// <inheritdoc />
            public int RequiredSymbolBits() { return 9; }

            /// <inheritdoc />
            public byte[] Preamble()
            {
                return preamble;
            }

            /// <inheritdoc />
            public void WritePreamble(Stream dest)
            {
                dest.Write(preamble, 0, preamble.Length);
            }

            /// <inheritdoc />
            public void ReadPreamble(Stream src)
            {
                src.Read(preamble, 0, PreambleSize);
            }

            // 8 bits for values, 1 for stop
        }
    }
}