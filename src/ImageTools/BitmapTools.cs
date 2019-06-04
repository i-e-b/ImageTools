using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImageTools
{
	public class BitmapTools
	{
		public unsafe delegate void Kernel32To64(byte* src, ushort* dst, BitmapData srcData, BitmapData dstData);
		public unsafe delegate void Kernel32To32(byte* src, byte* dst, BitmapData srcData, BitmapData dstData);

		public static unsafe void RunKernel(Bitmap src, Bitmap dst, Kernel32To64 kernel)
		{
			var ri = new Rectangle(Point.Empty, src.Size);
			var srcData = src.LockBits(ri, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
			try
			{
				var dstData = dst.LockBits(ri, ImageLockMode.ReadWrite, PixelFormat.Format64bppArgb);
				try
				{
					var s = (byte*)srcData.Scan0;
					var d = (ushort*)dstData.Scan0;

					kernel(s, d, srcData, dstData);
				}
				finally
				{
					dst.UnlockBits(dstData);
				}
			}
			finally
			{
				src.UnlockBits(srcData);
			}
		} 
        
        public static unsafe void RunKernel(Bitmap src, Bitmap dst, Kernel32To32 kernel)
        {
            var ri = new Rectangle(Point.Empty, src.Size);
            var srcData = src.LockBits(ri, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var dstData = dst.LockBits(ri, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                try
                {
                    var s = (byte*)srcData.Scan0;
                    var d = (byte*)dstData.Scan0;

                    kernel(s, d, srcData, dstData);
                }
                finally
                {
                    dst.UnlockBits(dstData);
                }
            }
            finally
            {
                src.UnlockBits(srcData);
            }
        }
        
        public static unsafe void YCoCgPlanes_To_ArgbImage(Bitmap dst, int offset, double[] Y, double[] Co, double[] Cg)
        {
            var ri = new Rectangle(Point.Empty, dst.Size);
            var dstData = dst.LockBits(ri, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            var len = dstData.Height * dstData.Width;
            try
            {
                var s = (uint*)dstData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    s[i] = ColorSpace.Ycocg_To_RGB32(Y[offset+i], Co[offset+i], Cg[offset+i]);
                }
            }
            finally
            {
                dst.UnlockBits(dstData);
            }
        }

        public static unsafe void ArgbImageToYCoCgPlanes(Bitmap src, out double[] Y, out double[] Co, out double[] Cg)
        {
            var ri = new Rectangle(Point.Empty, src.Size);
            var srcData = src.LockBits(ri, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var len = srcData.Height * srcData.Width;
            Y = new double[len];
            Co = new double[len];
            Cg = new double[len];
            try
            {
                var s = (uint*)srcData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    ColorSpace.RGB32_To_Ycocg(s[i], out var y, out var co, out var cg);
                    Y[i] = y;
                    Co[i] = co;
                    Cg[i] = cg;
                }
            }
            finally
            {
                src.UnlockBits(srcData);
            }
        }

        /// <summary>
        /// Copy a managed int array into a new ARGB32 bitmap image.
        /// No color transforms are made, the data must already be packed.
        /// </summary>
        public static Bitmap IntArrayToBitmap(Rectangle rct, int[] dest)
        {
            var destImage = new Bitmap(rct.Width, rct.Height, PixelFormat.Format32bppArgb);
            var bits2 = destImage.LockBits(rct, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            Marshal.Copy(dest, 0, bits2.Scan0, dest.Length);
            destImage.UnlockBits(bits2);
            return destImage;
        }

        /// <summary>
        /// Copy the data from a ARGB32 bitmap into a managed int array.
        /// No color transforms are made. The pixel data will remain packed
        /// </summary>
        public static Rectangle BitmapToIntArray(Bitmap sourceImage, out int[] source)
        {
            var rct = new Rectangle(0, 0, sourceImage.Width, sourceImage.Height);
            source = new int[rct.Width * rct.Height];
            var bits = sourceImage.LockBits(rct, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            Marshal.Copy(bits.Scan0, source, 0, source.Length);
            sourceImage.UnlockBits(bits);
            return rct;
        }
        
        
        public static unsafe void YCbCrPlanes_To_ArgbImage(Bitmap dst, int offset, double[] Y, double[] Co, double[] Cg)
        {
            var ri = new Rectangle(Point.Empty, dst.Size);
            var dstData = dst.LockBits(ri, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            var len = dstData.Height * dstData.Width;
            try
            {
                var s = (uint*)dstData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    s[i] = ColorSpace.Ycbcr_To_RGB32(Y[offset+i], Co[offset+i], Cg[offset+i]);
                }
            }
            finally
            {
                dst.UnlockBits(dstData);
            }
        }

        public static unsafe void ArgbImageToYCbCrPlanes(Bitmap src, out double[] Y, out double[] Co, out double[] Cg)
        {
            var ri = new Rectangle(Point.Empty, src.Size);
            var srcData = src.LockBits(ri, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var len = srcData.Height * srcData.Width;
            Y = new double[len];
            Co = new double[len];
            Cg = new double[len];
            try
            {
                var s = (uint*)srcData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    ColorSpace.RGB32_To_Ycbcr(s[i], out var y, out var co, out var cg);
                    Y[i] = y;
                    Co[i] = co;
                    Cg[i] = cg;
                }
            }
            finally
            {
                src.UnlockBits(srcData);
            }
        }

        
        
        public static unsafe void YUVPlanes_To_ArgbImage(Bitmap dst, int offset, double[] Y, double[] Co, double[] Cg)
        {
            var ri = new Rectangle(Point.Empty, dst.Size);
            var dstData = dst.LockBits(ri, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            var len = dstData.Height * dstData.Width;
            try
            {
                var s = (uint*)dstData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    s[i] = ColorSpace.YUV_To_RGB32(Y[offset+i], Co[offset+i], Cg[offset+i]);
                }
            }
            finally
            {
                dst.UnlockBits(dstData);
            }
        }
        
        public static unsafe void YUVPlanes_To_ArgbImage(Bitmap dst, int offset, float[] Y, float[] U, float[] V)
        {
            var ri = new Rectangle(Point.Empty, dst.Size);
            var dstData = dst.LockBits(ri, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            var len = dstData.Height * dstData.Width;
            try
            {
                var s = (uint*)dstData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    s[i] = ColorSpace.YUV_To_RGB32(Y[offset+i], U[offset+i], V[offset+i]);
                }
            }
            finally
            {
                dst.UnlockBits(dstData);
            }
        }

        public static unsafe void ArgbImageToYUVPlanes(Bitmap src, out double[] Y, out double[] U, out double[] V)
        {
            var ri = new Rectangle(Point.Empty, src.Size);
            var srcData = src.LockBits(ri, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var len = srcData.Height * srcData.Width;
            Y = new double[len];
            U = new double[len];
            V = new double[len];
            try
            {
                var s = (uint*)srcData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    ColorSpace.RGB32_To_YUV(s[i], out var y, out var u, out var v);
                    Y[i] = y;
                    U[i] = u;
                    V[i] = v;
                }
            }
            finally
            {
                src.UnlockBits(srcData);
            }
        }
        
        public static unsafe void ArgbImageToYUVPlanes_f(Bitmap src, out float[] Y, out float[] U, out float[] V)
        {
            var ri = new Rectangle(Point.Empty, src.Size);
            var srcData = src.LockBits(ri, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var len = srcData.Height * srcData.Width;
            Y = new float[len];
            U = new float[len];
            V = new float[len];
            try
            {
                var s = (uint*)srcData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    ColorSpace.RGB32_To_YUV(s[i], out var y, out var u, out var v);
                    Y[i] = (float)y;
                    U[i] = (float)u;
                    V[i] = (float)v;
                }
            }
            finally
            {
                src.UnlockBits(srcData);
            }
        }
	}
}