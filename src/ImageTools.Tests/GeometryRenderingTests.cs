using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ImageTools.DistanceFields;
using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class GeometryRenderingTests
    {
        [Test]
        public void render_anti_aliased_lines_with_scanline()
        {
            var sw = new Stopwatch();
            using (var bmp = new Bitmap(512,512, PixelFormat.Format32bppArgb))
            {
                var byteImage = ByteImage.FromBitmap(bmp);
                
                sw.Start();
                ScanlineDraw.DrawLine(byteImage, x1: 25, y1: 50, x2: 500, y2: 460, color: 0xffAA5577);
                ScanlineDraw.DrawLine(byteImage, x1: 25, y1: 50, x2: 500, y2: 220, color: 0xffEEDDCC);
                ScanlineDraw.DrawLine(byteImage, x1: 25, y1: 50, x2: 150, y2: 500, color: 0xffFFffFF);
                sw.Stop();
                
                byteImage!.RenderOnBitmap(bmp);
                bmp.SaveBmp("./outputs/draw-line-scan.bmp");
            }
            Assert.That(Load.FileExists("./outputs/draw-line-scan.bmp"));
            Console.WriteLine($"Core draw took {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
        }

        [Test]
        public void render_anti_aliased_lines_with_sdf()
        {
            var sw = new Stopwatch();
            using (var bmp = new Bitmap(512,512, PixelFormat.Format32bppArgb))
            {
                var byteImage = ByteImage.FromBitmap(bmp);

                sw.Start();
                SdfDraw.DrawLine(byteImage, thickness:0.1, x1: 25, y1: 50, x2: 500, y2: 220, color: 0xffEEDDCC);
                SdfDraw.DrawLine(byteImage, thickness:1.0, x1: 25, y1: 50, x2: 500, y2: 460, color: 0xffAA5577);
                SdfDraw.DrawLine(byteImage, thickness:3.0, x1: 25, y1: 50, x2: 150, y2: 500, color: 0xffFFffFF);
                sw.Stop();
                
                byteImage!.RenderOnBitmap(bmp);
                bmp.SaveBmp("./outputs/draw-line-sdf.bmp");
            }
            Assert.That(Load.FileExists("./outputs/draw-line-sdf.bmp"));
            Console.WriteLine($"Core draw took {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
        }

        [Test]
        public void fill_anti_aliased_polygon_with_sdf()
        {
            var polygon1 = Points(53.2f,  10,  10,/**/0,0,  3,2,  2,3,  2,0,  0,2);
            var polygon2 = Points(55.8f, 200, 200,/**/0,0,  3,2,  2,3,  2,0,  0,2);
            
            
            var sw = new Stopwatch();
            using (var bmp = new Bitmap(512,512, PixelFormat.Format32bppArgb))
            {
                var byteImage = ByteImage.FromBitmap(bmp);

                sw.Start();
                SdfDraw.FillPolygon(byteImage, polygon1, color: 0xffEEDDCC, FillMode.Alternate);
                SdfDraw.FillPolygon(byteImage, polygon2, color: 0xffAA5577, FillMode.Winding);
                sw.Stop();
                
                byteImage!.RenderOnBitmap(bmp);
                bmp.SaveBmp("./outputs/draw-poly-sdf.bmp");
            }
            Assert.That(Load.FileExists("./outputs/draw-poly-sdf.bmp"));
            Console.WriteLine($"Core draw took {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
        }

        private PointF[] Points(float scale, float dx, float dy, params int[] p)
        {
            var result = new List<PointF>();
            for (int i = 0; i < p!.Length - 1; i+=2)
            {
                result.Add(new PointF(p[i]*scale + dx, p[i+1]*scale + dy));
            }
            return result.ToArray();
        }
    }
}