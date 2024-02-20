using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ImageTools.DataCompression;
using ImageTools.DataCompression.Encoding;
using ImageTools.DataCompression.Experimental;
using ImageTools.DataCompression.Huffman;
using ImageTools.DataCompression.PPM;
using ImageTools.Utilities;
using ImageTools.WaveletTransforms;
using Lomont.Compression.Codec;
using NUnit.Framework;
// ReSharper disable AssignNullToNotNullAttribute

namespace ImageTools.Tests
{
    [TestFixture]
    public class LosslessDataCompressionTests {

        [Test]
        public void truncatable_encoder_round_trip()
        {
            var subject = new TruncatableEncoder();
            
            var expected = Moby;
            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(Encoding.UTF8.GetBytes(expected));
            
            subject.CompressStream(src, encoded);
            encoded.Seek(0, SeekOrigin.Begin);
            var ok = subject.DecompressStream(encoded, dst);
            
            dst.Seek(0, SeekOrigin.Begin);
            var actual = Encoding.UTF8.GetString(dst.ToArray());
            
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Compressed: {Bin.Human(encoded.Length)}; Expanded: {Bin.Human(dst.Length)}");
            Console.WriteLine(actual);
            
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(ok, "Stream was truncated, but should not have been");
        }
        
        [Test]
        public void Decoding_a_truncated_stream()
        {
            var subject = new TruncatableEncoder();
            
            var expected = Moby;
            var encoded = new MemoryStream();
            var truncated = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(Encoding.UTF8.GetBytes(expected));
            
            subject.CompressStream(src, encoded);
            
            var encodedRaw = encoded.ToArray();
            truncated.Write(encodedRaw, 0, encodedRaw.Length / 2);
            truncated.Seek(0, SeekOrigin.Begin);
            var ok = subject.DecompressStream(truncated, dst);
            
            dst.Seek(0, SeekOrigin.Begin);
            var actual = Encoding.UTF8.GetString(dst.ToArray());
            
            Console.WriteLine($"Original: {src.Length}; Compressed: {encoded.Length}; Truncated to: {truncated.Length}; Expanded: {dst.Length}");
            
            Assert.That(ok, Is.False, "Stream was not truncated, but should have been");
            Assert.That(actual, Is.EqualTo(expected.Substring(0, actual.Length)), "Truncation was not clean"); // we should get no 'junk' at the end of the decode
        }

        [Test]
        public void sum_tree_tests()
        {
            ISumTree subject = new SumTree(16);
            ISumTree comparison = new DumbTree(16, 0);
            
            Assert.That(subject.Total(), Is.EqualTo(comparison.Total()), "Totals did not match");
            Assert.That(subject.Total(), Is.EqualTo(16), "Wrong total");
            
            subject.IncrementSymbol(5, 1);
            comparison.IncrementSymbol(5, 1);
            
            Assert.That(subject.Total(), Is.EqualTo(comparison.Total()), "Totals did not match");
            Assert.That(subject.Total(), Is.EqualTo(16), "Wrong total");
            
            var fourSubject = subject.EncodeSymbol(4);
            var fourComparison = comparison.EncodeSymbol(4);
            var fiveSubject = subject.EncodeSymbol(5);
            var fiveComparison = comparison.EncodeSymbol(5);
            var sixSubject = subject.EncodeSymbol(6);
            var sixComparison = comparison.EncodeSymbol(6);
            
            Assert.That(fourSubject.high, Is.EqualTo(fourComparison.high));
            Assert.That(fiveSubject.high, Is.EqualTo(fiveComparison.high));
            Assert.That(sixSubject.high, Is.EqualTo(sixComparison.high));
        }

        [Test]
        public void fenwick_tree_tests()
        {
            // Just some basic coverage:
            var subject = new FenwickTree(256, -1);
            
            subject.IncrementSymbol(1, 1);
            Assert.That(subject.Find(0), Is.EqualTo(0), "Bad index 0");
            Assert.That(subject.Find(1), Is.EqualTo(1), "Bad index 1");
            Assert.That(subject.Find(2), Is.EqualTo(1), "Bad index 2");
            
            var f1 = subject.Find(128);
            Assert.That(f1, Is.EqualTo(127), "Bad index middle");
            
            subject.IncrementSymbol(30, 5);
            subject.SetSymbolCount(50, 10);
            
            Assert.That(subject.GetSymbolCount(30), Is.EqualTo(6), "Add failed");
            Assert.That(subject.GetSymbolCount(50), Is.EqualTo(10), "Set failed");
            
            var f2 = subject.Find(128);
            Assert.That(f2, Is.EqualTo(113), "Bad index after edit");
            
            Assert.That(subject.PrefixSum(51), Is.EqualTo(66), "Bad prefix");
            Assert.That(subject.RangeSum(1, 51), Is.EqualTo(65), "Bad range");
            
            var hist = subject.Histogram();
            Assert.That(hist![0], Is.EqualTo(1), "hist 0");
            Assert.That(hist[1], Is.EqualTo(2), "hist 1");
            Assert.That(hist[30], Is.EqualTo(6), "hist 30");
            Assert.That(hist[50], Is.EqualTo(10), "hist 50");
        }

        [Test]
        public void arithmetic_encoder_constant_values () {
            Console.WriteLine($"Actual values: Bit Size = {ArithmeticEncode.BIT_SIZE}, Precision = {ArithmeticEncode.PRECISION},\r\n" +
                              $"Symbol bits = {ArithmeticEncode.CODE_VALUE_BITS}, Frequency bits = {ArithmeticEncode.FREQUENCY_BITS},\r\n" +
                              $"Max code value = {ArithmeticEncode.MAX_CODE:X}, Max frequency value = {ArithmeticEncode.MAX_FREQ:X},\r\n" +
                              $"1/4 threshold = {ArithmeticEncode.ONE_QUARTER:X}, 2/4 threshold = {ArithmeticEncode.ONE_HALF:X}, 3/4 threshold = {ArithmeticEncode.THREE_QUARTERS:X}");
        }

        [Test]
        public void can_encode_a_byte_stream ()
        {
            var rnd = new Random();

            var subject = new ArithmeticEncode(new ProbabilityModels.SimpleLearningModel());
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

            var model = new ProbabilityModels.PushToFrontModel(fallOff:5);
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

            var subject = new ArithmeticEncode(new ProbabilityModels.SimpleLearningModel());
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

            var subject = new ArithmeticEncode(new ProbabilityModels.PushToFrontModel());
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

            var subject = new ArithmeticEncode(new ProbabilityModels.LearningMarkov_2D());
            var result = new MemoryStream();

            sw.Restart();
            var inputBytes = new byte[100_000];
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
        public void compressing_a_wavelet_image_with_AC () {
            // An experiment to see how a simple model and arith. coding works with Wavelet coefficients

            /* FINDINGS
             
            UINT32:
            =======
            Raw 'Y' size = 4mb
            AC encoded 'Y' size = 200.58kb          (fixed prescan model)
            Deflate encoded 'Y' size = 180.08kb

            UINT16:
            =======
            Raw 'Y' size = 2mb
            AC encoded 'Y' size = 180.85kb          (fixed prescan model)
            Deflate encoded 'Y' size = 151.15kb

            INT16:
            ======
            Raw 'Y' size = 2mb
            AC encoded 'Y' size = 202.89kb          (learning markov)
            Deflate encoded 'Y' size = 154.31kb

            FIBONACCI CODED:
            ================
            Raw 'Y' size = 319kb
            AC encoded 'Y' size = 231.1kb           (push to front model)
            AC encoded 'Y' size = 187.81kb          (rolling[4250])
            AC encoded 'Y' size = 169.03kb          (simple learning with burst start)
            AC encoded 'Y' size = 160.14kb			(simple learning model)
            AC encoded 'Y' size = 157.26kb          (fixed prescan limited to 256)
            AC encoded 'Y' size = 148.77kb          (fixed prescan model)
            AC encoded 'Y' size = 140.86kb          (rolling[8000] with divide)
            Deflate encoded 'Y' size = 123.47kb
            AC encoded 'Y' size = 121.46kb          (learning markov -- finally beat deflate!)
            AC encoded 'Y' size = 121.2kb           (learning markov with 256 byte lead-in)
            AC encoded 'Y' size = 121.04kb          (learning markov with +2)
            AC encoded 'Y' size = 120.27kb          (learning markov with 256b lead-in and +2 growth)
            AC encoded 'Y' size = 129.28kb          (learning markov-3 with 256b lead-in and +2 growth)

            ELIAS OMEGA CODED:
            ==================
            Raw 'Y' size = 228.34kb
            AC encoded 'Y' size = 126.76kb          (learning markov) Interesting that the raw size is smaller, but the compression is worse.

            BYTE BLOCK CODED:
            =================
            Raw 'Y' size = 1mb
            AC encoded 'Y' size = 131.08kb          (learning markov)

            SHORT BYTE BLOCK CODED:
            =======================
            Raw 'Y' size = 267.13kb
            AC encoded 'Y' size = 136.13kb          (learning markov)
            
            */


            var msY = new MemoryStream();
            var msU = new MemoryStream();
            var msV = new MemoryStream();

            var acY = new MemoryStream();
            var finalY = new MemoryStream();

            var sw = new Stopwatch();
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                sw.Restart();
                WaveletCompress.ReduceImage2D_ToStreams(bmp, CDF.Fwt97, msY, msU, msV);
                sw.Stop();
                Console.WriteLine($"Wavelet transform took {sw.Elapsed}");

                Console.WriteLine($"Raw 'Y' size = {Bin.Human(msY.Length)}");

                msY.Seek(0, SeekOrigin.Begin);
                var model = new ProbabilityModels.LearningMarkov_2D(256);

                // Try our simple encoding
                var subject = new ArithmeticEncode(model);
                msY.Seek(0, SeekOrigin.Begin);
                sw.Restart();
                model.WritePreamble(acY);
                subject.Encode(msY, acY);
                sw.Stop();
                Console.WriteLine($"Arithmetic coding took {sw.Elapsed}");

                Console.WriteLine($"AC encoded 'Y' size = {Bin.Human(acY.Length)}");


                subject.Reset();
                // Now decode:

                acY.Seek(0, SeekOrigin.Begin);
                sw.Restart();
                model.ReadPreamble(acY);
                subject.Decode(acY, finalY);
                sw.Stop();
                Console.WriteLine($"Arithmetic decoding took {sw.Elapsed}");

                finalY.Seek(0, SeekOrigin.Begin);
                msU.Seek(0, SeekOrigin.Begin);
                msV.Seek(0, SeekOrigin.Begin);

                sw.Restart();
                var resultBmp = WaveletCompress.RestoreImage2D_FromStreams(bmp.Width, bmp.Height, finalY, msU, msV, CDF.Iwt97);
                sw.Stop();
                Console.WriteLine($"Wavelet transform took {sw.Elapsed}");
                resultBmp.SaveBmp("./outputs/ArithmeticEncode_3.bmp");
            }
        }
        
        
        [Test]
        public void compressing_a_wavelet_image_with_LZMA () {
            /* FINDINGS
             
            Deflate encoded 'Y' size = 123.47kb
            AC encoded 'Y' size = 120.27kb          (learning markov with 256b lead-in and +2 growth)
            LZMA encoded 'Y' size = 117.49kb
            
            */


            var msY = new MemoryStream();
            var msU = new MemoryStream();
            var msV = new MemoryStream();

            var acY = new MemoryStream();
            var finalY = new MemoryStream();

            var sw = new Stopwatch();
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                sw.Restart();
                WaveletCompress.ReduceImage2D_ToStreams(bmp, CDF.Fwt97, msY, msU, msV);
                sw.Stop();
                Console.WriteLine($"Wavelet transform took {sw.Elapsed}");

                Console.WriteLine($"Raw 'Y' size = {Bin.Human(msY.Length)}");

                // Try compression
                msY.Seek(0, SeekOrigin.Begin);
                sw.Restart();
                DataCompression.LZMA.LzmaCompressor.Compress(msY, acY);
                sw.Stop();
                Console.WriteLine($"LZMA coding took {sw.Elapsed}");

                Console.WriteLine($"LZMA encoded 'Y' size = {Bin.Human(acY.Length)}");


                // Now decode:
                acY.Seek(0, SeekOrigin.Begin);
                sw.Restart();
                DataCompression.LZMA.LzmaCompressor.Decompress(acY, finalY);
                sw.Stop();
                Console.WriteLine($"LZMA decoding took {sw.Elapsed}");

                finalY.Seek(0, SeekOrigin.Begin);
                msU.Seek(0, SeekOrigin.Begin);
                msV.Seek(0, SeekOrigin.Begin);

                sw.Restart();
                var resultBmp = WaveletCompress.RestoreImage2D_FromStreams(bmp.Width, bmp.Height, finalY, msU, msV, CDF.Iwt97);
                sw.Stop();
                Console.WriteLine($"Wavelet transform took {sw.Elapsed}");
                resultBmp.SaveBmp("./outputs/LzmaEncode_3.bmp");
            }
            
