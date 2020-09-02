using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class DistanceFieldTests
    {
        [Test]
        public void rendering_orthogonal_field_expanded()
        {
            using (var bmp = Load.FromFile("./inputs/glyph.png"))
            {
                var horzField = DistanceField.HorizontalDistance(bmp);
                var vertField = DistanceField.VerticalDistance(bmp);
                using (var bmp2 = DistanceField.RenderToImage(8, horzField, vertField))
                {
                    bmp2.SaveJpeg("./outputs/df_expanded.jpg");
                }
            }

            Assert.That(Load.FileExists("./outputs/df_expanded.jpg"));
        }
        
        [Test]
        public void rendering_orthogonal_field_eroded()
        {
            using (var bmp = Load.FromFile("./inputs/glyph.png"))
            {
                var horzField = DistanceField.HorizontalDistance(bmp);
                var vertField = DistanceField.VerticalDistance(bmp);
                using (var bmp2 = DistanceField.RenderToImage(-8, horzField, vertField))
                {
                    bmp2.SaveJpeg("./outputs/df_eroded.jpg");
                }
            }

            Assert.That(Load.FileExists("./outputs/df_eroded.jpg"));
        }
        
        [Test]
        public void rendering_orthogonal_field_as_native()
        {
            using (var bmp = Load.FromFile("./inputs/glyph.png"))
            {
                var horzField = DistanceField.HorizontalDistance(bmp);
                var vertField = DistanceField.VerticalDistance(bmp);
                using (var bmp2 = DistanceField.RenderToImage(0, horzField, vertField))
                {
                    bmp2.SaveJpeg("./outputs/df_native.jpg");
                }
            }

            Assert.That(Load.FileExists("./outputs/df_native.jpg"));
        }

        [Test]
        public void can_calculate_a_orthogonal_field_from_a_bitmap()
        {
            using (var bmp = Load.FromFile("./inputs/glyph.png"))
            {
                var horzField = DistanceField.HorizontalDistance(bmp);
                using (var bmp2 = DistanceField.RenderFieldToImage(horzField))
                {
                    bmp2.SaveJpeg("./outputs/df_horz.jpg");
                }
                
                var vertField = DistanceField.VerticalDistance(bmp);
                using (var bmp3 = DistanceField.RenderFieldToImage(vertField))
                {
                    bmp3.SaveJpeg("./outputs/df_vert.jpg");
                }

                using (var bmp4 = DistanceField.RenderFieldToImage(horzField, vertField))
                {
                    bmp4.SaveJpeg("./outputs/df_both.jpg");
                }
            }

            Assert.That(Load.FileExists("./outputs/df_horz.jpg"));
        }

        [Test]
        public void render_from_scaled_fields_nearest_neighbour()
        {
            using (var bmp = Load.FromFile("./inputs/glyph.png"))
            {
                var horzField = DistanceField.HorizontalDistance(bmp);
                var vertField = DistanceField.VerticalDistance(bmp);

                for (int i = 1; i < 7; i++)
                {
                    var shade = 4 + (i*i)/2.0;
                    var thresh = -4 + i;
                    
                    var scaled = DistanceField.ReduceToVectors_nearest(i, horzField, vertField);
                    
                    using (var bmp2 = DistanceField.RenderToImage(thresh, shade, scaled)) { bmp2.SaveBmp($"./outputs/df_nearest_shade_{i}.bmp"); }
                    using (var bmp2 = DistanceField.RenderToImage(2*i, scaled)) { bmp2.SaveBmp($"./outputs/df_nearest_diff_{i}.bmp"); }
                }
            }
        }
        
        [Test]
        public void render_from_scaled_fields_experimental()
        {
            using (var bmp = Load.FromFile("./inputs/glyph.png"))
            {
                var horzField = DistanceField.HorizontalDistance(bmp);
                var vertField = DistanceField.VerticalDistance(bmp);

                for (int i = 1; i < 7; i++)
                {
                    var scaled = DistanceField.ReduceToVectors_experimental(i, horzField, vertField);
                    
                    using (var bmp2 = DistanceField.RenderToImage(0, i*i, scaled)) { bmp2.SaveBmp($"./outputs/df_exp_shade_{i}.bmp"); }
                    using (var bmp2 = DistanceField.RenderToImage(0, scaled)) { bmp2.SaveBmp($"./outputs/df_exp_diff_{i}.bmp"); }
                }
            }
        }
        
        [Test]
        public void render_from_scaled_fields_cubic_interpolation()
        {
            using (var bmp = Load.FromFile("./inputs/glyph.png"))
            {
                var horzField = DistanceField.HorizontalDistance(bmp);
                var vertField = DistanceField.VerticalDistance(bmp);

                for (int i = 1; i < 7; i++)
                {
                    var shade = 4 + (i*i)/2.0;
                    var thresh = -4 + i;
                    
                    var scaled = DistanceField.ReduceToVectors_cubicSpline(i, horzField, vertField);
                    
                    using (var bmp2 = DistanceField.RenderToImage(thresh, shade, scaled)) { bmp2.SaveBmp($"./outputs/df_cubic_shade_{i}.bmp"); }
                    using (var bmp2 = DistanceField.RenderToImage(2*i, scaled)) { bmp2.SaveBmp($"./outputs/df_cubic_diff_{i}.bmp"); }
                }
            }
        }
    }
}