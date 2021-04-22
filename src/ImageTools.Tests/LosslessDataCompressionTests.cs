using System;
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
using ImageTools.Utilities;
using ImageTools.WaveletTransforms;
using NUnit.Framework;

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
            
            Console.WriteLine($"Original: {src.Length}; Compressed: {encoded.Length}; Expanded: {dst.Length}");
            
            Assert.That(ok, "Stream was truncated, but should not have been");
            Assert.That(actual, Is.EqualTo(expected));
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
        
        [Test]
        public void ac_round_trip () {
            var expected = Moby;

            // Equivalent deflate: 645b
            // Original: 1.11kb; Encoded: 672b (simple learning + 16)
            // Original: 1.11kb; Encoded: 740b (simple learning + 1)
            // Original: 1.11kb; Encoded: 785b (rolling 1000)
            // Original: 1.11kb; Encoded: 859b (learning markov + 2)
            // Original: 1.11kb; Encoded: 893b (prescan -- 637b without preamble)

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
            var model = new ProbabilityModels.SimpleLearningModel();
            //var model = new ProbabilityModels.LearningMarkov();
            var subject = new ArithmeticEncode(model);
            src.Seek(0, SeekOrigin.Begin);
            model.WritePreamble(encoded);
            subject.Encode(src, encoded);
            encoded.Seek(0, SeekOrigin.Begin);

            subject.Reset();
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)}");
            model.ReadPreamble(encoded);
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

            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)}");
            lzPack.Decode(encoded, dst);

            dst.Seek(0, SeekOrigin.Begin);
            var result = Encoding.UTF8.GetString(dst.ToArray());

            Console.WriteLine(result);

            // failing at size limit. Are we deleting the dictionary entries out of order?
            Assert.That(result, Is.EqualTo(expected));
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

            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)}");
            lzPack.Decode(encoded, dst);

            dst.Seek(0, SeekOrigin.Begin);
            var result = Encoding.UTF8.GetString(dst.ToArray());

            Console.WriteLine("\r\n--------RESULT----------");
            Console.WriteLine(result);

            // failing at size limit. Are we deleting the dictionary entries out of order?
            Assert.That(result, Is.EqualTo(expected));
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
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)}");
            lzPack.Decode(encoded, dst);

            dst.Seek(0, SeekOrigin.Begin);
            var result = Encoding.UTF8.GetString(dst.ToArray());
            
            Console.WriteLine(result);

            // failing at size limit. Are we deleting the dictionary entries out of order?
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