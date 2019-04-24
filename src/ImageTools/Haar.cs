using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace ImageTools
{
	public class Haar
	{
		/// <summary>
		/// Returns a 64 bit-per-pixel image
		/// containing the gradients of a 32-bpp image
		/// </summary>
		public static unsafe Bitmap Gradients(Bitmap src)
		{
			var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format64bppArgb);

            // TODO: plane separation in the kernel runner
			Bitmangle.RunKernel(src, dst, GradientKernel);

			return dst;
		}

		static unsafe void GradientKernel(byte* s, ushort* d, BitmapData si, BitmapData di)
		{
            var bytePerPix = si.Stride / si.Width;
            var bytePer2Pix = bytePerPix * 2;
            // width wise
            var halfStride = si.Stride >> 1;
            for (int y = 0; y < si.Height; y++)
            {
                var yo = y * si.Stride;
                for (int x = 0; x < si.Stride; x += bytePer2Pix)
                {
                    for (int ch = 0; ch < bytePerPix; ch++)
                    {
                        var sptr = yo + x + ch;
                        var dptr = yo + (x >> 1) + ch;
                        var a = s[sptr];
                        var b = s[sptr + bytePerPix];

                        var aver = a + b;
                        var diff = 255 + (a - b);

                        d[dptr] = (ushort)(aver << 3);
                        d[dptr + halfStride] = (ushort)(diff << 3);
                    }
                }
            }

            /*
			var length = (si.Height * si.Stride) / 2;
			d[0] = 0;
			for (int i = 5; i < length; i++)
			{
                var j = i << 1;
                var a = s[j];
                var b = s[j - 4];

                var aver = a + b;
				var diff = 255 + (a - b);

                d[i] = (ushort)(aver << 4);
				d[i+length] = (ushort)(diff << 4);
			}*/
		}
	}
}