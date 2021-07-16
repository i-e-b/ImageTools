using System;
using System.Drawing;
using ImageTools.ImageDataFormats;
using ImageTools.Utilities;
using NUnit.Framework;
// ReSharper disable InconsistentNaming

namespace ImageTools.Tests
{
    [TestFixture]
    public class IntraFramePredictions
    {
        [Test]
        public void attempt_to_reduce_entropy_by_neighbors ()
        {
            using (var bmp = Load.FromFile("./inputs/3.png"))
            {
                using (var dst = new Bitmap(bmp))
                {
                    BitmapTools.ImageToPlanes(bmp, ColorSpace.RGBToYUV, out var Y, out var U, out var V);

                    // Really simple reduction.
                    // TODO: better non-wrapping prediction (see https://blog.tempus-ex.com/hello-video-codec/ )

                    var Y2 = LinearPredict(Y);
                    var U2 = LinearPredict(U);
                    var V2 = LinearPredict(V);

                    // save changed image
                    BitmapTools.PlanesToImage(dst, ColorSpace.YUVToRGB, 0, Y2, U2, V2);
                    dst.SaveBmp("./outputs/3_reduced.bmp");
                }

                Assert.That(Load.FileExists("./outputs/3_reduced.bmp"));
            }
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

                var difference = actual - predicted;
                minVal = Math.Min(difference, minVal);
                dst[i] = difference;
            }

            // Make all numbers positive
            Console.WriteLine($"Min = {minVal}");
            var maxVal = 0.0;
            for (int i = 2; i < dst.Length; i++)
            {
                dst[i] -= minVal;
                maxVal = Math.Max(dst[i], maxVal);
            }
            
            // Scale to 0..255
            var scale = 255.0 / maxVal + 0.005;
            Console.WriteLine($"Max = {maxVal}; Scale = {scale}");
            
            for (int i = 2; i < dst.Length; i++)
            {
                dst[i] *= scale;
            }
            

            return dst;
        }
    }
}