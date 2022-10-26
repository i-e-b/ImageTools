using System;
using ImageTools.ImageDataFormats;

namespace ImageTools
{
    /// <summary>
    /// Sequential decorrelation algorithms.
    /// These are generally quite effective and low complexity both
    /// in compute and algorithm design.
    /// <p></p>
    /// Being sequential and exact, they cannot be further quantised
    /// or reduced without significant degradation of the result.
    /// They also require the entire image to be decoded before pixel
    /// values are accurate (or even a reasonable approximation).
    /// These methods are best for lossless storage, using a general
    /// data compressions method.
    /// </summary>
    public class Decorrelation
    {
        /// <summary>
        /// Decorrelate a signal based on a LOCO-I prediction,
        /// as used in JPEG-LS standard.
        /// </summary>
        /// <remarks>
        /// See
        /// https://ieeexplore.ieee.org/document/488319
        /// https://www.hpl.hp.com/techreports/98/HPL-98-193.html</remarks>
        public static double[] LocoI_Predict(double[] src, int width, int height, out long numZeros)
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

        /// <summary>
        /// Decorrelate a signal based on a linear prediction
        /// </summary>
        public static double[] LinearPredict(double[] src)
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
            for (int i = 2; i < dst.Length; i++)
            {
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