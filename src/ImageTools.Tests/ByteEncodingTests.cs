using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageTools.ImageDataFormats;
using ImageTools.Utilities;
using NUnit.Framework;
// ReSharper disable PossibleNullReferenceException

namespace ImageTools.Tests
{
    [TestFixture]
    public class ByteEncodingTests {

        [Test]
        public void signed_to_unsigned () {
            var input = new[]{ 0,0,-1,1000,-2000,1,0 };

            var result = DataEncoding.SignedToUnsigned(input);

            Assert.That(result.Any(n=>n < 0), Is.False, "Wrong sign");
            Assert.That(result.Any(n=>n > 5000), Is.False, "Wrong scale");

            var final = DataEncoding.UnsignedToSigned(result);

            Assert.That(final, Is.EqualTo(input));
        }

        // Fibonacci encoding is a universal code good when you have lots of
        // small values, and the occasional large value

        [Test]
        public void fibonacci_number_round_trip(){
            for (uint i = 1; i <= 10; i++)
            {
                uint orig = i;
                var enc = DataEncoding.FibEncodeNum(orig, null).ToArray();

                Console.WriteLine(i.ToString("X2") + " -> " + string.Join("", enc.Select(b => b.ToString())));

                var dec = DataEncoding.FibDecodeNum(new Queue<byte>(enc));

                Assert.That(dec, Is.EqualTo(orig));
            }
        }

        [Test]
        public void fibonacci_encoding() {
            var input = new uint[]{ 0,0,0,0,0,1,1000,2000,1,0,0,0,0,0 };
            var output = DataEncoding.UnsignedFibEncode(input);

            Console.WriteLine($"{input.Length * 2} bytes in, {output.Length} bytes out");
            Console.WriteLine(string.Join(" ", output.Select(b=>b.ToString("X2"))));
        }

        [Test]
        public void fibonacci_streaming() {
            var rnd = new Random();
            var data_length = 100;//(int)(rnd.NextDouble() * 100);
            var input = new float[data_length];
            for (int i = 0; i < data_length; i++)
            {
                input[i] = (float)( (rnd.NextDouble() - 0.5) * (1.0 / rnd.NextDouble()));
            }

            var ms = new MemoryStream();

            Console.WriteLine(string.Join(" ", input));
            var expected = new float[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                expected[i] = (int)input[i];
            }
            
            DataEncoding.FibonacciEncode(input, 0, ms);

            ms.Seek(0, SeekOrigin.Begin);
            var output = ms.ToArray();

            
            var final = DataEncoding.FibonacciDecode(ms);
            Console.WriteLine();

            Console.WriteLine($"{input.Length * 2} bytes in, {output.Length} bytes out.");
            Console.WriteLine(string.Join(" ", output.Select(b=>b.ToString("X2"))));
            Console.WriteLine(string.Join(" ", final));
            
            Assert.That(final, Is.EqualTo(expected));
        }
        
        [Test]
        public void fibonacci_encoding_round_trip() {
            var input = new uint[]{ 0,0,0,0,0,1,1000,2000,1,0,0,0,0,0,1,2,3,4,5,6,0,0,0,1,0,0,0,5000,0,0 };
            
            Console.WriteLine("IN  -> "+string.Join(", ", input.Select(b=>b)));


            var output = DataEncoding.UnsignedFibEncode(input);

            
            Console.WriteLine("ENC -> "+string.Join(" ", output.Select(b=>b.ToString("X2"))));
            Console.WriteLine($"{input.Length * 2} bytes in, {output.Length} bytes out");

            var final = DataEncoding.UnsignedFibDecode(output);

            
            Console.WriteLine("OUT -> "+string.Join(", ", final.Select(b=>b)));

            Assert.That(final, Is.EqualTo(input));
        }

        [Test, Description("This demonstrates mixing bitwise fibonacci encoding and bytewise binary encoding in the same stream")]
        public void bitstream_mixed_coding () {
            var ms = new MemoryStream();
            var data = new BitwiseStreamWrapper(ms, 1);

            // encode some data
            DataEncoding.FibonacciEncodeOne(0, data);
            DataEncoding.FibonacciEncodeOne(1, data);
            DataEncoding.FibonacciEncodeOne(10, data);
            data.WriteByteUnaligned(0xC5); // jam a random byte in there to prove we can
            DataEncoding.FibonacciEncodeOne(100, data);
            DataEncoding.FibonacciEncodeOne(1000, data);

            data.Flush();

            // display it
            data.Rewind();
            while (!data.IsEmpty()){
                Console.Write(data.ReadBit());
            }
            Console.WriteLine();

            // test the decoding
            data.Rewind();
            Assert.That(DataEncoding.FibonacciDecodeOne(data), Is.EqualTo(0));
            Assert.That(DataEncoding.FibonacciDecodeOne(data), Is.EqualTo(1));
            Assert.That(DataEncoding.FibonacciDecodeOne(data), Is.EqualTo(10));
            Assert.That(data.ReadByteUnaligned(), Is.EqualTo(0xC5)); // don't forget to pull out that byte
            Assert.That(DataEncoding.FibonacciDecodeOne(data), Is.EqualTo(100));
            Assert.That(DataEncoding.FibonacciDecodeOne(data), Is.EqualTo(1000));
        }

