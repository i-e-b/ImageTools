using System;
using System.IO;
using System.Reflection;
using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests
{
	[TestFixture]
	[Description("Most of these tests produce images as output." +
				 "You should open these and check that they are satisfactory.")]
	public class DownscalingImages
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
				using (var bmp2 = FastScale.DisregardAspect(bmp, targetWidth, targetHeight))
				{
					bmp2.SaveJpeg("./outputs/1_scaled_slight.jpg");
				}
			}

			Assert.That(Load.FileExists("./outputs/1_scaled_slight.jpg"));
			using (var result = Load.FromFile("./outputs/1_scaled_slight.jpg"))
			{
				Assert.That(result.Width, Is.EqualTo(targetWidth));
				Assert.That(result.Height, Is.EqualTo(targetHeight));
			}
			Console.WriteLine("Go check the build output and ensure that \"/outputs/1_scaled_slight.jpg\" looks ok");
		}

		[Test]
		public void exact_small_downscale()
		{
			int targetWidth;
			int targetHeight;
			using (var bmp = Load.FromFile("./inputs/2.jpg"))
			{
				targetWidth = 100;
				targetHeight = 100;
				using (var bmp2 = FastScale.DisregardAspect(bmp, targetWidth, targetHeight))
				{
					bmp2.SaveJpeg("./outputs/2_scaled.jpg");
				}
			}

			Assert.That(Load.FileExists("./outputs/2_scaled.jpg"));
			using (var result = Load.FromFile("./outputs/2_scaled.jpg"))
			{
				Assert.That(result.Width, Is.EqualTo(targetWidth));
				Assert.That(result.Height, Is.EqualTo(targetHeight));
			}
			Console.WriteLine("Go check the build output and ensure that \"/outputs/2_scaled.jpg\" looks ok");
		}

		[Test]
		public void nearly_50_downscale()
		{
			int targetWidth;
			int targetHeight;
			using (var bmp = Load.FromFile("./inputs/1.jpg"))
			{
				targetWidth = (bmp.Width / 2) + 15;
				targetHeight = (bmp.Height / 2) + 15;
				using (var bmp2 = FastScale.DisregardAspect(bmp, targetWidth, targetHeight))
				{
					bmp2.SaveJpeg("./outputs/1_scaled.jpg");
				}
			}

			Assert.That(Load.FileExists("./outputs/1_scaled.jpg"));
			using (var result = Load.FromFile("./outputs/1_scaled.jpg"))
			{
				Assert.That(result.Width, Is.EqualTo(targetWidth));
				Assert.That(result.Height, Is.EqualTo(targetHeight));
			}
			Console.WriteLine("Go check the build output and ensure that \"/outputs/1_scaled.jpg\" looks ok");
		}

		[Test]
		public void over_50_downscale()
		{
			int targetWidth;
			int targetHeight;
			using (var bmp = Load.FromFile("./inputs/1.jpg"))
			{
				targetWidth = (bmp.Width / 2) - 5;
				targetHeight = (bmp.Height / 2) - 5;
				using (var bmp2 = FastScale.DisregardAspect(bmp, targetWidth, targetHeight))
				{
					bmp2.SaveJpeg("./outputs/1_scaled_51.jpg");
				}
			}

			Assert.That(Load.FileExists("./outputs/1_scaled_51.jpg"));
			using (var result = Load.FromFile("./outputs/1_scaled_51.jpg"))
			{
				Assert.That(result.Width, Is.EqualTo(targetWidth));
				Assert.That(result.Height, Is.EqualTo(targetHeight));
			}
			Console.WriteLine("Go check the build output and ensure that \"/outputs/1_scaled_51.jpg\" looks ok");
		}

		[Test]
		public void non_square_downscale_flat()
		{
			int targetWidth;
			int targetHeight;
			using (var bmp = Load.FromFile("./inputs/1.jpg"))
			{
				targetWidth = bmp.Width - 5;
				targetHeight = (bmp.Height / 2) - 5;
				using (var bmp2 = FastScale.DisregardAspect(bmp, targetWidth, targetHeight))
				{
					bmp2.SaveJpeg("./outputs/non_square_downscale_flat.jpg");
				}
			}

			Assert.That(Load.FileExists("./outputs/non_square_downscale_flat.jpg"));
			using (var result = Load.FromFile("./outputs/non_square_downscale_flat.jpg"))
			{
				Assert.That(result.Width, Is.EqualTo(targetWidth));
				Assert.That(result.Height, Is.EqualTo(targetHeight));
			}
			Console.WriteLine("Go check the build output and ensure that \"/outputs/non_square_downscale_flat.jpg\" looks ok");
		}
		
		[Test]
		public void non_square_downscale_tall()
		{
			int targetWidth;
			int targetHeight;
			using (var bmp = Load.FromFile("./inputs/1.jpg"))
			{
				targetWidth = (bmp.Width / 2) + 5;
				targetHeight = bmp.Height - 5;
				using (var bmp2 = FastScale.DisregardAspect(bmp, targetWidth, targetHeight))
				{
					bmp2.SaveJpeg("./outputs/non_square_downscale_tall.jpg");
				}
			}

			Assert.That(Load.FileExists("./outputs/non_square_downscale_tall.jpg"));
			using (var result = Load.FromFile("./outputs/non_square_downscale_tall.jpg"))
			{
				Assert.That(result.Width, Is.EqualTo(targetWidth));
				Assert.That(result.Height, Is.EqualTo(targetHeight));
			}
			Console.WriteLine("Go check the build output and ensure that \"/outputs/non_square_downscale_tall.jpg\" looks ok");
		}

		
		[Test]
		public void keep_aspect_downscale_flat()
		{
			using (var bmp = Load.FromFile("./inputs/1.jpg"))
			{
				int targetWidth = bmp.Width - 5;
				int targetHeight = (bmp.Height / 2) - 5;
				using (var bmp2 = FastScale.MaintainAspect(bmp, targetWidth, targetHeight))
				{
					bmp2.SaveJpeg("./outputs/keep_aspect_downscale_flat.jpg");
				}
			}

			Assert.That(Load.FileExists("./outputs/keep_aspect_downscale_flat.jpg"));
			Console.WriteLine("Go check the build output and ensure that \"/outputs/keep_aspect_downscale_flat.jpg\" looks ok");
		}
		
		[Test]
		public void keep_aspect_downscale_tall()
		{
			using (var bmp = Load.FromFile("./inputs/1.jpg"))
			{
				int targetWidth = (bmp.Width / 2) + 5;
				int targetHeight = bmp.Height - 5;
				using (var bmp2 = FastScale.MaintainAspect(bmp, targetWidth, targetHeight))
				{
					bmp2.SaveJpeg("./outputs/keep_aspect_downscale_tall.jpg");
				}
			}

			Assert.That(Load.FileExists("./outputs/keep_aspect_downscale_tall.jpg"));
			Console.WriteLine("Go check the build output and ensure that \"/outputs/keep_aspect_downscale_tall.jpg\" looks ok");
		}

		[Test]
		public void very_big_downscale_with_fine_details()
		{
			int targetWidth;
			int targetHeight;
			using (var bmp = Load.FromFile("./inputs/moire_sample.PNG"))
			{
				targetWidth = (bmp.Width / 8);
				targetHeight = (bmp.Height / 8);
				using (var bmp2 = FastScale.DisregardAspect(bmp, targetWidth, targetHeight))
				{
					bmp2.SaveJpeg("./outputs/moire_scaled.jpg");
				}
			}

			Assert.That(Load.FileExists("./outputs/moire_scaled.jpg"));
			using (var result = Load.FromFile("./outputs/moire_scaled.jpg"))
			{
				Assert.That(result.Width, Is.EqualTo(targetWidth));
				Assert.That(result.Height, Is.EqualTo(targetHeight));
			}
			Console.WriteLine("Go check the build output and ensure that \"/outputs/moire_scaled.jpg\" looks ok");
		}
		
		[Test]
		public void very_big_downscale_with_fine_details_non_power_two()
		{
			int targetWidth;
			int targetHeight;
			using (var bmp = Load.FromFile("./inputs/moire_sample.PNG"))
			{
				targetWidth = (bmp.Width / 8) + 20;
				targetHeight = (bmp.Height / 8) + 20;
				using (var bmp2 = FastScale.DisregardAspect(bmp, targetWidth, targetHeight))
				{
					bmp2.SaveJpeg("./outputs/moire_scaled_np2.jpg");
				}
			}

			Assert.That(Load.FileExists("./outputs/moire_scaled_np2.jpg"));
			using (var result = Load.FromFile("./outputs/moire_scaled_np2.jpg"))
			{
				Assert.That(result.Width, Is.EqualTo(targetWidth));
				Assert.That(result.Height, Is.EqualTo(targetHeight));
			}
			Console.WriteLine("Go check the build output and ensure that \"/outputs/moire_scaled_np2.jpg\" looks ok");
		}
		
		[Test]
		public void very_big_downscale_with_fine_details_non_power_two_smaller()
		{
			int targetWidth;
			int targetHeight;
			using (var bmp = Load.FromFile("./inputs/moire_sample.PNG"))
			{
				targetWidth = (int)(bmp.Width * 0.12);
				targetHeight = (int)(bmp.Height * 0.12);
				using (var bmp2 = FastScale.DisregardAspect(bmp, targetWidth, targetHeight))
				{
					bmp2.SaveJpeg("./outputs/moire_scaled_np2_2.jpg");
				}
			}

			Assert.That(Load.FileExists("./outputs/moire_scaled_np2_2.jpg"));
			using (var result = Load.FromFile("./outputs/moire_scaled_np2_2.jpg"))
			{
				Assert.That(result.Width, Is.EqualTo(targetWidth));
				Assert.That(result.Height, Is.EqualTo(targetHeight));
			}
			Console.WriteLine("Go check the build output and ensure that \"/outputs/moire_scaled_np2_2.jpg\" looks ok");
		}


        

        [Test, Explicit("Resizes inputs for other tests")]
        public void frame_resize()
        {
            
            var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var filePath = Path.Combine(basePath, "../../inputs/EasyFrames");
            var frames = Directory.EnumerateFiles(filePath);

            int f = 0;
            foreach (var frame in frames)
            {
                using (var bmp = Load.FromFile(frame))
                {
                    using (var bmp2 = FastScale.DisregardAspect(bmp, 256, 256))
                    {
                        bmp2.SaveJpeg($"./outputs/f{f++}.jpg");
                    }
                }
            }

        }
	}
}
