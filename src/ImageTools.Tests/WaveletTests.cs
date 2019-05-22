﻿using System;
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

        [Test]
        public void temp() {
            var check = new List<List<int>>();
            var temp = new List<int>();
            
            var max = 63;
            var limit = max >> 1;

            // this does it, but backwards and not quite in order
            for (int b = max; b > limit; b--)
            {
                for (int i = b; i > 0; i >>= 1)
                {
                    temp.Add(i);
                    if (i>>1 == (i+1)>>1) break;
                }
                temp.Reverse();
                check.Add(temp);
                temp = new List<int>();
            }

            Console.WriteLine(string.Join(",",check.SelectMany(l=>l)));
            check.Sort((a,b)=>a[0].CompareTo(b[0]));
            Console.WriteLine(string.Join(",",check.SelectMany(l=>l)));
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