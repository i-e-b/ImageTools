using System.Diagnostics;
using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests;

[TestFixture]
public class ThresholdingTests
{
    [Test]
    public void thresholding_an_easy_photographic_image()
    {
        using (var bmp = Load.FromFile("./inputs/wiki_pre_otsus_algorithm.jpg"))
        {
            var subject = new UnsharpThreshold();

            for (int scale = 2; scale < 5; scale++)
            {
                for (int exposure = -8; exposure <= 8; exposure += 4)
                {
                    using var result = subject.Matrix(bmp, true, scale, exposure);

                    result.SaveBmp($"./outputs/Threshold_wiki_s{scale}_e{exposure}.bmp");
                }
            }
        }
    }

    [Test]
    public void thresholding_a_photographic_image()
    {
        using (var bmp = Load.FromFile("./inputs/7.jpg"))
        {
            var subject = new UnsharpThreshold();

            for (int scale = 2; scale < 5; scale++)
            {
                for (int exposure = -8; exposure <= 8; exposure += 4)
                {
                    using var result = subject.Matrix(bmp, true, scale, exposure);

                    result.SaveBmp($"./outputs/Threshold_7_s{scale}_e{exposure}.bmp");
                }
            }
        }
    }

    [Test]
    public void thresholding_a_noisy_image()
    {
        using (var bmp = Load.FromFile("./inputs/6.png"))
        {
            var subject = new UnsharpThreshold();

            using var result = subject.Matrix(bmp, false, 4, 0);

            result.SaveBmp("./outputs/Threshold_6.bmp");
        }

        Assert.That(Load.FileExists("./outputs/Threshold_6.bmp"));
    }

    [Test]
    public void thresholding_a_blurred_image()
    {
        using var bmp     = Load.FromFile("./inputs/qr_code_tilted_blurred.png");
        var       subject = new UnsharpThreshold();

        for (int scale = 2; scale < 6; scale++)
        {
            for (int exposure = -8; exposure <= 8; exposure += 8)
            {
                using var result = subject.Matrix(bmp, false, scale, exposure);

                result.SaveBmp($"./outputs/Threshold_qr_blur_s{scale}_e{exposure}.bmp");
            }
        }
    }

    [Test]
    public void thresholding_and_opening_a_scratched_image()
    {
        using var bmp     = Load.FromFile("./inputs/qr_code_tilted_scratched.png");
        var       subject = new UnsharpThreshold();

        for (int scale = 2; scale < 6; scale++)
        {
            for (int exposure = -8; exposure <= 8; exposure += 8)
            {
                using var result = subject.Matrix(bmp, false, 4, exposure);

                using var morphed = MorphologicalTransforms.Opening2D(result, scale);

                morphed.SaveBmp($"./outputs/Threshold_qr_scratched_s{scale}_e{exposure}.bmp");
            }
        }
    }


    [Test]
    public void frequency_limit_and_thresholding_a_scratched_image()
    {
        using var bmp     = Load.FromFile("./inputs/qr_code_tilted_scratched.png");
        var       subject = new UnsharpThreshold();

        for (int scale = 8; scale <= 16; scale+=2)
        {
            for (int exposure = 4; exposure <= 8; exposure += 2)
            {
                using var limited = Blur.FrequencyFactor(bmp, [0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.25f, 0.5f, 0.75f, 1.0f]);
                using var morph   = MorphologicalTransforms.Opening2D(limited, scale / 2);
                using var result  = subject.Matrix(morph, false, 4, exposure);

                result.SaveBmp($"./outputs/Threshold_qr_scratched_s{scale}_e{exposure}_fLimit.bmp");
            }
        }
    }

    [Test]
    public void frequency_limit_and_thresholding_a_scratched_webcam_image()
    {
        using var bmp     = Load.FromFile("./inputs/qr_codes/webcam_scratched.png");
        var       subject = new UnsharpThreshold();

        for (int scale = 2; scale <= 8; scale+=2)
        {
            for (int exposure = 0; exposure <= 6; exposure += 2)
            {
                using var limited = Blur.FrequencyFactor(bmp, [0.0f, 0.0f, 0.0f, 0.25f, 0.5f, 0.75f, 1.0f]);
                using var morph   = MorphologicalTransforms.Opening2D(limited, scale / 2);
                using var result  = subject.Matrix(morph, false, 4, exposure);

                result.SaveBmp($"./outputs/Threshold_webcam_qr_scratched_s{scale}_e{exposure}_fLimit.bmp");
            }
        }
    }

    [Test]
    public void blurring_and_thresholding_a_scratched_image()
    {
        using var bmp     = Load.FromFile("./inputs/qr_code_tilted_scratched.png");
        var       subject = new UnsharpThreshold();

        for (int scale = 2; scale <= 10; scale += 2)
        {
            for (int exposure = -8; exposure <= 8; exposure += 8)
            {
                using var blurred = Blur.FastBlur(bmp, scale);
                using var result  = subject.Matrix(blurred, false, 4, exposure);


                result.SaveBmp($"./outputs/Threshold_blur_qr_scratched_s{scale}_e{exposure}.bmp");
            }
        }
    }

