using System;
using System.Collections.Generic;
using System.IO;
using ImageTools.DataCompression.Encoding;

namespace ImageTools.DataCompression
{
    /// <summary>
    /// A rough implementation of the LZSS algorithm
    /// https://en.wikipedia.org/wiki/Lempel%E2%80%93Ziv%E2%80%93Storer%E2%80%93Szymanski
    /// </summary>
    /// <remarks>This currently makes no attempt to optimise performance</remarks>
    public class LZSSPack {
        public void Decode(Stream src, Stream dst)
        {
            var model = new WideFlaggedModel();
            var inpt = new ArithmeticEncode(model, WideFlaggedModel.SYM_EndStream);

            var symlist = inpt.Decode(src);
            var result = new List<byte>();

            using (var list =  symlist.GetEnumerator()) {
                var notEnded = true;
                while (list.MoveNext() && notEnded) {
                    var sym = list.Current;
                    switch (sym)
                    {
                        case WideFlaggedModel.SYM_LongBackref:
                            {
                                list.MoveNext();
                                var brpos = list.Current << 8;
                                list.MoveNext();
                                brpos += list.Current;

                                list.MoveNext();
                                var brlen = list.Current;

                                brpos += brlen;
                                var p = result.Count - 1;
                                for (int j = 1; j <= brlen; j++) { result.Add(result[p-brpos+j]); }
                            }
                            break;

                        case WideFlaggedModel.SYM_ShortBackref:
                            {
                                list.MoveNext();
                                var brpos = list.Current;

                                list.MoveNext();
                                var brlen = list.Current;
                                
                                brpos += brlen;
                                var p = result.Count - 1;
                                for (int j = 1; j <= brlen; j++) { result.Add(result[p-brpos+j]); }
                            }
                            break;

                        case WideFlaggedModel.SYM_EndStream:
                            notEnded = false;
                            break;

                        default:
                            result.Add((byte)sym);
                            break;
                    }
                }
            }
            var final = result.ToArray();
            dst.Write(final, 0, final.Length);
        }

        public void Encode(Stream src, Stream dst)
        {
            var codes = new List<int>();

            EncoderSearchAlgorithm(src, codes);

            codes.Add(WideFlaggedModel.SYM_EndStream);
            var model = new WideFlaggedModel();
            var outp = new ArithmeticEncode(model, WideFlaggedModel.SYM_EndStream);
            outp.Encode(codes, dst);
        }

        /// <summary>
        /// Pyramid hash search.
        /// Currently uses plain byte addition, so gets lots of false positives
        /// </summary>
        /// <param name="src">Source data</param>
        /// <param name="codes">output code point array</param>
        private void EncoderSearchAlgorithm(Stream src, List<int> codes)
        {
            var windowSize = 8172;

            var rank1 = new byte[src.Length];
            src.Read(rank1, 0, (int)src.Length);
            
            var ranks = new List<byte[]>();
            ranks.Add(rank1);

            // --- BUILD the pyramid --- (this is the fast bit)
            var stride = 1;
            long sums = 0;
            for (int R = 2; R <= 256; R *= 2)
            {
                var prev = ranks[ranks.Count-1];
                var next = new byte[prev.Length - stride];
                ranks.Add(next);

                for (int i = 0; i < prev.Length - stride; i++)
                {
                    sums++;
                    next[i] = (byte)(prev[i] + prev[i+stride]);
                }

                stride *= 2;
            }
                        
            // --- SEARCH the pyramid --- (this is the slow bit)
            // look in 2^n-wide chunks for potentials:
            var matchFound = 0;
            var matchRejected = 0;
            var skipped = 0;
            var jumpTable = new int[rank1.Length]; // indexes that are covered by a larger replacement

            var matches = new List<PackMatch>(); // here we store our found bits

            for (int SearchRank = 8; SearchRank > 1; SearchRank--)
            {
                var n = SearchRank;
                var rank_n = ranks[n];
                var matchLength = (int)Math.Pow(2, n);

                for (int i = 0; i < rank_n.Length; i += matchLength)
                {
                    var limit = Math.Min(rank_n.Length, i + windowSize + matchLength);
                    for (int j = i + matchLength; j < limit; j++)
                    {
                        if (jumpTable[j] != 0) {
                            skipped += jumpTable[j] - j;
                            j = jumpTable[j];
                            if (j >= limit) break;
                        }
                        if (rank_n[i] != rank_n[j]) continue; // no potential match

                        // do a double check here
                        var realMatch = true;
                        for (int k = 0; k < matchLength; k++)
                        {
                            if (rank1[i + k] == rank1[j + k]) continue;

                            realMatch = false;
                            matchRejected++;
                            break;
                        }
                        if (!realMatch) continue;

                        matchFound++;

                        // Write to jump table. The 'replaced' section should not be searched again
                        for (int skip = 0; skip < matchLength; skip++)
                        {
                            jumpTable[j+skip] = j+matchLength;
                        }
                        
                        var left = i + matchLength; // right edge of left side
                        var right = j; // left edge of right side
                        matches.Add(new PackMatch{
                            Distance = right - left,
                            Right = right,
                            Length = matchLength
                        });
                        
                        break;
                    }
                } // end of seach at selected rank
            }

            // --- WRITE the output ---
            matches.Sort((a,b)=>a.Right.CompareTo(b.Right));
            int matchIndex = (matches.Count > 0) ? 0 : -1;
            for (int i = 0; i < rank1.Length; i++)
            {
                if (matchIndex >= 0 && i == matches[matchIndex].Right) {
                    // insert a backreference and skip
                    var m = matches[matchIndex];
                    if (m.Distance > 255) {
                        codes.Add(WideFlaggedModel.SYM_LongBackref);
                        codes.Add((m.Distance & 0xff00) >> 8);
                        codes.Add(m.Distance & 0xff);
                        codes.Add(m.Length);
                    } else { 
                        codes.Add(WideFlaggedModel.SYM_ShortBackref);
                        codes.Add(m.Distance & 0xff);
                        codes.Add(m.Length);
                    }
                    i += m.Length - 1;
                    matchIndex++;
                    if (matchIndex >= matches.Count) matchIndex = -1;
                    continue;
                }
                codes.Add(rank1[i]);
            }
        }
      
