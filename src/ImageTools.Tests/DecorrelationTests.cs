using System;
using System.Drawing;
using ImageTools.ImageDataFormats;
using ImageTools.Utilities;
using NUnit.Framework;
// ReSharper disable InconsistentNaming

namespace ImageTools.Tests
{
    [TestFixture]
    public class DecorrelationTests
    {
        [Test]
        public void attempt_to_reduce_entropy_by_neighbors__direct_linear ()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var dst = new Bitmap(bmp!))
                {
                    BitmapTools.ImageToPlanes(bmp, ColorSpace.RGBToYUV, out var Y, out var U, out var V);

                    // Really simple reduction, wraps left-right
                    var Y2 = LinearPredict(Y);
                    var U2 = LinearPredict(U);
                    var V2 = LinearPredict(V);

                    // save changed image
                    BitmapTools.PlanesToImage(dst, ColorSpace.YUVToRGB, 0, Y2, U2, V2);
                    dst.SaveBmp("./outputs/3_reduced_linear.bmp");
                }

                Assert.That(Load.FileExists("./outputs/3_reduced_linear.bmp"));
            }
        }
        
        [Test]
        public void attempt_to_reduce_entropy_by_neighbors__jpeg_LOCO_I ()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var dst = new Bitmap(bmp!))
                {
                    BitmapTools.ImageToPlanes(bmp, ColorSpace.RGBToYUV, out var Y, out var U, out var V);

                    // Lossless JPEG type prediction
                    var Y2 = LocoI_Predict(Y, bmp.Width, bmp.Height, out var numZeros);
                    var U2 = LocoI_Predict(U, bmp.Width, bmp.Height, out _);
                    var V2 = LocoI_Predict(V, bmp.Width, bmp.Height, out _);
                    
                    var pc = Y!.Length / (double)numZeros;
                    Console.WriteLine($"{numZeros} zeros ({pc:0.00}%)");

                    // save changed image
                    BitmapTools.PlanesToImage(dst, ColorSpace.YUVToRGB, 0, Y2, U2, V2);
                    dst.SaveBmp("./outputs/3_reduced_loco_i.bmp");
                }

                Assert.That(Load.FileExists("./outputs/3_reduced_loco_i.bmp"));
            }
        }
        
        private static double[] LocoI_Predict(double[] src, int width, int height, out long numZeros)
        {
            numZeros = 0;
            var minVal = 0.0;
            var dst = new double[src!.Length];
            dst[0] = src[0];
            dst[1] = src[1];
            
            // Prediction core
            
            for (int ya = 0; ya < src.Length; ya+=width) { dst[ya] = src[ya]; } // copy baseline first col
            for (int xa = 0; xa < width; xa++) { dst[xa] = src[xa]; } // copy baseline first row
            
            for (int y = 1; y < height; y++)
            {
                var thisRow = y * width;
                var prevRow = (y-1) * width;
                for (int x = 1; x < width; x++)
                {
                    var actual = src[x+thisRow];
                    // calculate our prediction
                    var a = src[x-1 + thisRow];
                    var b = src[x   + prevRow];
                    var c = src[x-1 + prevRow];
                    
                    var min_a_b = Math.Min(a,b);
                    var max_a_b = Math.Max(a,b);
                    
                    double predicted;
                    if (c >= max_a_b) {
                        predicted = min_a_b;
                    } else if (c <= min_a_b) {
                        predicted = max_a_b;
                    } else {
                        predicted = a + b - c;
                    }
                    predicted = (int)Clamp(predicted, 0, 255); // don't predict out of range, and drop precision we can't represent
                    
                    var difference =  actual - predicted;
                    minVal = Math.Min(difference, minVal);
                    dst[x+thisRow] = difference;
                }
            }

            // Make all numbers positive (only in predicted area)
            Console.WriteLine($"Min = {minVal}");
            var maxVal = 0.0;

            var average = 0.0;
            var count =0;
            for (int y = 1; y < height; y++)
            {
                var thisRow = y * width;
                for (int x = 1; x < width; x++)
                {
                    average += dst[x+thisRow]; count++; // measure before we zero-adjust
                    
                    dst[x+thisRow] = DataEncoding.SignedToUnsigned((int)dst[x+thisRow]) / 2.0;
                    
                    if (Math.Abs(dst[x+thisRow]) < 0.01) numZeros++;
                    
                    maxVal = Math.Max(dst[x+thisRow], maxVal);
                }
            }
            
            return dst;
        }

        private static double[] LinearPredict(double[] src)
        {
            var dst = new double[src!.Length];
            dst[0] = src[0];
            dst[1] = src[1];
            for (int i = 2; i < src.Length; i++)
            {
                var predicted = (src[i - 1] + src[i - 2]) / 2.0;
                var actual = src[i];
                predicted = Clamp(predicted, 0, 255); // don't predict out of range

                var difference = actual - predicted;
                dst[i] = difference;
            }

            // Make all numbers positive
            var maxVal = 0.0;
            var average = 0.0;
            var count =0;
            for (int i = 2; i < dst.Length; i++)
            {
                average += dst[i]; count++;
                
                dst[i] = Clamp(DataEncoding.SignedToUnsigned((int)dst[i]), 0, 255);
            }

            return dst;
        }

        private static double Clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

    }
}