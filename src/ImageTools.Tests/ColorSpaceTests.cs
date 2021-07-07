using System;
using System.Drawing;
using System.Drawing.Imaging;
using ImageTools.ImageDataFormats;
using ImageTools.Utilities;
using NUnit.Framework;
// ReSharper disable InconsistentNaming
// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable PossibleNullReferenceException

namespace ImageTools.Tests
{
    [TestFixture]
    public class ColorSpaceTests {
        [Test]
        public void Y_Cb_Cr__Round_trip () {
            var R_in = 127;
            var G_in = 20;
            var B_in = 30;

            var c_in = ColorSpace.ComponentToCompound(0, R_in, G_in, B_in);

            var YCgCb = ColorSpace.RGB32_To_Ycbcr32(c_in);
            var c_out = ColorSpace.Ycbcr32_To_RGB32(YCgCb);

            ColorSpace.CompoundToComponent(c_out, out _, out var R_out, out var G_out, out var B_out);

            Assert.That(R_out, Is.InRange(R_in - 2, R_in + 2));
            Assert.That(G_out, Is.InRange(G_in - 2, G_in + 2));
            Assert.That(B_out, Is.InRange(B_in - 2, B_in + 2));
        }
        
        [Test]
        public void Y_Co_Cg__Round_trip() {
            var R_in = 127;
            var G_in = 20;
            var B_in = 30;

            var c_in = ColorSpace.ComponentToCompound(0, R_in, G_in, B_in);

            var YCgCb = ColorSpace.RGB32_To_Ycocg32(c_in);
            var c_out = ColorSpace.Ycocg32_To_RGB32(YCgCb);
            // https://en.wikipedia.org/wiki/YCoCg

            ColorSpace.CompoundToComponent(c_out, out _, out var R_out, out var G_out, out var B_out);
            
            Assert.That(R_out, Is.InRange(R_in - 2, R_in + 2));
            Assert.That(G_out, Is.InRange(G_in - 2, G_in + 2));
            Assert.That(B_out, Is.InRange(B_in - 2, B_in + 2));
        }
        
        [Test]
        public void YUV_integer__Round_trip() {
            var R_in = 127;
            var G_in = 20;
            var B_in = 30;

            var c_in = ColorSpace.ComponentToCompound(0, R_in, G_in, B_in);

            ColorSpace.RGB32_To_YUV888(c_in, out var Y, out var U, out var V);
            ColorSpace.YUV888_To_RGB32(Y,U,V, out var c_out);

            ColorSpace.CompoundToComponent(c_out, out _, out var R_out, out var G_out, out var B_out);
            
            Assert.That(R_out, Is.InRange(R_in - 2, R_in + 2));
            Assert.That(G_out, Is.InRange(G_in - 2, G_in + 2));
            Assert.That(B_out, Is.InRange(B_in - 2, B_in + 2));
        }

