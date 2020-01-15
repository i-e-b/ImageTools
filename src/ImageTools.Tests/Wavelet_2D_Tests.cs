using System;
using System.Diagnostics;
using System.IO;
using ImageTools.ImageDataFormats;
using ImageTools.Utilities;
using ImageTools.WaveletTransforms;
using NUnit.Framework;
// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable PossibleNullReferenceException

namespace ImageTools.Tests
{
    [TestFixture]
    public class Wavelet_2D_Tests {

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
        public void haar_reduce_to_disk() {

            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                var interleavedFile = WaveletCompress.ReduceImage2D_ToFile(bmp, Haar.Forward);
                using (var fs = File.Open(Save.ToPath("./outputs/Haar_Planar2_32bpp_3.bin"), FileMode.Create))
                {
                    interleavedFile.WriteToStream(fs);
                    Console.WriteLine("Result size: " + Bin.Human(fs.Length));
                }
            }

            Assert.That(Load.FileExists("./outputs/Haar_Planar2_32bpp_3.bin"));
        }

        [Test]
        public void Roundtrip_haar_reduce_and_expand_test() {

            var sw = new Stopwatch();
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                var orig = bmp.Width*bmp.Height * 4;

                sw.Start();
                var compressed = WaveletCompress.ReduceImage2D_ToFile(bmp, Haar.Forward);
                Console.WriteLine($"Original = {Bin.Human(orig)}, compressed = {Bin.Human(compressed.ByteSize())}");
                
                using (var bmp2 = WaveletCompress.RestoreImage2D_FromFile(compressed, Haar.Inverse))
                {
                    sw.Stop();
                    Console.WriteLine($"Round trip took {sw.Elapsed}");
                    bmp2.SaveBmp("./outputs/Haar_3.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Haar_3.bmp"));
        }
        

        [Test]
        public void Roundtrip_integer_wavelet_reduce_and_expand_test() {

            var sw = new Stopwatch();
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                var orig = bmp.Width*bmp.Height * 4;

                sw.Start();
                var compressed = WaveletCompress.ReduceImage2D_ToFile(bmp, IntegerWavelet.Forward);
                Console.WriteLine($"Original = {Bin.Human(orig)}, compressed = {Bin.Human(compressed.ByteSize())}");
                
                using (var bmp2 = WaveletCompress.RestoreImage2D_FromFile(compressed, IntegerWavelet.Inverse))
                {
                    sw.Stop();
                    Console.WriteLine($"Round trip took {sw.Elapsed}");
                    bmp2.SaveBmp("./outputs/WaveInt_3.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/WaveInt_3.bmp"));
        }


        [Test]
        public void Roundtrip_CDF53_reduce_and_expand_test() {

            var sw = new Stopwatch();
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                var orig = bmp.Width*bmp.Height * 4;

                sw.Start();
                var compressed = WaveletCompress.ReduceImage2D_ToFile(bmp, CDF.Fwt53);
                Console.WriteLine($"Original = {Bin.Human(orig)}, compressed = {Bin.Human(compressed.ByteSize())}");
                
                using (var bmp2 = WaveletCompress.RestoreImage2D_FromFile(compressed, CDF.Iwt53))
                {
                    sw.Stop();
                    Console.WriteLine($"Round trip took {sw.Elapsed}");
                    bmp2.SaveBmp("./outputs/CDF53_RT_3.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/CDF53_RT_3.bmp"));
        }

        [Test]
        public void Roundtrip_CDF97_reduce_and_expand_test() {

            var sw = new Stopwatch();
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                var orig = bmp.Width*bmp.Height * 4;

                sw.Start();
                var compressed = WaveletCompress.ReduceImage2D_ToFile(bmp, CDF.Fwt97);
                Console.WriteLine($"Original = {Bin.Human(orig)}, compressed = {Bin.Human(compressed.ByteSize())}");
                
                using (var bmp2 = WaveletCompress.RestoreImage2D_FromFile(compressed, CDF.Iwt97))
                {
                    sw.Stop();
                    Console.WriteLine($"Round trip took {sw.Elapsed}");
                    bmp2.SaveBmp("./outputs/CDF97_RT_3.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/CDF97_RT_3.bmp"));
        }
                
