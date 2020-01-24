using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class ImageRotationTests {
        [Test]
        public void small_rotation_of_an_image () {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = Rotate.ShearRotate(bmp, 15.5))
                {
                    bmp2.SaveBmp("./outputs/3_rot15deg.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/3_rot15deg.bmp"));
        }
    }
}