using System.Drawing;
using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class ImageRotationTests
    {
        [Test]
        public void rotate_by_3_shear()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var bmp2 = Rotate.ShearRotate(bmp, 0)) bmp2.SaveBmp("./outputs/3_rot0deg0_shear.bmp");
                using (var bmp2 = Rotate.ShearRotate(bmp, 2.5)) bmp2.SaveBmp("./outputs/3_rot2deg5_shear.bmp");
                using (var bmp2 = Rotate.ShearRotate(bmp, 15.5)) bmp2.SaveBmp("./outputs/3_rot15deg5_shear.bmp");
                using (var bmp2 = Rotate.ShearRotate(bmp, 45.0)) bmp2.SaveBmp("./outputs/3_rot45deg0_shear.bmp");
                using (var bmp2 = Rotate.ShearRotate(bmp, 90.0)) bmp2.SaveBmp("./outputs/3_rot90deg0_shear.bmp");
                using (var bmp2 = Rotate.ShearRotate(bmp, 91.1)) bmp2.SaveBmp("./outputs/3_rot91deg1_shear.bmp");
            }

            Assert.That(Load.FileExists("./outputs/3_rot0deg0_shear.bmp"));
        }
        
        [Test]
        public void rotate_with_matrix_and_sampling ()
        {
            using var bmp = Load.FromFile("./inputs/3.png");
            using (var bmp2 = Rotate.SelectRotateDeg(bmp, 0)) bmp2.SaveBmp("./outputs/3_rot0deg0_sample.bmp");
            using (var bmp2 = Rotate.SelectRotateDeg(bmp, 2.5)) bmp2.SaveBmp("./outputs/3_rot2deg5_sample.bmp");
            using (var bmp2 = Rotate.SelectRotateDeg(bmp, 15.5)) bmp2.SaveBmp("./outputs/3_rot15deg5_sample.bmp");
            using (var bmp2 = Rotate.SelectRotateDeg(bmp, 45.0)) bmp2.SaveBmp("./outputs/3_rot45deg0_sample.bmp");
            using (var bmp2 = Rotate.SelectRotateDeg(bmp, 90.0)) bmp2.SaveBmp("./outputs/3_rot90deg0_sample.bmp");
            using (var bmp2 = Rotate.SelectRotateDeg(bmp, 91.1)) bmp2.SaveBmp("./outputs/3_rot91deg1_sample.bmp");
        }

        [Test, Explicit("Slow test")]
        public void multiple_sequential_rotations_SelectRotate()
        {
            var bmp = Load.FromFile("./inputs/3.png");

            Bitmap? bmp2 = null;

            for (int i = 0; i < 90; i++)
            {
                bmp2 = Rotate.SelectRotateDeg(bmp, 1.0);

                bmp.Dispose();
                bmp = bmp2;
            }

            bmp2?.SaveBmp("./outputs/3_rot_90_times_SelectRotate.bmp");
            bmp2?.Dispose();
        }

        [Test, Explicit("Slow test")]
        public void multiple_sequential_rotations_ShearRotate()
        {
            var bmp = Load.FromFile("./inputs/3.png");

            Bitmap? bmp2 = null;

            for (int i = 0; i < 90; i++)
            {
                bmp2 = Rotate.ShearRotate(bmp, 1.0);

                bmp.Dispose();
                bmp = bmp2;
            }

            bmp2?.SaveBmp("./outputs/3_rot_90_times_ShearRotate.bmp");
            bmp2?.Dispose();
        }
    }
}