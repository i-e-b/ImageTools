using System;
using System.Drawing;
using System.Drawing.Imaging;
using NUnit.Framework;

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

            var ycgcb = ColorSpace.RGB32_To_Ycbcr32(c_in);
            var c_out = ColorSpace.Ycbcr32_To_RGB32(ycgcb);

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

            var ycgcb = ColorSpace.RGB32_To_Ycocg32(c_in);
            var c_out = ColorSpace.Ycocg32_To_RGB32(ycgcb);
            // https://en.wikipedia.org/wiki/YCoCg

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

        [Test, Description("Outputs a sample image showing the color planes")]
        public void YCoCg_Swatch()
        {
            var width = 512;
            var height = 128;

            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                var qwidth = width >> 2;
                var dy = 255.0f / height;
                var dx = 255.0f / qwidth;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < qwidth; x++)
                    {
                        var c = Color.FromArgb((int) ColorSpace.Ycocg_To_RGB32(0, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x, y, c);
                        
                        c = Color.FromArgb((int) ColorSpace.Ycocg_To_RGB32(100, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + qwidth, y, c);

                        c = Color.FromArgb((int) ColorSpace.Ycocg_To_RGB32(180, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + (qwidth*2), y, c);

                        c = Color.FromArgb((int) ColorSpace.Ycocg_To_RGB32(255, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + (qwidth*3), y, c);
                    }
                }

                bmp.SaveBmp("./outputs/YCoCg_Swatch.bmp");
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
        public void Ycrcb_Swatch()
        {
            var width = 512;
            var height = 128;

            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                var qwidth = width >> 2;
                var dy = 255.0f / height;
                var dx = 255.0f / qwidth;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < qwidth; x++)
                    {
                        var c = Color.FromArgb((int) ColorSpace.Ycbcr_To_RGB32(0, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x, y, c);
                        
                        c = Color.FromArgb((int) ColorSpace.Ycbcr_To_RGB32(100, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + qwidth, y, c);

                        c = Color.FromArgb((int) ColorSpace.Ycbcr_To_RGB32(180, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + (qwidth*2), y, c);

                        c = Color.FromArgb((int) ColorSpace.Ycbcr_To_RGB32(255, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + (qwidth*3), y, c);
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
                var qwidth = width >> 2;
                var dy = 255.0f / height;
                var dx = 255.0f / qwidth;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < qwidth; x++)
                    {
                        var c = Color.FromArgb((int) ColorSpace.HSP_To_RGB32((int)(y * dy), (int)(x * dx), 0));
                        bmp.SetPixel(x, y, c);
                        
                        c = Color.FromArgb((int) ColorSpace.HSP_To_RGB32((int)(y * dy), (int)(x * dx), 100));
                        bmp.SetPixel(x + qwidth, y, c);

                        c = Color.FromArgb((int) ColorSpace.HSP_To_RGB32((int)(y * dy), (int)(x * dx), 180));
                        bmp.SetPixel(x + (qwidth*2), y, c);

                        c = Color.FromArgb((int) ColorSpace.HSP_To_RGB32((int)(y * dy), (int)(x * dx), 255));
                        bmp.SetPixel(x + (qwidth*3), y, c);
                    }
                }

                bmp.SaveBmp("./outputs/HSB_Swatch.bmp");
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
                var qwidth = width >> 2;
                var dy = 255.0f / height;
                var dx = 255.0f / qwidth;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < qwidth; x++)
                    {
                        var c = Color.FromArgb((int) ColorSpace.ExpToRGB32(0, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x, y, c);
                        
                        c = Color.FromArgb((int) ColorSpace.ExpToRGB32(100, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + qwidth, y, c);

                        c = Color.FromArgb((int) ColorSpace.ExpToRGB32(180, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + (qwidth*2), y, c);

                        c = Color.FromArgb((int) ColorSpace.ExpToRGB32(255, (int)(y * dy), (int)(x * dx)));
                        bmp.SetPixel(x + (qwidth*3), y, c);
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
    }
}