// ReSharper disable InconsistentNaming
namespace ImageTools.ImageDataFormats
{
    /// <summary>
    /// RGB colour components
    /// </summary>
    public struct ColorRGB {
        public int R;
        public int G;
        public int B;

        public static ColorRGB FromARGB32(uint c) {
            ColorSpace.CompoundToComponent(c, out _, out var r, out var g, out var b);
            return new ColorRGB { R = r, G = g, B = b };
        }

    }
}