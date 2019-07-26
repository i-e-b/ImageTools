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

	}
}