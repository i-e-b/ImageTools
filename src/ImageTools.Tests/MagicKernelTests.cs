using System;
using ImageTools.Utilities;
using NUnit.Framework;
// ReSharper disable PossibleNullReferenceException

namespace ImageTools.Tests
{
    [TestFixture]
    public class MagicKernelTests
    {
        [Test]
        public void Magic_upscale()
        {
            int targetWidth;
            int targetHeight;
            using (var bmp = Load.FromFile("./inputs/1.jpg"))
            {
                targetWidth = bmp.Width * 2;
                targetHeight = bmp.Height * 2;
                using (var bmp2 = MagicKernel.DoubleImage(bmp))
                {
                    bmp2.SaveBmp("./outputs/1_magic_double.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/1_magic_double.bmp"));
            using (var result = Load.FromFile("./outputs/1_magic_double.bmp"))
            {
                Assert.That(result.Width, Is.EqualTo(targetWidth));
                Assert.That(result.Height, Is.EqualTo(targetHeight));
            }
            Console.WriteLine("Go check the build output and ensure that \"/outputs/1_magic_double.bmp\" looks ok");
        }
    }
}