using System.Collections;
using System.Drawing;
using ImageTools.ImageDataFormats;
using ImageTools.Utilities;

#pragma warning disable CA1416 // Windows only
namespace ImageTools;

/// <summary>
/// Threshold images into 2 color bitmaps.
/// This is used for bar-code/matrix-codes.
/// </summary>
public class UnsharpThreshold
{
    private const int UpperLimit = 251;
    private const int LowerLimit = 4;

    /// <summary>
    /// Threshold an entire bitmap, returning a new bitmap
    /// </summary>
    /// <param name="src">Input bitmap</param>
    /// <param name="invert">If <c>true</c> the output bitmap will be inverted</param>
    /// <param name="scale">Target scale of features. Adjust to pick out different detail levels. Range 1..8 inclusive. 5 or 6 are good defaults</param>
    /// <param name="exposure">Negative for lighter image, positive for darker. Zero is no bias. Between -16 and 16 seem to work in most cases</param>
    public Bitmap Matrix(Bitmap src, bool invert, int scale, int exposure)
    {
        BitmapTools.ImageToPlanesByte(src, ColorSpace.RGBToYCoCg, out var luminance, out _, out _);

        var width  = src.Width;
        var height = src.Height;

        var columns = new int[luminance.Length];
        var results = new byte[luminance.Length];
        var noColor = new byte[luminance.Length];

        Array.Fill(noColor, (byte)127);

        byte white = 255;
        byte black = 0;

        if (invert)
        {
            white = 0;
            black = 255;
        }

        var radius = 1 << scale;
        var diam   = scale + 1;
        var right  = width - 1;
        var span   = width * radius;
        var leadIn = (-radius) * width;

        for (var x = 0; x < width; x++) { // for each column
            var sum = 0;

            // feed in
            var row = leadIn;
            for (var i = -radius; i < radius; i++) {
                var y = Math.Max(row, 0);
                sum += luminance[y + x] & 0xFF;
                row += width;
            }

            var end = luminance.Length - x - 1;
            row = 0;
            for (var y = 0; y < height; y++) {
                columns[row + x] = sum >> diam;

                // update running average
                var yr       = Math.Min(row + span, end);
                var yl       = Math.Max(row - span, 0);
                var incoming = luminance[yr + x];
                var outgoing = luminance[yl + x];

                sum += (int)incoming - (int)outgoing;
                row += width;
            }
        }

        for (var y = 0; y < height; y++) { // for each scanline
            var yOff = y * width;
            var sum = 0;

            // feed in
            for (var i = -radius; i < radius; i++) {
                var x = Math.Max(i, 0);
                sum += columns[yOff + x];
            }

            // running average threshold
            for (var x = 0; x < width; x++) {
                // calculate threshold values
                var actual = (luminance[yOff + x] & 0xFF) - exposure;
                var target = sum >>> diam;

                // don't let the target be too extreme
                if (target > UpperLimit) target = UpperLimit;
                if (target < LowerLimit) target = LowerLimit;

                // Decide what side of the threshold we are on
                results[yOff + x] = actual < target ? black : white;

                // update running average
                var xr = Math.Min(x + radius, right);
                var xl = Math.Max(x - radius, 0);
                var incoming = columns[yOff + xr];
                var outgoing = columns[yOff + xl];

                sum += incoming - outgoing;
            }
        }

        var dst = new Bitmap(src);
        BitmapTools.PlanesToImageByte(dst, ColorSpace.YCoCgToRGB, 0, results, noColor, noColor);
        return dst;
    }

    /// <summary>
    /// Threshold a single row in a bitmap, returning a bit array.
    /// The unsharp mask is applied only in horizontal.
    /// </summary>
    /// <param name="src">Input bitmap</param>
    /// <param name="row">Y-coordinate of row in <c>src</c> bitmap to process</param>
    /// <param name="invert">If <c>true</c> the output bitmap will be inverted</param>
    /// <param name="scale">Target scale of features. Adjust to pick out different detail levels. Range 1..8 inclusive. 5 or 6 are good defaults</param>
    /// <param name="exposure">Negative for lighter image, positive for darker. Zero is no bias. Between -16 and 16 seem to work in most cases</param>
    public BitArray Row(Bitmap src, int row, bool invert, int scale, int exposure)
    {
        throw new NotImplementedException();
    }
}