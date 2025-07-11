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
            using var textureBmp1 = Load.FromFile("./inputs/pixart.png"); // small texture on big quad -> interpolation
            using var textureBmp2 = Load.FromFile("./inputs/glyph.png"); // big texture on small quad -> super-sampling
            using var targetBmp = new Bitmap(512,512, PixelFormat.Format32bppArgb);
            
            var texture1 = ByteImage.FromBitmap(textureBmp1);
            var texture2 = ByteImage.FromBitmap(textureBmp2);
            var target = ByteImage.FromBitmap(targetBmp);
            
            var w = texture1.Bounds.Width;
            var h = texture1.Bounds.Height;
            var tri1 = new PerspectiveTriangle(texture1, Pt(20,20,10,  0,0), Pt(500, 10, 20,  w,0), Pt(10, 500, 20,  0,h));
            var tri2 = new PerspectiveTriangle(texture1, Pt(500, 10, 20,  w,0), Pt(10, 500, 20,  0,h), Pt(320,320,34,  w,h));
            
            w = texture2.Bounds.Width;
            h = texture2.Bounds.Height;
            var tri3 = new PerspectiveTriangle(texture2, Pt(500, 500, 5,  w,h), Pt(250, 480, 10,  0,h), Pt(480, 250, 10,  w,0));
            var tri4 = new PerspectiveTriangle(texture2, Pt(300, 300, 13,  0,0), Pt(480, 250, 10,  w,0), Pt(250,480,10,  0,h));
            
            sw.Start();
            tri1.Draw(target);
            tri2.Draw(target);
            tri3.Draw(target);
            tri4.Draw(target);
            sw.Stop();
                
            target!.RenderOnBitmap(targetBmp);
            targetBmp.SaveBmp("./outputs/texture_map_1.bmp");
            Assert.That(Load.FileExists("./outputs/texture_map_1.bmp"));
            Console.WriteLine("file:///"+Load.FullPath("./outputs/texture_map_1.bmp"));
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