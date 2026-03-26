using ImageTools.AnalyticalTransforms;
using ImageTools.ImageDataFormats;
using ImageTools.Utilities;
using NUnit.Framework;
// ReSharper disable InconsistentNaming

namespace ImageTools.Tests;

[TestFixture]
public class FrequencyDomainTests
{
    [Test]
    public void dct_test()
    {
        // This is a O(n²) naive algorithm. Don't use it on anything over about 64x64
        using var src  = Load.FromFile("./inputs/qr_code_small.png");

        var subject = new DCT(src.Width, src.Height);

        var srcMat = subject.BitmapToMatrices(src);
        var dctMat = subject.DCTMatrices(srcMat);

        var transformedBmp = subject.MatricesToBitmap(dctMat);
        transformedBmp.SaveBmp("./outputs/qr_code_small_dct.bmp");
    }

    [Test]
    public void fft_test()
    {
        using var src  = Load.FromFile("./inputs/qr_code.png");

        BitmapTools.ImageToPlanes(src, ColorSpace.RGBToYCoCg, out var Y, out _, out _);

        var len     = (uint)Math.Log2(Y.Length);
        var subject = new FFT2(len);

        var im = new double[Y.Length];
        subject.SpaceToFrequency(Y, im);

        var min = 0.0;
        var max = 0.0;
        foreach (var t in Y)
        {
            min = Math.Min(min, t);
            max = Math.Max(max, t);
        }

        var factor = 100.0;//max / 256.0;
        for (int i = 0; i < Y.Length; i++)
        {
            Y[i] = Math.Abs(Y[i] / factor);
            im[i] = 0.0;
        }

        Console.WriteLine($"Min={min:0.00}; Max={max:0.00};");

        BitmapTools.PlanesToImage(src, ColorSpace.YCoCgToRGB, 0, Y, im, im);
        src.SaveBmp("./outputs/qr_code__fft_1D.bmp");
    }


    [Test]
    [TestCase("qr_code_tilted_scratched.png")]
    [TestCase("qr_code_tilted_blurred.png")]
    [TestCase("qr_code_tilted.png")]
    [TestCase("qr_codes/sticker_faded_stained.jpg")]
    [TestCase("qr_codes/sticker_torn_crinkled.jpg")]
    [TestCase("qr_codes/webcam_scratched.png")]
    public void fft_scale_lowpass_test(string filename)
    {
        for (int scale = 4; scale <= 32; scale+=4)
        {
            using var src  = Load.FromFile($"./inputs/{filename}");

            BitmapTools.ImageToPlanes(src, ColorSpace.RGBToYCoCg, out var Y, out var co, out var cg);

            var source  = FFT2.Transpose(Y, src.Width);
            var len     = (uint)Math.Log2(source.Length);
            var subject = new FFT2(len);

            var im = new double[source.Length];

            // Cut off high-frequency co-efficients
            subject.SpaceToFrequency(source, im);
            for (int i = source.Length / scale; i < source.Length; i++)
            {
                source[i] = 0.0;
                im[i] = 0.0;
            }

            subject.FrequencyToSpace(source, im);

            // Reset phases
            for (int i = 0; i < source.Length; i++)
            {
                im[i] = 0.0;
            }

            // Transpose and process again
            source = FFT2.Transpose(source, src.Width);

            // Cut off high-frequency co-efficients
            subject.SpaceToFrequency(source, im);
            for (int i = source.Length / scale; i < source.Length; i++)
            {
                source[i] = 0.0;
                im[i] = 0.0;
            }

            subject.FrequencyToSpace(source, im);

            FFT2.Normalise(source, 255);

            BitmapTools.PlanesToImage(src, ColorSpace.YCoCgToRGB, 0, source, co, cg);
            src.SaveBmp($"./outputs/{filename.Replace("/", "_").Replace(".", "_")}_scale{scale:D2}_lp_fft_2D.bmp");
        }
    }


    [Test]
    [TestCase("qr_code_tilted_scratched.png")]
    [TestCase("qr_code_tilted_blurred.png")]
    [TestCase("qr_code_tilted.png")]
    [TestCase("qr_codes/sticker_faded_stained.jpg")]
    [TestCase("qr_codes/sticker_torn_crinkled.jpg")]
    [TestCase("qr_codes/webcam_scratched.png")]
    public void fft_scale_highpass_test(string filename)
    {
        for (int scale = 4; scale <= 32; scale+=4)
        {
            using var src  = Load.FromFile($"./inputs/{filename}");

            BitmapTools.ImageToPlanes(src, ColorSpace.RGBToYCoCg, out var Y, out var co, out var cg);

            var source  = FFT2.Transpose(Y, src.Width);
            var len     = (uint)Math.Log2(source.Length);
            var subject = new FFT2(len);

            var im = new double[source.Length];

            // Cut off low-frequency co-efficients
            subject.SpaceToFrequency(source, im);
            for (int i = 0; i < source.Length / scale; i++)
            {
                source[i] = 0.0;
                im[i] = 0.0;
            }

            subject.FrequencyToSpace(source, im);

            // Reset phases
            for (int i = 0; i < source.Length; i++)
            {
                im[i] = 0.0;
            }

            // Transpose and process again
            source = FFT2.Transpose(source, src.Width);

            // Cut off low-frequency co-efficients
            subject.SpaceToFrequency(source, im);
            for (int i = 0; i < source.Length / scale; i++)
            {
                source[i] = 0.0;
                im[i] = 0.0;
            }

            subject.FrequencyToSpace(source, im);

            FFT2.Normalise(source, 255);

            BitmapTools.PlanesToImage(src, ColorSpace.YCoCgToRGB, 0, source, co, cg);
            src.SaveBmp($"./outputs/{filename.Replace("/", "_").Replace(".", "_")}_scale{scale:D2}_hp_fft_2D.bmp");
        }
    }


    [Test]
    public void fft_hard_qr_test()
    {
        using var src  = Load.FromFile("./inputs/qr_codes/table_5.jpg");

        const int scale = 4;

        BitmapTools.ImageToPlanes(src, ColorSpace.RGB_To_HCL, out var h, out _, out _);

        var source = FFT2.Transpose(h, src.Width);

        var zero    = new double[source.Length];
        var len     = (uint)Math.Log2(source.Length);
        var subject = new FFT2(len);

        var im = new double[source.Length];

        // Cut off high-frequency co-efficients
        subject.SpaceToFrequency(source, im);
        for (int i = source.Length / scale; i < source.Length; i++)
        {
            source[i] = 0.0;
            im[i] = 0.0;
        }
        subject.FrequencyToSpace(source, im);

        // Reset phases
        for (int i = 0; i < source.Length; i++) { im[i] = 0.0; }

        // Transpose and process again
        source = FFT2.Transpose(source, src.Width);

        // Cut off high-frequency co-efficients
        subject.SpaceToFrequency(source, im);
        for (int i = source.Length / scale; i < source.Length; i++)
        {
            source[i] = 0.0;
            im[i] = 0.0;
        }
        subject.FrequencyToSpace(source, im);

        FFT2.Normalise(source, 255);

        BitmapTools.PlanesToImage(src, ColorSpace.HCL_To_RGB, 0, zero, zero, source);
        src.SaveBmp("./outputs/table_5_qr_code__rc_fft_2D.bmp");
    }
}