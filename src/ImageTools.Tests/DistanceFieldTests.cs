using System;
using System.Diagnostics;
using ImageTools.DistanceFields;
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
                    bmp2.SaveBmp("./outputs/df_expanded.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/df_expanded.bmp"));
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
                    bmp2.SaveBmp("./outputs/df_eroded.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/df_eroded.bmp"));
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
                    bmp2.SaveBmp("./outputs/df_native.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/df_native.bmp"));
        }

        [Test]
        public void can_calculate_a_orthogonal_field_from_a_bitmap()
        {
            using (var bmp = Load.FromFile("./inputs/glyph.png"))
            {
                var horzField = DistanceField.HorizontalDistance(bmp);
                using (var bmp2 = DistanceField.RenderFieldToImage(horzField))
                {
                    bmp2.SaveBmp("./outputs/df_horz.bmp");
                }

                var vertField = DistanceField.VerticalDistance(bmp);
                using (var bmp3 = DistanceField.RenderFieldToImage(vertField))
                {
                    bmp3.SaveBmp("./outputs/df_vert.bmp");
                }

                using (var bmp4 = DistanceField.RenderFieldToImage(horzField, vertField))
                {
                    bmp4.SaveBmp("./outputs/df_both.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/df_horz.bmp"));
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
                    var shade = 4 + (i * i) / 2.0;
                    var thresh = -4 + i;

                    var scaled = DistanceField.ReduceToVectors_nearest(i, horzField, vertField);

                    using (var bmp2 = DistanceField.RenderToImage(thresh, shade, scaled))
                    {
                        bmp2.SaveBmp($"./outputs/df_nearest_shade_{i}.bmp");
                    }

                    using (var bmp2 = DistanceField.RenderToImage(2 * i, scaled))
                    {
                        bmp2.SaveBmp($"./outputs/df_nearest_diff_{i}.bmp");
                    }
                }
            }
        }

        [Test]
        public void render_from_scaled_fields_box_zero()
        {
            using (var bmp = Load.FromFile("./inputs/glyph.png"))
            {
                var horzField = DistanceField.HorizontalDistance(bmp);
                var vertField = DistanceField.VerticalDistance(bmp);

                for (int i = 1; i < 7; i++)
                {
                    var scaled = DistanceField.ReduceToVectors_boxZero(i, horzField, vertField);

                    using (var bmp2 = DistanceField.RenderToImage(-i, 3.25 + i * 1.75, scaled))
                    {
                        bmp2.SaveBmp($"./outputs/df_exp_shade_{i}.bmp");
                    }

                    using (var bmp2 = DistanceField.RenderToImage(i, scaled))
                    {
                        bmp2.SaveBmp($"./outputs/df_exp_diff_{i}.bmp");
                    }
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
                    var shade = 4 + (i * i) / 2.0;
                    var thresh = -4 + i;

                    var scaled = DistanceField.ReduceToVectors_cubicSpline(i, horzField, vertField);

                    using (var bmp2 = DistanceField.RenderToImage(thresh, shade, scaled))
                    {
                        bmp2.SaveBmp($"./outputs/df_cubic_shade_{i}.bmp");
                    }

                    using (var bmp2 = DistanceField.RenderToImage(2 * i, scaled))
                    {
                        bmp2.SaveBmp($"./outputs/df_cubic_diff_{i}.bmp");
                    }
                }
            }
        }

        [Test]
        public void render_from_upscaled_fields_experimental()
        {
            using (var bmp = Load.FromFile("./inputs/glyph.png"))
            {
                var horzField = DistanceField.HorizontalDistance(bmp);
                var vertField = DistanceField.VerticalDistance(bmp);

                for (int i = 1; i < 7; i++)
                {
                    //var downscaled = DistanceField.ReduceToVectors_boxZero(i, horzField, vertField); // down scale, causes blocking but has less drop-out
                    var downscaled = DistanceField.ReduceToVectors_cubicSpline(i, horzField, vertField); // down scale, smooth results but has drop-out at zero-threshold

                    //var upscaled = DistanceField.RescaleVectors_nearest(downscaled, 1024, 1024); // pretty much useless
                    //var upscaled = DistanceField.RescaleVectors_cubic(downscaled, 1024, 1024); // severe ringing artifacts
                    var upscaled = DistanceField.RescaleVectors_bilinear(downscaled, 1024, 1024); // pretty good

                    using (var bmp2 = DistanceField.RenderToImage(i * 1.8, 2, upscaled))
                    {
                        bmp2.SaveBmp($"./outputs/df_upscale_shade_{i}.bmp");
                    }

                    using (var bmp2 = DistanceField.RenderToImage(2.5 * i, upscaled))
                    {
                        bmp2.SaveBmp($"./outputs/df_upscale_diff_{i}.bmp");
                    }
                }
            }
        }

        [Test]
        public void calculate_natural_distance_and_normal_from_a_bitmap()
        {
            // This is a 'true' 2D distance field, rather than the separate 1-directional fields above
            using (var bmp = Load.FromFile("./inputs/glyph.png"))
            {
                var field = DistanceField.DistanceAndGradient(bmp);
                using (var bmp2 = DistanceField.RenderFieldToImage(field))
                {
                    bmp2.SaveBmp("./outputs/df_natural.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/df_natural.bmp"));
        }

        [Test]
        public void ray_marched_circle_edges()
        {
            // An attempt at a reasonably efficient visibility from a point
            // using the distance field
            using (var bmp = Load.FromFile("./inputs/glyph.png"))
            {
                var field = DistanceField.DistanceAndGradient(bmp);
                var sw = new Stopwatch();
                sw.Start();
                using (var bmp2 = DistanceField.RenderPointVisibility(field, 370, 450))
                {
                    sw.Stop();
                    Console.WriteLine($"Visibility calculation took {sw.ElapsedMilliseconds} ms");
                    bmp2.SaveBmp("./outputs/df_point_visible.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/df_point_visible.bmp"));
        }
        
        [Test]
        public void ray_marched_circle_edges_with_limit()
        {
            // An attempt at a reasonably efficient visibility from a point
            // using the distance field
            using (var bmp = Load.FromFile("./inputs/glyph.png"))
            {
                var field = DistanceField.DistanceAndGradient(bmp);
                var sw = new Stopwatch();
                sw.Start();
                using (var bmp2 = DistanceField.RenderPointVisibility(field, 370, 450, 30))
                {
                    sw.Stop();
                    Console.WriteLine($"Visibility calculation took {sw.ElapsedMilliseconds} ms");
                    bmp2.SaveBmp("./outputs/df_point_visible_limit.bmp");
                }
            }

            Assert.That(Load.FileExists("./outputs/df_point_visible_limit.bmp"));
        }

        [Test]
        public void render_from_signed_distance_field()
        {
            // This is a 'true' 2D distance field, rather than the separate 1-directional fields above
            using (var bmp = Load.FromFile("./inputs/glyph.png"))
            {
                var field = DistanceField.DistanceAndGradient(bmp);
                using (var bmp2 = DistanceField.RenderToImage(0.0, field)) { bmp2.SaveBmp("./outputs/df_n_00.bmp"); }
                using (var bmp2 = DistanceField.RenderToImage(5.0, field)) { bmp2.SaveBmp("./outputs/df_n_50.bmp"); }
                using (var bmp2 = DistanceField.RenderToImage(-5.0, field))  { bmp2.SaveBmp("./outputs/df_n-50.bmp"); }
            }

            Assert.That(Load.FileExists("./outputs/df_n_00.bmp"));
        }
        
        [Test]
        public void render_from_upscaled_signed_distance_field()
        {
            using (var bmp = Load.FromFile("./inputs/glyph.png"))
            {
                var field = DistanceField.DistanceAndGradient(bmp);

                for (int i = 1; i < 7; i++)
                {
                    var downscaled = DistanceField.ReduceToDistance_cubicSpline(i, field);

                    var upscaled = DistanceField.RescaleDistance_bilinear(downscaled, 1024, 1024);

                    using (var bmp2 = DistanceField.RenderToImage(1.0 * i, upscaled))
                    {
                        bmp2.SaveBmp($"./outputs/df_upscale_dist_cubic-bilinear_{i}.bmp");
                    }
                }
                
                for (int i = 1; i < 7; i++)
                {
                    var downscaled = DistanceField.ReduceToVectors_boxZero(i, field);

                    var upscaled = DistanceField.RescaleDistance_bilinear(downscaled, 1024, 1024);

                    using (var bmp2 = DistanceField.RenderToImage((0.5 * i) - 0.75, upscaled))
                    {
                        bmp2.SaveBmp($"./outputs/df_upscale_dist_boxZero-bilinear_{i}.bmp");
                    }
                }
            }
        }
    }
}