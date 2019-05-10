using System;
using System.Collections.Generic;
using System.Linq;
using ImageTools.Utilities;
using NUnit.Framework;
// ReSharper disable PossibleNullReferenceException

namespace ImageTools.Tests
{
    [TestFixture]
    public class ByteEncodingTests {

        [Test]
        public void fibonacci_number_round_trip(){
            uint orig = 123456;
            var enc = DataEncoding.FibEncodeNum(orig, null).ToArray();
            
            Console.WriteLine(string.Join(" ", enc.Select(b=>b.ToString())));
            
            var dec = DataEncoding.FibDecodeNum(new Queue<byte>(enc));
            
            Assert.That(dec, Is.EqualTo(orig));
        }

        // Fibonacci encoding is a universal code good when you have lots of
        // small values, and the occasional large value

        [Test]
        public void fibonacci_encoding() {
            var input = new uint[]{ 0,0,0,0,0,1,1000,2000,1,0,0,0,0,0 };
            var output = DataEncoding.UnsignedFibEncode(input);

            Console.WriteLine($"{input.Length * 2} bytes in, {output.Length} bytes out");
            Console.WriteLine(string.Join(" ", output.Select(b=>b.ToString("X2"))));
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