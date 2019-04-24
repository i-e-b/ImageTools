using System;
using System.IO;
using NUnit.Framework;

namespace ImageTools.Tests
{
	[TestFixture]
	public class HaarWaveletTests
	{
		[Test]
		public void just_playing()
		{
			using (var bmp = Load.FromFile("./inputs/1.jpg"))
			{
				using (var bmp2 = Haar.Gradients(bmp))
				{
					bmp2.SaveJpeg("./outputs/64bpp_1.jpg");
				}
			}

			Assert.That(Load.FileExists("./outputs/64bpp_1.jpg"));
		}
	}
}