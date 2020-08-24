using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class DistanceFieldTests
    {
        [Test]
        public void can_calculate_a_orthogonal_field_from_a_bitmap()
        {
            using (var bmp = Load.FromFile("./inputs/glyph.png"))
            {
                var field = DistanceField.HorizontalDistance(bmp);
                using (var bmp2 = DistanceField.RenderToImage(field))
                {
                    bmp2.SaveJpeg("./outputs/df_horz.jpg");
                }
                
                field = DistanceField.VerticalDistance(bmp);
                using (var bmp3 = DistanceField.RenderToImage(field))
                {
                    bmp3.SaveJpeg("./outputs/df_vert.jpg");
                }
            }

            Assert.That(Load.FileExists("./outputs/df_horz.jpg"));
        }
    }
}