    [Test]
    public void thresholding_a_test_image()
    {
        using var bmp     = Load.FromFile("./inputs/3.png");
        var       subject = new UnsharpThreshold();

        for (int scale = 2; scale < 6; scale++)
        {
            for (int exposure = -8; exposure <= 8; exposure += 8)
            {
                using var result = subject.Matrix(bmp, false, scale, exposure);

                result.SaveBmp($"./outputs/Threshold_3_s{scale}_e{exposure}.bmp");
            }
        }
    }


    [Test]
    [TestCase("clear", 5, -4, 2)]
    [TestCase("clear_with_rotation", 5, -4, 2)]
    [TestCase("obscured", 4, 0, 2)]
    [TestCase("mild_shadow", 6, 0, 2)]
    [TestCase("strong_shadow", 4, 0, 2)]
    public void thresholding_qr_code_photos(string name, int scale, int exposure, int openRadius)
    {
        var subject = new UnsharpThreshold();

        using var original = Load.FromFile($"./inputs/qr_codes/{name}.jpg");

        var sw = Stopwatch.StartNew();

        using var thresholded = subject.Matrix(original, false, scale, exposure);
        sw.Stop();
        Console.WriteLine($"Thresholding transform took {sw.Elapsed}");

        thresholded.SaveBmp($"./outputs/qr_{name}_threshold_at_s{scale}_e{exposure}.bmp");

        sw.Restart();
        using var result = MorphologicalTransforms.Opening2D(thresholded, openRadius);
        sw.Stop();
        Console.WriteLine($"Opening transform took {sw.Elapsed}");

        result.SaveBmp($"./outputs/qr_{name}_opened_at_r{openRadius}.bmp");
    }

    [Test]
    [TestCase("clear", 5, -4, 2)]
    [TestCase("clear_with_rotation", 5, 0, 2)]
    [TestCase("obscured", 4, 0, 2)]
    [TestCase("mild_shadow", 6, 0, 2)]
    [TestCase("strong_shadow", 4, 0, 2)]
    public void thresholding_qr_code_photos_with_opening_first(string name, int scale, int exposure, int openRadius)
    {
        var subject = new UnsharpThreshold();

        using var original = Load.FromFile($"./inputs/qr_codes/{name}.jpg");

        var sw = Stopwatch.StartNew();

        using var morphed = MorphologicalTransforms.Opening2D(original, openRadius);
        sw.Stop();
        Console.WriteLine($"Opening transform took {sw.Elapsed}");
        morphed.SaveBmp($"./outputs/qr_cf_{name}_opened_at_r{openRadius}.bmp");

        sw.Restart();
        using var result = subject.Matrix(morphed, false, scale, exposure);
        sw.Stop();
        Console.WriteLine($"Thresholding transform took {sw.Elapsed}");

        result.SaveBmp($"./outputs/qr_cf_{name}_opened_at_r{openRadius}_threshold_at_s{scale}_e{exposure}.bmp");
    }


    [Test]
    [TestCase("clear", 5, -4, 2)]
    [TestCase("clear_with_rotation", 5, -4, 2)]
    [TestCase("obscured", 4, 0, 2)]
    [TestCase("mild_shadow", 6, 0, 2)]
    [TestCase("strong_shadow", 4, 0, 2)]
    public void thresholding_qr_code_photos_byte(string name, int scale, int exposure, int openRadius)
    {
        var subject = new UnsharpThreshold();

        using var original = Load.FromFile($"./inputs/qr_codes/{name}.jpg");

        var sw = Stopwatch.StartNew();

        using var thresholded = subject.Matrix(original, false, scale, exposure);
        sw.Stop();
        Console.WriteLine($"Thresholding transform took {sw.Elapsed}");

        thresholded.SaveBmp($"./outputs/qr_{name}_threshold_at_s{scale}_e{exposure}_byte.bmp");

        sw.Restart();
        using var result = MorphologicalTransformsByte.Opening2D(thresholded, openRadius);
        sw.Stop();
        Console.WriteLine($"Opening transform took {sw.Elapsed}");

        result.SaveBmp($"./outputs/qr_{name}_opened_at_r{openRadius}_byte.bmp");
    }

    [Test]
    [TestCase("clear", 5, -4, 2)]
    [TestCase("clear_with_rotation", 5, 0, 2)]
    [TestCase("obscured", 4, 0, 2)]
    [TestCase("mild_shadow", 6, 0, 2)]
    [TestCase("strong_shadow", 4, 0, 2)]
    public void thresholding_qr_code_photos_with_opening_first_byte(string name, int scale, int exposure, int openRadius)
    {
        var subject = new UnsharpThreshold();

        using var original = Load.FromFile($"./inputs/qr_codes/{name}.jpg");

        var sw = Stopwatch.StartNew();

        using var morphed = MorphologicalTransformsByte.Opening2D(original, openRadius);
        sw.Stop();
        Console.WriteLine($"Opening transform took {sw.Elapsed}");

        morphed.SaveBmp($"./outputs/qr_cf_{name}_opened_at_r{openRadius}_byte.bmp");

        sw.Restart();
        using var result = subject.Matrix(morphed, false, scale, exposure);
        sw.Stop();
        Console.WriteLine($"Thresholding transform took {sw.Elapsed}");

        result.SaveBmp($"./outputs/qr_cf_{name}_opened_at_r{openRadius}_threshold_at_s{scale}_e{exposure}_byte.bmp");
    }
}