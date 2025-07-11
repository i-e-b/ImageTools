using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests
{
	[TestFixture, Explicit]
	public class SampleDownscales
	{
		[Test, Ignore("Batch processing entry point")]
		public void batch_downscale_100 ()
		{
			foreach (var file in Directory.GetFiles(@"C:\temp\samples", "*.jpg", SearchOption.TopDirectoryOnly))
			{
				if (file.Contains("S.jpg")) continue;
				using var bmp  = Load.FromFile(file);
				using var bmp2 = FastScale.MaintainAspect(bmp, 100, 100);
				bmp2.SaveJpeg(file + ".100S.jpg");
			}
		}
		
		[Test, Ignore("Batch processing entry point")]
		public void batch_downscale_500 ()
		{
			foreach (var file in Directory.GetFiles(@"C:\temp\samples", "*.jpg", SearchOption.TopDirectoryOnly))
			{
				if (file.Contains("S.jpg")) continue;
				using var bmp  = Load.FromFile(file);
				using var bmp2 = FastScale.MaintainAspect(bmp, 500, 500);
				bmp2.SaveJpeg(file + ".500S.jpg");
			}
		}
	}
}