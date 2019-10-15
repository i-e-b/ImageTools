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
            Assert.That(ArithmeticEncode.BIT_SIZE, Is.EqualTo(32), nameof(ArithmeticEncode.BIT_SIZE));
            Assert.That(ArithmeticEncode.PRECISION, Is.EqualTo(32), nameof(ArithmeticEncode.PRECISION));
            Assert.That(ArithmeticEncode.CODE_VALUE_BITS, Is.EqualTo(17), nameof(ArithmeticEncode.CODE_VALUE_BITS));
            Assert.That(ArithmeticEncode.FREQUENCY_BITS, Is.EqualTo(15), nameof(ArithmeticEncode.FREQUENCY_BITS));
            Assert.That(ArithmeticEncode.MAX_CODE, Is.EqualTo(0x0001FFFF), nameof(ArithmeticEncode.MAX_CODE));
            Assert.That(ArithmeticEncode.MAX_FREQ, Is.EqualTo(0x00007FFF), nameof(ArithmeticEncode.MAX_FREQ));
            Assert.That(ArithmeticEncode.ONE_QUARTER, Is.EqualTo(0x00008000), nameof(ArithmeticEncode.ONE_QUARTER));
            Assert.That(ArithmeticEncode.ONE_HALF, Is.EqualTo(0x00010000), nameof(ArithmeticEncode.ONE_HALF));
            Assert.That(ArithmeticEncode.THREE_QUARTERS, Is.EqualTo(0x00018000), nameof(ArithmeticEncode.THREE_QUARTERS));
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
    }

    public class ListIO : IBitwiseIO
    {
        private readonly List<bool> _list;

        public ListIO()
        {
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
        public SymbolProbability GetCurrentProbability(int c)
        {
            var p = new SymbolProbability
            {
                low = cumulative_frequency[c],
                high = cumulative_frequency[c + 1],
                count = cumulative_frequency[257]
            };
            Update(c);
            return p;
        }

        private void Update(int c)
        {
            if (_frozen) return; // model is saturated
            for (int i = c + 1; i < 258; i++) cumulative_frequency[i]++;

            if (cumulative_frequency[257] >= ArithmeticEncode.MAX_FREQ)
            {
                _frozen = true;
            }

        }

        /// <inheritdoc />
        public SymbolProbability GetChar(uint scaledValue, ref int decodedSymbol)
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
                    if ( !_frozen)
                        Update(i);
                    return p;
                }
            throw new Exception("Decoder model found no symbol in this range");
        }

        /// <inheritdoc />
        public void Reset()
        {
            for (uint i = 0; i < 258; i++)
                cumulative_frequency[i] = i;
            _frozen = false;
        }
    }
}