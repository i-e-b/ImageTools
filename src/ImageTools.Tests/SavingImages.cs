using System.IO;
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

			Assert.That(File.Exists("./outputs/1.jpg"));
		}

	}
}