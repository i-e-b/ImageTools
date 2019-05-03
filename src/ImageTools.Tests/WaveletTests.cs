using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
	public class WaveletTests
	{
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
                using (var bmp2 = CDF_9_7.HorizontalGradients(bmp))
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
                using (var bmp2 = CDF_9_7.HorizontalGradients(bmp))
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
                using (var bmp2 = CDF_9_7.MortonReduceImage(bmp))
                {
                    bmp2.SaveJpeg("./outputs/Cdf97_Reduce_32bpp_3.jpg", quality: 100);
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_Reduce_32bpp_3.jpg"));
        }
	}
}