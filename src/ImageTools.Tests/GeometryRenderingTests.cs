﻿using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ImageTools.DistanceFields;
using ImageTools.GeneralTypes;
using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class GeometryRenderingTests
    {
        [Test]
        public void render_shapes_with_rectilinear_sdf()
        {
            var sw = new Stopwatch();
            using (var bmp = new Bitmap(512,512, PixelFormat.Format32bppArgb))
            {
                var byteImage = ByteImage.FromBitmap(bmp);

                sw.Start();
                SdfLimsDraw.DrawRectangle(byteImage, color: 0xffEEDDCC, 100, 80, 75, 30, 15);
                SdfLimsDraw.DrawOval(byteImage, color: 0xffAA5577, x1: 50, y1: 300, x2: 500, y2: 400);
                //SdfDraw.DrawLine(byteImage, thickness:3.0, x1: 25, y1: 50, x2: 150, y2: 500, color: 0xffFFffFF);
                sw.Stop();
                
                byteImage!.RenderOnBitmap(bmp);
                bmp.SaveBmp("./outputs/draw-shapes-sdf-lims.bmp");
            }
            Assert.That(Load.FileExists("./outputs/draw-shapes-sdf-lims.bmp"));
            Console.WriteLine($"Core draw took {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
        }

        [Test]
        public void render_anti_aliased_lines_with_scanline()
        {
            var sw = new Stopwatch();
            ScanlineDraw.SetGamma(2.2);
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
        
        [Test] // I don't have a scan-line version of this
        public void render_variable_width_lines_with_sdf()
        {
            var sw = new Stopwatch();
            using (var bmp = new Bitmap(512,512, PixelFormat.Format32bppArgb))
            {
                var byteImage = ByteImage.FromBitmap(bmp);

                sw.Start();
                SdfDraw.DrawPressureCurve(byteImage, color: 0xffFF00AA, new[]{
                    new Vector3( 50,  50, 25),
                    new Vector3(500,  10,  5),
                    new Vector3(200, 500, 10),
                    new Vector3( 20, 200, 15),
                    new Vector3(500, 500,  1)
                });
                sw.Stop();
                
                byteImage!.RenderOnBitmap(bmp);
                bmp.SaveBmp("./outputs/draw-variable-line-sdf.bmp");
            }
            Assert.That(Load.FileExists("./outputs/draw-variable-line-sdf.bmp"));
            Console.WriteLine($"Core draw took {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
        }
        
        [Test]
        public void fill_polygon_with_scanline()
        {
            var polygon1 = Points(45.6f,  10,  10,/**/3,0,  5,6,   0,2,     6,2,  1,6); // 5 point star
            var polygon2 = Points(40.2f, 200, 200,/**/3,0,  5,6,   0,2,     6,2,  1,6); // 5 point star with slightly different scale
            var polygon3 = Points(22.5f, 400, 200,/**/3,0,  5,6,   0,2,     6,2,  1,6); // this one runs off the edge to check clipping
            var polygon4 = Points(22.5f, 350, 100,/**/0,1,  3,0,   4,3,     1,4);       // this one has multiple small gradients to check anti-aliasing
            var polygon5 = Points(1f,     10, 250,/**/0,1,  1,0, 199,200, 200,199);     // A long thin poly with a cross-over to show drop-out behaviour
            
            ScanlineDraw.SetGamma(2.2);
            var sw = new Stopwatch();
            using (var bmp = new Bitmap(512,512, PixelFormat.Format32bppArgb))
            {
                var byteImage = ByteImage.FromBitmap(bmp);

                sw.Start();
                ScanlineDraw.FillPolygon(byteImage, polygon1, color: 0xffEEDDCC, FillMode.Alternate);
                ScanlineDraw.FillPolygon(byteImage, polygon2, color: 0xffAA5577, FillMode.Winding);
                ScanlineDraw.FillPolygon(byteImage, polygon3, color: 0xff55AAFF, FillMode.Winding);
                ScanlineDraw.FillPolygon(byteImage, polygon4, color: 0xffFFffFF, FillMode.Alternate); // this shows the AA problem with this scanline strategy
                ScanlineDraw.FillPolygon(byteImage, polygon5, color: 0xffAAffAA, FillMode.Alternate); // there is a rounding fault here
                sw.Stop();
                
                byteImage!.RenderOnBitmap(bmp);
                bmp.SaveBmp("./outputs/draw-poly-scan.bmp");
            }
            Assert.That(Load.FileExists("./outputs/draw-poly-scan.bmp"));
            Console.WriteLine($"Core draw took {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
        }

        [Test]
        public void fill_polygon_with_sdf()
        {
            var polygon1 = Points(45.6f,  10,  10,/**/3,0,  5,6,   0,2,     6,2,  1,6); // 5 point star
            var polygon2 = Points(40.2f, 200, 200,/**/3,0,  5,6,   0,2,     6,2,  1,6); // 5 point star with slightly different scale
            var polygon3 = Points(22.5f, 400, 200,/**/3,0,  5,6,   0,2,     6,2,  1,6); // this one runs off the edge to check clipping
            var polygon4 = Points(22.5f, 350, 100,/**/0,1,  3,0,   4,3,     1,4);       // this one has multiple small gradients to check anti-aliasing
            var polygon5 = Points(1f,     10, 250,/**/0,1,  1,0, 199,200, 200,199);     // A long thin poly with a cross-over to show drop-out behaviour
            
            var sw = new Stopwatch();
            using (var bmp = new Bitmap(512,512, PixelFormat.Format32bppArgb))
            {
                var byteImage = ByteImage.FromBitmap(bmp);

                sw.Start();
                SdfDraw.FillPolygon(byteImage, polygon1, color: 0xffEEDDCC, FillMode.Alternate);
                SdfDraw.FillPolygon(byteImage, polygon2, color: 0xffAA5577, FillMode.Winding);
                SdfDraw.FillPolygon(byteImage, polygon3, color: 0xff55AAFF, FillMode.Winding);
                SdfDraw.FillPolygon(byteImage, polygon4, color: 0xffFFffFF, FillMode.Alternate);
                SdfDraw.FillPolygon(byteImage, polygon5, color: 0xffAAffAA, FillMode.Alternate);
                sw.Stop();
                
                byteImage!.RenderOnBitmap(bmp);
                bmp.SaveBmp("./outputs/draw-poly-sdf.bmp");
            }
            Assert.That(Load.FileExists("./outputs/draw-poly-sdf.bmp"));
            Console.WriteLine($"Core draw took {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
        }

        // Check that multiple contours can work with the polygon fill (i.e. support holes)
        [Test]
        public void fill_complex_polygon_with_sdf()
        {
            var polygon1 = Points(22.5,  10.2, 10.8,/**/0,0,  10,0,   10,10,   0,10);         // outer box
            var polygon2 = Points(22.5,  20.5, 20.5,/**/2,2,  0,2,  1.5,3.5,  1,6,  3,4.5,  5,6, 4.5,3.5,  6,2,  4,2,  3,0); // CCW 5 point star with no crossings
            var polygon3 = ReversePoly(OffsetPoly(polygon2, 30.0,40.0));
            
            var polygon4 = Points(22.5,  250.2, 250.8,/**/0,0,  10,0,   10,10,   0,10);         // outer box
            var polygon5 = Points(22.5,  260.5, 260.5,/**/2,2,  0,2,  1.5,3.5,  1,6,  3,4.5,  5,6, 4.5,3.5,  6,2,  4,2,  3,0); // CCW 5 point star with no crossings
            var polygon6 = ReversePoly(OffsetPoly(polygon5, 30.0,40.0));
            
            var contour1 = Contour.Combine(polygon1, polygon2, polygon3);
            var contour2 = Contour.Combine(polygon4, polygon5, polygon6);
            
            var sw = new Stopwatch();
            using (var bmp = new Bitmap(512,512, PixelFormat.Format32bppArgb))
            {
                var byteImage = ByteImage.FromBitmap(bmp);

                sw.Start();
                SdfDraw.FillPolygon(byteImage, contour1, color: 0xffEEDDCC, FillMode.Alternate);
                SdfDraw.FillPolygon(byteImage, contour2, color: 0xffCCDDEE, FillMode.Winding);
                sw.Stop();
                
                byteImage!.RenderOnBitmap(bmp);
                bmp.SaveBmp("./outputs/draw-poly-hole-sdf.bmp");
            }
            Assert.That(Load.FileExists("./outputs/draw-poly-hole-sdf.bmp"));
            Console.WriteLine($"Core draw took {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
        }

        // Check that multiple contours can work with the polygon fill (i.e. support holes)
        [Test]
        public void fill_complex_polygon_with_scanline()
        {
            var polygon1 = Points(22.5,  10.2, 10.8,/**/0,0,  10,0,   10,10,   0,10);         // outer box
            var polygon2 = Points(22.5,  20.5, 20.5,/**/2,2,  0,2,  1.5,3.5,  1,6,  3,4.5,  5,6, 4.5,3.5,  6,2,  4,2,  3,0); // CCW 5 point star with no crossings
            var polygon3 = ReversePoly(OffsetPoly(polygon2, 30.0,40.0));
            
            var polygon4 = Points(22.5,  250.2, 250.8,/**/0,0,  10,0,   10,10,   0,10);         // outer box
            var polygon5 = Points(22.5,  260.5, 260.5,/**/2,2,  0,2,  1.5,3.5,  1,6,  3,4.5,  5,6, 4.5,3.5,  6,2,  4,2,  3,0); // CCW 5 point star with no crossings
            var polygon6 = ReversePoly(OffsetPoly(polygon5, 30.0,40.0));
            
            var contour1 = Contour.Combine(polygon1, polygon2, polygon3);
            var contour2 = Contour.Combine(polygon4, polygon5, polygon6);
            
            var sw = new Stopwatch();
            using (var bmp = new Bitmap(512,512, PixelFormat.Format32bppArgb))
            {
                var byteImage = ByteImage.FromBitmap(bmp);

                sw.Start();
                ScanlineDraw.FillPolygon(byteImage, contour1, color: 0xffEEDDCC, FillMode.Alternate);
                ScanlineDraw.FillPolygon(byteImage, contour2, color: 0xffCCDDEE, FillMode.Winding);
                sw.Stop();
                
                byteImage!.RenderOnBitmap(bmp);
                bmp.SaveBmp("./outputs/draw-poly-hole-scan.bmp");
            }
            Assert.That(Load.FileExists("./outputs/draw-poly-hole-scan.bmp"));
            Console.WriteLine($"Core draw took {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
        }

        [Test] // TODO: check the exactly 180 deg case
        public void pie_slices_with_sdf()
        {
            var sw = new Stopwatch();
            using (var bmp = new Bitmap(512,512, PixelFormat.Format32bppArgb))
            {
                var byteImage = ByteImage.FromBitmap(bmp);

                sw.Start();
                for (int j = 0; j < 8; j++)
                {
                    var yo = j * 50;
                    var c = j * 40.0;
                    for (int i = 0; i < 8; i++)
                    {
                        var xo = i * 50;
                        var a = i * 40.0;
                        SdfDraw.FillPartialRing(byteImage, color: 0xffFFffFF,
                            x1: xo + 10.0, y1: yo+10.0, x2: xo + 50.0, y2: yo+50.0,
                            startAngle: a, clockwiseAngle: c, thickness: 2.0);
                    }
                }
                sw.Stop();
                
                byteImage!.RenderOnBitmap(bmp);
                bmp.SaveBmp("./outputs/draw-pie-sdf.bmp");
            }
            Assert.That(Load.FileExists("./outputs/draw-pie-sdf.bmp"));
            Console.WriteLine($"Core draw took {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
        }

        private PointF[] Points(double scale, double dx, double dy, params double[] p)
        {
            var result = new List<PointF>();
            for (int i = 0; i < p!.Length - 1; i+=2)
            {
                result.Add(new PointF((float)(p[i]*scale + dx), (float)(p[i+1]*scale + dy)));
            }
            return result.ToArray();
        }
        
        private PointF[] OffsetPoly(PointF[] p, double dx, double dy)
        {
            var result = new List<PointF>();
            
            for (int i = 0; i < p!.Length; i++)
            {
                result.Add(new PointF((float)(p[i].X + dx), (float)(p[i].Y + dy)));
            }
            
            return result.ToArray();
        }
        
        /// <summary>
        /// reverse winding order
        /// </summary>
        private PointF[] ReversePoly(PointF[] p)
        {
            var result = new List<PointF>();
            
            for (int i = p!.Length - 1; i >= 0; i--)
            {
                result.Add(new PointF(p[i].X, p[i].Y));
            }
            
            return result.ToArray();
        }
    }
}