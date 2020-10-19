// ReSharper disable InconsistentNaming
namespace ImageTools.ImageDataFormats
{
    /// <summary>
    /// YUV colour components (this is high-precision YUV, not Ycbcr)
    /// </summary>
    public struct ColorYUV {
        public double Y;
        public double U;
        public double V;

        public static ColorYUV FromYUV32(uint c)
        {
            ColorSpace.CompoundToComponent(c, out _, out var y, out var u, out var v);
            return new ColorYUV { Y = y, U = u, V = v };
        }
    }
}