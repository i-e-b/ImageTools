using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class ColorSpaceTests {
        [Test]
        public void Y_Cb_Cr () {
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
        public void Y_Co_Cg() {
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
    }
}