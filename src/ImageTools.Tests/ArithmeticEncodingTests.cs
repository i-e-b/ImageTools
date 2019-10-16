using System;
using System.Diagnostics;
using System.IO;
using ImageTools.DataCompression.Encoding;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class ArithmeticEncodingTests{
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
            var subject = new ArithmeticEncode(new SimpleLearningModel());
            var result = new MemoryStream();

            var bytes = new byte[100]; // all zeros
            var data = new MemoryStream(bytes);
            data.Seek(0,SeekOrigin.Begin);

            subject.Encode(data, result);

            Console.WriteLine($"Encoded {result.Length} bytes for {bytes.Length} bytes of input");
            Assert.That(result.Length, Is.LessThan(50)); // really, it's the model that has the biggest effect
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

            var subject = new ArithmeticEncode(new BraindeadModel());
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
            for (uint i = 0; i < 258; i++) cumulative_frequency[i] = i;
            _frozen = false;
        }

        /// <inheritdoc />
        public uint GetCount()
        {
            return cumulative_frequency[257];
        }

        /// <inheritdoc />
        public int RequiredSymbolBits() { return 9; } // 8 bits for values, 1 for stop
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
        public int RequiredSymbolBits() { return 9; } // 8 bits for values, 1 for stop
    }
}