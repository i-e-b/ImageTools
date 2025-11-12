using System.Drawing;
using System.Drawing.Imaging;
using ImageTools.ImageDataFormats;
using ImageTools.Utilities;

namespace ImageTools;

/// <summary>
/// Erode, dilate, and related
/// </summary>
public class MorphologicalTransforms
{

    /// <summary>
    /// for each pixel, select the lightest luminance within <paramref name="radius"/>
    /// </summary>
    public static Bitmap Dilate2D(Bitmap src, int radius)
    {
        BitmapTools.ImageToPlanes(src, ColorSpace.RGBToYCoCg, out var luminance, out var co, out var cg);

        DilateColumns(luminance, radius, src.Width, src.Height);
        DilateRows(luminance, radius, src.Width, src.Height);

        var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
        BitmapTools.PlanesToImage(dst, ColorSpace.YCoCgToRGB, 0, luminance, co, cg);
        return dst;
    }

    /// <summary>
    /// for each pixel, select the darkest luminance within <paramref name="radius"/>
    /// </summary>
    public static Bitmap Erode2D(Bitmap src, int radius)
    {
        BitmapTools.ImageToPlanes(src, ColorSpace.RGBToYCoCg, out var luminance, out var co, out var cg);

        ErodeColumns(luminance, radius, src.Width, src.Height);
        ErodeRows(luminance, radius, src.Width, src.Height);

        var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
        BitmapTools.PlanesToImage(dst, ColorSpace.YCoCgToRGB, 0, luminance, co, cg);
        return dst;
    }

    /// <summary>
    /// Perform an erode then dilate with the same radius.
    /// This should remove small speckles of light on dark backgrounds
    /// </summary>
    public static Bitmap Opening2D(Bitmap src, int radius)
    {
        BitmapTools.ImageToPlanes(src, ColorSpace.RGBToYCoCg, out var luminance, out var co, out var cg);

        ErodeColumns(luminance, radius, src.Width, src.Height);
        ErodeRows(luminance, radius, src.Width, src.Height);

        DilateColumns(luminance, radius, src.Width, src.Height);
        DilateRows(luminance, radius, src.Width, src.Height);

        var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
        BitmapTools.PlanesToImage(dst, ColorSpace.YCoCgToRGB, 0, luminance, co, cg);
        return dst;
    }

    /// <summary>
    /// Perform an dilate then erode with the same radius.
    /// This should remove small speckles of dark on light backgrounds
    /// </summary>
    public static Bitmap Closing2D(Bitmap src, int radius)
    {
        BitmapTools.ImageToPlanes(src, ColorSpace.RGBToYCoCg, out var luminance, out var co, out var cg);

        DilateColumns(luminance, radius, src.Width, src.Height);
        DilateRows(luminance, radius, src.Width, src.Height);

        ErodeColumns(luminance, radius, src.Width, src.Height);
        ErodeRows(luminance, radius, src.Width, src.Height);

        var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
        BitmapTools.PlanesToImage(dst, ColorSpace.YCoCgToRGB, 0, luminance, co, cg);
        return dst;
    }

    /// <summary>
    /// Dilate samples only horizontally
    /// </summary>
    public static void DilateRows(double[] src, int radius, int srcWidth, int srcHeight)
    {
        if (radius < 1) return;
        if (srcWidth < radius) return;

        var samples = new double[radius * 2]; // search window
        var temp    = new double[srcWidth]; // temporary row values
        var end     = srcWidth - 1;

        for (var y = 0; y < srcHeight; y++)
        {
            var yOff = y * srcWidth;

            // pre-load samples
            for (var i = 0; i < radius; i++)
            {
                samples[i] = src[yOff]; // left side
                samples[i + radius] = src[yOff + i]; // right side
            }

            var si = 0; // index of next sample to overwrite.

            for (var x = 0; x < srcWidth; x++)
            {
                var max = samples[0];
                for (var i = 1; i < samples.Length; i++) { if (samples[i] > max) max = samples[i]; }
                temp[x] = max;

                var right = Math.Min(end, x + radius);
                samples[si++] = src[yOff + right];
                if (si >= samples.Length) si = 0;
            }

            for (var x = 0; x < srcWidth; x++)
            {
                src[yOff + x] = temp[x];
            }
        }
    }

