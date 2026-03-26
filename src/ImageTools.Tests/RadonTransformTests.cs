using ImageTools.Utilities;
using NUnit.Framework;

namespace ImageTools.Tests;

[TestFixture]
public class RadonTransformTests
{
    [Test]
    public void radon_transform()
    {
        using var bmp  = Load.FromFile("./inputs/slight_rotation.png");
        using var bmp2 = Radon.BuildRadon(bmp, out var highestEnergyRotation);

        Console.WriteLine($"Best rotation by energy = {highestEnergyRotation:0.00}");
        using (var bmp3 = Rotate.SelectRotateRad(bmp, highestEnergyRotation)) bmp3?.SaveBmp("./outputs/slight_rotation_restored.bmp");

        bmp2?.SaveBmp("./outputs/radon_rotation.bmp");

        Assert.That(Load.FileExists("./outputs/radon_rotation.bmp"));
    }


    [Test]
    public void radon_transform_barcode()
    {
        using var bmp  = Load.FromFile("./inputs/bar_code_tilted.png");
        using var bmp2 = Radon.BuildRadon(bmp, out var highestEnergyRotation);

        Console.WriteLine($"Best rotation by energy = {highestEnergyRotation:0.00}");
        using (var bmp3 = Rotate.SelectRotateRad(bmp, highestEnergyRotation)) bmp3?.SaveBmp("./outputs/bar_code_tilted_restored.bmp");

        bmp2?.SaveBmp("./outputs/radon_bar_code_tilted.bmp");

        Assert.That(Load.FileExists("./outputs/radon_bar_code_tilted.bmp"));
    }

    [Test]
    [TestCase("qr_code_tilted.png")]
    [TestCase("qr_codes/sticker_faded_stained.jpg")]
    [TestCase("qr_codes/sticker_torn_crinkled.jpg")]
    [TestCase("qr_codes/webcam_scratched.png")]
    [TestCase("qr_codes/clear_with_rotation.jpg")]
    public void radon_transform_qr_code(string srcName)
    {
        using var bmp  = Load.FromFile($"./inputs/{srcName}");
        using var bmp2 = Radon.BuildRadon(bmp, out var highestEnergyRotation);

        Console.WriteLine($"Best rotation by energy = {highestEnergyRotation:0.00}");

        using var bmp3 = Rotate.SelectRotateRad(bmp, highestEnergyRotation);

        bmp2?.SaveBmp($"./outputs/radon_{srcName}.bmp");
        bmp3?.SaveBmp($"./outputs/radon_{srcName}_restored.bmp");

        var thresh = new UnsharpThreshold();

        using var bmp4 = thresh.Matrix(bmp2!, false, 4, 0);
        bmp4.SaveBmp($"./outputs/radon_{srcName}_thresh.bmp");

        Assert.That(Load.FileExists($"./outputs/radon_{srcName}_restored.bmp"));
    }


    [Test]
    public void radon_transform_noisy_image()
    {
        using var bmp  = Load.FromFile("./inputs/6.png");
        using var bmp2 = Radon.BuildRadon(bmp, out var highestEnergyRotation);

        Console.WriteLine($"Best rotation by energy = {highestEnergyRotation:0.00}");
        using (var bmp3 = Rotate.SelectRotateRad(bmp, highestEnergyRotation)) bmp3?.SaveBmp("./outputs/radon_6_rotated.bmp");

        bmp2?.SaveBmp("./outputs/radon_6.bmp");

        Assert.That(Load.FileExists("./outputs/radon_6.bmp"));
    }

    [Test]
    public void radon_transform_street_sign()
    {
        using var bmp  = Load.FromFile("./inputs/twisty-road.bmp");
        using var bmp2 = Radon.BuildRadon(bmp, out var highestEnergyRotation);

        Console.WriteLine($"Best rotation by energy = {highestEnergyRotation:0.00}");
        using (var bmp3 = Rotate.SelectRotateRad(bmp, highestEnergyRotation)) bmp3?.SaveBmp("./outputs/radon_twisty-road_rotated.bmp");

        bmp2?.SaveBmp("./outputs/radon_twisty-road.bmp");

        Assert.That(Load.FileExists("./outputs/radon_twisty-road.bmp"));
    }
}