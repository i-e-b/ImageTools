using System;
using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class BlurringImages
    {
        [Test]
        public void can_blur_an_image_with_lookup()
        {
            using (var bmp = Load.FromFile("./inputs/1.jpg"))
            {
                using (var bmp2 = Blur.FastBlur(bmp, 5))
                {
                    bmp2.SaveJpeg("./outputs/1_fast_blurred.jpg");
                }
            }

            Assert.That(Load.FileExists("./outputs/1_fast_blurred.jpg"));
            Console.WriteLine("Go check the build output and ensure that \"/outputs/1_fast_blurred.jpg\" is blurry");
        }

        [Test]
        public void can_blur_an_image_with_shifts()
        {
            using (var bmp = Load.FromFile("./inputs/1.jpg"))
            {
                using (var bmp2 = Blur.ShiftBlur(bmp, 8))
                {
                    bmp2.SaveJpeg("./outputs/1_shiftblur.jpg");
                }
            }

            Assert.That(Load.FileExists("./outputs/1_shiftblur.jpg"));
        }

        [Test]
        public void can_blur_with_a_lowpass_filter()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = Blur.LowpassBlur(bmp, 5))
                {
                    bmp2.SaveJpeg("./outputs/3_lowpass.jpg");
                }
            }

            Assert.That(Load.FileExists("./outputs/3_lowpass.jpg"));
        }
        
        [Test]
        public void can_blur_noise_with_a_lowpass_filter()
        {
            using (var bmp = Load.FromFile("./inputs/6.png"))
            {
                using (var bmp2 = Blur.LowpassBlur(bmp, 15))
                {
                    bmp2.SaveJpeg("./outputs/6_lowpass.jpg");
                }
            }

            Assert.That(Load.FileExists("./outputs/6_lowpass.jpg"));
        }

        [Test]
        public void soft_focus_an_image () {

            using (var bmp = Load.FromFile("./inputs/4.png"))
            {
                using (var bmp2 = Blur.SoftFocus(bmp))
                {
                    bmp2.SaveJpeg("./outputs/4_soft.jpg");
                }
            }

            Assert.That(Load.FileExists("./outputs/4_soft.jpg"));
        }
        

        [Test]
        public void sharpen_an_image () {

            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = Blur.Sharpen(bmp))
                {
                    bmp2.SaveJpeg("./outputs/3_sharp.jpg");
                }
            }

            Assert.That(Load.FileExists("./outputs/3_sharp.jpg"));
        }
	}
}