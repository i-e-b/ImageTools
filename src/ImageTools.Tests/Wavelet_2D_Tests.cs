using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class Wavelet_2D_Tests {

        [Test]
        public void haar_test()
        {
            using (var bmp = Load.FromFile("./inputs/1.jpg"))
            {
                using (var bmp2 = Haar.Gradients(bmp))
                {
                    bmp2.SaveJpeg("./outputs/Haar_64bpp_1.jpg");
                }
            }

            Assert.That(Load.FileExists("./outputs/Haar_64bpp_1.jpg"));
        }
        
        [Test]
        public void cdf97_horz_test()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = WaveletCompress.HorizontalGradients(bmp))
                {
                    bmp2.SaveJpeg("./outputs/Cdf97_32bpp_3_HZ.jpg", quality: 100);
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_32bpp_3_HZ.jpg"));
        }
        
        [Test]
        public void cdf97_vert_test()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = WaveletCompress.VerticalGradients(bmp))
                {
                    bmp2.SaveJpeg("./outputs/Cdf97_32bpp_3_VT.jpg", quality: 100);
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_32bpp_3_VT.jpg"));
        }

        
        
        [Test]
        public void cdf97_morton_reduce_test()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = WaveletCompress.MortonReduceImage(bmp))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_Morton_32bpp_3.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_Morton_32bpp_3.bmp"));
        }
        
        
        [Test]
        public void cdf97_planar_reduce_test()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = WaveletCompress.Planar3ReduceImage(bmp))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_Planar_32bpp_3.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_Planar_32bpp_3.bmp"));
        }
        
        [Test]
        public void cdf97_planar_2_reduce_test_mixed_image()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = WaveletCompress.Planar2ReduceImage(bmp))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_Planar2_32bpp_3.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_Planar2_32bpp_3.bmp"));
        }
        
        [Test]
        public void cdf97_planar_2_reduce_test_natural_image()
        {
            using (var bmp = Load.FromFile("./inputs/4.png"))
            {
                using (var bmp2 = WaveletCompress.Planar2ReduceImage(bmp))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_Planar2_32bpp_4.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_Planar2_32bpp_4.bmp"));
        }

        [Test]
        public void compressing_non_power_of_two_image () {

            using (var bmp = Load.FromFile("./inputs/1.jpg"))
            {
                using (var bmp2 = WaveletCompress.Planar2ReduceImage(bmp))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_Planar2_32bpp_1.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_Planar2_32bpp_1.bmp"));
        }
        

        [Test]
        public void compressing_non_power_of_two_image_large () {

            using (var bmp = Load.FromFile("./inputs/moire_sample.PNG"))
            {
                using (var bmp2 = WaveletCompress.Planar2ReduceImage(bmp))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_Planar2_32bpp_moire.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_Planar2_32bpp_moire.bmp"));
        }
    }
}