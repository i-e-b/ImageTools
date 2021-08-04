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
                    
                    Console.WriteLine($"{numZeros} zeros");

                    // save changed image
                    BitmapTools.PlanesToImage(dst, ColorSpace.YUVToRGB, 0, Y2, U2, V2);
                    dst.SaveBmp("./outputs/3_reduced_loco_i.bmp");
                }

                Assert.That(Load.FileExists("./outputs/3_reduced_loco_i.bmp"));
            }
        }

        [Test]
        public void tunable_prediction()
        {
            // A bit of a test at learning
            
            var rnd = new Random();
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var dst = new Bitmap(bmp!))
                {
                    BitmapTools.ImageToPlanes(bmp, ColorSpace.RGBToYUV, out var Y, out var U, out var V);

                    // Start with something very close to LOCO-I
                    double wTL = -2, wT = 2, wDL = -1, wL = 2, wA = 0.5;
                    
                    // Random weights prediction (only on Y during 'learning')
                    long numZeros = 0;
                    double bestError = double.MaxValue;
                    double bL=0.0,bT=0.0,bTL=0.0,bTR=0.0,bA=0.0;
                    double vL = 0.0, vT = 0.0, vTL = 0.0, vDL = 0.0, vA = 0.0;
                    
                    for (int round = 0; round < 1000; round++) // use a big number to get results, but reduce for regular testing
                    {
                        // Test with current settings
                        MultiLinearPredict(Y, bmp.Width, bmp.Height,
                            wTL + vTL, wT + vT, wDL + vDL, wL + vL, wA+vA,
                            out var nz, out var newError);
                        
                        if (nz > numZeros && Math.Abs(bestError) > Math.Abs(newError)) // best so far?
                        {
                            numZeros = nz;
                            bestError = newError;
                            
                            wL  += vL;
                            wT  += vT;
                            wTL += vTL;
                            wDL += vDL;
                            wA  += vA;
                            
                            bL=wL;bT=wT;bTL=wTL;bTR=wDL;bA=wA;
                            
                            Console.WriteLine($"{nz} zeros in [{wTL},{wT},{wDL},{wL}]/{wA}, average error = {newError}");
                        }
                        
                        // twiddle settings
                        var dd = (rnd.NextDouble()-0.5)*2.0;
                        switch (round % 5)
                        {
                            case 0: vA += dd; break;
                            case 1: vL += dd; break;
                            case 2: vT += dd; break;
                            case 3: vTL += dd; break;
                            case 4: vDL += dd; break;
                        }
                    }
                    
                    
                    // Now do the output with the best coefficients we got from this sample
                    var Y2 = MultiLinearPredict(Y, bmp.Width, bmp.Height, bTL,bT,bTR,bL,bA, out _, out _);
                    var U2 = MultiLinearPredict(U, bmp.Width, bmp.Height, bTL,bT,bTR,bL,bA, out _, out _);
                    var V2 = MultiLinearPredict(V, bmp.Width, bmp.Height, bTL,bT,bTR,bL,bA, out _, out _);

                    Console.WriteLine($"\r\n\r\nBest coefficients, with {numZeros} zeros was: [{bTL},{bT},{bTR},{bL}]/{bA}, average error = {bestError}");
                    
                    // save changed image
                    BitmapTools.PlanesToImage(dst, ColorSpace.YUVToRGB, 0, Y2, U2, V2);
                    dst.SaveBmp("./outputs/3_reduced_random.bmp");
                }

                Assert.That(Load.FileExists("./outputs/3_reduced_random.bmp"));
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
                    if (Math.Abs(dst[x+thisRow]) < 0.01) numZeros++;
                    
                    dst[x+thisRow] -= minVal;
                    maxVal = Math.Max(dst[x+thisRow], maxVal);
                }
            }
            
            // Scale to 0..255 (only in predicted area)
            var scale = 255.0 / maxVal + 0.005;
            scale = Clamp(scale, 0.5, 1.0);
            average /= count;
            Console.WriteLine($"Max = {maxVal}; Scale = {scale}; Average error = {average}");
            
            
            for (int y = 1; y < height; y++)
            {
                var thisRow = y * width;
                for (int x = 1; x < width; x++)
                {
                    dst[x+thisRow] *= scale;
                }
            }
            

            return dst;
        }

        private static double[] MultiLinearPredict(double[] src, int width, int height, double wTL, double wT, double wDL, double wL, double wAdj,
            out long numZeros, out double average)
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
                    var vL  = src[x-1 + thisRow];
                    var vT  = src[x   + prevRow];
                    var vTL = src[x-1 + prevRow];
                    var vDL = x == 1 ? 0 : dst[x-1 + thisRow]; // include a balance from last error
                    
                    
                    var min_a_b = Math.Min(vL,vT);
                    var max_a_b = Math.Max(vL,vT);
                    
                    double predicted;
                    if (vTL >= max_a_b) {
                        predicted = min_a_b;
                    } else if (vTL <= min_a_b) {
                        predicted = max_a_b;
                    } else {
                        predicted = ((wL*vL) + (wT*vT) + (wTL*vTL) + (wDL*vDL)) * wAdj;
                    }
                    predicted = (int)Clamp(predicted, 0, 255); // don't predict out of range, and drop precision we can't represent
                    
                    var difference =  actual - predicted;
                    minVal = Math.Min(difference, minVal);
                    dst[x+thisRow] = difference;
                }
            }

            // Make all numbers positive (only in predicted area)
            //Console.WriteLine($"Min = {minVal}");
            var maxVal = 0.0;

            average = 0.0;
            var count =0;
            for (int y = 1; y < height; y++)
            {
                var thisRow = y * width;
                for (int x = 1; x < width; x++)
                {
                    var sample = dst[x+thisRow];
                    if (Math.Abs(sample) < 0.01) numZeros++;
                    average += sample; count++;
                    
                    dst[x+thisRow] = sample - minVal;
                    maxVal = Math.Max(dst[x+thisRow], maxVal);
                }
            }
            
            // Scale to 0..255 (only in predicted area)
            var scale = 255.0 / maxVal;
            scale = Clamp(scale, 0.5, 1.0);
            average /= count;
            //Console.Write($"Scale = {scale}; ");
            
            for (int y = 1; y < height; y++)
            {
                var thisRow = y * width;
                for (int x = 1; x < width; x++)
                {
                    dst[x+thisRow] *= scale;
                }
            }
            

            return dst;
        }
        
        private static double[] LinearPredict(double[] src)
        {
            var minVal = 0.0;
            var dst = new double[src!.Length];
            dst[0] = src[0];
            dst[1] = src[1];
            for (int i = 2; i < src.Length; i++)
            {
                var predicted = (src[i - 1] + src[i - 2]) / 2.0;
                var actual = src[i];
                predicted = Clamp(predicted, 0, 255); // don't predict out of range

                var difference = actual - predicted;
                minVal = Math.Min(difference, minVal);
                dst[i] = difference;
            }

            // Make all numbers positive
            Console.WriteLine($"Min = {minVal}");
            var maxVal = 0.0;
            var average = 0.0;
            var count =0;
            for (int i = 2; i < dst.Length; i++)
            {
                average += dst[i]; count++;
                
                dst[i] -= minVal;
                maxVal = Math.Max(dst[i], maxVal);
            }
            
            // Scale to 0..255
            var scale = 255.0 / maxVal + 0.005;
            scale = Clamp(scale, 0.5, 1.0);
            average /= count;
            Console.WriteLine($"Max = {maxVal}; Scale = {scale}; Average error = {average}");
            
            for (int i = 2; i < dst.Length; i++)
            {
                dst[i] *= scale;
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