        [Test]
        public void elias_omega_code () {
            // This is the 'recursive' version of Peter Elias' universal code family

            var samples = new uint[] { 0, 1, 2, 3, 10, 50, 100, 255, 1024, 10000 };
            var ms = new MemoryStream();
            var bits = new BitwiseStreamWrapper(ms, 5);

            // Encode to stream
            Console.WriteLine("Encoding:");
            foreach (var sample in samples)
            {
                Console.Write($"{sample}, ");
                DataEncoding.EliasOmegaEncodeOne(sample, bits);
            }

            bits.Flush();
            ms.Seek(0, SeekOrigin.Begin);

            // Display
            Console.WriteLine("Bitwise result:");
            var s = 7;
            while(bits.TryReadBit(out var b)) {
                s = (s+1)%8;
                if (s==0) Console.Write(" ");
                Console.Write(b);
            }
            
            ms.Seek(0, SeekOrigin.Begin);
            bits = new BitwiseStreamWrapper(ms, 5);

            // Decode
            Console.WriteLine("\r\nDecoding: (Note that this is not a self-synchronising code, and we read spare values off the end)");
            var safety = 50;
            while (DataEncoding.EliasOmegaTryDecodeOne(bits, out uint result) ) {
                if (safety-- < 0) { Assert.Fail("Decode loop did not terminate correctly"); }
                Console.Write($"{result}, ");
            }
        }

        [Test]
        public void byte_block_code () {
            // a simple flagged code that fits codes in bytes, and is always byte-aligned
            // this supports 22 bit unsigned integer range

            var samples = new ushort[] { 0, 1, 2, 3, 10, 50, 100, 255, 1024, 10000, 18383, 65535 };
            var ms = new MemoryStream();
            var bits = new BitwiseStreamWrapper(ms, 5);

            // Encode to stream
            Console.WriteLine("Encoding:");
            foreach (var sample in samples)
            {
                Console.Write($"{sample}, ");
                DataEncoding.ByteBlockEncodeOne(sample, bits);
            }

            bits.Flush();
            ms.Seek(0, SeekOrigin.Begin);

            // Display
            Console.WriteLine("Bitwise result:");
            var s = 7;
            while(bits.TryReadBit(out var b)) {
                s = (s+1)%8;
                if (s==0) Console.Write(" ");
                Console.Write(b);
            }
            
            ms.Seek(0, SeekOrigin.Begin);
            bits = new BitwiseStreamWrapper(ms, 5);

            // Decode
            Console.WriteLine("\r\nDecoding: (Note that this is not a self-synchronising code, and we read spare values off the end)");
            var safety = 50;
            while (DataEncoding.ByteBlockTryDecodeOne(bits, out var result) ) {
                if (safety-- < 0) { Assert.Fail("Decode loop did not terminate correctly"); }
                Console.Write($"{result}, ");
            }
        }

        [Test]
        public void short_byte_block_code () {
            // same as byte block, but with a special case for 0 value
            // this makes it no longer byte-aligned

            var samples = new uint[] { 0, 1, 2, 3, 10, 50, 100, 255, 1024, 10000, 18383, 65535 };
            var ms = new MemoryStream();
            var bits = new BitwiseStreamWrapper(ms, 5);

            // Encode to stream
            Console.WriteLine("Encoding:");
            foreach (var sample in samples)
            {
                Console.Write($"{sample}, ");
                DataEncoding.ShortByteBlockEncodeOne(sample, bits);
            }

            bits.Flush();
            ms.Seek(0, SeekOrigin.Begin);

            // Display
            Console.WriteLine("Bitwise result:");
            var s = 7;
            while(bits.TryReadBit(out var b)) {
                s = (s+1)%8;
                if (s==0) Console.Write(" ");
                Console.Write(b);
            }
            
            ms.Seek(0, SeekOrigin.Begin);
            bits = new BitwiseStreamWrapper(ms, 5);

            // Decode
            Console.WriteLine("\r\nDecoding: (Note that this is not a self-synchronising code, and we read spare values off the end)");
            var safety = 50;
            while (DataEncoding.ShortByteBlockTryDecodeOne(bits, out var result) ) {
                if (safety-- < 0) { Assert.Fail("Decode loop did not terminate correctly"); }
                Console.Write($"{result}, ");
            }
        }

    }
}