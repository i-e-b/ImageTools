using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using ImageTools.DataCompression;
using ImageTools.DataCompression.Encoding;
using ImageTools.Utilities;
using ImageTools.WaveletTransforms;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class LosslessDataCompressionTests {
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

            var subject = new ArithmeticEncode(new ProbabilityModels.RollingLearningModel(500));
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

            var subject = new ArithmeticEncode(new ProbabilityModels.BraindeadModel());
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
            AC encoded 'Y' size = 160.14kb			(simple learning model)
            AC encoded 'Y' size = 231.1kb           (push to front model)
            AC encoded 'Y' size = 148.77kb          (fixed prescan model)
            Deflate encoded 'Y' size = 123.47kb

            
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
                //var model = new ProbabilityModels.PrescanModel(msY);
                //var model = new ProbabilityModels.PushToFrontModel();
                var model = new ProbabilityModels.SimpleLearningModel();

                // Try our simple encoding
                var subject = new ArithmeticEncode(model);
                msY.Seek(0, SeekOrigin.Begin);
                sw.Restart();
                subject.Encode(msY, acY);
                sw.Stop();
                Console.WriteLine($"Arithmetic coding took {sw.Elapsed}");

                Console.WriteLine($"AC encoded 'Y' size = {Bin.Human(acY.Length + model.Preamble().Length)}");// add 256 if using pre-scan

                // Compare to deflate
                /*msY.Seek(0, SeekOrigin.Begin);
                
                using (var tmp = new MemoryStream())
                {   // byte-by-byte writing to DeflateStream is *very* slow, so we buffer
                    using (var gs = new DeflateStream(tmp, CompressionLevel.Optimal, true))
                    {
                        msY.WriteTo(gs);
                        gs.Flush();
                    }
                    Console.WriteLine($"Deflate encoded 'Y' size = {Bin.Human(tmp.Length)}");
                }*/


                // Now decode:
                subject.Reset();
                acY.Seek(0, SeekOrigin.Begin);
                sw.Restart();
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
        public void ac_round_trip () {
            var expected = 
                @"####Call me Ishmael. Some years ago--never mind how long precisely--having
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

            var encoded = new MemoryStream();
            var dst = new MemoryStream();
            var src = new MemoryStream(Encoding.UTF8.GetBytes(expected));

            var model = new ProbabilityModels.SimpleLearningModel();
            var subject = new ArithmeticEncode(model);
            subject.Encode(src, encoded);
            encoded.Seek(0, SeekOrigin.Begin);

            model.Reset();
            Console.WriteLine($"Original: {Bin.Human(src.Length)}; Encoded: {Bin.Human(encoded.Length)}");
            subject.Decode(encoded, dst);

            dst.Seek(0, SeekOrigin.Begin);
            var result = Encoding.UTF8.GetString(dst.ToArray());

            Console.WriteLine("\r\n--------RESULT----------");
            Console.WriteLine(result);

            // failing at size limit. Are we deleting the dictionary entries out of order?
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void lzw_round_trip () {
            var expected = 
                @"Call me Ishmael. Some years ago--never mind how long precisely--having
little or no money in my purse, and nothing particular to interest me on
shore, I thought I would sail about a little and see the watery part of
the world. It is a way I have of driving off the spleen and regulating
the circulation. Whenever I find myself growing grim about the mouth;
whenever it is a damp, drizzly November in my soul; whenever I find
myself involuntarily pausing before coffin warehouses, and bringing up
the rear of every funeral I meet; and especially whenever my hypos get
such an upper hand of me, that it requires a strong moral principle to
prevent me from deliberately stepping into the street, and methodically
knocking people's hats off--then, I account it high time to get to sea
as soon as I can. This is my substitute for pistol and ball. With a
philosophical flourish Cato throws himself upon his sword; I quietly
take to the ship. There is nothing surprising in this. If they but knew
it, almost all men in their degree, some time or other, cherish very
nearly the same feelings towards the ocean with me.";

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
            var expected = 
                @"####Call me Ishmael. Some years ago--never mind how long precisely--having
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
            var expected = 
@"Call me Ishmael. Some years ago--never mind how long precisely--having
little or no money in my purse, and nothing particular to interest me on
shore, I thought I would sail about a little and see the watery part of
the world. It is a way I have of driving off the spleen and regulating
the circulation. Whenever I find myself growing grim about the mouth;
whenever it is a damp, drizzly November in my soul; whenever I find
myself involuntarily pausing before coffin warehouses, and bringing up
the rear of every funeral I meet; and especially whenever my hypos get
such an upper hand of me, that it requires a strong moral principle to
prevent me from deliberately stepping into the street, and methodically
knocking people's hats off--then, I account it high time to get to sea
as soon as I can. This is my substitute for pistol and ball. With a
philosophical flourish Cato throws himself upon his sword; I quietly
take to the ship. There is nothing surprising in this. If they but knew
it, almost all men in their degree, some time or other, cherish very
nearly the same feelings towards the ocean with me.";

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

        
        [Test, Explicit("This is currently impractically slow")]
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
                // AC alone    = 160.14kb
                // 256,128,64: (1:45 rel) Scans = 108387474644;  Replacements =  893; size = 139.69kb
                // 64/63: (3:35)          Scans =  65184192961;  Replacements = 1680; size = 146.81kb
                // 256: (1:54)            Scans =  40270743703;  Replacements =  239; size = 151.01kb

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
                var model = new ProbabilityModels.SimpleLearningModel();
                var arithmeticEncode = new ArithmeticEncode(model);
                lzY.Seek(0, SeekOrigin.Begin);
                arithmeticEncode.Encode(lzY, acY);

                Console.WriteLine($"LZ encoded 'Y' size = {Bin.Human(lzY.Length)}");
                Console.WriteLine($"AC encoded 'Y' size = {Bin.Human(acY.Length+model.Preamble().Length)}");

                // Now decode:
                arithmeticEncode.Reset();
                acY.Seek(0, SeekOrigin.Begin);
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

}