        private struct PackMatch
        {
            public int Distance;
            public int Right;
            public int Length;
        }

        /// <summary>
        /// A model for LZSS pack and arithmetic encoding
        /// </summary>
        public class WideFlaggedModel : IProbabilityModel
        {
            private readonly ulong[] cumulative_frequency;
            private bool _frozen;

            /// <summary>A two byte back reference (dist, len)</summary>
            public const int SYM_ShortBackref = 256;
            /// <summary>A three byte back reference (distx2, len)</summary>
            public const int SYM_LongBackref = 257;

            /// <summary>Stream termination symbol (must be highest value symbol)</summary>
            public const int SYM_EndStream = 258;

            public const int CumulativeCount = SYM_EndStream + 1;
            public const int TableSize = SYM_EndStream + 2;

            public WideFlaggedModel()
            {
                cumulative_frequency = new ulong[TableSize];
                Reset();
            }

            /// <inheritdoc />
            public SymbolProbability GetCurrentProbability(int symbol)
            {
                if (symbol >= CumulativeCount) throw new Exception("Symbol can not be represented: " + symbol);
                var p = new SymbolProbability
                {
                    low = cumulative_frequency[symbol],
                    high = cumulative_frequency[symbol + 1],
                    count = cumulative_frequency[CumulativeCount]
                };
                Update(symbol);
                return p;
            }

            private void Update(int c)
            {
                if (_frozen) return; // model is saturated
                for (int i = c + 1; i < TableSize; i++) cumulative_frequency[i]++;

                if (cumulative_frequency[CumulativeCount] >= ArithmeticEncode.MAX_FREQ)
                {
                    _frozen = true;
                }

            }

            /// <inheritdoc />
            public SymbolProbability GetChar(ulong scaledValue, ref int decodedSymbol)
            {
                for (int i = 0; i < CumulativeCount; i++)
                    if (scaledValue < cumulative_frequency[i + 1])
                    {
                        decodedSymbol = i;
                        var p = new SymbolProbability
                        {
                            low = cumulative_frequency[i],
                            high = cumulative_frequency[i + 1],
                            count = cumulative_frequency[CumulativeCount]
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
                for (uint i = 0; i < TableSize; i++) cumulative_frequency[i] = i;
                _frozen = false;
            }

            /// <inheritdoc />
            public ulong GetCount()
            {
                return cumulative_frequency[CumulativeCount];
            }

            /// <inheritdoc />
            public int RequiredSymbolBits() { return 10; }

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


    }
}