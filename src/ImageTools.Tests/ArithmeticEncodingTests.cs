using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

/*
            Assert.That(ArithmeticEncode.BIT_SIZE, Is.EqualTo(32), nameof(ArithmeticEncode.BIT_SIZE));
            Assert.That(ArithmeticEncode.PRECISION, Is.EqualTo(31), nameof(ArithmeticEncode.PRECISION));
            Assert.That(ArithmeticEncode.CODE_VALUE_BITS, Is.EqualTo(16), nameof(ArithmeticEncode.CODE_VALUE_BITS));
            Assert.That(ArithmeticEncode.FREQUENCY_BITS, Is.EqualTo(14), nameof(ArithmeticEncode.FREQUENCY_BITS));
            Assert.That(ArithmeticEncode.MAX_CODE, Is.EqualTo(0x0001FFFF), nameof(ArithmeticEncode.MAX_CODE));
            Assert.That(ArithmeticEncode.MAX_FREQ, Is.EqualTo(0x00003FFF), nameof(ArithmeticEncode.MAX_FREQ));
            Assert.That(ArithmeticEncode.ONE_QUARTER, Is.EqualTo(0x00008000), nameof(ArithmeticEncode.ONE_QUARTER));
            Assert.That(ArithmeticEncode.ONE_HALF, Is.EqualTo(0x00010000), nameof(ArithmeticEncode.ONE_HALF));
            Assert.That(ArithmeticEncode.THREE_QUARTERS, Is.EqualTo(0x00018000), nameof(ArithmeticEncode.THREE_QUARTERS));
*/
        }

        [Test]
        public void can_encode_a_byte_stream ()
        {
            var rnd = new Random();

            var result = new ListIO();
            var subject = new ArithmeticEncode(new TestModel(), result);

            var bytes = new byte[100];
            rnd.NextBytes(bytes);
            var data = new MemoryStream(bytes);
            data.Seek(0,SeekOrigin.Begin);

            subject.Encode(data);

            var str = string.Join("", result.BitList());
            Console.WriteLine($"Encoded {result.ByteLength()} bytes for {bytes.Length} bytes of input");
            Console.WriteLine(str);
        }
        
        [Test]
        public void encoding_a_trivial_dataset_takes_few_bytes ()
        {
            var result = new ListIO();
            var subject = new ArithmeticEncode(new TestModel(), result);

            var bytes = new byte[100]; // all zeros
            var data = new MemoryStream(bytes);
            data.Seek(0,SeekOrigin.Begin);

            subject.Encode(data);

            Assert.That(result.ByteLength(), Is.LessThan(50)); // really, it's the model that has the biggest effect
        }

        [Test]
        public void can_recover_an_encoded_byte_stream (){
            var rnd = new Random();

            var result = new ListIO();
            var subject = new ArithmeticEncode(new TestModel(), result);

            var inputBytes = new byte[100];
            rnd.NextBytes(inputBytes);
            /*for (int i = 0; i < inputBytes.Length; i++)
            {
                inputBytes[i] = (byte) (i % 10);
                //inputBytes[i] = 127;
            }*/
            var data = new MemoryStream(inputBytes);
            data.Seek(0,SeekOrigin.Begin);

            subject.Encode(data);

            var str = string.Join("", result.BitList());
            Console.WriteLine($"Encoded {result.ByteLength()} bytes for {inputBytes.Length} bytes of input");
            Console.WriteLine(str);

            subject.Reset();
            var final = new MemoryStream();
            try
            {
                subject.Decode(final);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Decode failed: " + ex);
            }
            final.Seek(0, SeekOrigin.Begin);

            var finalData = final.ToArray();

            Assert.That(finalData, Is.EquivalentTo(inputBytes), "Decoded data was not the same as the input");
        }
    }

    public class ListIO : IBitwiseIO
    {
        private readonly List<bool> _list;
        private int readPos;
        private int _runout;

        public ListIO()
        {
            readPos = 0;
            _runout = 0;
            _list = new List<bool>();
        }

        public IEnumerable<char> BitList() {
            return _list.Select(v=>v?'1':'0');
        }

        public int ByteLength() {
            return (_list.Count / 8) + 1;
        }

        /// <inheritdoc />
        public void OutputBit(bool value)
        {
            _list.Add(value);
        }

        /// <inheritdoc />
        public uint GetBit()
        {
            if (readPos >= _list.Count) {
                if (_runout++ < 32) return 0; // allow us to run off the end of the input a bit.
                throw new Exception("End of data before EOL symbol found"); // EOL
            }
            return _list[readPos++] ? 1u : 0u;
        }
    }

    /// <summary>
    /// A really simple model for testing
    /// </summary>
    public class TestModel : IProbabilityModel
    {
        private readonly uint[] cumulative_frequency;
        private bool _frozen;

        public TestModel()
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
}