    /// <summary>
    /// Dilate samples only vertically
    /// </summary>
    public static void DilateColumns(double[] src, int radius, int srcWidth, int srcHeight)
    {
        if (radius < 1) return;
        if (srcHeight < radius) return;

        var samples = new double[radius * 2]; // search window
        var temp    = new double[srcHeight]; // temporary column values

        for (var x = 0; x < srcWidth; x++)
        {
            // pre-load samples
            var dy = 0;
            for (var i = 0; i < radius; i++)
            {
                samples[i] = src[x]; // left side
                samples[i + radius] = src[x + dy]; // right side
                dy += srcWidth;
            }

            var si = 0; // index of next sample to overwrite.

            // main section
            var safeEnd = srcHeight - radius;
            var yOff    = x;
            for (var y = 0; y < safeEnd; y++)
            {
                var max  = samples[0];
                for (var i = 1; i < samples.Length; i++) { if (samples[i] > max) max = samples[i]; }
                temp[y] = max;

                samples[si++] = src[yOff];
                if (si >= samples.Length) si = 0;
                yOff += srcWidth;
            }

            // runoff
            for (var y = safeEnd; y < srcHeight; y++)
            {
                var max  = samples[0];
                for (var i = 1; i < samples.Length; i++) { if (samples[i] > max) max = samples[i]; }
                temp[y] = max;

                samples[si++] = src[yOff];
                if (si >= samples.Length) si = 0;
            }

            yOff = x;
            for (var y = 0; y < srcHeight; y++)
            {
                src[yOff] = temp[y];
                yOff += srcWidth;
            }
        }
    }

    /// <summary>
    /// Erode samples only horizontally
    /// </summary>
    public static void ErodeRows(double[] src, int radius, int srcWidth, int srcHeight)
    {
        if (radius < 1) return;
        if (srcWidth < radius) return;

        var samples = new double[radius * 2]; // search window
        var temp    = new double[srcWidth]; // temporary row values
        var end     = srcWidth - 1;

        for (var y = 0; y < srcHeight; y++)
        {
            var yOff = y * srcWidth;

            // pre-load samples
            for (var i = 0; i < radius; i++)
            {
                samples[i] = src[yOff]; // left side
                samples[i + radius] = src[yOff + i]; // right side
            }

            var si = 0; // index of next sample to overwrite.

            for (var x = 0; x < srcWidth; x++)
            {
                var min = samples[0];
                for (var i = 1; i < samples.Length; i++) { if (samples[i] < min) min = samples[i]; }
                temp[x] = min;

                var right = Math.Min(end, x + radius);
                samples[si++] = src[yOff + right];
                if (si >= samples.Length) si = 0;
            }

            for (var x = 0; x < srcWidth; x++)
            {
                src[yOff + x] = temp[x];
            }
        }
    }

    /// <summary>
    /// Erode samples only vertically
    /// </summary>
    public static void ErodeColumns(double[] src, int radius, int srcWidth, int srcHeight)
    {
        if (radius < 1) return;
        if (srcHeight < radius) return;

        var samples = new double[radius * 2]; // search window
        var temp    = new double[srcHeight]; // temporary column values

        for (var x = 0; x < srcWidth; x++)
        {
            // pre-load samples
            var dy = 0;
            for (var i = 0; i < radius; i++)
            {
                samples[i] = src[x]; // left side
                samples[i + radius] = src[x + dy]; // right side
                dy += srcWidth;
            }

            var si = 0; // index of next sample to overwrite.

            // main section
            var safeEnd = srcHeight - radius;
            var yOff    = x;
            for (var y = 0; y < safeEnd; y++)
            {
                var min  = samples[0];
                for (var i = 1; i < samples.Length; i++) { if (samples[i] < min) min = samples[i]; }
                temp[y] = min;

                samples[si++] = src[yOff];
                if (si >= samples.Length) si = 0;
                yOff += srcWidth;
            }

            // runoff
            for (var y = safeEnd; y < srcHeight; y++)
            {
                var min  = samples[0];
                for (var i = 1; i < samples.Length; i++) { if (samples[i] < min) min = samples[i]; }
                temp[y] = min;

                samples[si++] = src[yOff];
                if (si >= samples.Length) si = 0;
            }

            yOff = x;
            for (var y = 0; y < srcHeight; y++)
            {
                src[yOff] = temp[y];
                yOff += srcWidth;
            }
        }
    }
}