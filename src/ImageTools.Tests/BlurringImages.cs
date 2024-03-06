using System;
using ImageTools.ImageDataFormats;
using ImageTools.Utilities;
using NUnit.Framework;
// ReSharper disable AssignNullToNotNullAttribute

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
        
        [Test]
        public void kernel_convolve_an_image () {
            double[,] kernel = {{0.25,0.5,0.25},{0.5, -3.0, 0.5}, {0.25, 0.5, 0.25}}; // discrete Laplace
            
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = Blur.Convolve(bmp, ColorSpace.Identity, ColorSpace.Identity, kernel))
                {
                    bmp2.SaveJpeg("./outputs/3_convolve_laplace.jpg");
                }
            }

            Assert.That(Load.FileExists("./outputs/3_convolve_laplace.jpg"));
        }
        
        [Test]
        public void kernel_blur_an_image () {
            double[,] kernel = {{0.05,0.1,0.05},{0.1, 0.4, 0.1}, {0.05, 0.1, 0.05}};
            
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = Blur.Convolve(bmp, ColorSpace.Identity, ColorSpace.Identity, kernel))
                {
                    bmp2.SaveJpeg("./outputs/3_convolve_blur.jpg");
                }
            }

            Assert.That(Load.FileExists("./outputs/3_convolve_blur.jpg"));
        }
        
        [Test]
        public void kernel_sharpen_an_image () {
            double[,] kernel = {{0.0,-1.0,0.0},{-1.0, 5, -1.0}, {0.0, -1.0, 0.0}};
            
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = Blur.Convolve(bmp, ColorSpace.Identity, ColorSpace.Identity, kernel))
                {
                    bmp2.SaveJpeg("./outputs/3_convolve_sharp.jpg");
                }
            }

            Assert.That(Load.FileExists("./outputs/3_convolve_sharp.jpg"));
        }

        [Test] // this gives a neat effect on noise
        public void kernel_cycling()
        {
            double bf = 1.0 / 256.0;
            double[,] blur = {
                {bf*1, bf*4,  bf*6,  bf*4,  bf*1},
                {bf*4, bf*16, bf*24, bf*16, bf*4},
                {bf*6, bf*24, bf*36, bf*24, bf*6},
                {bf*4, bf*16, bf*24, bf*16, bf*4},
                {bf*1, bf*4,  bf*6,  bf*4,  bf*1}
            };/*
            double sf = -1.0 / 256.0;
            double[,] unSharpMask = {
                {sf*1, sf*4,  sf*6,    sf*4,  sf*1},
                {sf*4, sf*16, sf*24,   sf*16, sf*4},
                {sf*6, sf*24, sf*-476, sf*24, sf*6},
                {sf*4, sf*16, sf*24,   sf*16, sf*4},
                {sf*1, sf*4,  sf*6,    sf*4,  sf*1}
            };*/
            double[,] sharpen = {{0.0,-1.0,0.0},{-1.0, 5, -1.0}, {0.0, -1.0, 0.0}};

            using var original = Load.FromFile("./inputs/6.png");
            var next = original;
            for (int i = 0; i < 30; i++)
            {
                var tmp1 = Blur.Convolve(next, ColorSpace.Identity, ColorSpace.Identity, blur);
                var tmp2 = Blur.Convolve(tmp1, ColorSpace.Identity, ColorSpace.Identity, sharpen);
                next.Dispose();
                tmp1.Dispose();
                next = tmp2;
            }
            
            next.SaveJpeg("./outputs/6_convolve_loop.jpg");
            next.Dispose();

            Assert.That(Load.FileExists("./outputs/6_convolve_loop.jpg"));
        }

        [Test]
        public void kernel_offsets()
        {
            var offsets1 = Blur.KernelSampleOffsets(1);
            Assert.That(offsets1, Is.EqualTo(new[]{0}).AsCollection, "1-sized");
            
            var offsets2 = Blur.KernelSampleOffsets(2);
            Assert.That(offsets2, Is.EqualTo(new[]{-1,1}).AsCollection, "2-sized");
            
            var offsets3 = Blur.KernelSampleOffsets(3);
            Assert.That(offsets3, Is.EqualTo(new[]{-1,0,1}).AsCollection, "3-sized");
            
            var offsets4 = Blur.KernelSampleOffsets(4);
            Assert.That(offsets4, Is.EqualTo(new[]{-2,-1,1,2}).AsCollection, "4-sized");
            
            var offsets5 = Blur.KernelSampleOffsets(5);
            Assert.That(offsets5, Is.EqualTo(new[]{-2,-1,0,1,2}).AsCollection, "5-sized");
        }
    }
}