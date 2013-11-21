using System;
using System.IO;
using NUnit.Framework;

namespace ImageTools.Tests
{
	[TestFixture]
	[Description("Most of these tests produce images as output." +
				 "You should open these and check that they are satisfactory.")]
	public class DownscalingImages : WithCleanedOutput
	{
		[Test]
		public void slight_downscale()
		{
			int targetWidth;
			int targetHeight;
			using (var bmp = Load.FromFile("./inputs/1.jpg"))
			{
				targetWidth = bmp.Width - 10;
				targetHeight = bmp.Height - 10;
				using (var bmp2 = Scale.DisregardAspect(bmp, targetWidth, targetHeight))
				{
					bmp2.SaveJpeg("./outputs/1_scaled.jpg");
				}
			}

			Assert.That(File.Exists("./outputs/1_scaled.jpg"));
			using (var result = Load.FromFile("./outputs/1_scaled.jpg"))
			{
				Assert.That(result.Width, Is.EqualTo(targetWidth));
				Assert.That(result.Height, Is.EqualTo(targetHeight));
			}
			Console.WriteLine("Go check the build output and ensure that \"/outputs/1_scaled.jpg\" looks ok");
		}
	}
}