            // Check that input and output of compressor are identical
            msY.Seek(0, SeekOrigin.Begin);
            finalY.Seek(0, SeekOrigin.Begin);

            while (true)
            {
                var inp = msY.ReadByte();
                var outp = finalY.ReadByte();
                Assert.AreEqual(inp, outp);
                
                if (inp < 0 || outp < 0) break;
            }
        }
        
        [Test]
        public void compressing_a_wavelet_image_with_PPM () {
            var msY = new MemoryStream();
            var msU = new MemoryStream();
            var msV = new MemoryStream();

            /*
             
             Results -- compressions is nowhere near as good as deflate or arithmetic coding,
                        but the speed is very good given the non-existent optimisations
             
             PPM:
                Raw 'Y' size = 320.86kb
                Compression took 00:00:00.0058395
                Encoded 'Y' size = 177.29kb
                Decompression took 00:00:00.0054271
            
            Compared to Markov Arithmetic Coding;
                Raw 'Y' size = 320.86kb
                Arithmetic coding took 00:00:00.1050660
                AC encoded 'Y' size = 122.52kb
                Arithmetic decoding took 00:00:00.3786367
*/

            var acY = new MemoryStream();
            var finalY = new MemoryStream();

            var sw = new Stopwatch();
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                sw.Restart();
                WaveletCompress.ReduceImage2D_ToStreams(bmp, CDF.Fwt97, msY, msU, msV);
                sw.Stop();
                Console.WriteLine($"Wavelet transform took {sw.Elapsed}");

                Console.WriteLine($"Raw 'Y' size = {Bin.Human(msY.Length)}");

                msY.Seek(0, SeekOrigin.Begin);

                // Try the PPM 'Finnish' Hash match compressor
                var subject = new HashMatchCompressor();
                msY.Seek(0, SeekOrigin.Begin);
                sw.Restart();
                var compressed = subject.Compress(msY.ToArray());
                sw.Stop();
                Console.WriteLine($"Compression took {sw.Elapsed}");

                acY.Write(compressed, 0, compressed.Length);
                Console.WriteLine($"Encoded 'Y' size = {Bin.Human(acY.Length)}");


                // Now decode:

                acY.Seek(0, SeekOrigin.Begin);
                sw.Restart();
                var decompressed = subject.Decompress(acY.ToArray());
                sw.Stop();
                finalY.Write(decompressed, 0, decompressed.Length);
                Console.WriteLine($"Decompression took {sw.Elapsed}");

                finalY.Seek(0, SeekOrigin.Begin);
                msU.Seek(0, SeekOrigin.Begin);
                msV.Seek(0, SeekOrigin.Begin);

                sw.Restart();
                var resultBmp = WaveletCompress.RestoreImage2D_FromStreams(bmp.Width, bmp.Height, finalY, msU, msV, CDF.Iwt97);
                sw.Stop();
                Console.WriteLine($"Wavelet transform took {sw.Elapsed}");
                resultBmp.SaveBmp("./outputs/PPM_Hash_Encode_3.bmp");
            }
        }
        
        [Test]
        public void compressing_a_wavelet_image_with_PPM_and_custom_settings () {
/*
            Raw 'Y' size = 320.86kb
            
            20,3,-: 175.43kb
            18,3,-: 175.39kb
            15,3,-: 175.31kb
            12,3,-: 177.29kb
            10,3,-: 179.32kb
             8,3,-: 180.96kb
            
            20,2,-: 169.58kb
            18,2,-: 169.58kb  <--- (256kb LUT)
            15,2,-: 169.88kb
            12,2,-: 172.16kb
            10,2,-: 174.41kb
             8,2,-: 176.67kb
            
            10,1,-: 171.04kb
             8,1,-: 171.81kb  <--- (256b LUT)
             6,1,-: 173.44kb
             4,1,-: 174.04kb  <--- (16b LUT)    Quite a good result for very small embedded systems
             2,1,-: 174.38kb
             1,1,-: 174.67kb  <--- (1b LUT)     This is essentially run-length encoding
*/

            
            var subject = new HashMatchCompressor(1, 1);
            
            
            var msY = new MemoryStream();
            var msU = new MemoryStream();
            var msV = new MemoryStream();

            var acY = new MemoryStream();
            var finalY = new MemoryStream();

            var sw = new Stopwatch();
            using var bmp = Load.FromFile("./inputs/3.png");
            
            sw.Restart();
            WaveletCompress.ReduceImage2D_ToStreams(bmp, CDF.Fwt97, msY, msU, msV);
            sw.Stop();
            Console.WriteLine($"Wavelet transform took {sw.Elapsed}");

            Console.WriteLine($"Raw 'Y' size = {Bin.Human(msY.Length)}");

            msY.Seek(0, SeekOrigin.Begin);

            // Try the PPM 'Finnish' Hash match compressor
            msY.Seek(0, SeekOrigin.Begin);
            sw.Restart();
            var compressed = subject.Compress(msY.ToArray());
            sw.Stop();
            Console.WriteLine($"Compression took {sw.Elapsed}");

            acY.Write(compressed, 0, compressed.Length);
            Console.WriteLine($"Encoded 'Y' size = {Bin.Human(acY.Length)}");


            // Now decode:

            acY.Seek(0, SeekOrigin.Begin);
            sw.Restart();
            var decompressed = subject.Decompress(acY.ToArray());
            sw.Stop();
            finalY.Write(decompressed, 0, decompressed.Length);
            Console.WriteLine($"Decompression took {sw.Elapsed}");

            finalY.Seek(0, SeekOrigin.Begin);
            msU.Seek(0, SeekOrigin.Begin);
            msV.Seek(0, SeekOrigin.Begin);

            sw.Restart();
            var resultBmp = WaveletCompress.RestoreImage2D_FromStreams(bmp.Width, bmp.Height, finalY, msU, msV, CDF.Iwt97);
            sw.Stop();
            Console.WriteLine($"Wavelet transform took {sw.Elapsed}");
            resultBmp.SaveBmp("./outputs/PPM_Hash_Encode_Custom_3.bmp");
        }

        [Test]
        public void xor_forward_filter()
        {
            var srcBytes = Encoding.UTF8.GetBytes(Moby);

            Console.WriteLine("Before:");
            //Console.WriteLine(Bin.HexString(srcBytes));
            Console.WriteLine(Encoding.UTF8.GetString(srcBytes));
            
            var length = DeflateSize(srcBytes);
            Console.WriteLine($"\r\nDeflate before transform: {Bin.Human(length)}");
            
            for (int i = 1; i < srcBytes.Length; i++)
            {
                srcBytes[i] = (byte)(srcBytes[i] ^ srcBytes[i-1]);
            }
            
            length = DeflateSize(srcBytes);
            Console.WriteLine($"Deflate after transform: {Bin.Human(length)}");
            
            Console.WriteLine("\r\n\r\nAfter:");
            Console.WriteLine(Bin.HexString(srcBytes));
            //Console.WriteLine(Encoding.UTF8.GetString(srcBytes));
            
            // Must go in reverse
            for (int i = srcBytes.Length - 1; i > 0; i--)
            {
                srcBytes[i] = (byte)(srcBytes[i] ^ srcBytes[i-1]);
            }
            
            Console.WriteLine("\r\n\r\nUn-applied:");
            //Console.WriteLine(Bin.HexString(srcBytes));
            Console.WriteLine(Encoding.UTF8.GetString(srcBytes));
        }

        private static long DeflateSize(byte[] srcBytes)
        {
            var test = new MemoryStream();
            using (var gz =  new DeflateStream(test, CompressionLevel.Optimal, true)) {
                gz.Write(srcBytes,0,srcBytes.Length);
                gz.Flush();
            }
            var length = test.Length;
            return length;
        }

        public const string Moby = @"####Call me Ishmael. Some years ago--never mind how long precisely--having
