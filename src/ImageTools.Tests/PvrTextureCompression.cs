using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using ImageTools.ImageDataFormats;
using ImageTools.Utilities;
using NUnit.Framework;
// ReSharper disable InconsistentNaming

namespace ImageTools.Tests
{
    /// <summary>
    /// A compression scheme based on "PowerVR Texture Compression".
    /// This does not use the same storage format, but keeps the idea
    /// of a reduced min/max, and a full scale modulation layer.
    /// </summary>
    [TestFixture]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    public class PvrTextureCompression
    {
        [Test]
        public void downscaling_image_to_minimum_and_maximum_values__YUV()
        {
            using var bmp = Load.FromFile("./inputs/3.png");
            using var dst = new Bitmap(bmp, new Size(bmp.Width / 4, bmp.Height / 4));
            
            BitmapTools.ImageToPlanes(bmp, ColorSpace.RGBToYUV, out var Y, out var U, out var V);

            ReduceBy4ToRange(Y, bmp.Width, bmp.Height, out var Ymin, out var Ymax);
            ReduceBy4ToRange(U, bmp.Width, bmp.Height, out var Umin, out var Umax);
            ReduceBy4ToRange(V, bmp.Width, bmp.Height, out var Vmin, out var Vmax);

            BitmapTools.PlanesToImage(dst, ColorSpace.YUVToRGB, 0, Ymin, Umin, Vmin);
            dst.SaveBmp("./outputs/3_PVR_min_YUV.bmp");
            
            BitmapTools.PlanesToImage(dst, ColorSpace.YUVToRGB, 0, Ymax, Umax, Vmax);
            dst.SaveBmp("./outputs/3_PVR_max_YUV.bmp");

            Assert.That(Load.FileExists("./outputs/3_PVR_min_YUV.bmp"));
            Assert.That(Load.FileExists("./outputs/3_PVR_max_YUV.bmp"));
        }
        
        [Test]
        public void restore_image_to_original_size_with_linear_interpolation()
        {
            using var bmp = Load.FromFile("./inputs/3.png");
            using var dst = new Bitmap(bmp);
            
            BitmapTools.ImageToPlanes(bmp, ColorSpace.RGBToYUV, out var Y, out var U, out var V);

            ReduceBy4ToRange(Y, bmp.Width, bmp.Height, out var Ymin, out var Ymax);
            ReduceBy4ToRange(U, bmp.Width, bmp.Height, out var Umin, out var Umax);
            ReduceBy4ToRange(V, bmp.Width, bmp.Height, out var Vmin, out var Vmax);

            ExpandBy4(Ymin, bmp.Width / 4, bmp.Height / 4, out var YminLarge);
            ExpandBy4(Umin, bmp.Width / 4, bmp.Height / 4, out var UminLarge);
            ExpandBy4(Vmin, bmp.Width / 4, bmp.Height / 4, out var VminLarge);
            
            ExpandBy4(Ymax, bmp.Width / 4, bmp.Height / 4, out var YmaxLarge);
            ExpandBy4(Umax, bmp.Width / 4, bmp.Height / 4, out var UmaxLarge);
            ExpandBy4(Vmax, bmp.Width / 4, bmp.Height / 4, out var VmaxLarge);

            BitmapTools.PlanesToImage(dst, ColorSpace.YUVToRGB, 0, YminLarge, UminLarge, VminLarge);
            dst.SaveBmp("./outputs/3_PVR_min_expanded_YUV.bmp");
            
            BitmapTools.PlanesToImage(dst, ColorSpace.YUVToRGB, 0, YmaxLarge, UmaxLarge, VmaxLarge);
            dst.SaveBmp("./outputs/3_PVR_max_expanded_YUV.bmp");

            Assert.That(Load.FileExists("./outputs/3_PVR_min_expanded_YUV.bmp"));
            Assert.That(Load.FileExists("./outputs/3_PVR_max_expanded_YUV.bmp"));
        }
        
        [Test]
        public void interpolate_between_min_and_max()
        {
            using var bmp = Load.FromFile("./inputs/3.png");
            using var dst = new Bitmap(bmp);
            
            BitmapTools.ImageToPlanes(bmp, ColorSpace.RGBToYUV, out var Y, out var U, out var V);

            ReduceBy4ToRange(Y, bmp.Width, bmp.Height, out var Ymin, out var Ymax);
            ReduceBy4ToRange(U, bmp.Width, bmp.Height, out var Umin, out var Umax);
            ReduceBy4ToRange(V, bmp.Width, bmp.Height, out var Vmin, out var Vmax);

            ExpandBy4(Ymin, bmp.Width / 4, bmp.Height / 4, out var YminLarge);
            ExpandBy4(Umin, bmp.Width / 4, bmp.Height / 4, out var UminLarge);
            ExpandBy4(Vmin, bmp.Width / 4, bmp.Height / 4, out var VminLarge);
            
            ExpandBy4(Ymax, bmp.Width / 4, bmp.Height / 4, out var YmaxLarge);
            ExpandBy4(Umax, bmp.Width / 4, bmp.Height / 4, out var UmaxLarge);
            ExpandBy4(Vmax, bmp.Width / 4, bmp.Height / 4, out var VmaxLarge);
            
            CalculateFraction(Y, YminLarge, YmaxLarge, out var fractions);
            QuantiseFractions(fractions);
            
            SelectByFraction(fractions, YminLarge, YmaxLarge, out var Ynew);
            SelectByFraction(fractions, UminLarge, UmaxLarge, out var Unew);
            SelectByFraction(fractions, VminLarge, VmaxLarge, out var Vnew);

            BitmapTools.PlanesToImage(dst, ColorSpace.Scaled, 0, fractions, fractions, fractions);
            dst.SaveBmp("./outputs/3_PVR_interpolation_Y.bmp");
            
            BitmapTools.PlanesToImage(dst, ColorSpace.YUVToRGB, 0, Ynew, Unew, Vnew);
            dst.SaveBmp("./outputs/3_PVR_interpolated_YUV.bmp");
            
            Assert.That(Load.FileExists("./outputs/3_PVR_interpolation_Y.bmp"));
            Assert.That(Load.FileExists("./outputs/3_PVR_interpolated_YUV.bmp"));
        }
        
        [Test]
        public void interpolate_between_min_and_max_RGB_and_YUV()
        {
            using var bmp = Load.FromFile("./inputs/3.png");
            using var dst = new Bitmap(bmp);
            using var dstSmall = new Bitmap(bmp, new Size(bmp.Width / 4, bmp.Height / 4));
            
            BitmapTools.ImageToPlanes(bmp, ColorSpace.RGBToYUV, out var Y, out var U, out var V);
            BitmapTools.ImageToPlanes(bmp, ColorSpace.Native, out var R, out var G, out var B);

            ReduceBy4ToRange(R, bmp.Width, bmp.Height, out var Rmin, out var Rmax);
            ReduceBy4ToRange(G, bmp.Width, bmp.Height, out var Gmin, out var Gmax);
            ReduceBy4ToRange(B, bmp.Width, bmp.Height, out var Bmin, out var Bmax);
            ReduceBy4ToRange(Y, bmp.Width, bmp.Height, out var Ymin, out var Ymax);

            ExpandBy4(Rmin, bmp.Width / 4, bmp.Height / 4, out var RminLarge);
            ExpandBy4(Gmin, bmp.Width / 4, bmp.Height / 4, out var GminLarge);
            ExpandBy4(Bmin, bmp.Width / 4, bmp.Height / 4, out var BminLarge);
            ExpandBy4(Ymin, bmp.Width / 4, bmp.Height / 4, out var YminLarge);
            
            ExpandBy4(Rmax, bmp.Width / 4, bmp.Height / 4, out var RmaxLarge);
            ExpandBy4(Gmax, bmp.Width / 4, bmp.Height / 4, out var GmaxLarge);
            ExpandBy4(Bmax, bmp.Width / 4, bmp.Height / 4, out var BmaxLarge);
            ExpandBy4(Ymax, bmp.Width / 4, bmp.Height / 4, out var YmaxLarge);
            
            CalculateFraction(Y, YminLarge, YmaxLarge, out var fractions);
            QuantiseFractions(fractions);
            
            SelectByFraction(fractions, RminLarge, RmaxLarge, out var Rnew);
            SelectByFraction(fractions, GminLarge, GmaxLarge, out var Gnew);
            SelectByFraction(fractions, BminLarge, BmaxLarge, out var Bnew);
            
            BitmapTools.PlanesToImage(dstSmall, ColorSpace.Native, 0, Rmin, Gmin, Bmin);
            dstSmall.SaveBmp("./outputs/3_PVR_min_RGB.bmp");
            
            BitmapTools.PlanesToImage(dstSmall, ColorSpace.Native, 0, Rmax, Gmax, Bmax);
            dstSmall.SaveBmp("./outputs/3_PVR_max_RGB.bmp");

            BitmapTools.PlanesToImage(dst, ColorSpace.Scaled, 0, fractions, fractions, fractions);
            dst.SaveBmp("./outputs/3_PVR_interpolation_rgbY.bmp");
            
            BitmapTools.PlanesToImage(dst, ColorSpace.Native, 0, Rnew, Gnew, Bnew);
            dst.SaveBmp("./outputs/3_PVR_interpolated_RGB.bmp");
            
            Assert.That(Load.FileExists("./outputs/3_PVR_interpolation_rgbY.bmp"));
            Assert.That(Load.FileExists("./outputs/3_PVR_interpolated_RGB.bmp"));
        }

        [Test]
        public void interpolate_on_Y_channel_only_UV_are_averaged()
        {
            using var bmp = Load.FromFile("./inputs/3.png");
            using var dst = new Bitmap(bmp);
            
            BitmapTools.ImageToPlanes(bmp, ColorSpace.RGBToYUV, out var Y, out var U, out var V);

            ReduceBy4ToRange(Y, bmp.Width, bmp.Height, out var Ymin, out var Ymax);
            
            ReduceBy4ToAverage(U, bmp.Width, bmp.Height, out var Usmall);
            ReduceBy4ToAverage(V, bmp.Width, bmp.Height, out var Vsmall);

            ExpandBy4(Ymin, bmp.Width / 4, bmp.Height / 4, out var YminLarge);
            ExpandBy4(Ymax, bmp.Width / 4, bmp.Height / 4, out var YmaxLarge);
            
            ExpandBy4(Usmall, bmp.Width / 4, bmp.Height / 4, out var Urestored);
            ExpandBy4(Vsmall, bmp.Width / 4, bmp.Height / 4, out var Vrestored);
            
            CalculateFraction(Y, YminLarge, YmaxLarge, out var fractions);
            
            SelectByFraction(fractions, YminLarge, YmaxLarge, out var Ynew);
            QuantiseFractions(fractions);

            BitmapTools.PlanesToImage(dst, ColorSpace.Scaled, 0, fractions, fractions, fractions);
            dst.SaveBmp("./outputs/3_PVR_interpolation_Y_only.bmp");
            
            BitmapTools.PlanesToImage(dst, ColorSpace.YUVToRGB, 0, Ynew, Urestored, Vrestored);
            dst.SaveBmp("./outputs/3_PVR_interpolated_Y_only.bmp");
            
            Assert.That(Load.FileExists("./outputs/3_PVR_interpolation_Y_only.bmp"));
            Assert.That(Load.FileExists("./outputs/3_PVR_interpolated_Y_only.bmp"));
            
        }
        
        [Test]
        public void interpolate_on_Y_channel_only_linear_color_space()
        {
            using var bmp = Load.FromFile("./inputs/3.png");
            using var dst = new Bitmap(bmp);
            using var dstSmall = new Bitmap(bmp, new Size(bmp.Width / 4, bmp.Height / 4));
            
            BitmapTools.ImageToPlanes(bmp, ColorSpace.sRGB_To_Oklab, out var Y, out var U, out var V);

            ReduceBy4ToRange(Y, bmp.Width, bmp.Height, out var Ymin, out var Ymax);
            
            ReduceBy4ToAverage(U, bmp.Width, bmp.Height, out var Usmall);
            ReduceBy4ToAverage(V, bmp.Width, bmp.Height, out var Vsmall);

            ExpandBy4(Ymin, bmp.Width / 4, bmp.Height / 4, out var YminLarge);
            ExpandBy4(Ymax, bmp.Width / 4, bmp.Height / 4, out var YmaxLarge);
            
            ExpandBy4(Usmall, bmp.Width / 4, bmp.Height / 4, out var Urestored);
            ExpandBy4(Vsmall, bmp.Width / 4, bmp.Height / 4, out var Vrestored);
            
            CalculateFraction(Y, YminLarge, YmaxLarge, out var fractions);
            
            SelectByFraction(fractions, YminLarge, YmaxLarge, out var Ynew);
            QuantiseFractions(fractions);
            
            BitmapTools.PlanesToImage(dstSmall, ColorSpace.Oklab_To_sRGB, 0, Ymin, Usmall, Vsmall);
            dstSmall.SaveBmp("./outputs/3_PVR_min_oklab.bmp");
            
            BitmapTools.PlanesToImage(dstSmall, ColorSpace.Oklab_To_sRGB, 0, Ymax, Usmall, Vsmall);
            dstSmall.SaveBmp("./outputs/3_PVR_max_oklab.bmp");

            BitmapTools.PlanesToImage(dst, ColorSpace.Scaled, 0, fractions, fractions, fractions);
            dst.SaveBmp("./outputs/3_PVR_interpolation_Y_oklab.bmp");
            
            BitmapTools.PlanesToImage(dst, ColorSpace.Oklab_To_sRGB, 0, Ynew, Urestored, Vrestored);
            dst.SaveBmp("./outputs/3_PVR_interpolated_Y_oklab.bmp");
            
            Assert.That(Load.FileExists("./outputs/3_PVR_interpolation_Y_oklab.bmp"));
            Assert.That(Load.FileExists("./outputs/3_PVR_interpolated_Y_oklab.bmp"));
            
        }

        /// <summary>
        /// Reduce fractions to 2 bits of information
        /// </summary>
        private void QuantiseFractions(double[] fractions)
        {
            for (int i = 0; i < fractions.Length; i++)
            {
                var v = fractions[i];
                if (v < 0.25) v = 0.0;
                else if (v < 0.5) v = 0.3;
                else if (v < 0.75) v = 0.6;
                else v = 1.0;
                fractions[i] = v;
            }
        }

        /// <summary>
        /// Pick a value as a fraction between min and max
        /// </summary>
        private void SelectByFraction(double[] fraction, double[] minimum, double[] maximum, out double[] result)
        {
            result = new double[fraction.Length];
            for (int i = 0; i < fraction.Length; i++)
            {
                var diff = maximum[i] - minimum[i];
                result[i] = minimum[i] + diff * fraction[i];
            }
        }

        /// <summary>
        /// Pick a fraction [0..1] for each point in original, where 0.0 is the value
        /// in 'minimum' and 1.0 is the value in maximum.
        /// </summary>
        private void CalculateFraction(double[] original, double[] minimum, double[] maximum, out double[] fractions)
        {
            fractions = new double[original.Length];
            var prev = 0.0;
            for (int i = 0; i < original.Length; i++)
            {
                var diff = maximum[i] - minimum[i];
                if (diff < 0.0001) fractions[i] = prev;
                else
                {
                    var orig = original[i] - minimum[i];
                    prev = ClampFrac(orig / diff);
                    fractions[i] = prev;
                }
            }
        }

        private static double ClampFrac(double d)
        {
            if (d < 0.0) return 0.0;
            if (d > 1.0) return 1.0;
            return d;
        }

        /// <summary>
        /// Expand an image to 4x its original dimensions
        /// </summary>
        private void ExpandBy4(double[] small, int w, int h, out double[] large)
        {
            var lw = w * 4;
            var lh = h * 4;
            large = new double[lw * lh];
            
            // expand in X first
            for (int y = 0; y < h; y++)
            {
                var yoff_out = y * lw;
                var yoff_in = y * w;
                
                for (int x = 0; x < lw; x++)
                {
                    var f = (x / (double)lw) * w;
                    var s1 = (int)Math.Floor(f);
                    var s2 = Math.Min(s1 + 1, w - 1);
                    var p2 = f-s1;
                    var p1 = 1 - p2;
                    
                    large[yoff_out + x] = small[yoff_in + s1] * p1 + small[yoff_in + s2] * p2;
                }
            }

            // expand in-place across Y
            for (int x = 0; x < lw; x++)
            {
                for (int y = lh - 1; y >= 0; y--)
                {
                    var f = (y / (double)lh) * h;
                    var s1 = (int)Math.Floor(f);
                    var s2 = Math.Min(s1 + 1, h - 1);
                    var p2 = f - s1;
                    var p1 = 1 - p2;

                    var sample = large[s1 * lw + x] * p1 + large[s2 * lw + x] * p2;
                    large[y*lw + x] = sample;
                }
            }
        }

        /// <summary>
        /// Reduce an image to 1/4 of its original dimensions,
        /// returning the maximum and minimum values in each 4x4
        /// </summary>
        private void ReduceBy4ToRange(double[] src, int sWidth, int sHeight, out double[] mins, out double[] maxes)
        {
            var w = sWidth / 4;
            var h = sHeight / 4;
            var dstSize = w * h;
            mins = new double[dstSize];
            maxes = new double[dstSize];
            
            // scan across output, sample inputs
            for (int y = 0; y < h; y++)
            {
                var yoff = y * w;
                for (int x = 0; x < w; x++)
                {
                    // get sample
                    SampleBlockMinMax(src, sWidth, sHeight, x, y, out var max, out var min);

                    // set min/max
                    mins[yoff+x] = min;
                    maxes[yoff+x] = max;
                }
            }
        }
        
        /// <summary>
        /// Reduce an image to 1/4 of its original dimensions,
        /// returning the average for each 4x4
        /// </summary>
        private void ReduceBy4ToAverage(double[] src, int sWidth, int sHeight, out double[] averages)
        {
            var w = sWidth / 4;
            var h = sHeight / 4;
            var dstSize = w * h;
            averages = new double[dstSize];
            
            // scan across output, sample inputs
            for (int y = 0; y < h; y++)
            {
                var yoff = y * w;
                for (int x = 0; x < w; x++)
                {
                    // get sample
                    SampleBlockAve(src, sWidth, sHeight, x, y, out var ave);

                    // set min/max
                    averages[yoff+x] = ave;
                }
            }
        }

        private static void SampleBlockMinMax(double[] src, int sWidth, int sHeight, int x, int y, out double max, out double min)
        {
            var sx = x * 4;// + 2;
            var sy = y * 4;// + 2;
            min = src[sy*sWidth + sx];
            max = min;
            
            var stride = sWidth;
            var lwidth = sWidth - 1;
            var lheight = sHeight - 1;

            for (int yi = -2; yi < 3; yi++)
            {
                for (int xi = -2; xi < 3; xi++)
                {
                    var px = sx + xi;
                    var py = sy + yi;
                    if (px < 0) px=0;
                    if (px > lwidth) px = lwidth;
                    if (py < 0) py=0;
                    if (py > lheight) py = lheight;
                    
                    var value = src[py * stride + px];
                    min = Math.Min(min, value);
                    max = Math.Max(max, value);
                }
            }
        }
        
        private static void SampleBlockAve(double[] src, int sWidth, int sHeight, int x, int y, out double ave)
        {
            var sx = x * 4;
            var sy = y * 4;
            ave = 0;
            var count = 0;
            
            var stride = sWidth;
            var lwidth = sWidth - 1;
            var lheight = sHeight - 1;

            for (int yi = -2; yi < 3; yi++)
            {
                for (int xi = -2; xi < 3; xi++)
                {
                    var px = sx + xi;
                    var py = sy + yi;
                    if (px < 0) px=0;
                    if (px > lwidth) px = lwidth;
                    if (py < 0) py=0;
                    if (py > lheight) py = lheight;
                    
                    ave += src[py * stride + px];
                    count++;
                }
            }
            ave /= count;
        }
    }
}