using System;
using System.Collections.Generic;
using System.IO;
using ImageTools.DataCompression.Encoding;

namespace ImageTools.DataCompression.Experimental
{
    /// <summary>
    /// Back-reference with arithmetic encoding
    /// </summary>
    public class LZAC
    {
        private readonly IProbabilityModel _model;
        private readonly ArithmeticEncode _encoder;
        private const int TerminationSymbol = 256;

        public LZAC()
        {
            _model = new ProbabilityModels.LearningModel(TerminationSymbol, 2);
            _encoder = new ArithmeticEncode(_model, TerminationSymbol);
        }
        
        /// <summary>
        /// Compress source data to encoded stream
        /// </summary>
        public void Encode(Stream src, Stream encoded)
        {
            //var symbols = SimpleScanningLz(src);
            var symbols = SimpleDictionaryLzw(src);
            
            _encoder.Encode(symbols, encoded);
        }

        /// <summary>
        /// Write a value 0..32894 into a symbol stream as one or two symbols in the range 0..255
        /// </summary>
        private static void Write32kRef(List<int> symbols, int value)
        {
            if (value >= 32894) throw new Exception("value out of range");
            if (value <= 127) // insert 1-byte position
            {
                symbols.Add(value & 0x7F);
            }
            else // insert 2-byte position
            {
                int offset = value - 127;
                symbols.Add(0x80 | ((offset & 0x7F00) >> 8));
                symbols.Add(offset & 0xFF);
            }
        }


        /// <summary>
        /// Dynamic memory, hash lookups. This should be better at finding matches
        /// </summary>
        private static List<int> SimpleDictionaryLzw(Stream src)
        {
            // https://jsfiddle.net/i_e_b/jzgj1uou/
            bool literalMode = true; // which run-length type we are in
            List<int> waitingSym = new(); // waiting back-refs or literals
            List<int> symbols = new(); // LZ symbol stream (fed into AC)
            Dictionary<string, int> lookBack = new(); // backreference dictionary TODO: change to byte array rather than string
            
            int maxSpan = 0;
            int bytesRead = 0;
            var read = src.ReadByte();
            if (read < 0) throw new Exception("Zero length input");
            
            int i = 1; // next byte to look at
            string key = ""+(char)read; // look-back key TODO: change to byte list
            int lookBackRef = 256; // first non-byte
            
            while (true)
            {
                // Read input
                read = src.ReadByte();
                if (read < 0) break; // end of input data
                bytesRead++;
                var b = (char)read;

                var keyNext = key + b;
                if (lookBack.ContainsKey(keyNext))  // if look-back contains key and current char, don't output.
                {
                    key = keyNext; // grow look-back key
                }
                else // next key not in dictionary, must add to output.
                {
                    // Here, we do some run-length encoding
                    // when we switch between literals and back-refs,
                    // then we output a run-length and a set of symbols.
                    // The symbol stream therefore alternate between runs and symbols.
                    // The run lengths are 0..127 for literals, and 128..255 for back-refs
                    
                    if (key.Length > 1) // dictionary entry - we want to add a back-reference
                    {
                        if (literalMode) // flush literals, switch to back-refs
                        {
                            literalMode = false;
                            if (waitingSym.Count > 0)
                            {
                                maxSpan = Math.Max(maxSpan, waitingSym.Count);
                                symbols.Add(waitingSym.Count - 1);
                                symbols.AddRange(waitingSym);
                                waitingSym.Clear();
                            }
                        }
                        
                        // add to back-refs
                        //waitingSym.Add(lookBack[key] - 256);
                        Console.WriteLine($"Ref: {lookBack[key] - 255}");
                        Write32kRef(waitingSym, lookBack[key] - 256);

                        // flush if too long.
                        if (waitingSym.Count > 126)
                        {
                            maxSpan = Math.Max(maxSpan, waitingSym.Count);
                            symbols.Add(waitingSym.Count + 127);
                            symbols.AddRange(waitingSym);
                            waitingSym.Clear();
                        }
                    }
                    else // literal
                    {
                        if (!literalMode) // flush back-refs, switch to literals
                        {
                            literalMode = true;
                            if (waitingSym.Count > 0)
                            {
                                maxSpan = Math.Max(maxSpan, waitingSym.Count);
                                symbols.Add(waitingSym.Count + 127);
                                symbols.AddRange(waitingSym);
                                waitingSym.Clear();
                            }
                        }
                        
                        // add to literals
                        waitingSym.Add(b);

                        // flush if too long.
                        if (waitingSym.Count > 126)
                        {
                            maxSpan = Math.Max(maxSpan, waitingSym.Count);
                            symbols.Add(waitingSym.Count - 1);
                            symbols.AddRange(waitingSym);
                            waitingSym.Clear();
                        }
                    }
                    
                    lookBack.Add(keyNext, lookBackRef++);

                    key = ""+b;
                }
            }
            
            
            // Flush anything left over
            if (waitingSym.Count > 0)
            {
                maxSpan = Math.Max(maxSpan, waitingSym.Count);
                
                if (literalMode) symbols.Add(waitingSym.Count - 1);
                else symbols.Add(waitingSym.Count + 127);
                
                symbols.AddRange(waitingSym);
                waitingSym.Clear();
            }

            // Write end-of-stream marker
            symbols.Add(TerminationSymbol);
            
            Console.WriteLine($"\r\n\r\n{bytesRead} bytes -> {symbols.Count} symbols. Max span = {maxSpan}\r\n");
            foreach (var sym in symbols)
            {
                if(sym > 120 || sym < 31) Console.Write($" {sym} ");
                else Console.Write((char)sym);
            }

            return symbols;
        }