        [Test]
        public void cdf97_horz_test()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = WaveletCompress.HorizontalGradients(bmp))
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
                using (var bmp2 = WaveletCompress.VerticalGradients(bmp))
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
                using (var bmp2 = WaveletCompress.MortonReduceImage(bmp))
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
                using (var bmp2 = WaveletCompress.Planar3ReduceImage(bmp))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_Planar_32bpp_3.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_Planar_32bpp_3.bmp"));
        }
        
        [Test]
        public void cdf97_planar_2_reduce_test_mixed_image()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = WaveletCompress.Planar2ReduceImage(bmp))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_Planar2_32bpp_3.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_Planar2_32bpp_3.bmp"));
        }
        

        [Test]
        public void cdf97_reduce_to_disk() {

            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                var interleavedFile = WaveletCompress.ReduceImage2D_ToFile(bmp, CDF.Fwt97);
                using (var fs = File.Open(Save.ToPath("./outputs/CDF_Planar2_32bpp_3.bin"), FileMode.Create))
                {
                    interleavedFile.WriteToStream(fs);
                    
                    var bpp = (fs.Length*8.0) / (bmp.Width * bmp.Height);
                    Console.WriteLine($"Result size: {Bin.Human(fs.Length)} ({bpp} bpp)");
                }
            }

            Assert.That(Load.FileExists("./outputs/CDF_Planar2_32bpp_3.bin"));
        }
        
        [Test]
        public void cdf97_planar_2_reduce_test_natural_image()
        {
            using (var bmp = Load.FromFile("./inputs/4.png"))
            {
                using (var bmp2 = WaveletCompress.Planar2ReduceImage(bmp))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_Planar2_32bpp_4.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_Planar2_32bpp_4.bmp"));
        }
        
        [Test]
        public void cdf97_planar_2_reduce_test_noise_image()
        {
            using (var bmp = Load.FromFile("./inputs/6.png"))
            {
                using (var bmp2 = WaveletCompress.Planar2ReduceImage(bmp))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_Planar2_32bpp_6.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_Planar2_32bpp_6.bmp"));
        }

        [Test]
        public void compressing_non_power_of_two_image () {

            using (var bmp = Load.FromFile("./inputs/1.jpg"))
            {
                using (var bmp2 = WaveletCompress.Planar2ReduceImage(bmp))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_Planar2_32bpp_1.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_Planar2_32bpp_1.bmp"));
        }
        
        [Test]
        public void compressing_non_power_of_two_image_large () {

            using (var bmp = Load.FromFile("./inputs/moire_sample.PNG"))
            {
                using (var bmp2 = WaveletCompress.Planar2ReduceImage(bmp))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_Planar2_32bpp_moire.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_Planar2_32bpp_moire.bmp"));
        }

        [Test]
        public void decompressing_an_image_to_normal_size()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                var compressed = WaveletCompress.ReduceImage2D_ToFile(bmp, CDF.Fwt97);

                // ReSharper disable once RedundantArgumentDefaultValue
                using (var bmp2 = WaveletCompress.RestoreImage2D_FromFile(compressed, CDF.Iwt97, scale: 1))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_3_1to1.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_3_1to1.bmp"));
        }

        [Test]
        public void decompressing_a_truncated_file_to_normal_image_size()
        {
            // This works nicely with Deflate compression, but shows artifacts with arithmetic coding. Need a way to catch unexpected end of stream nicely?
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                var compressed = WaveletCompress.ReduceImage2D_ToFile(bmp, CDF.Fwt97);

                Console.WriteLine($"Compressed to {Bin.Human(compressed.ByteSize())}");

                var ms = new MemoryStream();
                compressed.WriteToStream(ms);
                ms.Seek(0, SeekOrigin.Begin);
                var bytes = new byte[(long)(ms.Length * 0.5)];
                ms.Read(bytes, 0, bytes.Length);
                var trunc = new MemoryStream(bytes);

                
                Console.WriteLine($"Restoring image from first {Bin.Human(bytes.Length)}");

                var truncated = InterleavedFile.ReadFromStream(trunc);

                // ReSharper disable once RedundantArgumentDefaultValue
                using (var bmp2 = WaveletCompress.RestoreImage2D_FromFile(truncated, CDF.Iwt97, scale: 1))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_3_truncated.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_3_truncated.bmp"));
        }

        [Test]
        public void decompressing_an_image_to_half_size () {

            
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                var compressed = WaveletCompress.ReduceImage2D_ToFile(bmp, CDF.Fwt97);

                using (var bmp2 = WaveletCompress.RestoreImage2D_FromFile(compressed, CDF.Iwt97, scale: 2))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_3_HALF.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_3_HALF.bmp"));
        }
        
        [Test]
        public void decompressing_an_image_to_half_size_non_power_two()
        {
            using (var bmp = Load.FromFile("./inputs/1.jpg"))
            {
                var compressed = WaveletCompress.ReduceImage2D_ToFile(bmp, CDF.Fwt97);

                using (var bmp2 = WaveletCompress.RestoreImage2D_FromFile(compressed, CDF.Iwt97, scale: 2))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_1_HALF.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_1_HALF.bmp"));
        }
        
        [Test]
        public void decompressing_an_image_to_quarter_size_non_power_two()
        {
            using (var bmp = Load.FromFile("./inputs/1.jpg"))
            {
                var compressed = WaveletCompress.ReduceImage2D_ToFile(bmp, CDF.Fwt97);

                using (var bmp2 = WaveletCompress.RestoreImage2D_FromFile(compressed, CDF.Iwt97, scale: 3))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_1_QUARTER.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_1_QUARTER.bmp"));
        }
        
        [Test]
        public void decompressing_an_image_to_quarter_size () {

            
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                var compressed = WaveletCompress.ReduceImage2D_ToFile(bmp, CDF.Fwt97);

                using (var bmp2 = WaveletCompress.RestoreImage2D_FromFile(compressed, CDF.Iwt97, scale: 3))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_3_QUARTER.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_3_QUARTER.bmp"));
        }
        
        [Test]
        public void decompressing_an_image_to_eighth_size () {

            
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                var compressed = WaveletCompress.ReduceImage2D_ToFile(bmp, CDF.Fwt97);

                using (var bmp2 = WaveletCompress.RestoreImage2D_FromFile(compressed, CDF.Iwt97, scale: 4))
                {
                    bmp2.SaveBmp("./outputs/Cdf97_3_EIGHTH.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/Cdf97_3_EIGHTH.bmp"));
        }
    }
}