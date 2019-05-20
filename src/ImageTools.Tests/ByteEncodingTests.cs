using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            var input = new double[data_length];
            for (int i = 0; i < data_length; i++)
            {
                input[i] = ( (rnd.NextDouble() - 0.5) * (1.0 / rnd.NextDouble()));
            }

            var ms = new MemoryStream();
            //var input = new double[] { 0, 1, -1, 1000.1, -2000.9, 0.9, 1.1 };
            //var input = new double[] { 1,2,3,4,5,6,7,8,9,10};
            //var input = new double[] { 0,0,0,0,1,1,1,1,-1,-1,0,0,0,0};


            Console.WriteLine(string.Join(" ", input));
            var expected = new double[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                expected[i] = (int)input[i];
            }
            
            DataEncoding.FibonacciEncode(input, ms);

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
    }
}