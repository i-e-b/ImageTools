using ImageTools.ImageStorageFileFormats;
using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests
{
	[TestFixture]
	public class SavingImages
	{

		[Test]
		public void can_load_and_save_a_copy_of_an_image ()
		{
			using (var bmp = Load.FromFile("./inputs/1.jpg"))
			{
				bmp.SaveJpeg("./outputs/1.jpg");
			}

			Assert.That(Load.FileExists("./outputs/1.jpg"));
		}

		[Test]
		public void can_save_and_read_wavelet_image_format()
		{
			using (var bmp = Load.FromFile("./inputs/1.jpg"))
			{
				bmp.SaveWaveletImageFormat("./outputs/1.wfi");
			}
			Assert.That(Load.FileExists("./outputs/1.wfi"));

			using (var bmp2 = WaveletImageFormat.LoadFile("./outputs/1.wfi"))
			{
				bmp2.SaveJpeg("./outputs/1_round_trip.jpg");
			}
			Assert.That(Load.FileExists("./outputs/1_round_trip.jpg"));
		}
	}
}