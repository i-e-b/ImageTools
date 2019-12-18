using System;
using System.Diagnostics;
using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class ColorCellTests {

        
        [Test]
        public void compressing_and_restoring_a_color_cell_image__test_image_1 () {
            using (var bmp = Load.FromFile("./inputs/1.jpg"))
            {
                var bytes = ColorCellEncoding.EncodeImage2D(bmp);

                using (var bmp2 = ColorCellEncoding.DecodeImage2D(bytes))
                {
                    bmp2.SaveBmp("./outputs/CC_32bpp_1.bmp");
                }

                var bpp = (bytes.Length*8.0) / (bmp.Width * bmp.Height);

                Console.WriteLine($"Encoded to {Bin.Human(bytes.Length)} ({bpp} bpp)");
            }

            Assert.That(Load.FileExists("./outputs/CC_32bpp_1.bmp"));
        }
        [Test]
        public void compressing_and_restoring_a_color_cell_image__test_image_2 () {
            using (var bmp = Load.FromFile("./inputs/2.jpg"))
            {
                var bytes = ColorCellEncoding.EncodeImage2D(bmp);

                using (var bmp2 = ColorCellEncoding.DecodeImage2D(bytes))
                {
                    bmp2.SaveBmp("./outputs/CC_32bpp_2.bmp");
                }

                var bpp = (bytes.Length*8.0) / (bmp.Width * bmp.Height);

                Console.WriteLine($"Encoded to {Bin.Human(bytes.Length)} ({bpp} bpp)");
            }

            Assert.That(Load.FileExists("./outputs/CC_32bpp_2.bmp"));
        }

        [Test]
        public void compressing_and_restoring_a_color_cell_image__mixed_image () {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                var bytes = ColorCellEncoding.EncodeImage2D(bmp);

                using (var bmp2 = ColorCellEncoding.DecodeImage2D(bytes))
                {
                    bmp2.SaveBmp("./outputs/CC_32bpp_3.bmp");
                }

                var bpp = (bytes.Length*8.0) / (bmp.Width * bmp.Height);

                Console.WriteLine($"Encoded to {Bin.Human(bytes.Length)} ({bpp} bpp)");
            }

            Assert.That(Load.FileExists("./outputs/CC_32bpp_3.bmp"));
        }

        [Test]
        public void compressing_and_restoring_a_color_cell_image__mixed_image__low_quality () {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                var bytes = ColorCellEncoding.EncodeImage2D_Tight(bmp, dither:false);

                using (var bmp2 = ColorCellEncoding.DecodeImage2D_Tight(bytes))
                {
                    bmp2.SaveBmp("./outputs/CC_16bpp_3.bmp");
                }

                var bpp = (bytes.Length*8.0) / (bmp.Width * bmp.Height);

                Console.WriteLine($"Encoded to {Bin.Human(bytes.Length)} ({bpp} bpp)");
            }

            Assert.That(Load.FileExists("./outputs/CC_16bpp_3.bmp"));
        }

        [Test]
        public void compressing_and_restoring_a_color_cell_image__mixed_image__low_quality__with_dither () {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                var bytes = ColorCellEncoding.EncodeImage2D_Tight(bmp, dither:true);

                using (var bmp2 = ColorCellEncoding.DecodeImage2D_Tight(bytes))
                {
                    bmp2.SaveBmp("./outputs/CC_16bpp_3_dither.bmp");
                }

                var bpp = (bytes.Length*8.0) / (bmp.Width * bmp.Height);

                Console.WriteLine($"Encoded to {Bin.Human(bytes.Length)} ({bpp} bpp)");
            }

            Assert.That(Load.FileExists("./outputs/CC_16bpp_3_dither.bmp"));
        }
        
        [Test]
        public void compressing_and_restoring_a_color_cell_image__natural_image () {
            using (var bmp = Load.FromFile("./inputs/4.png"))
            {
                var bytes = ColorCellEncoding.EncodeImage2D(bmp);

                using (var bmp2 = ColorCellEncoding.DecodeImage2D(bytes))
                {
                    bmp2.SaveBmp("./outputs/CC_32bpp_4.bmp");
                }

                var bpp = (bytes.Length*8.0) / (bmp.Width * bmp.Height);

                Console.WriteLine($"Encoded to {Bin.Human(bytes.Length)} ({bpp} bpp)");
            }

            Assert.That(Load.FileExists("./outputs/CC_32bpp_4.bmp"));
        }
        
        [Test]
        public void compressing_and_restoring_a_color_cell_image__natural_image__low_quality () {
            using (var bmp = Load.FromFile("./inputs/4.png"))
            {
                var bytes = ColorCellEncoding.EncodeImage2D_Tight(bmp, dither:false);

                using (var bmp2 = ColorCellEncoding.DecodeImage2D_Tight(bytes))
                {
                    bmp2.SaveBmp("./outputs/CC_16bpp_4.bmp");
                }

                var bpp = (bytes.Length*8.0) / (bmp.Width * bmp.Height);

                Console.WriteLine($"Encoded to {Bin.Human(bytes.Length)} ({bpp} bpp)");
            }

            Assert.That(Load.FileExists("./outputs/CC_16bpp_4.bmp"));
        }
        
        [Test]
        public void compressing_and_restoring_a_color_cell_image__natural_image__low_quality__with_dither () {
            using (var bmp = Load.FromFile("./inputs/4.png"))
            {
                var bytes = ColorCellEncoding.EncodeImage2D_Tight(bmp, dither: true);

                using (var bmp2 = ColorCellEncoding.DecodeImage2D_Tight(bytes))
                {
                    bmp2.SaveBmp("./outputs/CC_16bpp_4_dither.bmp");
                }

                var bpp = (bytes.Length*8.0) / (bmp.Width * bmp.Height);

                Console.WriteLine($"Encoded to {Bin.Human(bytes.Length)} ({bpp} bpp)");
            }

            Assert.That(Load.FileExists("./outputs/CC_16bpp_4_dither.bmp"));
        }

        /// <summary>
        /// Color cell compression keeps the fine grain texture of noisy images, and
        /// doesn't change output size based on it.
        /// </summary>
        [Test]
        public void compressing_and_restoring_a_color_cell_image__noise_image()
        {
            using (var bmp = Load.FromFile("./inputs/6.png"))
            {
                var bytes = ColorCellEncoding.EncodeImage2D(bmp);

                using (var bmp2 = ColorCellEncoding.DecodeImage2D(bytes))
                {
                    bmp2.SaveBmp("./outputs/CC_32bpp_6.bmp");
                }

                var bpp = (bytes.Length*8.0) / (bmp.Width * bmp.Height);

                Console.WriteLine($"Encoded to {Bin.Human(bytes.Length)} ({bpp} bpp)");
            }

            Assert.That(Load.FileExists("./outputs/CC_32bpp_6.bmp"));
        }
        
        [Test]
        public void compressing_and_restoring_a_color_cell_image__noise_image__low_quality()
        {
            using (var bmp = Load.FromFile("./inputs/6.png"))
            {
                var bytes = ColorCellEncoding.EncodeImage2D_Tight(bmp, dither: false);

                using (var bmp2 = ColorCellEncoding.DecodeImage2D_Tight(bytes))
                {
                    bmp2.SaveBmp("./outputs/CC_16bpp_6.bmp");
                }

                var bpp = (bytes.Length*8.0) / (bmp.Width * bmp.Height);

                Console.WriteLine($"Encoded to {Bin.Human(bytes.Length)} ({bpp} bpp)");
            }

            Assert.That(Load.FileExists("./outputs/CC_16bpp_6.bmp"));
        }
        
        [Test]
        public void compressing_and_restoring_a_color_cell_image__large_image () {
            using (var bmp = Load.FromFile("./inputs/moire_sample.PNG"))
            {
                var sw = new Stopwatch();
                sw.Start();
                var bytes = ColorCellEncoding.EncodeImage2D(bmp);
                sw.Stop();
                Console.WriteLine($"Core compress took {sw.Elapsed}");


                sw.Reset();
                sw.Start();
                using (var bmp2 = ColorCellEncoding.DecodeImage2D(bytes))
                {
                    sw.Stop();
                    Console.WriteLine($"Core decompress took {sw.Elapsed}");
                    bmp2.SaveBmp("./outputs/CC_32bpp_5.bmp");
                }

                var bpp = (bytes.Length*8.0) / (bmp.Width * bmp.Height);

                Console.WriteLine($"Encoded to {Bin.Human(bytes.Length)} ({bpp} bpp)");
            }

            Assert.That(Load.FileExists("./outputs/CC_32bpp_5.bmp"));
        }
    }
}