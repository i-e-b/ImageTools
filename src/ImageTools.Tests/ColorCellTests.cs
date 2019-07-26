using System;
using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class ColorCellTests {

        [Test]
        public void compressing_and_restoring_a_color_cell_image__mixed_image () {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                var bytes = ColorCellEncoding.EncodeImage2D(bmp);

                using (var bmp2 = ColorCellEncoding.DecodeImage2D(bytes))
                {
                    bmp2.SaveBmp("./outputs/CC_16bpp_3.bmp");
                }

                var bpp = (bytes.Length*8.0) / (bmp.Width * bmp.Height);

                Console.WriteLine($"Encoded to {Bin.Human(bytes.Length)} ({bpp} bpp)");
            }

            Assert.That(Load.FileExists("./outputs/CC_16bpp_3.bmp"));
        }
        
        [Test]
        public void compressing_and_restoring_a_color_cell_image__natural_image () {
            using (var bmp = Load.FromFile("./inputs/4.png"))
            {
                var bytes = ColorCellEncoding.EncodeImage2D(bmp);

                using (var bmp2 = ColorCellEncoding.DecodeImage2D(bytes))
                {
                    bmp2.SaveBmp("./outputs/CC_16bpp_4.bmp");
                }

                var bpp = (bytes.Length*8.0) / (bmp.Width * bmp.Height);

                Console.WriteLine($"Encoded to {Bin.Human(bytes.Length)} ({bpp} bpp)");
            }

            Assert.That(Load.FileExists("./outputs/CC_16bpp_4.bmp"));
        }
    }
}