little or no money in my purse, and nothing particular to interest me on
shore, I thought I would sail about a little and see the watery part of
the world. It is a way I have of driving off the spleen and regulating
the circulation. Whenever I find myself growing grim about the mouth;
whenever it is a damp, drizzly November in my soul; whenever I find
myself involuntarily pausing before coffin warehouses, and bringing up
the rear of every funeral I meet;#### and especially whenever my hypos get
such an upper hand of me, that it requires a strong moral principle to
prevent me from deliberately stepping into the street, and methodically
knocking people's hats off--then, I account it high time to get to sea
as soon as I can. This is my substitute for pistol and ball. With a
philosophical flourish Cato throws himself upon his sword; I quietly
take to the ship. There is nothing surprising in this. If they but knew
it, almost all men in their degree, some time or other, cherish very
nearly the same feelings towards the ocean with me.####";
        
		public const string Poem = @"Wherever you go, I follow,
hands held across sidewalks
frozen
or slopes more slippery
called love,
life, and 1685 miles
of ""I can't wait
to be there when you fall.""";

        [Test, Description("From the Bluetooth LE spec")]
        public void data_whitener_test()
        {
            var input = Encoding.ASCII.GetBytes("Hello,      world!");
            Console.WriteLine(Bin.HexString(input));
            Console.WriteLine(Bin.BinString(input));
            Console.WriteLine();

            // "Whiten" input (try to reduce bit-level correlations)
            Whitener.BtLeWhiten(input, 0x88);
            Console.WriteLine(Bin.HexString(input));
            Console.WriteLine(Bin.BinString(input));
            Console.WriteLine();
            
            // Re-apply transform to get original data
            Whitener.BtLeWhiten(input, 0x88);
            Console.WriteLine(Bin.HexString(input));
            Console.WriteLine(Bin.BinString(input));
            Console.WriteLine(Encoding.ASCII.GetString(input));
            
            
            var large = Encoding.ASCII.GetBytes(Moby);
            var beforeSz = DeflateSize(large);
            Whitener.BtLeWhiten(large, 50);
            var afterSz = DeflateSize(large);
            Console.WriteLine($"Before: {Bin.Human(beforeSz)}; After: {Bin.Human(afterSz)}");
        }

        [Test]
        public void ac_round_trip () {
            var expected = Moby;

            // Equivalent deflate: 645b
            // Original: 1.11kb; Encoded: 672b (simple learning + 16)
            // Original: 1.11kb; Encoded: 740b (simple learning + 1)
            // Original: 1.11kb; Encoded: 785b (rolling 1000)
            // Original: 1.11kb; Encoded: 859b (learning markov + 2)
            // Original: 1.11kb; Encoded: 859b (LearningMarkov_2D)
            // Original: 1.11kb; Encoded: 893b (prescan -- 637b without preamble)
            // Original: 1.11kb; Encoded: 1.03kb (LearningMarkov_3D)
            // Original: 1.11kb; Encoded: 1.05kb (LearningMarkov_2DH4)

            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(Encoding.UTF8.GetBytes(expected));

            src.Seek(0, SeekOrigin.Begin);
            var test = new MemoryStream();
            using (var gz =  new DeflateStream(test, CompressionLevel.Optimal, true)) {
                var bytes = Encoding.UTF8.GetBytes(expected);
                gz.Write(bytes,0,bytes.Length);
                gz.Flush();
            }
            Console.WriteLine($"Equivalent deflate: {Bin.Human(test.Length)}");


            src.Seek(0, SeekOrigin.Begin);
            //var model = new ProbabilityModels.PrescanModel(src);
            //var model = new ProbabilityModels.SimpleLearningModel();
            //var model = new ProbabilityModels.LearningMarkov_2D();
            var model = new ProbabilityModels.LearningMarkov_3D();
            var subject = new ArithmeticEncode(model);
            src.Seek(0, SeekOrigin.Begin);
            model.WritePreamble(encoded);
            subject.Encode(src, encoded);
            encoded.Seek(0, SeekOrigin.Begin);

            subject.Reset();
            var percent = (100.0 * encoded.Length) / src.Length;
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            model.ReadPreamble(encoded);
            subject.Decode(encoded, dst);

            dst.Seek(0, SeekOrigin.Begin);
            var result = Encoding.UTF8.GetString(dst.ToArray());

            Console.WriteLine("\r\n--------RESULT----------");
            Console.WriteLine(result);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void lzac_round_trip()
        {
            var expected = Moby;

            // Equivalent deflate: 645b
            // ...
            // Original: 1.11kb; Encoded: 714b (62.8%)  <-- before implementing LZ part, this is just the AC
            // Original: 1.11kb; Encoded: 713b (62.7%)  <-- with basic LZ, plus AC (before implementing decode)
            // Original: 1.11kb; Encoded: 691b (60.8%)  <-- wide symbol space LZW (before implementing decode)
            // Original: 1.11kb; Encoded: 685b (60.2%)  <-- wide symbol + 4 gain

            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(Encoding.UTF8.GetBytes(expected));


            src.Seek(0, SeekOrigin.Begin);
            var subject = new LZAC();
            src.Seek(0, SeekOrigin.Begin);
            subject.Encode(src, encoded);
            encoded.Seek(0, SeekOrigin.Begin);

            subject.Reset();
            var percent = (100.0 * encoded.Length) / src.Length;
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            subject.Decode(encoded, dst);

            dst.Seek(0, SeekOrigin.Begin);
            var result = Encoding.UTF8.GetString(dst.ToArray());

            Console.WriteLine("\r\n--------RESULT----------");
            Console.WriteLine(result);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void lzw_round_trip () {
            var expected = Moby;

            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(Encoding.UTF8.GetBytes(expected));
            var lzPack = new LZWPack();

            lzPack.Encode(src, encoded);
            encoded.Seek(0, SeekOrigin.Begin);

            var percent = (100.0 * encoded.Length) / src.Length;
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            lzPack.Decode(encoded, dst);

            dst.Seek(0, SeekOrigin.Begin);
            var result = Encoding.UTF8.GetString(dst.ToArray());

            Console.WriteLine(result);

            // failing at size limit. Are we deleting the dictionary entries out of order?
            Assert.That(result, Is.EqualTo(expected));
            Assert.Pass($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
        }

        [Test]
        public void lzss_round_trip () {
            var expected = Moby;

            //Original: 1.11kb; Encoded: 773b

            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(Encoding.UTF8.GetBytes(expected));
            var lzPack = new LZSSPack();

            lzPack.Encode(src, encoded);
            encoded.Seek(0, SeekOrigin.Begin);

            var percent = (100.0 * encoded.Length) / src.Length;
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            lzPack.Decode(encoded, dst);

            dst.Seek(0, SeekOrigin.Begin);
            var result = Encoding.UTF8.GetString(dst.ToArray());

            Console.WriteLine("\r\n--------RESULT----------");
            Console.WriteLine(result);

            // failing at size limit. Are we deleting the dictionary entries out of order?
            Assert.That(result, Is.EqualTo(expected));
            Assert.Pass($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
        }

        [Test]
        public void lzmw_round_trip () {
            var expected = Moby;

            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(Encoding.UTF8.GetBytes(expected));
            var lzPack = new LZMWPack(sizeLimit:50);

            lzPack.Encode(src, encoded);
            encoded.Seek(0, SeekOrigin.Begin);
            var percent = (100.0 * encoded.Length) / src.Length;
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            lzPack.Decode(encoded, dst);

            dst.Seek(0, SeekOrigin.Begin);
            var result = Encoding.UTF8.GetString(dst.ToArray());
            
            Console.WriteLine(result);

            // failing at size limit. Are we deleting the dictionary entries out of order?
            Assert.That(result, Is.EqualTo(expected));
            Assert.Pass($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
        }

        [Test]
        public void lzma_round_trip () {
            // Original: 1.11kb; Encoded: 784b (69.0%)
            var expected = Moby;

            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(Encoding.UTF8.GetBytes(expected));

            DataCompression.LZMA.LzmaCompressor.Compress(src, encoded);
            encoded.Seek(0, SeekOrigin.Begin);
            var percent = (100.0 * encoded.Length) / src.Length;
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            DataCompression.LZMA.LzmaCompressor.Decompress(encoded, dst);
            Console.WriteLine();

            dst.Seek(0, SeekOrigin.Begin);
            var result = Encoding.UTF8.GetString(dst.ToArray());
            
            Console.WriteLine(result);

            Assert.That(result, Is.EqualTo(expected));
            Assert.Pass($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
        }
        
        [Test]
        public void lzjb_round_trip () {
            // Original: 1.11kb; Encoded: 1.03kb (92.6%)  <-- not great, but it is very fast
            var expected = Moby;

            var src = Encoding.UTF8.GetBytes(expected);
            var dst = new byte[src.Length];
            var encoded = new byte[src.Length];

            var compressLength = Lzjb.Compress(src, encoded); // TODO: implement with Stream?
            
            var percent = (100.0 * compressLength) / src.Length;
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(compressLength)} ({percent:0.0}%)");

            /*for (int i = 0; i < compressLength; i++)
            {
                if (encoded[i] > 30 && encoded[i] < 120) Console.Write((char)encoded[i]);
                else Console.Write($"_{encoded[i]}");
            }*/
            
            var decompressLength = Lzjb.Decompress(encoded, compressLength, dst);
            Console.WriteLine();

            var result = Encoding.UTF8.GetString(dst, 0, decompressLength);
            
            Console.WriteLine(result);

            Assert.That(result, Is.EqualTo(expected));
            Assert.Pass($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
        }
        
        [Test]
        public void SplitTree_round_trip () {
            var expected = Moby;

            var src = Encoding.UTF8.GetBytes(expected);
            var encoded = new MemoryStream();
            
            var beforeSz = DeflateSize(src);

            SplitTree.Compress(src, encoded);
            var afterBytes = encoded.ToArray();
            
            var afterSz = DeflateSize(afterBytes);
            
            var percent = (100.0 * encoded.Length) / src.Length;
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            Console.WriteLine($"Deflate before: {Bin.Human(beforeSz)}; after: {Bin.Human(afterSz)}");
        }
        
        [Test]
        public void hash_match_round_trip()
        {
            var subject = new HashMatchCompressor();
            
            var expected = Moby;
            var src = Encoding.UTF8.GetBytes(expected);

            var encoded = subject.Compress(src);
            var dst = subject.Decompress(encoded);
            
            var actual = Encoding.UTF8.GetString(dst);
            
            var percent = (100.0 * encoded.Length) / src.Length;
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            
            Assert.That(actual, Is.EqualTo(expected));
            Assert.Pass($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
        }

        [Test]
        public void push_to_front_round_trip()
        {
            // Original: 1.11kb; Encoded: 793b (69.7%)
            var expected = Moby;

            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(Encoding.UTF8.GetBytes(expected));

            PushToFrontEncoder.Compress(src, encoded);
            encoded.Seek(0, SeekOrigin.Begin);
            var percent = (100.0 * encoded.Length) / src.Length;
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            PushToFrontEncoder.Decompress(encoded, dst);
            Console.WriteLine();

            dst.Seek(0, SeekOrigin.Begin);
            var result = Encoding.UTF8.GetString(dst.ToArray());
            
            Console.WriteLine(result);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void compress_wavelet_image_with_LZSS () {

            var msY = new MemoryStream();
            var msU = new MemoryStream();
            var msV = new MemoryStream();

            var lzY = new MemoryStream();
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                WaveletCompress.ReduceImage2D_ToStreams(bmp, CDF.Fwt97, msY, msU, msV);

                Console.WriteLine($"Raw 'Y' size = {Bin.Human(msY.Length)}");

                var lzPack = new LZSSPack();
                msY.Seek(0, SeekOrigin.Begin);
                lzPack.Encode(msY, lzY);

                Console.WriteLine($"LZSS/AC encoded 'Y' size = {Bin.Human(lzY.Length)}");

                // Target size = 123.47kb (deflate)
                // AC alone    = 148.77kb (fixed prescan)

                // Pyramid 8..2 (0:01 rel 0:03 dev)                  Replacements = 84108; size = 160.19kb
                // Pyramid 8..3 (0:01 rel)                           Replacements = 35047; size = 160.21kb
                // Pyramid 8..4 (0:01 rel)                           Replacements = 14062; size = 160.24kb

                //256,128,..8,4:(4:35 rel)    Scans = 175551308299;  Replacements = 12931; size = 136.29kb
                // 256,128,64: (1:45 rel)     Scans = 108387474644;  Replacements =   893; size = 139.69kb
                // 64/63: (3:35)              Scans =  65184192961;  Replacements =  1680; size = 146.81kb
                // 256: (1:54)                Scans =  40270743703;  Replacements =   239; size = 151.01kb
                
                // 256,255 L32k               Scans =  16198448550;  Replacements =   221; size = 152.88kb
                // 256,128 L32k (0:24)        Scans =   8224517736;  Replacements =   354; size = 151.07kb

                // Block transform:
                // BLOCK = 512;  Time = 0:21; Scans =   4601994394;  Replacements = 10910; size = 583.88kb
                // BLOCK = 1024; Time = 0:53; Scans =  17171489936;  Replacements = 10045; size = 604.14kb
                // Limit 32k: div2 (0:08)     Scans =   2611780919;  Replacements = 11215; size = 459.23kb
                // Limit 32k: -- (6:14)       Scans = 107226517443;  Replacements =  9333; size = 484.81kb

                // B32, L32, 256-64/2 (0:35)  Scans =  11746908064;  Replacements =   799; size = 146.57kb
                // B32, L32, 256-4/2 (1:23)   Scans =  21363090799;  Replacements = 11938; size = 145.12kb
                // B32, L4, 256-4/2 (0:15)    Scans =   5161354977;  Replacements = 11532; size = 145.37kb

                // reverse LZSS...
                msY = new MemoryStream();
                lzY.Seek(0, SeekOrigin.Begin);
                lzPack.Decode(lzY, msY);

                msY.Seek(0, SeekOrigin.Begin);
                msU.Seek(0, SeekOrigin.Begin);
                msV.Seek(0, SeekOrigin.Begin);

                var resultBmp = WaveletCompress.RestoreImage2D_FromStreams(bmp.Width, bmp.Height, msY, msU, msV, CDF.Iwt97);
                resultBmp.SaveBmp("./outputs/LZSS_3.bmp");

            }
        }

        [Test, Explicit("This is currently impractically slow, and faulty")]
        public void compress_wavelet_image_with_LZW_and_AC () {
            // playing with simple dictionary coding

            var msY = new MemoryStream();
            var msU = new MemoryStream();
            var msV = new MemoryStream();

            var lzY = new MemoryStream();
            var acY = new MemoryStream();
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                
                WaveletCompress.ReduceImage2D_ToStreams(bmp, CDF.Fwt97, msY, msU, msV);

                Console.WriteLine($"Raw 'Y' size = {Bin.Human(msY.Length)}");

                var lzPack = new LZWPack();
                msY.Seek(0, SeekOrigin.Begin);
                lzPack.Encode(msY, lzY);

                lzY.Seek(0, SeekOrigin.Begin);
                //var model = new ProbabilityModels.PrescanModel(lzY);
                var model = new ProbabilityModels.LearningMarkov_2D();
                var arithmeticEncode = new ArithmeticEncode(model);
                lzY.Seek(0, SeekOrigin.Begin);
                model.WritePreamble(acY);
                arithmeticEncode.Encode(lzY, acY);

                Console.WriteLine($"LZ encoded 'Y' size = {Bin.Human(lzY.Length)}");
                Console.WriteLine($"AC encoded 'Y' size = {Bin.Human(acY.Length)}");

                // Now decode:
                acY.Seek(0, SeekOrigin.Begin);
                arithmeticEncode.Reset();
                model.ReadPreamble(acY);
                lzY = new MemoryStream();
                arithmeticEncode.Decode(acY, lzY);

                // reverse LZMW...
                msY = new MemoryStream();
                lzY.Seek(0, SeekOrigin.Begin);
                lzPack.Decode(lzY, msY);

                msY.Seek(0, SeekOrigin.Begin);
                msU.Seek(0, SeekOrigin.Begin);
                msV.Seek(0, SeekOrigin.Begin);

                var resultBmp = WaveletCompress.RestoreImage2D_FromStreams(bmp.Width, bmp.Height, msY, msU, msV, CDF.Iwt97);
                resultBmp.SaveBmp("./outputs/LZW_AC_3.bmp");
            }
        }

        [Test]
        public void compress_wavelet_image_with_LZMW_and_AC () {
            // playing with simple dictionary coding

            var msY = new MemoryStream();
            var msU = new MemoryStream();
            var msV = new MemoryStream();

            var lzY = new MemoryStream();
            var acY = new MemoryStream();
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                
                WaveletCompress.ReduceImage2D_ToStreams(bmp, CDF.Fwt97, msY, msU, msV);

                Console.WriteLine($"Raw 'Y' size = {Bin.Human(msY.Length)}");

////////////////////////////////////////////////////////////////////////////////
// limit: 25 -> 172.28kb; 152.11kb
// limit: 34 -> 171.17kb; 152kb
// limit: 50 -> 170.99kb; 152.59kb
//                        151.21kb  <-- prescan
// limit: 55 -> 171.3kb;  152.82kb
// limit: 75 -> 173.1kb;  154.25kb
// limit:100 -> 179.12kb; 157.03kb
// limit:150 -> 188.4kb;  160.98kb 
//      Deflate encoded = 123.47kb
// LZW -> 165.13kb; 147.76kb

                var lzPack = new LZMWPack(sizeLimit:50);
                //var lzPack = new LZWPack();
                msY.Seek(0, SeekOrigin.Begin);
                lzPack.Encode(msY, lzY);
                //msY.CopyTo(lzY);

                lzY.Seek(0, SeekOrigin.Begin);
                var model = new ProbabilityModels.LearningMarkov_2D();
                var arithmeticEncode = new ArithmeticEncode(model);
                lzY.Seek(0, SeekOrigin.Begin);
                model.WritePreamble(acY);
                arithmeticEncode.Encode(lzY, acY);

                Console.WriteLine($"LZ encoded 'Y' size = {Bin.Human(lzY.Length)}");
                Console.WriteLine($"AC encoded 'Y' size = {Bin.Human(acY.Length)}");
                arithmeticEncode.Reset();

                // Now decode:
                acY.Seek(0, SeekOrigin.Begin);
                arithmeticEncode.Reset();
                model.ReadPreamble(acY);
                lzY = new MemoryStream();
                arithmeticEncode.Decode(acY, lzY);

                // reverse LZMW...
                msY = new MemoryStream();
                lzY.Seek(0, SeekOrigin.Begin);
                lzPack.Decode(lzY, msY);

                msY.Seek(0, SeekOrigin.Begin);
                msU.Seek(0, SeekOrigin.Begin);
                msV.Seek(0, SeekOrigin.Begin);

                var resultBmp = WaveletCompress.RestoreImage2D_FromStreams(bmp.Width, bmp.Height, msY, msU, msV, CDF.Iwt97);
                resultBmp.SaveBmp("./outputs/LZAC_3.bmp");

                // Deflate encoded   = 123.47kb <-- size to beat
                // LZ+AC best so far = 157.03kb (sizeLimit:100; SimpleLearningModel)
                // AC only best      = 148.78kb (fixed prescan model)

                // AC STATS (with 138kb input & fib coded source)
                //================================================
                // Simple Learning = 135.17kb
                // Prescan         = 134.23kb (including preamble)
                // Push to front   = 252.43kb (!)
                // Rolling (500)   = 260.96kb
                // Rolling (MaxF)  = 235.91kb

                // Other sources (simple learn)
                //===============
                // INT16   = 165.68kb
                // UINT16  = 148.28kb
                // UINT32  = 168.25kb 

                // LZ STATS
                //==========
                // Raw 'Y' size = 319kb
                // Deflate encoded 'Y' size = 123.47kb <-- size to beat
                // Best so far = 137.99kb (sizeLimit:200)
                //
                // Unbounded dict length = 52140
                //  Estimated size = 101.84kb
                //  real size      = 156.13kb
                // Average backreference value
                //  Insert at end    =  9000
                //  Insert at start  = 17066
                //  Sorted           = 10109
                //  Pull on match    =  6907
                //  Ins Start + PoM  =  6188   <--- best so far
                //  Constant shuffle =  6906
                //  Ins Strt + consh =  6189
                //
                // Bounded length  = 10'000
                //  Estimated size = 109.07kb
                //  real size      = 150.11kb
                //  Ave backref    = 1806
                //
                // Bounded length  = 1'000
                //  Estimated size = 128.68kb
                //  real size      = 140.47kb
                //  Ave backref    = 231
                //
                // Bounded length  = 255
                //  Estimated size = 154.34kb
                //  real size      = 139.09kb
                //  real wit byte  = 160.35kb  <-- i.e. no fibonacci for dict offset
                //  Ave backref    = 56
            }
        }

        [Test, Description(@"Chris Lomont's LZ algorithm. Designed to be compact, not fast")]
        public void compress_firmware_image_lzcl()
        {
            // Equivalent deflate: 307.28kb (61.6%)
            //
            // Original: 499.23kb; Encoded: 326.84kb (65.5%)
            
            var path = @"C:\temp\LargeEspIdf.bin";
            var expected = File.ReadAllBytes(path);

            var codec = new LzclCodec();
            var encoded = codec.Compress(expected);
            
            var percent = (100.0 * encoded.Length) / expected.Length;
            Console.WriteLine($"Original: {Bin.Human(expected.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            
            var result = codec.Decompress(encoded);
            Console.WriteLine();
            
            Assert.That(result, Is.EqualTo(expected).AsCollection);
            Assert.Pass($"Original: {Bin.Human(expected.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
        }

        [Test, Description("This is the 7zip algorithm. Beats deflate by a fair margin")]
        public void compress_firmware_image_lzma()
        {
            // Equivalent deflate: 321.13kb (64.0%) <-- bin v1
            // Equivalent deflate: 307.28kb (61.6%) <-- bin v2
            //
            // Original: 503.48kb; Encoded: 291.35kb (57.9%)  <-- bin v1
            // Original: 499.23kb; Encoded: 277.37kb (55.6%)  <-- bin v2
            
            var path = @"C:\temp\LargeEspIdf.bin";
            var expected = File.ReadAllBytes(path);
            

            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(expected);

            DataCompression.LZMA.LzmaCompressor.Compress(src, encoded);
            encoded.Seek(0, SeekOrigin.Begin);
            var percent = (100.0 * encoded.Length) / src.Length;
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            DataCompression.LZMA.LzmaCompressor.Decompress(encoded, dst);
            Console.WriteLine();

            dst.Seek(0, SeekOrigin.Begin);
            var result = dst.ToArray();
            
            Assert.That(result, Is.EqualTo(expected).AsCollection);
            Assert.Pass($"Original: {Bin.Human(expected.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
        }
        
        [Test]
        public void compress_firmware_image_push_to_front()
        {
            // Original: 503.48kb; Encoded: 471.83kb (93.7%) <-- plain
            // Original: 499.24kb; Encoded: 418.63kb (83.9%) <-- with STX
            var path = @"C:\temp\LargeEspIdf.bin";
            var expected = File.ReadAllBytes(path);
            
            var stxIn = Stx.ForwardTransform(expected);

            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(stxIn);

            PushToFrontEncoder.Compress(src, encoded);
            encoded.Seek(0, SeekOrigin.Begin);
            var percent = (100.0 * encoded.Length) / src.Length;
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            PushToFrontEncoder.Decompress(encoded, dst);
            Console.WriteLine();

            dst.Seek(0, SeekOrigin.Begin);
            var stxOut = dst.ToArray();
            var result = Stx.ReverseTransform(stxOut);
            
            Assert.That(result, Is.EqualTo(expected).AsCollection);
        }
        
        [Test]
        public void compress_firmware_image_ac () {
            // Equivalent deflate:             307.28kb (61.6%)
            // LearningMarkov_2D:     Encoded: 377.64kb (75.3%) bin v2 -> Original: 499.23kb; Encoded: 367.67kb (73.6%)
            // LearningMarkov_2DHn=2: Encoded: 377.64kb (75.3%)
            // LearningMarkov_3D:     Encoded: 392.41kb (78.3%)
            // LearningMarkov_2DHn=4: Encoded: 432.58kb (86.3%)
            // SimpleLearningModel:   Encoded: 443.36kb (88.4%)
            // BraindeadModel:        Encoded: 494.35kb (98.6%)
            // PushToFrontModel:      Encoded: 774.3kb (154.4%)

            // 2DHn:
            // 1,8=(75.8%);  2,8=(75.8%);  3,8=(86.2%)
            // 1,16=(76.4%); 2,16=(76.4%); 3,16=(87.2%)
            
            // 2D (gain):
            // 1=(75.7%); 2=(75.3%); 3=(75.3%); 4=(75.4%); 8=(75.8%); 16=(76.4%)
            
            // Simple (gain)
            // 1=(88.4%); 2=(88.4%); 4=(88.4%); 8=(88.4%); 16=(88.4%); 32=(88.4%)
            
            var path = @"C:\temp\LargeEspIdf.bin";
            var expected = File.ReadAllBytes(path);

            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(expected);

            src.Seek(0, SeekOrigin.Begin);
            var test = new MemoryStream();
            using (var gz =  new DeflateStream(test, CompressionLevel.Optimal, true)) {
                var bytes = expected;
                gz.Write(bytes,0,bytes.Length);
                gz.Flush();
            }
            var percentDeflate = (100.0 * test.Length) / src.Length;
            Console.WriteLine($"Equivalent deflate: {Bin.Human(test.Length)} ({percentDeflate:0.0}%)");
            
            src.Seek(0, SeekOrigin.Begin);
            //var model = new ProbabilityModels.PushToFrontModel(3);
            //var model = new ProbabilityModels.SimpleLearningModel(32);
            var model = new ProbabilityModels.LearningMarkov_2D(0, 2);
            //var model = new ProbabilityModels.BraindeadModel();
            //var model = new ProbabilityModels.MixShiftModel();
            //var model = new ProbabilityModels.LearningMarkov_3D();
            //var model = new ProbabilityModels.LearningMarkov_2DHn(0, 3,16);
            
            model.Reset();
            var subject = new ArithmeticEncode(model);
            src.Seek(0, SeekOrigin.Begin);
            model.WritePreamble(encoded);
            subject.Encode(src, encoded);
            encoded.Seek(0, SeekOrigin.Begin);

            subject.Reset();
            model.Reset();
            var percent = (100.0 * encoded.Length) / src.Length;
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            model.ReadPreamble(encoded);
            subject.Decode(encoded, dst);

            dst.Seek(0, SeekOrigin.Begin);
            var result = dst.ToArray();
            
            //Console.WriteLine("\r\n--------RESULT----------");
            //Console.WriteLine(result);

            Assert.That(result, Is.EqualTo(expected).AsCollection);
        }
        
        [Test]
        public void compress_firmware_image_truncatable_encoder () {
            // Equivalent deflate:          307.28kb (61.6%)
            // Original: 503.48kb; Encoded: 383.31kb (76.1%) <-- bin v1
            // Original: 499.23kb; Encoded: 371.55kb (74.4%) <-- bin v2
            var path = @"C:\temp\LargeEspIdf.bin";
            var expected = File.ReadAllBytes(path);

            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(expected);
            
            src.Seek(0, SeekOrigin.Begin);
            new TruncatableEncoder().CompressStream(src, encoded);
            encoded.Seek(0, SeekOrigin.Begin);

            var percent = (100.0 * encoded.Length) / src.Length;
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            
            new TruncatableEncoder().DecompressStream(encoded, dst);

            dst.Seek(0, SeekOrigin.Begin);
            var result = dst.ToArray();

            Assert.That(result, Is.EqualTo(expected).AsCollection);
        }

        [Test]
        public void info__histogram_of_file()
        {
            var hist = new int[256];
            var path = @"C:\temp\LargeEspIdf.bin";
            var data = File.ReadAllBytes(path);

            foreach (var b in data)
            {
                hist[b]++;
            }

            for (var index = 0; index < hist.Length; index++)
            {
                var count = hist[index];
                var bar = new string('#', (int)Math.Log(count));
                Console.WriteLine($"{index:X2} : {bar} {count}");
            }
        }
        
        [Test]
        public void info__n_grams_of_file()
        {
            var data = File.ReadAllBytes(@"C:\temp\LargeEspIdf.bin");
            NGrams.TestNGramsOfData(6, 32, data);
        }
        
        [Test]
        public void info__n_grams_of_string()
        {
            var data = Encoding.UTF8.GetBytes(Moby);
            NGrams.TestNGramsOfData(3, 32, data);
        }


        [Test]
        public void compress_firmware_experimental_lzac()
        {
            // Equivalent deflate: 321.13kb (64.0%)
            // Original: 503.48kb; Encoded: 393.49kb (78.2%) <-- gain = 2
            // Original: 503.48kb; Encoded: 390.77kb (77.6%) <-- gain = 4
            
            var path = @"C:\temp\LargeEspIdf.bin";
            var expected = File.ReadAllBytes(path);

            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(expected);
            
            src.Seek(0, SeekOrigin.Begin);
            
            new LZAC().Encode(src, encoded);
            encoded.Seek(0, SeekOrigin.Begin);

            var percent = (100.0 * encoded.Length) / src.Length;
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            
            //new TruncatableEncoder().DecompressStream(encoded, dst);

            //dst.Seek(0, SeekOrigin.Begin);
            //var result = dst.ToArray();

            //Assert.That(result, Is.EqualTo(expected).AsCollection);
        }

        [Test]
        public void compress_firmware_lzjb()
        {
            // Equivalent deflate: 307.28kb (61.6%)
            // Original: 499.23kb; Encoded: 424.56kb (85.0%)
            // Original: 499.23kb; Encoded+AC: 360.96kb (72.3%) <-- Markov2D_v2(256, 2)
            // Original: 499.23kb; Encoded+AC: 387.62kb (77.6%) <-- Markov3D_v2(256, 4)
            // Original: 499.23kb; Encoded+AC: 373.62kb (74.8%) <-- SimpleLearningModel_v2(256, 4)
            
            var path = @"C:\temp\LargeEspIdf.bin";
            var expected = File.ReadAllBytes(path);
            var encoded = new byte[expected.Length];
            var result = new byte[expected.Length];
            
            var compressLength = Lzjb.Compress(expected, encoded);
            
            var percent = (100.0 * compressLength) / expected.Length;
            Console.WriteLine($"Original: {Bin.Human(expected.Length)}; Encoded: {Bin.Human(compressLength)} ({percent:0.0}%)");
            
            
            var acSrc = new MemoryStream(encoded, 0, compressLength);
            var acDst = new MemoryStream();
            
            var subject = new ArithmeticEncoder2(new Markov2D_v2(256, 2));
            //var subject = new ArithmeticEncoder2(new Markov3D_v2(256, 4));
            //var subject = new ArithmeticEncoder2(new SimpleLearningModel_v2(256, 4));
            
            subject.CompressStream(new ByteSymbolStream(acSrc), acDst);
            
            percent = (100.0 * acDst.Length) / expected.Length;
            Console.WriteLine($"Original: {Bin.Human(expected.Length)}; Encoded+AC: {Bin.Human(acDst.Length)} ({percent:0.0}%)");

            var resultLength = Lzjb.Decompress(encoded, compressLength, result);
            result = result.Take(resultLength).ToArray();
            
            Assert.That(result, Is.EqualTo(expected).AsCollection);
        }

        [Test]
        public void compress_firmware_hash_match()
        {
            // Equivalent deflate: 321.13kb (64.0%)
            // Original: 499.23kb; Encoded: 441.91kb (88.5%)
            var path = @"C:\temp\LargeEspIdf.bin";
            var expected = File.ReadAllBytes(path);
            
            var subject = new HashMatchCompressor();
            
            var encoded = subject.Compress(expected);
            var dst = subject.Decompress(encoded);
            
            
            var percent = (100.0 * encoded.Length) / expected.Length;
            Console.WriteLine($"Original: {Bin.Human(expected.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            
            var result = dst.ToArray();

            Assert.That(result, Is.EqualTo(expected).AsCollection);
        }

        [Test]
        public void compress_firmware_rle()
        {
            // Equivalent deflate: 321.13kb (64.0%)
            // Original: 499.23kb; Encoded: 483.22kb (96.8%)
            var path = @"C:\temp\LargeEspIdf.bin";
            var expected = File.ReadAllBytes(path);
            
            var encoded = SimpleRle.Compress(expected);
            var dst = SimpleRle.Decompress(encoded);
            
            var percent = (100.0 * encoded.Length) / expected.Length;
            Console.WriteLine($"Original: {Bin.Human(expected.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            
            var result = dst.ToArray();

            Assert.That(result, Is.EqualTo(expected).AsCollection);
            Assert.Pass($"Original: {Bin.Human(expected.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
        }
        
        [Test]
        public void arithmetic_encoder_2_compress_firmware()
        {
            // Equivalent deflate: 321.13kb (64.0%) <-- old
            // Equivalent deflate: 307.28kb (61.6%) <-- new
            //
            // Original: 499.23kb; Encoded: 369.71kb (74.1%)  <-- Markov2D_v2(256, 1)
            // Original: 499.23kb; Encoded: 367.67kb (73.6%)  <-- Markov2D_v2(256, 2)
            // Original: 499.23kb; Encoded: 367.86kb (73.7%)  <-- Markov2D_v2(256, 4)
            //
            // Original: 499.23kb; Encoded: 354.88kb (71.1%)  <-- Markov3D_v2(256, 4)
            // Original: 499.23kb; Encoded: 346.17kb (69.3%)  <-- Markov3D_v2(256, 8)
            // Original: 499.23kb; Encoded: 341.95kb (68.5%)  <-- Markov3D_v2(256, 16)
            // Original: 499.23kb; Encoded: 341.62kb (68.4%)  <-- Markov3D_v2(256, 20)
            // 
            // Original: 499.23kb; Encoded: 437.15kb (87.6%)  <-- BytePreScanModel, no preamble
            // Original: 499.23kb; Encoded: 437.35kb (87.6%)  <-- SimpleLearningModel_v2(256,2)
            // Original: 499.23kb; Encoded: 499.59kb (100.1%) <-- FlatModel_v2(256)
            
            var path = @"C:\temp\LargeEspIdf.bin";
            var expected = File.ReadAllBytes(path);
            
            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(expected);
            
            //var subject = new ArithmeticEncoder2(new Markov2D_v2(256, 2));
            var subject = new ArithmeticEncoder2(new Markov3D_v2(256, 20));
            //var subject = new ArithmeticEncoder2(new FlatModel_v2(256));
            //var subject = new ArithmeticEncoder2(new SimpleLearningModel_v2(256, 2));
            //var subject = new ArithmeticEncoder2(new BytePreScanModel(expected, dst));
            
            subject.CompressStream(new ByteSymbolStream(src), encoded);
            encoded.Seek(0, SeekOrigin.Begin);
            var ok = subject.DecompressStream(encoded, new ByteSymbolStream(dst));
            
            dst.Seek(0, SeekOrigin.Begin);
            var actual = dst.ToArray();
            
            var percent = (100.0 * encoded.Length) / expected.Length;
            Console.WriteLine($"Original: {Bin.Human(expected.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            
            Assert.That(actual, Is.EqualTo(expected).AsCollection);
            Assert.That(ok, "Stream was truncated, but should not have been");
            Assert.Pass($"Original: {Bin.Human(expected.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
        }
        
        [Test]
        public void arithmetic_encoder_2_compress_firmware_with_index_push_to_front()
        {
            // Equivalent deflate: 307.28kb (61.6%)
            // Original: 499.23kb; Encoded: 422.78kb (84.7%) <-- Markov2D_v2(256, 2)
            // Original: 499.23kb; Encoded: 421.16kb (84.4%) <-- SimpleLearningModel_v2(256, 2)
            // Original: 499.23kb; Encoded: 420.97kb (84.3%) <-- BytePreScanModel
            
            var path = @"C:\temp\LargeEspIdf.bin";
            var expected = File.ReadAllBytes(path);
            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            
            
            var ptf = IndexPushToFront.Transform(expected);
            var src = new MemoryStream(ptf);
            
            //var subject = new ArithmeticEncoder2(new Markov2D_v2(256, 2));
            //var subject = new ArithmeticEncoder2(new SimpleLearningModel_v2(256, 2));
            var subject = new ArithmeticEncoder2(new BytePreScanModel(ptf, dst));
            
            subject.CompressStream(new ByteSymbolStream(src), encoded);
            encoded.Seek(0, SeekOrigin.Begin);
            var ok = subject.DecompressStream(encoded, new ByteSymbolStream(dst));
            
            dst.Seek(0, SeekOrigin.Begin);
            var actual = dst.ToArray();
            
            var percent = (100.0 * encoded.Length) / expected.Length;
            Console.WriteLine($"Original: {Bin.Human(expected.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            
            Assert.That(actual, Is.EqualTo(expected).AsCollection);
            Assert.That(ok, "Stream was truncated, but should not have been");
        }
        
        [Test]
        public void arithmetic_encoder_2_compress_firmware_with_fibonacci_push_to_front()
        {
            // Equivalent deflate: 307.28kb (61.6%)
            // Original: 499.23kb; Encoded: 425.95kb (85.3%) <-- Markov2D_v2(256, 2)
            // Original: 499.23kb; Encoded: 445.11kb (89.2%) <-- SimpleLearningModel_v2(256, 2)
            
            var path = @"C:\temp\LargeEspIdf.bin";
            var expected = File.ReadAllBytes(path);
            var src = new MemoryStream(expected);
            var encoded = new MemoryStream();
            var ptf = new MemoryStream();
            var dst = new MemoryStream();
            var final = new MemoryStream();

            PushToFrontEncoder.Compress(src, ptf);
            ptf.Seek(0, SeekOrigin.Begin);
            
            var subject = new ArithmeticEncoder2(new Markov2D_v2(256, 2));
            //var subject = new ArithmeticEncoder2(new Markov3D_v2(256, 2));
            //var subject = new ArithmeticEncoder2(new SimpleLearningModel_v2(256, 2));
            
            subject.CompressStream(new ByteSymbolStream(ptf), encoded);
            encoded.Seek(0, SeekOrigin.Begin);
            var ok = subject.DecompressStream(encoded, new ByteSymbolStream(dst));
            
            dst.Seek(0, SeekOrigin.Begin);
            PushToFrontEncoder.Decompress(dst, final);
            final.Seek(0, SeekOrigin.Begin);
            var actual = final.ToArray();
            
            var percent = (100.0 * encoded.Length) / expected.Length;
            Console.WriteLine($"Original: {Bin.Human(expected.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            
            Assert.That(actual, Is.EqualTo(expected).AsCollection);
            Assert.That(ok, "Stream was truncated, but should not have been");
            Assert.Pass($"Original: {Bin.Human(expected.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
        }
        
        [Test]
        public void arithmetic_encoder_2_compress_firmware_nybble_pre_scan()
        {
            // Equivalent deflate: 321.13kb (64.0%)
            //
            // Original: 499.23kb; Encoded: 456.93kb (91.5%) <-- NybblePreScanModel
            //
            // Original: 499.23kb; Encoded: 438.90kb (87.9%) <-- Markov2D_v2(16, 2)
            // Original: 499.23kb; Encoded: 438.92kb (87.9%) <-- Markov2D_v2(16, 4)
            // Original: 499.23kb; Encoded: 438.94kb (87.9%) <-- Markov2D_v2(16, 8)
            //
            // Original: 499.23kb; Encoded: 408.87kb (81.9%) <-- Markov3D_v2(16, 1)
            // Original: 499.23kb; Encoded: 408.94kb (81.9%) <-- Markov3D_v2(16, 2)
            // Original: 499.23kb; Encoded: 409.14kb (82.0%) <-- Markov3D_v2(16, 4)
            //
            // Original: 499.23kb; Encoded: 375.37kb (75.2%) <-- Markov4D_v2(16, 1)
            // Original: 499.23kb; Encoded: 375.60kb (75.2%) <-- Markov4D_v2(16, 2)
            // Original: 499.23kb; Encoded: 377.77kb (75.7%) <-- Markov4D_v2(16, 4)
            
            var path = @"C:\temp\LargeEspIdf.bin";
            var expected = File.ReadAllBytes(path);
            
            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(expected);
            
            //var subject = new ArithmeticEncoder2(new Markov2D_v2(16, 2));
            //var subject = new ArithmeticEncoder2(new Markov3D_v2(16, 1));
            var subject = new ArithmeticEncoder2(new Markov4D_v2(16, 1)); // experimental
            subject.CompressStream(new NybbleSymbolStream(src), encoded);
            encoded.Seek(0, SeekOrigin.Begin);
            var ok = subject.DecompressStream(encoded, new NybbleSymbolStream(dst));
            
            dst.Seek(0, SeekOrigin.Begin);
            var actual = dst.ToArray();
            
            var percent = (100.0 * encoded.Length) / expected.Length;
            Console.WriteLine($"Original: {Bin.Human(expected.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            
            Assert.That(actual, Is.EqualTo(expected).AsCollection);
            Assert.That(ok, "Stream was truncated, but should not have been");
            Assert.Pass($"Original: {Bin.Human(expected.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
        }
        
        [Test]
        public void arithmetic_encoder_2_round_trip()
        {
            var subject = new ArithmeticEncoder2(new Markov2D_v2(256, 4));
            
            var expected = Moby;
            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(Encoding.UTF8.GetBytes(expected));
            
            subject.CompressStream(new ByteSymbolStream(src), encoded);
            encoded.Seek(0, SeekOrigin.Begin);
            var ok = subject.DecompressStream(encoded, new ByteSymbolStream(dst));
            
            dst.Seek(0, SeekOrigin.Begin);
            var actual = Encoding.UTF8.GetString(dst.ToArray());
            
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Compressed: {Bin.Human(encoded.Length)}; Expanded: {Bin.Human(dst.Length)}");
            Console.WriteLine(actual);
            
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(ok, "Stream was truncated, but should not have been");
            var percent = (100.0 * encoded.Length) / expected.Length;
            Assert.Pass($"Original: {Bin.Human(expected.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
        }

        [Test]
        public void firmware_histogram_check()
        {
            var path = @"C:\temp\LargeEspIdf.bin";
            var expected = File.ReadAllBytes(path);
            
            var histogram = new int[16];
            var total = 0.0;
            foreach (var b in expected)
            {
                var upper = (b >> 4) & 0x0F;
                var lower = b & 0x0F;
                total += 2.0;
                
                histogram[upper]++;
                histogram[lower]++;
            }

            for (int i = 0; i < histogram.Length; i++)
            {
                var percent = (histogram[i]*100.0) / total;
                Console.WriteLine($" {i:X}: {histogram[i]} ({percent}%)");
            }
        }

        [Test]
        public void firmware_haar_transform_test()
        {
            var path = @"C:\temp\LargeEspIdf.bin";
            var data = File.ReadAllBytes(path);

            BinaryFolding.Encode(data);
            
            File.WriteAllBytes(@"C:\temp\_Haar1.bin", data);
        }

        [Test]
        public void huffman_encoding_string()
        {
            var expected = Moby;

            // Equivalent deflate: 645b
            // Original: 1.11kb; Encoded: 639b (56.2%) <-- This does not include outputting probability preamble
            var sw = new Stopwatch();
            
            var huffmanTree = new StringHuffmanTree();

            // Build the Huffman tree
            sw.Restart();
            huffmanTree.Build(expected);
            sw.Stop();
            Console.WriteLine($"Initialisation took {sw.Elapsed}");

            // Encode
            sw.Restart();
            var encoded = huffmanTree.Encode(expected);
            sw.Stop();
            Console.WriteLine($"Encode took {sw.Elapsed}");
            
            var encodedLength = encoded.Length / 8;
            var percent = (100.0 * encodedLength) / expected.Length;
            Console.WriteLine($"Original: {Bin.Human(expected.Length)}; Encoded: {Bin.Human(encodedLength)} ({percent:0.0}%)");

            /*Console.Write("Encoded: ");
            foreach (bool bit in encoded)
            {
                Console.Write((bit ? 1 : 0) + "");
            }
            Console.WriteLine();*/

            // Decode
            sw.Restart();
            var decoded = huffmanTree.Decode(encoded);
            sw.Stop();
            Console.WriteLine($"Decode took {sw.Elapsed}");
            Console.WriteLine("Decoded: " + decoded);
            
            Assert.That(decoded, Is.EqualTo(Moby));
            Assert.Pass($"Original: {Bin.Human(expected.Length)}; Encoded: {Bin.Human(encodedLength)} ({percent:0.0}%)");
        }
        
        [Test]
        public void huffman_encoding_firmware()
        {
            // Equivalent deflate: 307.28kb (61.6%)
            // Original: 499.23kb; Encoded: 438.68kb (87.9%) <-- and it's also slow to encode
            
            var path = @"C:\temp\LargeEspIdf.bin";
            var expected = File.ReadAllBytes(path);

            var sw = new Stopwatch();
            
            var huffmanTree = new ByteHuffmanTree();

            // Build the Huffman tree
            sw.Restart();
            huffmanTree.Build(expected);
            sw.Stop();
            Console.WriteLine($"Initialisation took {sw.Elapsed}");

            // Encode
            sw.Restart();
            var encoded = huffmanTree.Encode(expected);
            sw.Stop();
            Console.WriteLine($"Encode took {sw.Elapsed}");
            
            var encodedLength = encoded.Length / 8;
            var percent = (100.0 * encodedLength) / expected.Length;
            Console.WriteLine($"Original: {Bin.Human(expected.Length)}; Encoded: {Bin.Human(encodedLength)} ({percent:0.0}%)");

            // Decode
            sw.Restart();
            var decoded = huffmanTree.Decode(encoded);
            sw.Stop();
            Console.WriteLine($"Decode took {sw.Elapsed}");
            
            Assert.That(decoded, Is.EqualTo(expected).AsCollection);
        }

        [Test]
        public void external_quick_sort_works()
        {
            // This sort doesn't touch the data itself.
            // Used to help with porting Go code.
            var values =   new []{6,7,5,2,3,1,4,9,8};
            var expected = new []{1,2,3,4,5,6,7,8,9};
            
            IndexedSort.ExternalQSort(
                length:  values.Length,
                compare: (i, j) => { if (values[i] < values[j]) return -1; if (values[i] > values[j]) return 1; return 0; },
                swap:    (i, j) => { (values[i], values[j]) = (values[j], values[i]); });
            
            
            Assert.That(values, Is.EqualTo(expected).AsCollection);
        }

        [Test]
        public void STX_transform_on_strings()
        {
            var sw = new Stopwatch();
            var input = Encoding.UTF8.GetBytes(Moby.Replace("\r\n","\n"));
            
            sw.Restart();
            var output = Stx.ForwardTransform(input);
            sw.Stop();
            Console.WriteLine($"Stx.ForwardTransform took {sw.Elapsed}");
            
            Console.WriteLine(Encoding.UTF8.GetString(output));
            
            Console.WriteLine("\r\n------------------------------\r\n");
            
            sw.Restart();
            var result = Stx.ReverseTransform(output);
            sw.Stop();
            Console.WriteLine($"Stx.ReverseTransform took {sw.Elapsed}");
            
            Console.WriteLine(Encoding.UTF8.GetString(result));
            Assert.That(result, Is.EqualTo(input));
        }
        [Test]
        public void STX_transform_on_binary_data()
        {
            // Equivalent deflate: 307.28kb (61.6%) <-- new
            // Original: 499.23kb; Encoded: 348.32kb (69.8%)
            
            var sw = new Stopwatch();
            var path = @"C:\temp\LargeEspIdf.bin"; // takes 890ms on my machine
            var input = File.ReadAllBytes(path);
            
            sw.Restart();
            var output = Stx.ForwardTransform(input); // 00:00:00.0051616
            sw.Stop();
            Console.WriteLine($"Stx.ForwardTransform took {sw.Elapsed}");
            
            Console.WriteLine("\r\n------------------------------\r\n");
            
            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(output);
            var aec = new ArithmeticEncoder2(new Markov2D_v2(256, 2));
            aec.CompressStream(new ByteSymbolStream(src), encoded);
            encoded.Seek(0, SeekOrigin.Begin);
            aec.DecompressStream(encoded, new ByteSymbolStream(dst));
            dst.Seek(0, SeekOrigin.Begin);
            var actual = dst.ToArray();
            var percent = (100.0 * encoded.Length) / input.Length;
            Console.WriteLine($"Original: {Bin.Human(input.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            
            Console.WriteLine("\r\n------------------------------\r\n");
            
            sw.Restart();
            var result = Stx.ReverseTransform(actual); // 00:00:00.0081399
            sw.Stop();
            Console.WriteLine($"Stx.ReverseTransform took {sw.Elapsed}");

            Assert.That(result, Is.EqualTo(input));
        }

        [Test]
        public void BWST_transform_on_strings()
        {
            var sw = new Stopwatch();
            var input = Encoding.UTF8.GetBytes(Moby.Replace("\r\n","\n"));
            
            // Deflate test
            // Without BWT: 634b
            // After BWT:   656b
            
            sw.Restart();
            var output = Bwst.ForwardTransform(input);
            sw.Stop();
            Console.WriteLine($"Bwst.ForwardTransform took {sw.Elapsed}");
            
            Console.WriteLine(Encoding.UTF8.GetString(output));
            
            Console.WriteLine("\r\n------------------------------\r\n");
            
            sw.Restart();
            var result = Bwst.ReverseTransform(output);
            sw.Stop();
            Console.WriteLine($"Bwst.ReverseTransform took {sw.Elapsed}");
            
            Console.WriteLine(Encoding.UTF8.GetString(result));
            Assert.That(result, Is.EqualTo(input));
        }
        
        [Test]
        public void BWST_transform_on_binary_data()
        {
            // Equivalent deflate: 307.28kb (61.6%) <-- new
            // Original: 499.23kb; Encoded: 338.34kb (67.8%)
            
            var sw = new Stopwatch();
            var path = @"C:\temp\LargeEspIdf.bin"; // takes around 5 min on my machine
            var input = File.ReadAllBytes(path);
            
            sw.Restart();
            var output = Bwst.ForwardTransform(input); // 6 seconds
            sw.Stop();
            Console.WriteLine($"Bwst.ForwardTransform took {sw.Elapsed}");
            
            Console.WriteLine("\r\n------------------------------\r\n");
            
            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(output);
            var aec = new ArithmeticEncoder2(new Markov2D_v2(256, 2));
            aec.CompressStream(new ByteSymbolStream(src), encoded);
            encoded.Seek(0, SeekOrigin.Begin);
            aec.DecompressStream(encoded, new ByteSymbolStream(dst));
            dst.Seek(0, SeekOrigin.Begin);
            var actual = dst.ToArray();
            var percent = (100.0 * encoded.Length) / input.Length;
            Console.WriteLine($"Original: {Bin.Human(input.Length)}; Encoded: {Bin.Human(encoded.Length)} ({percent:0.0}%)");
            
            Console.WriteLine("\r\n------------------------------\r\n");
            
            sw.Restart();
            var result = Bwst.ReverseTransform(actual); // 5 minutes
            sw.Stop();
            Console.WriteLine($"Bwst.ReverseTransform took {sw.Elapsed}");

            Assert.That(result, Is.EqualTo(input));
        }
    }

    [TestFixture]
    public class FoldingHashTests {

        [Test]
        public void hash_pyramid () {
            // MULTI-SCALE HASHING by folding, for searching and to reduce computation (at a cost of fewer matches)
            //var rank1 = GetRawImageBytes();
            var rank1 = GetEncodedImageBytes();
            //var rank1 = Encoding.UTF8.GetBytes(LosslessDataCompressionTests.Moby);
            //var rank1 = new byte[]{ 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32 };
            //var rank1 = Enumerable.Range(1,64).Select(v=>(byte)v).ToArray();

            Console.WriteLine($"Source data is {Bin.Human(rank1.Length)}");

            var sw = new Stopwatch();

            var ranks = new List<byte[]>();
            ranks.Add(rank1);

            // --- BUILD the pyramid ---
            sw.Restart();
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
            sw.Stop();
            

            Console.WriteLine("PYRAMID:");
            Console.WriteLine($"Sums = {sums} for {ranks.Count} ranks took {sw.Elapsed}");
            /*Console.WriteLine("Rank 1  =\r\n"+string.Join(" ", ranks[0]));
            Console.WriteLine("Rank 2  =\r\n"+string.Join(" ", ranks[1]));
            Console.WriteLine("Rank 4  =\r\n"+string.Join(" ", ranks[2]));
            Console.WriteLine("Rank 8  =\r\n"+string.Join(" ", ranks[3]));
            Console.WriteLine("Rank 16 =\r\n"+string.Join(" ", ranks[4]));
            Console.WriteLine("Rank 32 =\r\n"+string.Join(" ", ranks[5]));*/
            Console.WriteLine($"Total storage = {ranks.Sum(r => r.Length)}");


            // --- SEARCH the pyramid ---
            // look in 2^n-wide chunks for potentials:
            sw.Restart();
            var matchFound = 0;
            var matchRejected = 0;
            var skipped = 0;
            var jumpTable = new int[rank1.Length]; // indexes that are covered by a larger replacement

            for (int SearchRank = 8; SearchRank > 2; SearchRank--)
            {
                var n = SearchRank; // 1..8
                var rank_n = ranks[n];
                var offs = (int)Math.Pow(2, n);
                var windowSize = 8172;

                var sampleLen = Math.Min(offs, 10);
                Console.WriteLine($"Searching rank = {n}; length = {offs}; data extent = {rank_n.Length}; lookahead window = {windowSize}");
                for (int i = 0; i < rank_n.Length; i += offs)
                {
                    var limit = Math.Min(rank_n.Length, i + windowSize + offs);
                    for (int j = i + offs; j < limit; j++)
                    {
                        if (jumpTable[j] != 0) {
                            skipped += jumpTable[j] - j;
                            j = jumpTable[j];
                            if (j >= limit) break;
                        }
                        if (rank_n[i] != rank_n[j]) continue; // no potential match

                        // do a double check here
                        var realMatch = true;
                        for (int k = 0; k < offs; k++)
                        {
                            if (rank1[i + k] == rank1[j + k]) continue;

                            realMatch = false;
                            matchRejected++;
                            break;
                        }
                        if (!realMatch) continue;

                        matchFound++;
                        // build a sample of each:
                        //ShowStringSample(sampleLen, rank1, i, j);
                        //ShowHexSample(sampleLen, rank1, i, j);

                        // Write to jump table. The 'replaced' section should not be searched again
                        for (int skip = 0; skip < offs; skip++)
                        {
                            jumpTable[j+skip] = j+offs;
                        }

                        break;
                    }
                    if (sw.Elapsed.TotalSeconds > 5)
                    {
                        Console.WriteLine("Hit test cycle limit");
                        break; // limit searching
                    }
                } // end of seach at selected rank
                Console.WriteLine($"End of rank {n}: found {matchFound} matching pairs so far.");
            }
            sw.Stop();
            Console.WriteLine($"Searching took {sw.Elapsed}; found {matchFound} matching pairs." +
                              $"Rejected {matchRejected} potentials. Skipped {skipped} bytes of larger scale matches");
        }

        private static void ShowStringSample(int sampleLen, byte[] rank1, int i, int j)
        {
            var A = "";
            var B = "";
            for (int k = 0; k < sampleLen; k++)
            {
                var a = rank1[i + k];
                if (a == 0x0d || a == 0x0a) a = 32;

                var b = rank1[j + k];
                if (b == 0x0d || b == 0x0a) b = 32;

                A += (char) a;
                B += (char) b;
            }

            Console.WriteLine($"Found ({i},{j}) => {A}; {B}");
        }
        private static void ShowHexSample(int sampleLen, byte[] rank1, int i, int j)
        {
            var A = "";
            var B = "";
            for (int k = 0; k < sampleLen; k++)
            {
                A += rank1[i + k].ToString("X2");
                B += rank1[j + k].ToString("X2");
            }

            Console.WriteLine($"Found ({i},{j}) => {A}; {B}");
        }

        
        public static byte[] GetEncodedImageBytes()
        {
            var msY = new MemoryStream();
            var msU = new MemoryStream();
            var msV = new MemoryStream();

            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                WaveletCompress.ReduceImage2D_ToStreams(bmp, CDF.Fwt97, msY, msU, msV);
                msY.Seek(0, SeekOrigin.Begin);
                return msY.ToArray();
            }
        }

        public static byte[] GetRawImageBytes()
        {
            using (var bmp = Load.FromFile("./inputs/3.png")) {
                var ri = new Rectangle(Point.Empty, bmp.Size);
                var srcData = bmp.LockBits(ri, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var dest = new byte[srcData.Stride * srcData.Height];
                Marshal.Copy(srcData.Scan0, dest, 0, dest.Length);
                return dest;
            }
        }
    }
}