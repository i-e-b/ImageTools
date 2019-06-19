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
    public class Wavelet_3D_Tests
    {
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

            Assert.That(img3d.Y.LongLength, Is.EqualTo(256 * 256 * 64)); // every dimension must be a power of 2, but not the same one


            // STEP 2: Do the decomposition
            var sw = new Stopwatch();
            sw.Start();
            WaveletCompress.ReduceImage3D_2(img3d);
            sw.Stop();
            Console.WriteLine($"Core transform took {sw.Elapsed}");

            // STEP 3: output frames for inspection
            for (int z = 0; z < frames.Length; z++)
            {
                using (Bitmap f = img3d.ReadSlice(z))
                {
                    f.SaveBmp($"./outputs/Cdf97_3d_f{z}.bmp");
                }
            }
        }

        [Test]
        public void single_file_storage()
        {
            // STEP 1: Load frames
            var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var filePath = Path.Combine(basePath, "../../inputs/EasyFrames");
            var frames = Directory.EnumerateFiles(filePath)
                .OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f)?.Substring(1) ?? "0"))
                .ToArray();

            Assert.That(frames.Length, Is.GreaterThan(5), "Not enough frames");
            var img3d = new Image3d(frames);

            // Raw frames as doubles is *big*
            Console.WriteLine($"3D image memory size: {img3d.ByteSize() / 1048576L} MB");

            Assert.That(img3d.Y.LongLength, Is.EqualTo(256 * 256 * 64)); // every dimension must be a power of 2, but not the same one

            // STEP 2: Do the decomposition
            var sw = new Stopwatch();
            sw.Start();
            var targetPath = Path.Combine(basePath, "outputs/w3d_a_packed.bin");
            WaveletCompress.ReduceImage3D_ToFile(img3d, targetPath);
            sw.Stop();
            Console.WriteLine($"Compression took {sw.Elapsed}. Written to {targetPath}");

            // STEP 3: Restore original from file
            sw.Reset();
            sw.Start();
            Image3d result = WaveletCompress.RestoreImage3D_FromFile(targetPath);
            sw.Stop();
            Console.WriteLine($"Restore took {sw.Elapsed}");
            
            // STEP 4: output frames for inspection
            for (int z = 0; z < result.Depth; z++)
            {
                using (Bitmap f = result.ReadSlice(z))
                {
                    f.SaveBmp($"./outputs/w3d_f{z}.bmp");
                }
            }
        }

        [Test]
        public void experimental_color_space()
        {
            // STEP 1: Load frames
            var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var filePath = Path.Combine(basePath, "../../inputs/EasyFrames");
            var frames = Directory.EnumerateFiles(filePath)
                .OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f)?.Substring(1) ?? "0"))
                .ToArray();

            Assert.That(frames.Length, Is.GreaterThan(5), "Not enough frames");
            var img3d = new Image3d(frames, ColorSpace.RGBToExp);

            // Raw frames as doubles is *big*
            Console.WriteLine($"3D image memory size: {img3d.ByteSize() / 1048576L} MB");

            Assert.That(img3d.Y.LongLength, Is.EqualTo(256 * 256 * 64)); // every dimension must be a power of 2, but not the same one

            // STEP 2: Do the decomposition
            var sw = new Stopwatch();
            sw.Start();
            var targetPath = Path.Combine(basePath, "outputs/w3d_a_packed.bin");
            WaveletCompress.ReduceImage3D_ToFile(img3d, targetPath);
            sw.Stop();
            Console.WriteLine($"Compression took {sw.Elapsed}. Written to {targetPath}");

            // STEP 3: Restore original from file
            sw.Reset();
            sw.Start();
            Image3d result = WaveletCompress.RestoreImage3D_FromFile(targetPath);
            sw.Stop();
            Console.WriteLine($"Restore took {sw.Elapsed}");
            
            // STEP 4: output frames for inspection
            for (int z = 0; z < result.Depth; z++)
            {
                using (Bitmap f = result.ReadSlice(z, ColorSpace.ExpToRGB))
                {
                    f.SaveBmp($"./outputs/w3d_f{z}.bmp");
                }
            }
        }
    }
}