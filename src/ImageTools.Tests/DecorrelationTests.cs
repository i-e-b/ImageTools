using System;
using System.Drawing;
using ImageTools.ImageDataFormats;
using ImageTools.Utilities;
using NUnit.Framework;
// ReSharper disable InconsistentNaming

namespace ImageTools.Tests
{
    [TestFixture]
    public class DecorrelationTests
    {
        // TODO: restoration of original image
        
        [Test]
        public void attempt_to_reduce_entropy_by_neighbors__direct_linear__YUV_space ()
        {
            using var bmp = Load.FromFile("./inputs/3.png");
            using var dst = new Bitmap(bmp!);
            
            BitmapTools.ImageToPlanes(bmp, ColorSpace.RGBToYUV, out var Y, out var U, out var V);

            // Really simple reduction, wraps left-right
            var Y2 = Decorrelation.LinearPredict(Y);
            var U2 = Decorrelation.LinearPredict(U);
            var V2 = Decorrelation.LinearPredict(V);

            // save changed image
            BitmapTools.PlanesToImage(dst, ColorSpace.YUVToRGB, 0, Y2, U2, V2);
            dst.SaveBmp("./outputs/3_reduced_linear_YUV.bmp");

            Assert.That(Load.FileExists("./outputs/3_reduced_linear_YUV.bmp"));
        }
        
        [Test]
        public void attempt_to_reduce_entropy_by_neighbors__direct_linear__RGB_space ()
        {
            using var bmp = Load.FromFile("./inputs/3.png");
            using var dst = new Bitmap(bmp!);
            
            BitmapTools.ImageToPlanes(bmp, ColorSpace.Native, out var R, out var G, out var B);

            // Really simple reduction, wraps left-right
            var R2 = Decorrelation.LinearPredict(R);
            var G2 = Decorrelation.LinearPredict(G);
            var B2 = Decorrelation.LinearPredict(B);

            // save changed image
            BitmapTools.PlanesToImage(dst, ColorSpace.Native, 0, R2, G2, B2);
            dst.SaveBmp("./outputs/3_reduced_linear_RGB.bmp");

            Assert.That(Load.FileExists("./outputs/3_reduced_linear_RGB.bmp"));
        }
        
        [Test]
        public void attempt_to_reduce_entropy_by_neighbors__jpeg_LOCO_I__YUV_space ()
        {
            using var bmp = Load.FromFile("./inputs/3.png");
            using var dst = new Bitmap(bmp!);
            
            BitmapTools.ImageToPlanes(bmp, ColorSpace.RGBToYUV, out var Y, out var U, out var V);

            // Lossless JPEG type prediction
            var Y2 = Decorrelation.LocoI_Predict(Y, bmp.Width, bmp.Height, out var numZeros);
            var U2 = Decorrelation.LocoI_Predict(U, bmp.Width, bmp.Height, out _);
            var V2 = Decorrelation.LocoI_Predict(V, bmp.Width, bmp.Height, out _);
                    
            var pc = Y!.Length / (double)numZeros;
            Console.WriteLine($"{numZeros} zeros ({pc:0.00}%)");

            // save changed image
            BitmapTools.PlanesToImage(dst, ColorSpace.YUVToRGB, 0, Y2, U2, V2);
            dst.SaveBmp("./outputs/3_reduced_loco_i_YUV.bmp");

            Assert.That(Load.FileExists("./outputs/3_reduced_loco_i_YUV.bmp"));
        }
        
        [Test]
        public void attempt_to_reduce_entropy_by_neighbors__jpeg_LOCO_I__RGB_space ()
        {
            using var bmp = Load.FromFile("./inputs/3.png");
            using var dst = new Bitmap(bmp!);
            
            BitmapTools.ImageToPlanes(bmp, ColorSpace.Native, out var R, out var G, out var B);

            // Lossless JPEG type prediction
            var R2 = Decorrelation.LocoI_Predict(R, bmp.Width, bmp.Height, out var numZeros);
            var G2 = Decorrelation.LocoI_Predict(G, bmp.Width, bmp.Height, out _);
            var B2 = Decorrelation.LocoI_Predict(B, bmp.Width, bmp.Height, out _);
                    
            var pc = R!.Length / (double)numZeros;
            Console.WriteLine($"{numZeros} zeros ({pc:0.00}%)");

            // save changed image
            BitmapTools.PlanesToImage(dst, ColorSpace.Native, 0, R2, G2, B2);
            dst.SaveBmp("./outputs/3_reduced_loco_i_RGB.bmp");

            Assert.That(Load.FileExists("./outputs/3_reduced_loco_i_RGB.bmp"));
        }

    }
}