        [Test, Description("show the result of just one plane at a time from an image")]
        public void Y_Cb_Cr__separations()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var dst = new Bitmap(bmp))
                {
                    BitmapTools.ArgbImageToYUVPlanes(bmp, out var Yp, out var Up, out var Vp);
                    var zeroP = new double[Yp.Length]; // to zero out other planes
                    for (int i = 0; i < zeroP.Length; i++) { zeroP[i] = 127.5; }

                    BitmapTools.YUVPlanes_To_ArgbImage(dst, 0, Yp, zeroP, zeroP);
                    dst.SaveBmp("./outputs/3_YUV_Y-only.bmp");
                    
                    BitmapTools.YUVPlanes_To_ArgbImage(dst, 0, zeroP, Up, zeroP);
                    dst.SaveBmp("./outputs/3_YUV_U-only.bmp");
                    
                    BitmapTools.YUVPlanes_To_ArgbImage(dst, 0, zeroP, zeroP, Vp);
                    dst.SaveBmp("./outputs/3_YUV_V-only.bmp");
                }
            }
        }
        
        [Test, Description("show the result of just one plane at a time from an image")]
        public void Y_Co_Cg__separations()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var dst = new Bitmap(bmp))
                {
                    BitmapTools.ArgbImageToYCoCgPlanes(bmp, out var Yp, out var Co, out var Cg);
                    var zeroP = new double[Yp.Length]; // to zero out other planes
                    for (int i = 0; i < zeroP.Length; i++) { zeroP[i] = 127.5; }

                    BitmapTools.YCoCgPlanes_To_ArgbImage(dst, 0, Yp, zeroP, zeroP);
                    dst.SaveBmp("./outputs/3_YCoCg_Y-only.bmp");
                    
                    BitmapTools.YCoCgPlanes_To_ArgbImage(dst, 0, zeroP, Co, zeroP);
                    dst.SaveBmp("./outputs/3_YCoCg_Co-only.bmp");
                    
                    BitmapTools.YCoCgPlanes_To_ArgbImage(dst, 0, zeroP, zeroP, Cg);
                    dst.SaveBmp("./outputs/3_YCoCg_Cg-only.bmp");
                }
            }
        }
        
        [Test, Description("show the result of just one plane at a time from an image")]
        public void HSP__separations()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var dst = new Bitmap(bmp))
                {
                    BitmapTools.ArgbImageToHspPlanes(bmp, out var Hp, out var Sp, out var Pp);
                    var zeroP = new double[Hp.Length]; // to zero out other planes
                    var maxP = new double[Hp.Length]; // to zero out other planes
                    for (int i = 0; i < zeroP.Length; i++) { zeroP[i] = 0; maxP[i] = 255; }

                    BitmapTools.HspPlanes_To_ArgbImage(dst, 0, Hp, maxP, maxP);
                    dst.SaveBmp("./outputs/3_HSP_H-only.bmp");
                    
                    BitmapTools.HspPlanes_To_ArgbImage(dst, 0, zeroP, zeroP, Sp);
                    dst.SaveBmp("./outputs/3_HSP_S-only.bmp");
                    
                    BitmapTools.HspPlanes_To_ArgbImage(dst, 0, maxP, zeroP, Pp);
                    dst.SaveBmp("./outputs/3_HSP_P-only.bmp");
                    
                    BitmapTools.HspPlanes_To_ArgbImage(dst, 0, Hp, Sp, Pp);
                    dst.SaveBmp("./outputs/3_HSP_restored.bmp");
                }
            }
        }
        
        [Test, Description("show the result of just one plane at a time from an image")]
        public void RGB__separations()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var dst = new Bitmap(bmp))
                {
                    BitmapTools.ImageToPlanes(bmp, ColorSpace.Native , out var R, out var G, out var B);


                    BitmapTools.PlanesToImage(dst, ColorSpace.Native, 0, R, R, R);
                    dst.SaveBmp("./outputs/3_RGB_R-only.bmp");
                    
                    BitmapTools.PlanesToImage(dst, ColorSpace.Native, 0, G, G, G);
                    dst.SaveBmp("./outputs/3_RGB_G-only.bmp");
                    
                    BitmapTools.PlanesToImage(dst, ColorSpace.Native, 0, B, B, B);
                    dst.SaveBmp("./outputs/3_RGB_B-only.bmp");
                }
            }
        }
        
        [Test, Description("show the result of just one plane at a time from an image")]
        public void Oklab__separations()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var dst = new Bitmap(bmp))
                {
                    BitmapTools.ImageToPlanes(bmp, ColorSpace.sRGB_To_Oklab, out var Lp, out var Ap, out var Bp);
                    
                    var maxL = 0.0;
                    for (int i = 0; i < Lp.Length; i++) { maxL = Math.Max(maxL, Lp[i]); }
                    Console.WriteLine($"Max value of L = {maxL:0.00}");
                    
                    var zeroP = new double[Lp.Length]; // to zero out other planes
                    var maxP = new double[Lp.Length]; // to zero out other planes
                    for (int i = 0; i < zeroP.Length; i++) { zeroP[i] = 0.0; maxP[i] = 0.5; }

                    //BitmapTools.PlanesToImage(dst, ColorSpace.Oklab_To_LinearRGB_Byte, 0, Lp, zeroP, zeroP); // use this to get a representation of the gamma correction
                    BitmapTools.PlanesToImage(dst, ColorSpace.Oklab_To_sRGB, 0, Lp, zeroP, zeroP);
                    dst.SaveBmp("./outputs/3_Oklab_L-only.bmp");
                    
                    BitmapTools.PlanesToImage(dst, ColorSpace.Oklab_To_sRGB, 0, maxP, Ap, zeroP);
                    dst.SaveBmp("./outputs/3_Oklab_A-only.bmp");
                    
                    BitmapTools.PlanesToImage(dst, ColorSpace.Oklab_To_sRGB, 0, maxP, zeroP, Bp);
                    dst.SaveBmp("./outputs/3_Oklab_B-only.bmp");
                    
                    BitmapTools.PlanesToImage(dst, ColorSpace.Oklab_To_sRGB, 0, Lp, Ap, Bp);
                    dst.SaveBmp("./outputs/3_Oklab_restored.bmp");
                }
            }
        }
        
        
        [Test, Description("show the result of just one plane at a time from an image")]
        public void KLA__separations()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var dst = new Bitmap(bmp))
                {
                    BitmapTools.ImageToPlanes(bmp, ColorSpace.RGB_To_KLA, out var Lp, out var Ap, out var Bp);
                    
                    var maxL = 0.0;
                    for (int i = 0; i < Lp.Length; i++) { maxL = Math.Max(maxL, Lp[i]); }
                    Console.WriteLine($"Max value of L = {maxL:0.00}");
                    
                    var zeroP = new double[Lp.Length]; // to zero out other planes
                    var maxP = new double[Lp.Length]; // to zero out other planes
                    for (int i = 0; i < zeroP.Length; i++) { zeroP[i] = 0.0; maxP[i] = 0.5; }

                    BitmapTools.PlanesToImage(dst, ColorSpace.KLA_To_RGB, 0, Lp, zeroP, zeroP); // TODO: inverse transform
                    dst.SaveBmp("./outputs/3_KLA_1-only.bmp");
                    
                    BitmapTools.PlanesToImage(dst, ColorSpace.KLA_To_RGB, 0, maxP, Ap, zeroP);
                    dst.SaveBmp("./outputs/3_KLA_2-only.bmp");
                    
                    BitmapTools.PlanesToImage(dst, ColorSpace.KLA_To_RGB, 0, maxP, zeroP, Bp);
                    dst.SaveBmp("./outputs/3_KLA_3-only.bmp");
                    
                    BitmapTools.PlanesToImage(dst, ColorSpace.KLA_To_RGB, 0, Lp, Ap, Bp);
                    dst.SaveBmp("./outputs/3_KLA_restored.bmp");
                }
            }
        }

        
        [Test, Description("show the result of just one plane at a time from an image")]
        public void YIQ__separations()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var dst = new Bitmap(bmp))
                {
                    BitmapTools.ImageToPlanes(bmp, ColorSpace.RGBToYiq , out var Yp, out var Ip, out var Qp);

                    var zeroP = new double[Yp.Length]; // to zero out other planes
                    for (int i = 0; i < zeroP.Length; i++) { zeroP[i] = 127.5; }

                    BitmapTools.PlanesToImage(dst, ColorSpace.YiqToRGB, 0, Yp, zeroP, zeroP);
                    dst.SaveBmp("./outputs/3_YIQ_Y-only.bmp");
                    
                    BitmapTools.PlanesToImage(dst, ColorSpace.YiqToRGB, 0, zeroP, Ip, zeroP);
                    dst.SaveBmp("./outputs/3_YIQ_I-only.bmp");
                    
                    BitmapTools.PlanesToImage(dst, ColorSpace.YiqToRGB, 0, zeroP, zeroP, Qp);
                    dst.SaveBmp("./outputs/3_YIQ_Q-only.bmp");
                }
            }
        }
        
        [Test, Description("Outputs a sample image showing the color planes")]
        public void YCoCg_Swatch()
        {
            var width = 512;
            var height = 128;

            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                var q_width = width >> 2;
                var dy = 255.0f / height;
                var dx = 255.0f / q_width;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < q_width; x++)
                    {
                        var c = Color.FromArgb((int) ColorSpace.Ycocg_To_RGB32(0, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x, y, c);
                        
                        c = Color.FromArgb((int) ColorSpace.Ycocg_To_RGB32(100, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + q_width, y, c);

                        c = Color.FromArgb((int) ColorSpace.Ycocg_To_RGB32(180, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + (q_width*2), y, c);

                        c = Color.FromArgb((int) ColorSpace.Ycocg_To_RGB32(255, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + (q_width*3), y, c);
                    }
                }

                bmp.SaveBmp("./outputs/YCoCg_Swatch.bmp");
            }
        }
        
        [Test, Description("Outputs a sample image showing the color planes")]
        public void Yiq_Swatch()
        {
            var width = 512;
            var height = 128;

            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                var q_width = width >> 2;
                var dy = 255.0f / height;
                var dx = 255.0f / q_width;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < q_width; x++)
                    {
                        var c = Color.FromArgb((int) ColorSpace.Yiq_To_RGB32(0, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x, y, c);
                        
                        c = Color.FromArgb((int) ColorSpace.Yiq_To_RGB32(100, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + q_width, y, c);

                        c = Color.FromArgb((int) ColorSpace.Yiq_To_RGB32(180, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + (q_width*2), y, c);

                        c = Color.FromArgb((int) ColorSpace.Yiq_To_RGB32(255, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + (q_width*3), y, c);
                    }
                }

                bmp.SaveBmp("./outputs/Yiq_Swatch.bmp");
            }
        }

        [Test, Description("Outputs a sample image showing the color planes")]
        public void YUV_Swatch()
        {
            var width = 256;
            var height = 128;

            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                var halfWidth = width >> 1;
                var dy = 255.0f / height;
                var dx = 255.0f / halfWidth;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < halfWidth; x++)
                    {
                        var c = Color.FromArgb((int) ColorSpace.YUV_To_RGB32(100, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x, y, c);

                        c = Color.FromArgb((int) ColorSpace.YUV_To_RGB32(200, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + halfWidth, y, c);
                    }
                }

                bmp.SaveBmp("./outputs/YUV_Swatch.bmp");
            }
        }
        
        [Test, Description("Outputs a sample image showing the color planes")]
        public void YUV_integer_Swatch()
        {
            var width = 512;
            var height = 128;

            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                var q_width = width >> 2;
                var dy = 255.0f / height;
                var dx = 255.0f / q_width;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < q_width; x++)
                    {
                        var c = Color.FromArgb((int) ColorSpace.YUV888_To_RGB32(0, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x, y, c);
                        
                        c = Color.FromArgb((int) ColorSpace.YUV888_To_RGB32(100, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + q_width, y, c);

                        c = Color.FromArgb((int) ColorSpace.YUV888_To_RGB32(180, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + (q_width*2), y, c);

                        c = Color.FromArgb((int) ColorSpace.YUV888_To_RGB32(255, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + (q_width*3), y, c);
                    }
                }

                bmp.SaveBmp("./outputs/YUV_int_Swatch.bmp");
            }
        }

        
        [Test, Description("Outputs a sample image showing the color planes")]
        public void Ycrcb_Swatch()
        {
            var width = 512;
            var height = 128;

            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                var q_width = width >> 2;
                var dy = 255.0f / height;
                var dx = 255.0f / q_width;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < q_width; x++)
                    {
                        var c = Color.FromArgb((int) ColorSpace.Ycbcr_To_RGB32(0, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x, y, c);
                        
                        c = Color.FromArgb((int) ColorSpace.Ycbcr_To_RGB32(100, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + q_width, y, c);

                        c = Color.FromArgb((int) ColorSpace.Ycbcr_To_RGB32(180, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + (q_width*2), y, c);

                        c = Color.FromArgb((int) ColorSpace.Ycbcr_To_RGB32(255, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + (q_width*3), y, c);
                    }
                }

                bmp.SaveBmp("./outputs/Ycrcb_Swatch.bmp");
            }
        }
        
        [Test, Description("Outputs a sample image showing the color planes")]
        public void HSP_Swatch()
        {
            var width = 512;
            var height = 128;

            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                var q_width = width >> 2;
                var dy = 255.0f / height;
                var dx = 255.0f / q_width;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < q_width; x++)
                    {
                        var c = Color.FromArgb((int) ColorSpace.HSP_To_RGB32((int)(y * dy), (int)(x * dx), 0));
                        bmp.SetPixel(x, y, c);
                        
                        c = Color.FromArgb((int) ColorSpace.HSP_To_RGB32((int)(y * dy), (int)(x * dx), 100));
                        bmp.SetPixel(x + q_width, y, c);

                        c = Color.FromArgb((int) ColorSpace.HSP_To_RGB32((int)(y * dy), (int)(x * dx), 180));
                        bmp.SetPixel(x + (q_width*2), y, c);

                        c = Color.FromArgb((int) ColorSpace.HSP_To_RGB32((int)(y * dy), (int)(x * dx), 255));
                        bmp.SetPixel(x + (q_width*3), y, c);
                    }
                }

                bmp.SaveBmp("./outputs/HSB_Swatch.bmp");
            }
        }

        [Test, Description("Outputs a sample image showing the color planes")]
        public void HCL_Swatch() {
            var width = 512;
            var height = 128;

            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                var q_width = width >> 2;
                var dy = 255.0f / height;
                var dx = 255.0f / q_width;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < q_width; x++)
                    {
                        var c = Color.FromArgb((int) ColorSpace.HCL_To_RGB32((int)(y * dy), (int)(x * dx), 0));
                        bmp.SetPixel(x, y, c);
                        
                        c = Color.FromArgb((int) ColorSpace.HCL_To_RGB32((int)(y * dy), (int)(x * dx), 100));
                        bmp.SetPixel(x + q_width, y, c);

                        c = Color.FromArgb((int) ColorSpace.HCL_To_RGB32((int)(y * dy), (int)(x * dx), 180));
                        bmp.SetPixel(x + (q_width*2), y, c);

                        c = Color.FromArgb((int) ColorSpace.HCL_To_RGB32((int)(y * dy), (int)(x * dx), 255));
                        bmp.SetPixel(x + (q_width*3), y, c);
                    }
                }

                bmp.SaveBmp("./outputs/HCL_Swatch.bmp");
            }
        }
        
        [Test, Description("Outputs a sample image showing the color planes")]
        public void Oklab_Swatch() {
            // https://bottosson.github.io/posts/oklab/
            var width = 512;
            var height = 128;

            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                var q_width = width >> 2;
                var dy = 2.0f / height;
                var dx = 2.0f / q_width;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < q_width; x++)
                    {
                        var c = Color.FromArgb((int) ColorSpace.Oklab_To_RGB32(0.25, (y * dy)-1.0, (x * dx)-1.0));
                        bmp.SetPixel(x, y, c);
                        
                        c = Color.FromArgb((int) ColorSpace.Oklab_To_RGB32(0.5, (y * dy)-1.0, (x * dx)-1.0));
                        bmp.SetPixel(x + q_width, y, c);

                        c = Color.FromArgb((int) ColorSpace.Oklab_To_RGB32(0.75, (y * dy)-1.0, (x * dx)-1.0));
                        bmp.SetPixel(x + (q_width*2), y, c);

                        c = Color.FromArgb((int) ColorSpace.Oklab_To_RGB32(1.0, (y * dy)-1.0, (x * dx)-1.0));
                        bmp.SetPixel(x + (q_width*3), y, c);
                    }
                }

                bmp.SaveBmp("./outputs/Oklab_Swatch.bmp");
            }
        }

        [Test]
        public void Oklab_ColorSpace()
        {
            for (int R_in = 0; R_in < 256; R_in += 64)
            for (int G_in = 0; G_in < 256; G_in += 64)
            for (int B_in = 0; B_in < 256; B_in += 64)
            {
                Console.Write($"R = {R_in}; G = {G_in}; B = {B_in};    ");
                ColorSpace.LinearRGB_To_Oklab(R_in/255.0, G_in/255.0, B_in/255.0, out var X, out var Y, out var Z);

                Console.Write($"X = {X:0.00}; Y = {Y:0.00}; Z = {Z:0.00};    ");
                ColorSpace.Oklab_To_LinearRGB(X, Y, Z, out var R_out, out var G_out, out var B_out);

                R_out*=255.0;
                G_out*=255.0;
                B_out*=255.0;
                Console.WriteLine($"round trip: R = {R_out}; G = {G_out}; B = {B_out};\r\n");

                // range test are weighted to relative visual importance
                Assert.That(R_out, Is.InRange(R_in - 4, R_in + 4), "Red out of range");
                Assert.That(G_out, Is.InRange(G_in - 2, G_in + 2), "Green out of range");
                Assert.That(B_out, Is.InRange(B_in - 6, B_in + 6), "Blue out of range");
            }
        }

        [Test]
        public void Experimental_ColorSpace()
        {
            var rnd = new Random();

            for (int i = 0; i < 100; i++)
            {
                var R_in = rnd.Next(0, 256);
                var G_in = rnd.Next(0, 256);
                var B_in = rnd.Next(0, 256);

                Console.Write($"R = {R_in}; G = {G_in}; B = {B_in};");

                ColorSpace.RGBToExp(R_in, G_in, B_in, out var X, out var Y, out var Z);

                Console.WriteLine($"X = {X}; Y = {Y}; Z = {Z};");
                var c_out = ColorSpace.ExpToRGB32(X, Y, Z);

                ColorSpace.CompoundToComponent(c_out, out _, out var R_out, out var G_out, out var B_out);
                Console.WriteLine($"R = {R_out}; G = {G_out}; B = {B_out};\r\n");

                // range test are weighted to relative visual importance
                Assert.That(R_out, Is.InRange(R_in - 4, R_in + 4), "Red out of range");
                Assert.That(G_out, Is.InRange(G_in - 2, G_in + 2), "Green out of range");
                Assert.That(B_out, Is.InRange(B_in - 6, B_in + 6), "Blue out of range");
            }
        }

        [Test, Description("Outputs a sample image showing the color planes")]
        public void Experimental_Swatch()
        {
            var width = 512;
            var height = 128;

            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                var q_width = width >> 2;
                var dy = 255.0f / height;
                var dx = 255.0f / q_width;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < q_width; x++)
                    {
                        var c = Color.FromArgb((int) ColorSpace.ExpToRGB32(0, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x, y, c);
                        
                        c = Color.FromArgb((int) ColorSpace.ExpToRGB32(100, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + q_width, y, c);

                        c = Color.FromArgb((int) ColorSpace.ExpToRGB32(180, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + (q_width*2), y, c);

                        c = Color.FromArgb((int) ColorSpace.ExpToRGB32(255, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + (q_width*3), y, c);
                    }
                }

                bmp.SaveBmp("./outputs/EXP_Swatch.bmp");
            }
        }
        
        [Test, Description("show the result of just one plane at a time from an image")]
        public void Experimental__separations()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var dst = new Bitmap(bmp))
                {
                    BitmapTools.ImageToPlanes(bmp, ColorSpace.RGBToExp, out var X, out var Y, out var Z);
                    var zeroP = new double[X.Length]; // to zero out other planes
                    for (int i = 0; i < zeroP.Length; i++) { zeroP[i] = 127.5; }

                    BitmapTools.PlanesToImage(dst, ColorSpace.ExpToRGB,0, X, zeroP, zeroP);
                    dst.SaveBmp("./outputs/3_EXP_X.bmp");
                    
                    BitmapTools.PlanesToImage(dst, ColorSpace.ExpToRGB,0, zeroP, Y, zeroP);
                    dst.SaveBmp("./outputs/3_EXP_Y.bmp");
                    
                    BitmapTools.PlanesToImage(dst, ColorSpace.ExpToRGB,0, zeroP, zeroP, Z);
                    dst.SaveBmp("./outputs/3_EXP_Z.bmp");
                    
                    BitmapTools.PlanesToImage(dst, ColorSpace.ExpToRGB,0, X, Y, Z);
                    dst.SaveBmp("./outputs/3_EXP_Full.bmp");
                }
            }
        }

        [Test, Description("Invert brightness without changing colour")]
        public void invert_brightness_but_not_colour__img3 () {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var dst = new Bitmap(bmp)) {
                    BitmapTools.ImageToPlanes(bmp, ColorSpace.RGB_To_HSP, out var Hue, out var Sat, out var Light);

                    for (int i = 0; i < Light.Length; i++) { Light[i] = 255 - Light[i]; }

                    BitmapTools.PlanesToImage(dst, ColorSpace.HSP_To_RGB,0, Hue, Sat, Light);
                    dst.SaveBmp("./outputs/3_Invert_HSP.bmp");
                }
                
                using (var dst = new Bitmap(bmp)) {
                    BitmapTools.ImageToPlanes(bmp, ColorSpace.RGBToYCoCg, out var Y, out var Co, out var Cg);

                    for (int i = 0; i < Y.Length; i++) { Y[i] = 255 - Y[i]; }

                    BitmapTools.PlanesToImage(dst, ColorSpace.YCoCgToRGB,0, Y, Co, Cg);
                    dst.SaveBmp("./outputs/3_Invert_YCoCg.bmp");
                }
                
                using (var dst = new Bitmap(bmp)) {
                    BitmapTools.ImageToPlanes(bmp, ColorSpace.RGBToYiq, out var Y, out var I, out var Q);

                    for (int i = 0; i < Y.Length; i++) { Y[i] = 255 - Y[i]; }

                    BitmapTools.PlanesToImage(dst, ColorSpace.YiqToRGB,0, Y, I, Q);
                    dst.SaveBmp("./outputs/3_Invert_Yiq.bmp");
                }
            }
        }
        
        [Test, Description("Invert brightness without changing colour")]
        public void invert_brightness_but_not_colour__img4 () {
            using (var bmp = Load.FromFile("./inputs/4.png"))
            {
                using (var dst = new Bitmap(bmp)) {
                    BitmapTools.ImageToPlanes(bmp, ColorSpace.RGB_To_HSP, out var Hue, out var Sat, out var Light);

                    for (int i = 0; i < Light.Length; i++) { Light[i] = 255 - Light[i]; }

                    BitmapTools.PlanesToImage(dst, ColorSpace.HSP_To_RGB,0, Hue, Sat, Light);
                    dst.SaveBmp("./outputs/4_Invert_HSP.bmp");
                }
                
                using (var dst = new Bitmap(bmp)) {
                    BitmapTools.ImageToPlanes(bmp, ColorSpace.RGBToYCoCg, out var Y, out var Co, out var Cg);

                    for (int i = 0; i < Y.Length; i++) { Y[i] = 255 - Y[i]; }

                    BitmapTools.PlanesToImage(dst, ColorSpace.YCoCgToRGB,0, Y, Co, Cg);
                    dst.SaveBmp("./outputs/4_Invert_YCoCg.bmp");
                }
                
                using (var dst = new Bitmap(bmp)) {
                    BitmapTools.ImageToPlanes(bmp, ColorSpace.RGBToYiq, out var Y, out var I, out var Q);

                    for (int i = 0; i < Y.Length; i++) { Y[i] = 255 - Y[i]; }

                    BitmapTools.PlanesToImage(dst, ColorSpace.YiqToRGB,0, Y, I, Q);
                    dst.SaveBmp("./outputs/4_Invert_Yiq.bmp");
                }
            }
        }

        [Test]
        public void rgb_to_linear_round_trip()
        {
            double sR = 0.4, sG = 0.1, sB = 0.9;
            
            var linear = ColorSpace.RgbToLinear(sR, sG, sB);
            Assert.That(linear.R, Is.Not.EqualTo(sR), "Red did not change");
            Assert.That(linear.G, Is.Not.EqualTo(sG), "Green did not change");
            Assert.That(linear.B, Is.Not.EqualTo(sB), "Blue did not change");
            
            var logarithmic = ColorSpace.LinearToRgb(linear.R, linear.G, linear.B);
            Assert.That(Math.Round(logarithmic.R, 5), Is.EqualTo(sR), "Red did not survive round trip");
            Assert.That(Math.Round(logarithmic.G, 5), Is.EqualTo(sG), "Green did not survive round trip");
            Assert.That(Math.Round(logarithmic.B, 5), Is.EqualTo(sB), "Blue did not survive round trip");
        }
        
        [Test]
        public void rgb_to_from_linear_zero_point()
        {
            double sR = 0.0, sG = 0.0, sB = 0.0;
            
            var linear = ColorSpace.RgbToLinear(sR, sG, sB);
            Assert.That(linear.R, Is.Zero, "Red linear zero");
            Assert.That(linear.G, Is.Zero, "Green linear zero");
            Assert.That(linear.B, Is.Zero, "Blue linear zero");
            
            var logarithmic = ColorSpace.LinearToRgb(sR, sG, sB);
            Assert.That(logarithmic.R, Is.Zero, "Red log zero");
            Assert.That(logarithmic.G, Is.Zero, "Green log zero");
            Assert.That(logarithmic.B, Is.Zero, "Blue log zero");
        }
        
        [Test]
        public void rgb_to_from_linear_white_point()
        {
            double sR = 1.0, sG = 1.0, sB = 1.0;
            
            var linear = ColorSpace.RgbToLinear(sR, sG, sB);
            Assert.That(linear.R, Is.EqualTo(1.0), "Red linear one");
            Assert.That(linear.G, Is.EqualTo(1.0), "Green linear one");
            Assert.That(linear.B, Is.EqualTo(1.0), "Blue linear one");
            
            var logarithmic = ColorSpace.LinearToRgb(sR, sG, sB);
            Assert.That(logarithmic.R, Is.EqualTo(1.0), "Red log one");
            Assert.That(logarithmic.G, Is.EqualTo(1.0), "Green log one");
            Assert.That(logarithmic.B, Is.EqualTo(1.0), "Blue log one");
        }
    }
}