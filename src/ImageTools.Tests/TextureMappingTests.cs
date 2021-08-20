using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using ImageTools.TextureRendering;
using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class TextureMappingTests
    {
        [Test]
        public void mapping_a_textured_triangle()
        {
            var sw = new Stopwatch();
            //using var textureBmp = Load.FromFile("./inputs/pixart.png");
            using var textureBmp = Load.FromFile("./inputs/glyph.png");
            using var targetBmp = new Bitmap(512,512, PixelFormat.Format32bppArgb);
            
            var texture = ByteImage.FromBitmap(textureBmp);
            var target = ByteImage.FromBitmap(targetBmp);
            var w = texture.Bounds.Width;
            var h = texture.Bounds.Height;

            var tri1 = new PerspectiveTriangle(texture, Pt(20,20,10,  0,0), Pt(500, 10, 20,  w,0), Pt(10, 500, 20,  0,h));
            var tri2 = new PerspectiveTriangle(texture, Pt(500, 10, 20,  w,0), Pt(10, 500, 20,  0,h), Pt(320,320,34,  w,h));
            
            sw.Start();
            tri1.Draw(target);
            tri2.Draw(target);
            sw.Stop();
                
            target!.RenderOnBitmap(targetBmp);
            targetBmp.SaveBmp("./outputs/texture_map_1.bmp");
            Assert.That(Load.FileExists("./outputs/texture_map_1.bmp"));
            Console.WriteLine($"Core draw took {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
        }

        private Point3DTexture2D Pt(int x, int y, int z, int u, int v)
        {
            return new Point3DTexture2D{
                x = x,
                y = y,
                z = z,
                u = u,
                v = v
            };
        }
    }
}