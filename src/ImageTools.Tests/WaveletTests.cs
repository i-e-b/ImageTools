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
        public void cdf97_simple_test()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = CDF_9_7.HorizontalGradients(bmp))
                {
                    bmp2.SaveJpeg("./outputs/Cdf97_32bpp_3.jpg", quality: 100);
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_32bpp_3.jpg"));
        }

        
        
        [Test]
        public void cdf97_reduce_test()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = CDF_9_7.ReduceImage(bmp))
                {
                    bmp2.SaveJpeg("./outputs/Cdf97_Reduce_32bpp_3.jpg", quality: 100);
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_Reduce_32bpp_3.jpg"));
        }
	}
}