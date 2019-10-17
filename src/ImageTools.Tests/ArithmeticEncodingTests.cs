using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using ImageTools.DataCompression.Encoding;
using ImageTools.Utilities;
using ImageTools.WaveletTransforms;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class ArithmeticEncodingTests {
        [Test]
        public void constants_are_correct_values () {
            Console.WriteLine($"Actual values: Bit Size = {ArithmeticEncode.BIT_SIZE}, Precision = {ArithmeticEncode.PRECISION},\r\n" +
                              $"Symbol bits = {ArithmeticEncode.CODE_VALUE_BITS}, Frequency bits = {ArithmeticEncode.FREQUENCY_BITS},\r\n" +
                              $"Max code value = {ArithmeticEncode.MAX_CODE:X}, Max frequency value = {ArithmeticEncode.MAX_FREQ:X},\r\n" +
                              $"1/4 threshold = {ArithmeticEncode.ONE_QUARTER:X}, 2/4 threshold = {ArithmeticEncode.ONE_HALF:X}, 3/4 threshold = {ArithmeticEncode.THREE_QUARTERS:X}");
        }

        [Test]
        public void can_encode_a_byte_stream ()
        {
            var rnd = new Random();

            var subject = new ArithmeticEncode(new SimpleLearningModel());
            var result = new MemoryStream();

            var bytes = new byte[100];
            rnd.NextBytes(bytes);
            var data = new MemoryStream(bytes);
            data.Seek(0,SeekOrigin.Begin);

            subject.Encode(data, result);

            Console.WriteLine($"Encoded {result.Length} bytes for {bytes.Length} bytes of input");
        }
        
        [Test]
        public void encoding_a_low_entropy_dataset_takes_few_bytes ()
        {
            var bytes = new byte[100]; // all zeros
            var data = new MemoryStream(bytes);
            data.Seek(0,SeekOrigin.Begin);

            var model = new PrescanModel(data);
            var subject = new ArithmeticEncode(model);
            var result = new MemoryStream();

            data.Seek(0,SeekOrigin.Begin);
            subject.Encode(data, result);
            // The probability model has the largest effect. ArithmeticEncode is just an efficient way of expressing that.
            // Simple learning: 39 bytes
            // Push to front:    3 bytes (falloff = 5)
            // Braindead:       21 bytes
            // Prescan:          2 bytes (plus up to 256 for table!)

            var bpb = (result.Length * 8.0) / bytes.Length;
            Console.WriteLine($"Encoded {result.Length} bytes for {bytes.Length} bytes of input. ({bpb} bits per byte)");
            Assert.That(result.Length, Is.LessThan(50));
        }

        [Test]
        public void can_recover_an_encoded_byte_stream (){
            var rnd = new Random();

            var subject = new ArithmeticEncode(new SimpleLearningModel());
            var result = new MemoryStream();

            var inputBytes = new byte[100];
            rnd.NextBytes(inputBytes);
            var data = new MemoryStream(inputBytes);
            data.Seek(0,SeekOrigin.Begin);

            subject.Encode(data, result);

            Console.WriteLine($"Encoded {result.Length} bytes for {inputBytes.Length} bytes of input");

            subject.Reset();
            var final = new MemoryStream();
            try
            {
                result.Seek(0,SeekOrigin.Begin);
                subject.Decode(result, final);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Decode failed: " + ex);
            }
            final.Seek(0, SeekOrigin.Begin);

            var finalData = final.ToArray();

            Assert.That(finalData, Is.EquivalentTo(inputBytes), "Decoded data was not the same as the input");
        }

        [Test]
        public void encoder_supports_multiple_models () {

            var rnd = new Random();

            var subject = new ArithmeticEncode(new PushToFrontModel());
            var result = new MemoryStream();

            var inputBytes = new byte[100];
            rnd.NextBytes(inputBytes);
            for (int i = 25; i < 75; i++) // shove in a lot of zeros
            {
                inputBytes[i] = 0;
            }
            var data = new MemoryStream(inputBytes);
            data.Seek(0,SeekOrigin.Begin);

            subject.Encode(data, result);

            Console.WriteLine($"Encoded {result.Length} bytes for {inputBytes.Length} bytes of input");

            subject.Reset();
            var final = new MemoryStream();
            try
            {
                result.Seek(0, SeekOrigin.Begin);
                subject.Decode(result, final);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Decode failed: " + ex);
            }
            final.Seek(0, SeekOrigin.Begin);

            var finalData = final.ToArray();

            Assert.That(finalData, Is.EquivalentTo(inputBytes), "Decoded data was not the same as the input");
        }
        
        [Test]
        public void encoder_supports_large_input_data () {
            var sw = new Stopwatch();

            var subject = new ArithmeticEncode(new BraindeadModel());
            var result = new MemoryStream();

            sw.Restart();
            var inputBytes = new byte[1_000_000];
            for (int i = 0; i < inputBytes.Length; i++) { inputBytes[i] = (byte)(i&0xE0); } // bias to zeros
            var data = new MemoryStream(inputBytes);
            data.Seek(0,SeekOrigin.Begin);
            sw.Stop();
            Console.WriteLine($"Generating test data took {sw.Elapsed}");


            sw.Restart();
            subject.Encode(data, result);
            sw.Stop();

            Console.WriteLine($"Encoded {result.Length} bytes for {inputBytes.Length} bytes of input in {sw.Elapsed}");

            subject.Reset();
            var final = new MemoryStream();
            try
            {
                result.Seek(0, SeekOrigin.Begin);
                sw.Restart();
                subject.Decode(result, final);
                sw.Stop();
                Console.WriteLine($"Decoded in {sw.Elapsed}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Decode failed: " + ex);
            }
            final.Seek(0, SeekOrigin.Begin);
            var finalData = final.ToArray();

            sw.Restart();
            Assert.That(finalData.Length, Is.EqualTo(inputBytes.Length), "Decoded data was the wrong length");
            for (int i = 0; i < finalData.Length; i++)
            {
                if (finalData[i] != inputBytes[i]) Assert.Fail("Data differed at index "+i);
            }
            sw.Stop();
            Console.WriteLine($"Testing result data took {sw.Elapsed}");


            // This is taking 20+ seconds:
            /*
            sw.Restart();
            var finalData = final.ToArray();
            Assert.That(finalData, Is.EquivalentTo(inputBytes), "Decoded data was not the same as the input");
            sw.Stop();
            Console.WriteLine($"Testing result data took {sw.Elapsed}");
            */
        }

        [Test]
        public void compressing_a_wavelet_image () {
            // An experiment to see how a simple model and arith. coding works with Wavelet coefficients

            /* FINDINGS
             
            UINT32:
            =======

            Raw 'Y' size = 4mb
            AC encoded 'Y' size = 261.75kb	        (simple learning model)
            AC encoded 'Y' size = 352.09kb          (push to front model, falloff = 3)
            AC encoded 'Y' size = 200.58kb          (fixed prescan model)
            Deflate encoded 'Y' size = 180.08kb

            UINT16:
            =======
            Raw 'Y' size = 2mb
            AC encoded 'Y' size = 244.59kb          (simple learning model)
            AC encoded 'Y' size = 299.75kb          (push to front model, falloff = 3)
            AC encoded 'Y' size = 180.85kb          (fixed prescan model)
            Deflate encoded 'Y' size = 151.15kb

            INT16:
            ======
            Raw 'Y' size = 2mb
            AC encoded 'Y' size = 336.18kb			(simple learning model)
            AC encoded 'Y' size = 327.33kb          (push to front model)
            AC encoded 'Y' size = 228.48kb          (fixed prescan model)
            Deflate encoded 'Y' size = 154.31kb

            FIBONACCI CODED:
            ================

            Raw 'Y' size = 319kb
            AC encoded 'Y' size = 175.86kb			(simple learning model)
            AC encoded 'Y' size = 233.3kb           (push to front model)
            AC encoded 'Y' size = 148.78kb          (fixed prescan model)
            Deflate encoded 'Y' size = 123.47kb

            
            */


            var msY = new MemoryStream();
            var msU = new MemoryStream();
            var msV = new MemoryStream();

            var acY = new MemoryStream();

            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                WaveletCompress.ReduceImage2D_ToStreams(bmp, CDF.Fwt97, msY, msU, msV);

                Console.WriteLine($"Raw 'Y' size = {Bin.Human(msY.Length)}");

                msY.Seek(0, SeekOrigin.Begin);
                //var model = new PrescanModel(msY);
                //var model = new PushToFrontModel();
                var model = new SimpleLearningModel();

                // Try our simple encoding
                var subject = new ArithmeticEncode(model);
                msY.Seek(0, SeekOrigin.Begin);
                subject.Encode(msY, acY);

                Console.WriteLine($"AC encoded 'Y' size = {Bin.Human(acY.Length + model.Preamble().Length)}");// add 256 if using pre-scan

                // Compare to deflate
                msY.Seek(0, SeekOrigin.Begin);
                
                using (var tmp = new MemoryStream())
                {   // byte-by-byte writing to DeflateStream is *very* slow, so we buffer
                    using (var gs = new DeflateStream(tmp, CompressionLevel.Optimal, true))
                    {
                        msY.WriteTo(gs);
                        gs.Flush();
                    }
                    Console.WriteLine($"Deflate encoded 'Y' size = {Bin.Human(tmp.Length)}");
                }
            }
        }

        [Test]
        public void LZ_test () {
            // playing with simple dictionary coding

            var msY = new MemoryStream();
            var msU = new MemoryStream();
            var msV = new MemoryStream();

            var lzY = new MemoryStream();
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                
                WaveletCompress.ReduceImage2D_ToStreams(bmp, CDF.Fwt97, msY, msU, msV);
                //var test = Encoding.UTF8.GetBytes("Hello world this is my test message");
                //msY.Write(test, 0, test.Length);
                msY.Seek(0, SeekOrigin.Begin);

                Console.WriteLine($"Raw 'Y' size = {Bin.Human(msY.Length)}");

                var subject = new LZPack();
                var dump = subject.Encode(msY, lzY);

                Console.WriteLine($"Estimated size = {Bin.Human(dump.Count * 2)}");

                var limit = 1000;
                long brsum = 0;
                foreach (var entry in dump)
                {
                    if (limit-- > 0) Console.Write($"{entry.DictIdx}_{entry.Extension:X2},");
                    brsum+= entry.DictIdx > 0 ? entry.DictIdx : 0;
                }
                var aveBackRef = brsum / dump.Count;

                Console.WriteLine($"Average backref value = {aveBackRef} (smaller is better)");

                Console.WriteLine($"AC encoded 'Y' size = {Bin.Human(lzY.Length)}");
            }
        }
    }

    public class LZPack
    {
        private readonly List<byte[]> _dict;

        public LZPack()
        {
            _dict = new List<byte[]>(1000);
        }

        public List<LZEntry> Encode(Stream src, Stream dst)
        {
            var result = new List<LZEntry>();
            var pattern = new List<byte>();

            int b, matchIdx;
            while ((b = src.ReadByte()) >= 0) {
                // test for existing pattern match
                // if found, extend the pattern and continue
                // if not, write the dictionary that matches all but the last + the last byte.
                pattern.Add((byte)b);
                if (AnyPrefixMatch(pattern)) {
                    continue;
                }

                // now there should be exactly one entry in the dictionary that is a prefix of the pattern
                matchIdx = GetMatchIndex(pattern);
                result.Add(new LZEntry{DictIdx = matchIdx, Extension = LastOf(pattern)});
                _dict.Add(pattern.ToArray());

                // TODO: sort dictionary somehow? (by length, push-to-front?)
                // TODO: limit the dictionary length
                pattern.Clear();
            }

            // last pattern?
            if (pattern.Count > 0)
            {
                matchIdx = GetMatchIndex(pattern);
                result.Add(new LZEntry { DictIdx = matchIdx, Extension = LastOf(pattern)});
            }

            Console.WriteLine($"Max dict length = {_dict.Count}");
            return result;
        }

        private byte LastOf(List<byte> pattern) { return pattern[pattern.Count - 1]; }

        private int GetMatchIndex(List<byte> pattern)
        {
            for (var i = 0; i < _dict.Count; i++)
            {
                var entry = _dict[i];
                if (entry.Length != pattern.Count - 1) continue;
                if (!EntryIsPrefix(entry, pattern)) continue;
                return i;
            }

            return -1;
        }

        private bool AnyPrefixMatch(List<byte> pattern)
        {
            foreach (var entry in _dict)
            {
                if (entry.Length < pattern.Count) continue;
                if (!PatternIsPrefix(entry, pattern)) continue;
                return true;
            }
            return false;
        }

        private bool PatternIsPrefix(byte[] entry, List<byte> pattern)
        {
            for (int i = 0; i < pattern.Count; i++)
            {
                if (entry[i] != pattern[i]) return false;
            }
            return true;
        }
        
        private bool EntryIsPrefix(byte[] entry, List<byte> pattern)
        {
            for (int i = 0; i < pattern.Count - 1; i++)
            {
                if (entry[i] != pattern[i]) return false;
            }
            return true;
        }

        public class LZEntry
        {
            public int DictIdx { get; set; }
            public byte Extension { get; set; }
        }
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

            for ( int i = 0 ; i < 257 ; i++ ) {
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
            for (int j = i - 1; j >= 0; j--) { _symbols[j+1] = _symbols[j]; } // shift right
            _symbols[0] = tmp; // set at head
        }

        /// <inheritdoc />
        public SymbolProbability GetChar(long scaledValue, ref int decodedSymbol)
        {
            
            for ( int i = 0 ; i < 257 ; i++ ) {
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
            for ( int i = 0 ; i < 257 ; i++ )
                if ( scaledValue < cumulative_frequency[i+1] ) {
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
            throw new Exception("Decoder model found no symbol range for scaled value = "+scaledValue);
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

// 8 bits for values, 1 for stop
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
            for ( int i = 0 ; i < 257 ; i++ )
                if ( scaledValue < cumulative_frequency[i+1] ) {
                    decodedSymbol = i;
                    var p = new SymbolProbability
                    {
                        low = cumulative_frequency[i],
                        high = cumulative_frequency[i + 1],
                        count = cumulative_frequency[257]
                    };
                    return p;
                }
            throw new Exception("Decoder model found no symbol range for scaled value = "+scaledValue);
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

        /// <summary>
        /// Create a model for known data.
        /// </summary>
        /// <remarks>Would need another that takes a known table</remarks>
        public PrescanModel(Stream targetData)
        {
            // count values
            preamble = new byte[256];
            var countTable = new long[257];
            long len = 0;
            int b;
            while ((b = targetData.ReadByte()) >= 0)
            {
                countTable[b]++;
                len++;
            }

            // scale them to fit in a frequency table if required
            if (len > ArithmeticEncode.MAX_FREQ) {
                Console.WriteLine("Data counts must be scaled");
                var scale = len / ArithmeticEncode.MAX_FREQ;
                for (int i = 0; i < countTable.Length; i++)
                {
                    if (countTable[i] == 0) continue;
                    countTable[i] = (countTable[i] / scale) | 1;
                    //if (countTable[i] > 255) countTable[i] = 255; // saturate so we can have a small byte array
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
            cumulative_frequency[257] = v+1; // `+1` for stop symbol
            
            // build preamble for decode
            for (int i = 0; i < 256; i++)
            {
                preamble[i] = (byte) countTable[i];
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
            for ( int i = 0 ; i < 257 ; i++ )
                if ( scaledValue < cumulative_frequency[i+1] ) {
                    decodedSymbol = i;
                    var p = new SymbolProbability
                    {
                        low = cumulative_frequency[i],
                        high = cumulative_frequency[i + 1],
                        count = cumulative_frequency[257]
                    };
                    return p;
                }
            throw new Exception("Decoder model found no symbol range for scaled value = "+scaledValue);
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

// 8 bits for values, 1 for stop
    }
}