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
    [TestCase("clear", 5, -4)]
    [TestCase("clear_with_rotation", 5, -4)]
    [TestCase("obscured", 5, -4)]
    [TestCase("mild_shadow", 6, 0)]
    [TestCase("strong_shadow", 4, 1)]
    public void thresholding_qr_code_photos(string name, int scale, int exposure)
    {
        var subject = new UnsharpThreshold();

        using var original = Load.FromFile($"./inputs/qr_codes/{name}.jpg");

        /*using var radon = Radon.BuildRadon(original, out var highestEnergyRotation);

        Console.WriteLine($"Best rotation by energy = {highestEnergyRotation:0.00}");
        using var rotated = Rotate.SelectRotateRad(original, highestEnergyRotation - (Math.PI/2.0)); // radon likes to rotate QR codes CCW

        if (rotated is null) throw new Exception("Failed to rotate");

        rotated.SaveBmp($"./outputs/qr_{name}_rotated.bmp");*/

        using var thresholded = subject.Matrix(/*rotated*/original, false, scale, exposure);

        using var result = MorphologicalTransforms.Opening2D(thresholded, 1);

        result.SaveBmp($"./outputs/qr_{name}_threshold_at_s{scale}_e{exposure}.bmp");
    }
}