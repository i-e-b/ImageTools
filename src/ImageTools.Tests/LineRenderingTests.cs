using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using ImageTools.DistanceFields;
using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class LineRenderingTests
    {
        [Test]
        public void rendering_single_anti_aliased_line()
        {
            var sw = new Stopwatch();
            using (var bmp = new Bitmap(512,512, PixelFormat.Format32bppArgb))
            {
                var byteImage = ByteImage.FromBitmap(bmp);
                
                sw.Start();
                ScanlineDraw.LineOnBitmap(byteImage, x1: 25, y1: 50, x2: 500, y2: 460, color: 0xffAA5577);
                ScanlineDraw.LineOnBitmap(byteImage, x1: 25, y1: 50, x2: 500, y2: 220, color: 0xffEEDDCC);
                ScanlineDraw.LineOnBitmap(byteImage, x1: 25, y1: 50, x2: 150, y2: 500, color: 0xffFFffFF);
                sw.Stop();
                
                byteImage!.RenderOnBitmap(bmp);
                bmp.SaveBmp("./outputs/draw-aa-single.bmp");
            }
            Assert.That(Load.FileExists("./outputs/draw-aa-single.bmp"));
            Console.WriteLine($"Core draw took {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
        }

        [Test]
        public void render_a_single_anti_aliased_line_with_thickness()
        {
            var sw = new Stopwatch();
            using (var bmp = new Bitmap(512,512, PixelFormat.Format32bppArgb))
            {
                var byteImage = ByteImage.FromBitmap(bmp);

                sw.Start();
                SdfDraw.LineOnBitmap(byteImage, thickness:0.1, x1: 25, y1: 50, x2: 500, y2: 220, color: 0xffEEDDCC);
                SdfDraw.LineOnBitmap(byteImage, thickness:1.0, x1: 25, y1: 50, x2: 500, y2: 460, color: 0xffAA5577);
                SdfDraw.LineOnBitmap(byteImage, thickness:3.0, x1: 25, y1: 50, x2: 150, y2: 500, color: 0xffFFffFF);
                sw.Stop();
                
                byteImage!.RenderOnBitmap(bmp);
                bmp.SaveBmp("./outputs/draw-aa-thick.bmp");
            }
            Assert.That(Load.FileExists("./outputs/draw-aa-thick.bmp"));
            Console.WriteLine($"Core draw took {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
        }
    }
}