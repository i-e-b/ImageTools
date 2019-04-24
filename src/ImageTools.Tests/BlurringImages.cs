using System;
using System.IO;
using NUnit.Framework;

namespace ImageTools.Tests
{
	[TestFixture]
	public class BlurringImages
	{
		[Test]
		public void can_blur_an_image ()
		{
			using (var bmp = Load.FromFile("./inputs/1.jpg"))
			{
				using (var bmp2 = Blur.FastBlur(bmp, 4))
				{
					bmp2.SaveJpeg("./outputs/1_blurred.jpg");
				}
			}

			Assert.That(Load.FileExists("./outputs/1_blurred.jpg"));
			Console.WriteLine("Go check the build output and ensure that \"/outputs/1_blurred.jpg\" is blurry");
		}
	}
}