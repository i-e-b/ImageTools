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


        var subject = new FFT2();
        var len     = (uint)Math.Log2(Y.Length);
        subject.init(len);

        var im = new double[Y.Length];
        subject.run(Y, im);

        var min = 0.0;
        var max = 0.0;
        for (int i = 0; i < Y.Length; i++)
        {
            min = Math.Min(min, Y[i]);
            max = Math.Max(max, Y[i]);
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
    public void fft_scale_focussing_test()
    {
        using var src  = Load.FromFile("./inputs/qr_codes/qr_code_tilted_scratched.png");


        BitmapTools.ImageToPlanes(src, ColorSpace.RGBToYCoCg, out var Y, out var co, out var cg);


        var subject = new FFT2();
        var len     = (uint)Math.Log2(Y.Length);
        subject.init(len);

        var im = new double[Y.Length];

        subject.run(Y, im);

        var min = 0.0;
        var max = 0.0;
        for (int i = 0; i < Y.Length; i++)
        {
            min = Math.Min(min, Y[i]);
            max = Math.Max(max, Y[i]);
        }
        Console.WriteLine($"Min={min:0.00}; Max={max:0.00};");


        // Cut off high-frequency co-efficients
        for (int i = Y.Length / 24; i < Y.Length; i++)
        {
            Y[i] = 0.0;
            im[i] = 0.0;
        }

        // Cut off low power co-efficients
        var thresh  = 5000.0;
        var reduced = 0;
        for (int i = 0; i < Y.Length; i++)
        {
            if (Math.Abs(Y[i]) < thresh)
            {
                reduced++;
                Y[i] = 0.0;
                im[i] = 0.0;
            }
        }

        var percent = (100.0 * reduced) / Y.Length;
        Console.WriteLine($"Cut {reduced} co-efficients ({percent}%)");

        subject.run(Y, im, true);


        BitmapTools.PlanesToImage(src, ColorSpace.YCoCgToRGB, 0, Y, co, cg);
        src.SaveBmp("./outputs/qr_code_scratched__rc_fft_1D.bmp");
    }


    [Test]
    public void fft_hard_qr_test()
    {
        using var src  = Load.FromFile("./inputs/qr_codes/table_5.jpg");


        BitmapTools.ImageToPlanes(src, ColorSpace.RGB_To_HCL, out var h, out var c, out var l);

        var source = h;

        // Despeckle hue image
        MorphologicalTransforms.Opening2D(source, src.Width, src.Height, 1);
        MorphologicalTransforms.Closing2D(source, src.Width, src.Height, 1);

        var zero   = new double[source.Length];

        BitmapTools.PlanesToImage(src, ColorSpace.HCL_To_RGB, 0, zero, zero, source);
        src.SaveBmp("./outputs/table_5_qr_code__decomp.bmp");

        var subject = new FFT2();
        var len     = (uint)Math.Log2(source.Length);
        subject.init(len);

        var im = new double[source.Length];

        subject.run(source, im);

        var min = 0.0;
        var max = 0.0;
        for (int i = 0; i < source.Length; i++)
        {
            min = Math.Min(min, source[i]);
            max = Math.Max(max, source[i]);
        }
        Console.WriteLine($"Min={min:0.00}; Max={max:0.00};");


        // Cut off high-frequency co-efficients
        for (int i = source.Length / 8; i < source.Length; i++)
        {
            source[i] = 0.0;
            im[i] = 0.0;
        }

        // Cut off low power co-efficients
        var thresh  = 5000.0;
        var reduced = 0;
        for (int i = 0; i < source.Length; i++)
        {
            if (Math.Abs(source[i]) < thresh)
            {
                reduced++;
                source[i] = 0.0;
                im[i] = 0.0;
            }
        }

        var percent = (100.0 * reduced) / source.Length;
        Console.WriteLine($"Cut {reduced} co-efficients ({percent}%)");

        subject.run(source, im, true);


        BitmapTools.PlanesToImage(src, ColorSpace.HCL_To_RGB, 0, zero, zero, source);
        src.SaveBmp("./outputs/table_5_qr_code__rc_fft_1D.bmp");
    }
}