﻿using System;
using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests
{
	[TestFixture]
	[Description("Most of these tests produce images as output." +
				 "You should open these and check that they are satisfactory.")]
	public class UpscalingImagesTests
	{
		
		[Test]
		public void FastScale_double()
		{
			int targetWidth;
			int targetHeight;
			using (var bmp = Load.FromFile("./inputs/1.jpg"))
			{
				targetWidth = bmp.Width * 2;
				targetHeight = bmp.Height * 2;
				using (var bmp2 = FastScale.DisregardAspect(bmp, targetWidth, targetHeight))
				{
					bmp2.SaveJpeg("./outputs/1_double.jpg");
				}
			}

			Assert.That(Load.FileExists("./outputs/1_double.jpg"));
			using (var result = Load.FromFile("./outputs/1_double.jpg"))
			{
				Assert.That(result.Width, Is.EqualTo(targetWidth));
				Assert.That(result.Height, Is.EqualTo(targetHeight));
			}
			Console.WriteLine("Go check the build output and ensure that \"/outputs/1_double.jpg\" looks ok");
		}
		
        [Test]
        public void FastScale_double_pixelart()
        {
            int targetWidth;
            int targetHeight;
            using (var bmp = Load.FromFile("./inputs/pixart.png"))
            {
                targetWidth = bmp.Width * 2;
                targetHeight = bmp.Height * 2;
                using (var bmp2 = FastScale.DisregardAspect(bmp, targetWidth, targetHeight))
                {
                    bmp2.SaveJpeg("./outputs/pixart_double.jpg");
                }
            }

            Assert.That(Load.FileExists("./outputs/pixart_double.jpg"));
            using (var result = Load.FromFile("./outputs/pixart_double.jpg"))
            {
                Assert.That(result.Width, Is.EqualTo(targetWidth));
                Assert.That(result.Height, Is.EqualTo(targetHeight));
            }
            Console.WriteLine("Go check the build output and ensure that \"/outputs/pixart_double.jpg\" looks ok");
        }


		
        [Test]
        public void EPX_double_pixelart()
        {
            using (var bmp = Load.FromFile("./inputs/pixart.png"))
            {
                using (var bmp2 = PixelScale.EPX_2x(bmp))
                {
                    bmp2.SaveJpeg("./outputs/pixart_epx2x.jpg");
                }
            }

            Assert.That(Load.FileExists("./outputs/pixart_epx2x.jpg"));
        }
		
        [Test]
        public void EPX_double_natural()
        {
            using (var bmp = Load.FromFile("./inputs/1.jpg"))
            {
                using (var bmp2 = PixelScale.EPX_2x(bmp))
                {
                    bmp2.SaveJpeg("./outputs/1_epx2x.jpg");
                }
            }

            Assert.That(Load.FileExists("./outputs/1_epx2x.jpg"));
        }


        [Test]
		public void FastScale_slight_upscale()
		{
			int targetWidth;
			int targetHeight;
			using (var bmp = Load.FromFile("./inputs/1.jpg"))
			{
				targetWidth = (int)(bmp.Width * 1.2);
				targetHeight = (int)(bmp.Height * 1.2);
				using (var bmp2 = FastScale.DisregardAspect(bmp, targetWidth, targetHeight))
				{
					bmp2.SaveJpeg("./outputs/1_plus_ten.jpg");
				}
			}

			Assert.That(Load.FileExists("./outputs/1_plus_ten.jpg"));
			using (var result = Load.FromFile("./outputs/1_plus_ten.jpg"))
			{
				Assert.That(result.Width, Is.EqualTo(targetWidth));
				Assert.That(result.Height, Is.EqualTo(targetHeight));
			}
			Console.WriteLine("Go check the build output and ensure that \"/outputs/1_plus_ten.jpg\" looks ok");
		}

	}
}