        /// <summary>
        /// Basic one-shot scanning, fixed memory use
        /// </summary>
        private static List<int> SimpleScanningLz(Stream src)
        {
            // Simple LZ encoding: back reference, limited to 32K window, 32 byte reference size
            // Symbols are:
            //   0..255   -> literal byte
            //   256..287 -> 1..32 byte sized back reference
            //   289      -> end of stream
            // A back reference is followed by 1 or 2 bytes of distance
            // First MSB of 1st byte is set where there is a second byte.
            // Distance is to start of data in the window.
            // Window starts at full size, full of zeros.
            // Window is written with all input bytes, wrapping at end of window buffer.

            const int windowSize = 32767; // how big can window get?
            const int matchDistanceLimit = 32894; // how big a distance can be
            const int matchLengthLimit = 32; // how big can a back-reference get?
            const int matchLengthMinimum = 3; // a match less than this is written as literals

            var windowPos = 0; // write position in window
            var window = new byte[windowSize]; // starts at full size, populated with zeros.
            List<int> symbols = new(); // LZ symbol stream (fed into AC)

            int matchLeftSide = -1; // index into window, or negative if no match
            int matchLength = -1; // length of match, or negative if no match

            int bytesRead = 0;

            void WriteToWindowAndAdvance(byte b1)
            {
                // write into window
                window[windowPos] = b1;
                windowPos = (windowPos + 1) % windowSize;
            }

            void TryToFindMatch(byte b2)
            {
                for (int i = 0; i < windowSize; i++)
                {
                    if (window[i] == b2)
                    {
                        matchLeftSide = i;
                        matchLength = 1;
                        return;
                    }
                }
            }

            while (true)
            {
                // Read input
                var read = src.ReadByte();
                if (read < 0) break; // end of input data

                bytesRead++;
                var b = (byte)read;

                // Try to find or extend match
                if (matchLength > 0) // existing match, try to extend
                {
                    // If it does match, extend the length further.
                    // The length is limited to 32767, and is allowed to wrap.
                    int nextIdx = (matchLeftSide + matchLength) % windowSize;
                    if (matchLength < matchLengthLimit && window[nextIdx] == b) // match continues, extend
                    {
                        matchLength++;
                        continue; // successful match extension
                    }
                }

                // If at end of match, write the smaller of a back-reference,
                // or the literals. We don't write a back reference
                // unless it is at least 3 bytes long.
                if (matchLength > 0)
                {
                    if (matchLength < matchLengthMinimum) // Not worth inserting a back-reference TODO: calculate how many bytes we'd use
                    {
                        for (int i = 0; i < matchLength; i++)
                        {
                            var literal = window[matchLeftSide + i];

                            // write to output
                            symbols.Add(literal);

                            // write into window
                            WriteToWindowAndAdvance(literal);

                            Console.Write($"{(char)literal}");
                        }
                    }
                    else // insert a back-reference
                    {
                        symbols.Add(255 + matchLength); // back-ref marker, with size
                        int matchDistance = windowPos - matchLeftSide;
                        if (matchDistance < 0) matchDistance += windowSize;
                        if (matchDistance > matchDistanceLimit) throw new Exception("Algorithm bug: distance is larger than window!");

                        if (matchLeftSide <= 127) // insert 1-byte position
                        {
                            symbols.Add(matchLeftSide & 0x7F);
                        }
                        else // insert 2-byte position
                        {
                            int offset = matchDistance - 127;
                            symbols.Add(0x80 | ((offset & 0x7F00) >> 8));
                            symbols.Add(offset & 0xFF);
                        }

                        // Add the match into the window
                        for (int i = 0; i < matchLength; i++)
                        {
                            WriteToWindowAndAdvance(window[matchLeftSide + i]);
                        }

                        Console.Write($"[{matchLength},{matchDistance}]");
                    }
                }

                // Reset match counters
                matchLeftSide = -1;
                matchLength = -1;

                // Try to find a match
                TryToFindMatch(b);

                // If we haven't started a match, write a literal
                if (matchLength < 1)
                {
                    // write to output
                    symbols.Add(b);
                    Console.Write($"_{(char)b}");

                    WriteToWindowAndAdvance(b);
                }
            }

            // Write end-of-stream marker
            symbols.Add(TerminationSymbol);
            
            Console.WriteLine($"\r\n\r\n{bytesRead} bytes -> {symbols.Count} symbols");
            
            return symbols;
        }


        /// <summary>
        /// Decompress encoded stream to destination
        /// </summary>
        public void Decode(Stream encoded, Stream dst)
        {
            // TODO: implement
        }

        public void Reset()
        {
            _model.Reset();
            _encoder.Reset();
        }
    }
}