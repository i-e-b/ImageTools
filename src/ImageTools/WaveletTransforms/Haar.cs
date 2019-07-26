using System.Drawing;
using System.Drawing.Imaging;
using ImageTools.Utilities;

namespace ImageTools.WaveletTransforms
{
    /// <summary>
    /// Supplies an implementation of the Haar wavelet and its inverse for use with `WaveletCompress`.
    /// The Haar wavelet is very simple to understand, but produces much poorer results than the CDF 9/7 implementation.
    /// </summary>
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
			BitmapTools.RunKernel(src, dst, GradientKernel);

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
			}
            //*/
		}

        public static void Forward (float[] buf, float[] x, int n, int offset, int stride) {
            int i;

            if (n <= 4) return; // haar breaks down at very small scales

            // pick out stride data into a contiguous array
            for (i = 0; i < n; i++) { x[i] = buf[i * stride + offset]; }

            // Do the Haar transform in place
            for (i = 0; i < n; i+=2) {
                var left = x[i];
                var right = x[i+1];

                var ave = (left + right) / 2;
                var diff = right - ave;
                x[i] = ave;
                x[i+1] = diff;
            }
            
            // Pack into buffer (using stride and offset)
            // The raw output is like [DC][AC][DC][AC]...
            // we want it as          [DC][DC]...[AC][AC]
            var hn = n/2;
            for (i = 0; i < hn; i++) {
                buf[i * stride + offset] = x[i*2];
                buf[(i + hn) * stride + offset] = x[1 + i * 2];
            }
        }

        public static void Inverse (float[] buf, float[] x, int n, int offset, int stride) {
            int i;

            if (n <= 4) return;
                        
            // Unpack from stride into working buffer
            // The raw input is like [DC][DC]...[AC][AC]
            // we want it as         [DC][AC][DC][AC]...
            var hn = n/2;
            for (i = 0; i < hn; i++) {
                x[i*2] = buf[i * stride + offset];
                x[1 + i * 2] = buf[(i + hn) * stride + offset];
            }

            // reverse the Haar transform
            for (i = 0; i < n; i+=2) {
                var ave = x[i];
                var diff = x[i+1];

                var right = ave + diff;
                var left = (ave * 2) - right;
                x[i] = left;
                x[i+1] = right;
            }

            
            // write back stride data
            for (i = 0; i < n; i++) { buf[i * stride + offset] = x[i]; }
        }
    }
}