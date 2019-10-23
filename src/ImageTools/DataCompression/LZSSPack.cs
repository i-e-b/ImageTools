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

        
        const uint PRIME_BASE = 0x101;
        const uint PRIME_MOD = 0x3B9ACA07;

        public void Encode(Stream src, Stream dst)
        {
            // Minimum match size. Tune so matches are longer that the backref data
            var minSize = 3;

            long stat_replacements = 0;
            long stat_scans = 0;

            // get a static buffer. TODO: make this a proper streaming impl
            var ms = new MemoryStream();
            src.CopyTo(ms);
            var buffer = ms.ToArray();

            // EXP 1: rolling hash @ size = 4 (catch "and ","the " etc)
            var len = (int)src.Length;
            //var size = 4;

            // values for back references. We keep the longest
            var backRefLen = new int[len]; // lengths of back references
            var backRefPos = new int[len]; // distance between backref and source
            var backRefOcc = new int[len]; // marker to detect overlaps
            var hashVals = new uint[len];

            //for (int i = 0; i < len; i++) { backRefPos[i] = int.MaxValue; }

            //for (int size = 64; size > 63; size--)
            for (int size = minSize; size < 256; size++)
            //for (int size = minSize; size < 4; size++)
            {
                // TODO: optimise this so we do less loops

                var anyFound = false;

                // Build up the hashVals array for this window size:
                long power = 1;
                long hash2 = 0;
                for (int i = 0; i < size; i++) { power = (power * PRIME_BASE) % PRIME_MOD; } // calculate the correct 'power' value
                for (int i = 0; i < len; i++)
                {
                    // add the last letter
                    hash2 = hash2 * PRIME_BASE + buffer[i];
                    hash2 %= PRIME_MOD;

                    // remove the first character, if needed
                    if (i >= size) {
                        hash2 -= power * buffer[i - size] % PRIME_MOD;
                        if (hash2 < 0) hash2 += PRIME_MOD;
                    }

                    // store the hash at this point
                    hashVals[i] = (uint)hash2;
                    stat_scans++;
                }

                // compare -- any match values are probably a back reference
                // At the moment, we assume hash matches *are* valid back references. TODO: double check.

                // Scan backward from each character, look for matches behind it.
                for (int fwd = len - 1; fwd >= size; fwd--)
                {
                    for (int bkw = fwd - size; bkw >= size; bkw--)
                    {
                        stat_scans++;
                        // record the longest, closest matches
                        if (hashVals[fwd] != hashVals[bkw]) continue;

                        anyFound = true; // continue searching, even if we decide this match wasn't worth while

                        var dist = (fwd - bkw) - size;

                        // If the size of the back reference would be more than we save, reject it.
                        // the back reference length can be 1 byte or two, so that affects the rejection size limit.
                        if (dist > 255 && size < 4) {
                            //fwd -= size - 1;
                            break; // move to next outer
                        }

                        // If this back reference overlaps with another, keep the longer one.
                        var overlap = 0;
                        for (int i = bkw+1; i <= fwd; i++) {
                            if (backRefOcc[i] > 0) {
                                overlap = backRefOcc[i];
                                break;
                            }
                        }
                        if (overlap > 0) { // we've found a better replacement here
                            /*if (backRefLen[overlap] >= size) continue; // this replacement is no better than the old one
                            stat_replacements--;
                            // wipe out the old one
                            var start = overlap - (backRefPos[overlap] + backRefLen[overlap]);
                            var end = backRefPos[overlap];
                            //Console.WriteLine($"Replacing ({backRefPos[overlap]},{backRefLen[overlap]})@{overlap} with ({dist},{size})@{fwd}  --> {start} to {end}");

                            for (int i = start; i <= end; i++) { backRefOcc[i] = 0; }
                            backRefLen[overlap] = 0;
                            backRefPos[overlap] = 0;*/
                            // TODO: fix the above
                            break;
                        }

                        stat_replacements++;
                        backRefLen[fwd] = size;
                        backRefPos[fwd] = dist;

                        // mark overlaps
                        for (int i = fwd - size; i <= fwd; i++) { backRefOcc[i] = fwd; }

                        fwd -= size - 1; // skip back
                        break; // stop searching for matches for this point (fwd)
                    }
                }
                if (!anyFound) {
                    Console.WriteLine($"Ran out of matches at length {size}");
                    break;
                }
            }



            // Now, mark any characters covered by a backreference, so we know not to output
            var rem = 0;
            for (int i = len - 1; i >= 0; i--)
            {
                stat_scans++;
                if (backRefLen[i] > rem) rem = backRefLen[i];
                hashVals[i] = (rem > 0) ? 0u : 1u; // use the hash values to store the output flag
                rem--;
            }


            // Now the backRef* arrays show have all the back ref length and distance values

            // Write to output
            //Console.WriteLine("\r\n----------- Encoding: ---------------");
            var model = new WideFlaggedModel();
            var outp = new ArithmeticEncode(model, WideFlaggedModel.SYM_EndStream);
            var codes = new List<int>();
            for (int i = 0; i < len; i++)
            {
                stat_scans++;

                if (backRefLen[i] > 0) {
                    if (backRefPos[i] > 255) {
                        //Console.Write($"(L{backRefPos[i]},{backRefLen[i]})"); 
                        codes.Add(WideFlaggedModel.SYM_LongBackref);
                        codes.Add((backRefPos[i] & 0xff00) >> 8);
                        codes.Add(backRefPos[i] & 0xff);
                        codes.Add(backRefLen[i]);
                    } else {
                        //Console.Write($"(S{backRefPos[i]},{backRefLen[i]})"); 
                        codes.Add(WideFlaggedModel.SYM_ShortBackref);
                        codes.Add(backRefPos[i] & 0xff);
                        codes.Add(backRefLen[i]);
                    }
                }
                else if (hashVals[i] > 0) {
                    //Console.Write((char)buffer[i]);
                    codes.Add(buffer[i]);
                }
            }
            codes.Add(WideFlaggedModel.SYM_EndStream);

            Console.WriteLine($"Statistics: Scans = {stat_scans};  Replacements = {stat_replacements}");
            outp.Encode(codes, dst);
        }

        
        /// <summary>
        /// A model for LZSS pack and arithmetic encoding
        /// </summary>
        public class WideFlaggedModel : IProbabilityModel
        {
            private readonly uint[] cumulative_frequency;
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
                cumulative_frequency = new uint[TableSize];
                Reset();
            }

            /// <inheritdoc />
            public SymbolProbability GetCurrentProbability(int symbol)
            {
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
                    //Console.WriteLine("Ran out of model precision. Will freeze probabilities.");
                    _frozen = true;
                }

            }

            /// <inheritdoc />
            public SymbolProbability GetChar(long scaledValue, ref int decodedSymbol)
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
            public uint GetCount()
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
        }


    }
}