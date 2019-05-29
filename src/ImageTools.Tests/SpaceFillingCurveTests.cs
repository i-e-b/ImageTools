using System;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class SpaceFillingCurveTests
    {

        [Test]
        public void can_convert_to_and_from_hilbert_space ()
        {
            var d = Hilbert.xy2d(32, 6, 4);
            Console.WriteLine($"6, 4 -> {d}");

            Hilbert.d2xy(32, d, out var x, out var y);
            Console.WriteLine($"{d} -> {x}, {y}");

            Assert.That(x, Is.EqualTo(6));
            Assert.That(y, Is.EqualTo(4));
        }

        [Test]
        public void large_size_hilbert_test ()
        {
            var d = Hilbert.xy2d(1024, 1000, 512);  // `d` up to 1048576
            Console.WriteLine($"6, 4 -> {d}");

            Hilbert.d2xy(1024, d, out var x, out var y);
            Console.WriteLine($"{d} -> {x}, {y}");

            Assert.That(x, Is.EqualTo(1000));
            Assert.That(y, Is.EqualTo(512));
        }

        [Test]
        public void can_convert_to_and_from_2d_morton_space () {
            var g = Morton.EncodeMorton2(5, 7);
            Console.WriteLine($"5, 7 -> {g}");

            Morton.DecodeMorton2(g, out var x, out var y);
            Console.WriteLine($"{g} -> {x}, {y}");

            Assert.That(x, Is.EqualTo(5));
            Assert.That(y, Is.EqualTo(7));
        }

        [Test]
        public void large_size_morton_2d_test () {
            var g = Morton.EncodeMorton2(1025, 1023);
            Console.WriteLine($"1025, 1023 -> {g}");

            Morton.DecodeMorton2(g, out var x, out var y);
            Console.WriteLine($"{g} -> {x}, {y}");

            Assert.That(x, Is.EqualTo(1025));
            Assert.That(y, Is.EqualTo(1023));
        }
        
        [Test]
        public void can_convert_to_and_from_3d_morton_space () {
            var g = Morton.EncodeMorton3(5, 7, 9);
            Console.WriteLine($"5, 7, 9 -> {g}");

            Morton.DecodeMorton3(g, out var x, out var y, out var z);
            Console.WriteLine($"{g} -> {x}, {y}, {z}");

            Assert.That(x, Is.EqualTo(5));
            Assert.That(y, Is.EqualTo(7));
            Assert.That(z, Is.EqualTo(9));
        }

        [Test]
        public void large_size_morton_3d_test () {
            var g = Morton.EncodeMorton3(1001, 1002, 1003);
            Console.WriteLine($"1001, 1002, 1003 -> {g}");
            
            Morton.DecodeMorton3(g, out var x, out var y, out var z);
            Console.WriteLine($"{g} -> {x}, {y}, {z}");

            Assert.That(x, Is.EqualTo(1001));
            Assert.That(y, Is.EqualTo(1002));
            Assert.That(z, Is.EqualTo(1003));
        }
    }
}