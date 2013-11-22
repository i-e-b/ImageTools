using System;
using System.IO;
using NUnit.Framework;

namespace ImageTools.Tests
{
	[TestFixture]
	[Description("Most of these tests produce images as output." +
				 "You should open these and check that they are satisfactory.")]
	public class UpscalingImages
	{
		[Test, Ignore("Not implemented")]
		public void slight_upscale()
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

			Assert.That(File.Exists("./outputs/1_plus_ten.jpg"));
			using (var result = Load.FromFile("./outputs/1_plus_ten.jpg"))
			{
				Assert.That(result.Width, Is.EqualTo(targetWidth));
				Assert.That(result.Height, Is.EqualTo(targetHeight));
			}
			Console.WriteLine("Go check the build output and ensure that \"/outputs/1_plus_ten.jpg\" looks ok");
		}

	}
}
