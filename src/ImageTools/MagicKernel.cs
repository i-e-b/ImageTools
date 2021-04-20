using System;
using System.Drawing;
using ImageTools.ImageDataFormats;
using ImageTools.Utilities;

namespace ImageTools
{
    /// <summary>
    /// Scaling and processing based on http://www.johncostella.com/magic/
    /// </summary>
    public static class MagicKernel
    {
        // ints: a,b,c,d => ((a + 3 * (b + c) + d + 4) >> 3);
        // floats: a,b,c,d => (a + 3 * (b + c) + d) / 8;
        public static Bitmap DoubleImage(Bitmap src)
        {
            throw new NotImplementedException();
            //BitmapTools.ImageToPlanesf(src, ColorSpace.sRGB_To_Oklab, out var srcY, out var srcU, out var srcV);
            
            
            
            //BitmapTools.PlanesToImage_f(dst, ColorSpace.Oklab_To_sRGB, 0, dstY, dstU, dstV);
        }
    }
}