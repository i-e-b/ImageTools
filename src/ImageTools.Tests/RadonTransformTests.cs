using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class RadonTransformTests
    {
        [Test]
        public void radon_transform()
        {
            using var bmp  = Load.FromFile("./inputs/slight_rotation.png");
            using var bmp2 = Radon.BuildRadon(bmp);

            bmp2?.SaveBmp("./outputs/radon_rotation.bmp");

            Assert.That(Load.FileExists("./outputs/radon_rotation.bmp"));
        }


        [Test]
        public void radon_transform_barcode()
        {
            using var bmp  = Load.FromFile("./inputs/bar_code_tilted.png");
            using var bmp2 = Radon.BuildRadon(bmp);

            bmp2?.SaveBmp("./outputs/radon_bar_code_tilted.bmp");

            Assert.That(Load.FileExists("./outputs/radon_bar_code_tilted.bmp"));
        }

        [Test]
        public void radon_transform_qr_code()
        {
            using var bmp  = Load.FromFile("./inputs/qr_code_tilted.png");
            using var bmp2 = Radon.BuildRadon(bmp);

            bmp2?.SaveBmp("./outputs/qr_code_tilted.bmp");

            Assert.That(Load.FileExists("./outputs/qr_code_tilted.bmp"));
        }
    }
}