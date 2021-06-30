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
                ScanlineDraw.SetGamma(2.2);
                
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
        public void fill_polygon_with_scanline()
        {
            var polygon1 = Points(45.6f,  10,  10,/**/3,0,  5,6,  0,2,  6,2,  1,6);
            var polygon2 = Points(45.6f, 200, 200,/**/3,0,  5,6,  0,2,  6,2,  1,6);
            var polygon3 = Points(22.5f, 400, 200,/**/3,0,  5,6,  0,2,  6,2,  1,6); // this one run off the edge to check clipping
            
            var sw = new Stopwatch();
            using (var bmp = new Bitmap(512,512, PixelFormat.Format32bppArgb))
            {
                var byteImage = ByteImage.FromBitmap(bmp);

                sw.Start();
                ScanlineDraw.FillPolygon(byteImage, polygon1, color: 0xffEEDDCC, FillMode.Alternate);
                ScanlineDraw.FillPolygon(byteImage, polygon2, color: 0xffAA5577, FillMode.Winding);
                ScanlineDraw.FillPolygon(byteImage, polygon3, color: 0xff55AAFF, FillMode.Winding);
                sw.Stop();
                
                byteImage!.RenderOnBitmap(bmp);
                bmp.SaveBmp("./outputs/draw-poly-scan.bmp");
            }
            Assert.That(Load.FileExists("./outputs/draw-poly-scan.bmp"));
            Console.WriteLine($"Core draw took {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
        }

        [Test]
        public void fill_anti_aliased_polygon_with_sdf()
        {
            var polygon1 = Points(45.6f,  10,  10,/**/3,0,  5,6,  0,2,  6,2,  1,6);
            var polygon2 = Points(45.6f, 200, 200,/**/3,0,  5,6,  0,2,  6,2,  1,6);
            var polygon3 = Points(22.5f, 400, 200,/**/3,0,  5,6,  0,2,  6,2,  1,6); // this one run off the edge to check clipping
            
            var sw = new Stopwatch();
            using (var bmp = new Bitmap(512,512, PixelFormat.Format32bppArgb))
            {
                var byteImage = ByteImage.FromBitmap(bmp);

                sw.Start();
                SdfDraw.FillPolygon(byteImage, polygon1, color: 0xffEEDDCC, FillMode.Alternate);
                SdfDraw.FillPolygon(byteImage, polygon2, color: 0xffAA5577, FillMode.Winding);
                SdfDraw.FillPolygon(byteImage, polygon3, color: 0xff55AAFF, FillMode.Winding);
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