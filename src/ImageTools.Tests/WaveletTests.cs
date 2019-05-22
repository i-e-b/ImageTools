using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
	public class WaveletTests
	{
		[Test]
		public void haar_test()
		{
			using (var bmp = Load.FromFile("./inputs/1.jpg"))
			{
				using (var bmp2 = Haar.Gradients(bmp))
				{
					bmp2.SaveJpeg("./outputs/Haar_64bpp_1.jpg");
				}
			}

			Assert.That(Load.FileExists("./outputs/Haar_64bpp_1.jpg"));
		}
        
        [Test]
        public void cdf97_horz_test()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = CDF_9_7.HorizontalGradients(bmp))
                {
                    bmp2.SaveJpeg("./outputs/Cdf97_32bpp_3_HZ.jpg", quality: 100);
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_32bpp_3_HZ.jpg"));
        }
        
        [Test]
        public void cdf97_vert_test()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = CDF_9_7.VerticalGradients(bmp))
                {
                    bmp2.SaveJpeg("./outputs/Cdf97_32bpp_3_VT.jpg", quality: 100);
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_32bpp_3_VT.jpg"));
        }

        
        
        [Test]
        public void cdf97_morton_reduce_test()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = CDF_9_7.MortonReduceImage(bmp))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_Morton_32bpp_3.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_Morton_32bpp_3.bmp"));
        }
        
        
        [Test]
        public void cdf97_planar_reduce_test()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = CDF_9_7.Planar3ReduceImage(bmp))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_Planar_32bpp_3.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_Planar_32bpp_3.bmp"));
        }
        
        [Test]
        public void cdf97_planar_2_reduce_test()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = CDF_9_7.Planar2ReduceImage(bmp))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_Planar2_32bpp_3.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_Planar2_32bpp_3.bmp"));
        }

        static int count;
        private void wc(int x, int y, int z) {
            count++;
            //Console.Write($"{x},{y},{z}; ");
        }

        [Test]
        public void temp() {

            //int width = 16, height = 3, depth = 3;
            int maxDim = 256; // largest of any dimension

            // should be along the lines of
            // 0,0,0; 1,0,0; 0,1,0; 0,0,1; 1,1,0; 0,1,1; 1,1,1; 
            count = 0;

            for (int i = 0; i < maxDim; i++)
            {
                //Console.WriteLine();
                // corner
                wc(i,i,i);

                // axiis (excl corner),
                for (int a = 0; a < i; a++)
                {
                    wc(a,i,i); // x
                    wc(i,a,i); // y
                    wc(i,i,a); // z

                    // faces
                    for (int b = 0; b < i; b++)
                    {
                        wc(a,b,i);
                        wc(i,a,b);
                        wc(b,i,a);
                    }
                }
                
            }

            Console.WriteLine($"Final point count = {count}; Expecting {Math.Pow(maxDim,3)}");

        }

        [Test]
        public void wavelet_3d_image_reduction()
        {
            // STEP 1: Load frames
            var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var filePath = Path.Combine(basePath, "../../inputs/EasyFrames");
            var frames = Directory.EnumerateFiles(filePath)
                .OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f)?.Substring(1) ?? "0"))
                .ToArray();

            Assert.That(frames.Length, Is.GreaterThan(5), "Not enough frames");
            var img3d = new Image3d(frames);

            Assert.That(img3d.Y.LongLength, Is.EqualTo(256*256*64)); // every dimension must be a power of 2, but not the same one


            // STEP 2: Do the decomposition
            var sw = new Stopwatch();
            sw.Start();
            CDF_9_7.ReduceImage3D_2(img3d);
            sw.Stop();
            Console.WriteLine($"Core transform took {sw.Elapsed}");

            // STEP 3: output frames for inspection
            for (int z = 0; z < frames.Length; z++)
            {
                using (Bitmap f = img3d.ReadSlice(z)) {
                    f.SaveBmp($"./outputs/Cdf97_3d_f{z}.bmp");
                }
            }
        }
	}
}