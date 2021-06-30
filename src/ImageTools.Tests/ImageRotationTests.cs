using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class ImageRotationTests {
        [Test]
        public void rotate_by_3_shear () {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = Rotate.ShearRotate(bmp, 15.5))
                {
                    bmp2.SaveBmp("./outputs/3_rot15deg.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/3_rot15deg.bmp"));
        }
        
        [Test]
        public void rotate_with_matrix_and_sampling () {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = Rotate.SelectRotate(bmp, 15.5))
                {
                    bmp2.SaveBmp("./outputs/3_rot15deg_sample.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/3_rot15deg_sample.bmp"));
        }
    }
}