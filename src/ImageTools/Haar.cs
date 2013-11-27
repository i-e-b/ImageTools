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

			Bitmangle.RunKernel(src, dst, GradientKernel);

			return dst;
		}

		static unsafe void GradientKernel(byte* s, ushort* d, BitmapData si, BitmapData di)
		{
			var length = si.Height * si.Stride;
			d[0] = 0;
			for (int i = 1; i < length; i++)
			{
				var x = 255 + (s[i] - s[i - 1]);
				d[i] = (ushort)x;
			}
		}
	}
}