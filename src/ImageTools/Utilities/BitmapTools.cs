﻿using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImageTools.ImageDataFormats;
// ReSharper disable InconsistentNaming

namespace ImageTools.Utilities
{
	public class BitmapTools
	{
        public delegate void TripleToTripleSpace(double i1, double i2, double i3, out double o1, out double o2, out double o3);

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

        
        public static unsafe void YCoCgPlanes_To_ArgbImage_f(Bitmap dst, int offset, float[] Y, float[] Co, float[] Cg)
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

        public static unsafe void ArgbImageToYCoCgPlanes_f(Bitmap src, out float[] Y, out float[] Co, out float[] Cg)
        {
            var ri = new Rectangle(Point.Empty, src.Size);
            var srcData = src.LockBits(ri, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var len = srcData.Height * srcData.Width;
            Y = new float[len];
            Co = new float[len];
            Cg = new float[len];
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

        public static unsafe void ArgbImageToYUVPlanes_ForcePower2(Bitmap src,
            out float[] Y, out float[] U, out float[] V,
            out int width, out int height)
        {
            var ri = new Rectangle(Point.Empty, src.Size);
            var srcData = src.LockBits(ri, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            var srcHeight = srcData.Height;
            var srcWidth = srcData.Width;
            width = Bin.NextPow2(srcData.Width);
            height = Bin.NextPow2(srcData.Height);

            var len = height * width;

            Y = new float[len];
            U = new float[len];
            V = new float[len];
            double yv=0, u=0, v=0;
            int stride = srcData.Stride / sizeof(uint);
            try
            {
                var s = (uint*)srcData.Scan0;
                for (int y = 0; y < srcHeight; y++)
                {
                    var src_yo = stride * y;
                    var dst_yo = width * y;
                    for (int x = 0; x < srcWidth; x++)
                    {
                        var src_i = src_yo + x;
                        var dst_i = dst_yo + x;
                        ColorSpace.RGB32_To_YUV(s[src_i], out yv, out u, out v);
                        Y[dst_i] = (float)yv;
                        U[dst_i] = (float)u;
                        V[dst_i] = (float)v;
                    }
                    // Continue filling any extra space with the last sample (stops zero-ringing)
                    for (int x = srcWidth; x < width; x++)
                    {
                        var dst_i = dst_yo + x;
                        Y[dst_i] = (float)yv;
                        U[dst_i] = (float)u;
                        V[dst_i] = (float)v;
                    }
                }
                // fill any remaining rows with copies of the one above (in the planes, so we get the x-smear too)
                var end = srcHeight * width;
                for (int f = end; f < len; f++)
                {
                    Y[f] = Y[f-width];
                    U[f] = U[f-width];
                    V[f] = V[f-width];
                }
            }
            finally
            {
                src.UnlockBits(srcData);
            }
        }

        
        public static unsafe void XYZPlanes_To_ArgbImage(Bitmap dst, int offset, double[] X, double[] Y, double[] Z)
        {
            var ri = new Rectangle(Point.Empty, dst.Size);
            var dstData = dst.LockBits(ri, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            var len = dstData.Height * dstData.Width;
            try
            {
                var s = (uint*)dstData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    s[i] = ColorSpace.XYZ_To_RGB32(X[offset+i], Y[offset+i], Z[offset+i]);
                }
            }
            finally
            {
                dst.UnlockBits(dstData);
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
            if (len > Y.Length) len = Y.Length;
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

        /// <summary>
        /// fill an image from a larger set of source planes
        /// </summary>
        /// <param name="dst">Target of the copy, used as size of slice to take</param>
        /// <param name="offset">sample-wise offset in source planes</param>
        /// <param name="srcWidth">the original plane width</param>
        /// <param name="Y">Luminance plane</param>
        /// <param name="U">blue-green plane</param>
        /// <param name="V">red-yellow plane</param>
        public static unsafe void YUVPlanes_To_ArgbImage_Slice(Bitmap dst,
            int offset, int srcWidth,
            float[] Y, float[] U, float[] V)
        {
            var ri = new Rectangle(Point.Empty, dst.Size);
            var dstData = dst.LockBits(ri, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            var dstHeight = dstData.Height;
            var dstWidth = dstData.Width;
            try
            {
                int stride = dstData.Stride / sizeof(uint);
                var s = (uint*)dstData.Scan0;
                
                for (int y = 0; y < dstHeight; y++)
                {
                    var dst_yo = stride * y;
                    var src_yo = offset + (srcWidth * y);
                    for (int x = 0; x < dstWidth; x++)
                    {
                        var src_i = src_yo + x;
                        var dst_i = dst_yo + x;
                        s[dst_i] = ColorSpace.YUV_To_RGB32(Y[src_i], U[src_i], V[src_i]);
                    }       
                }
            }
            finally
            {
                dst.UnlockBits(dstData);
            }
        }

        public static unsafe void YUVPlanes_To_ArgbImage_Slice(Bitmap dst,
            int offset, int srcWidth,
            double[] Y, double[] U, double[] V)
        {
            var ri = new Rectangle(Point.Empty, dst.Size);
            var dstData = dst.LockBits(ri, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            var dstHeight = dstData.Height;
            var dstWidth = dstData.Width;
            try
            {
                int stride = dstData.Stride / sizeof(uint);
                var s = (uint*)dstData.Scan0;
                
                for (int y = 0; y < dstHeight; y++)
                {
                    var dst_yo = stride * y;
                    var src_yo = offset + (srcWidth * y);
                    for (int x = 0; x < dstWidth; x++)
                    {
                        var src_i = src_yo + x;
                        var dst_i = dst_yo + x;
                        s[dst_i] = ColorSpace.YUV_To_RGB32(Y[src_i], U[src_i], V[src_i]);
                    }       
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
        
        public static unsafe void ArgbImageToXYZPlanes(Bitmap src, out double[] X, out double[] Y, out double[] Z)
        {
            var ri = new Rectangle(Point.Empty, src.Size);
            var srcData = src.LockBits(ri, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var len = srcData.Height * srcData.Width;
            X = new double[len];
            Y = new double[len];
            Z = new double[len];
            try
            {
                var s = (uint*)srcData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    ColorSpace.RGB32_To_XYZ(s[i], out var x, out var y, out var z);
                    X[i] = x;
                    Y[i] = y;
                    Z[i] = z;
                }
            }
            finally
            {
                src.UnlockBits(srcData);
            }
        }
        
        public static unsafe void YUVPlanes_To_ArgbImage_int(Bitmap dst, int offset, int[] Y, int[] U, int[] V)
        {
            var ri = new Rectangle(Point.Empty, dst.Size);
            var dstData = dst.LockBits(ri, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            var len = dstData.Height * dstData.Width;
            if (len > Y.Length) len = Y.Length;
            try
            {
                var s = (uint*)dstData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    ColorSpace.YUV888_To_RGB32(Y[offset+i], U[offset+i], V[offset+i], out var c);
                    s[i] = c;
                }
            }
            finally
            {
                dst.UnlockBits(dstData);
            }
        }
        
        public static unsafe void ArgbImageToYUVPlanes_int(Bitmap src, out int[] Y, out int[] U, out int[] V)
        {
            var ri = new Rectangle(Point.Empty, src.Size);
            var srcData = src.LockBits(ri, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var len = srcData.Height * srcData.Width;
            Y = new int[len];
            U = new int[len];
            V = new int[len];
            try
            {
                var s = (uint*)srcData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    ColorSpace.RGB32_To_YUV888(s[i], out var y, out var u, out var v);
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

        /// <summary>
        /// Convert an image into YUV planes, with extra space for further operations
        /// </summary>
        public static unsafe void ArgbImageToYUVPlanes_Overscale(Bitmap src, int width, int height, out double[] Y, out double[] U, out double[] V)
        {
            var ri = new Rectangle(Point.Empty, src.Size);
            var srcData = src.LockBits(ri, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            
            var srcWidth = src.Width;
            var dstWidth = Math.Max(width, srcWidth);
            
            var srcHeight = src.Height;
            var dstHeight = Math.Max(height, srcHeight);
            
            var len = dstWidth * dstHeight;
            Y = new double[len];
            U = new double[len];
            V = new double[len];
            
            try
            {
                var s = (uint*)srcData.Scan0;
                for (int y = 0; y < srcHeight; y++)
                {
                    var sy = y * srcWidth;
                    var dy = y * dstWidth;
                    for (int x = 0; x < srcWidth; x++)
                    {
                        var i = x + sy;
                        var j = x + dy;
                        ColorSpace.RGB32_To_YUV(s[i], out var _y, out var _u, out var _v);
                        Y[j] = _y;
                        U[j] = _u;
                        V[j] = _v;
                    }
                }
            }
            finally
            {
                src.UnlockBits(srcData);
            }
        }

        

        public static short[] YuvPlanes_To_Rgb565(float[] Y, float[] U, float[] V,
            int srcWidth, int dstWidth, int dstHeight)
        {
            var sampleCount = dstWidth * dstHeight;
            var s = new short[sampleCount];
            int stride = srcWidth;

            for (int y = 0; y < dstHeight; y++)
            {
                var dst_yo = stride * y;
                var src_yo = srcWidth * y;
                for (int x = 0; x < dstWidth; x++)
                {
                    var src_i = src_yo + x;
                    var dst_i = dst_yo + x;
                    s[dst_i] = ColorSpace.YUV_To_RGB565(Y[src_i], U[src_i], V[src_i]);
                }
            }
            return s;
        }

        // This handles non-power-two input sizes
        public static void Rgb565_To_YuvPlanes_ForcePower2(short[] src, int srcWidth, int srcHeight,
            out float[] Y, out float[] U, out float[] V,
            out int width, out int height)
        {
            width = Bin.NextPow2(srcWidth);
            height = Bin.NextPow2(srcHeight);

            var len = height * width;

            Y = new float[len];
            U = new float[len];
            V = new float[len];
            float yv = 0, u = 0, v = 0;
            int stride = srcWidth;
            var s = src;
            for (int y = 0; y < srcHeight; y++)
            {
                var src_yo = stride * y;
                var dst_yo = width * y;
                for (int x = 0; x < srcWidth; x++)
                {
                    var src_i = src_yo + x;
                    var dst_i = dst_yo + x;
                    ColorSpace.RGB565_To_YUV(s[src_i], out yv, out u, out v);
                    Y[dst_i] = yv;
                    U[dst_i] = u;
                    V[dst_i] = v;
                }
                // Continue filling any extra space with the last sample (stops zero-ringing)
                for (int x = srcWidth; x < width; x++)
                {
                    var dst_i = dst_yo + x;
                    Y[dst_i] = yv;
                    U[dst_i] = u;
                    V[dst_i] = v;
                }
            }
            // fill any remaining rows with copies of the one above (full size, so we get the x-smear too)
            var end = srcHeight * width;
            for (int f = end; f < len; f++)
            {
                Y[f] = Y[f - width];
                U[f] = U[f - width];
                V[f] = V[f - width];
            }
        }

        public static unsafe void ArgbImageToHspPlanes(Bitmap src, out double[] Hp, out double[] Sp, out double[] Bp)
        {
            var ri = new Rectangle(Point.Empty, src.Size);
            var srcData = src.LockBits(ri, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var len = srcData.Height * srcData.Width;
            Hp = new double[len];
            Sp = new double[len];
            Bp = new double[len];
            try
            {
                var s = (uint*)srcData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    ColorSpace.RGB32_To_HSP(s[i], out var y, out var u, out var v);
                    Hp[i] = y;
                    Sp[i] = u;
                    Bp[i] = v;
                }
            }
            finally
            {
                src.UnlockBits(srcData);
            }
        }

        public static unsafe void HspPlanes_To_ArgbImage(Bitmap dst, int offset, double[] Hp, double[] Sp, double[] Pp)
        {
            var ri = new Rectangle(Point.Empty, dst.Size);
            var dstData = dst.LockBits(ri, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            var len = dstData.Height * dstData.Width;
            try
            {
                var s = (uint*)dstData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    s[i] = ColorSpace.HSP_To_RGB32((int)Hp[offset + i], (int)Sp[offset + i], (int)Pp[offset + i]);
                }
            }
            finally
            {
                dst.UnlockBits(dstData);
            }
        }

        /// <summary>
        /// Convert a bitmap image to a set of color planes, given a space conversion.
        /// Output ranges are 0..255 doubles
        /// </summary>
        public static unsafe void ImageToPlanes(Bitmap src, TripleToTripleSpace conversion, out double[] Xs, out double[] Ys, out double[] Zs)
        {
            var ri = new Rectangle(Point.Empty, src.Size);
            var srcData = src.LockBits(ri, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var len = srcData.Height * srcData.Width;
            Xs = new double[len];
            Ys = new double[len];
            Zs = new double[len];
            try
            {
                var s = (uint*)srcData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    ColorSpace.CompoundToComponent(s[i], out _, out var r, out var g, out var b);
                    conversion(r,g,b, out var x, out var y, out var z);
                    Xs[i] = x;
                    Ys[i] = y;
                    Zs[i] = z;
                }
            }
            finally
            {
                src.UnlockBits(srcData);
            }
        }

        /// <summary>
        /// Convert a bitmap image to a set of color planes, given a space conversion.
        /// Output ranges are 0..255 doubles, in UnsafeDoubleArray objects
        /// </summary>
        public static unsafe void ImageToPlanes_UA(Bitmap src, TripleToTripleSpace conversion, out UnsafeDoubleArray Xs, out UnsafeDoubleArray Ys, out UnsafeDoubleArray Zs)
        {
            var ri      = new Rectangle(Point.Empty, src.Size);
            var srcData = src.LockBits(ri, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var len     = srcData.Height * srcData.Width;
            Xs = new UnsafeDoubleArray(len);
            Ys = new UnsafeDoubleArray(len);
            Zs = new UnsafeDoubleArray(len);
            try
            {
                var s = (uint*)srcData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    ColorSpace.CompoundToComponent(s[i], out _, out var r, out var g, out var b);
                    conversion(r,g,b, out var x, out var y, out var z);
                    Xs[i] = x;
                    Ys[i] = y;
                    Zs[i] = z;
                }
            }
            finally
            {
                src.UnlockBits(srcData);
            }
        }

        /// <summary>
        /// Convert a bitmap image to a set of color planes, given a space conversion.
        /// Output ranges are 0..255 floats
        /// </summary>
        public static unsafe void ImageToPlanesf(Bitmap src, TripleToTripleSpace conversion, out float[] Xs, out float[] Ys, out float[] Zs)
        {
            var ri = new Rectangle(Point.Empty, src.Size);
            var srcData = src.LockBits(ri, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var len = srcData.Height * srcData.Width;
            Xs = new float[len];
            Ys = new float[len];
            Zs = new float[len];
            try
            {
                var s = (uint*)srcData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    ColorSpace.CompoundToComponent(s[i], out _, out var r, out var g, out var b);
                    conversion(r,g,b, out var x, out var y, out var z);
                    Xs[i] = (float)x;
                    Ys[i] = (float)y;
                    Zs[i] = (float)z;
                }
            }
            finally
            {
                src.UnlockBits(srcData);
            }
        }

        public static unsafe void PlanesToImage(Bitmap dst, TripleToTripleSpace conversion, int offset, double[] Xs, double[] Ys, double[] Zs)
        {
            var ri = new Rectangle(Point.Empty, dst.Size);
            var dstData = dst.LockBits(ri, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            var len = dstData.Height * dstData.Width;
            try
            {
                var s = (uint*)dstData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    conversion(Xs[offset + i], Ys[offset + i], Zs[offset + i], out var r, out var g, out var b);
                    s[i] = ColorSpace.ComponentToCompound(255, r, g, b);
                }
            }
            finally
            {
                dst.UnlockBits(dstData);
            }
        }


        public static unsafe void PlanesToImage_UA(Bitmap dst, TripleToTripleSpace conversion, int offset, UnsafeDoubleArray Xs, UnsafeDoubleArray Ys, UnsafeDoubleArray Zs)
        {
            var ri      = new Rectangle(Point.Empty, dst.Size);
            var dstData = dst.LockBits(ri, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            var len     = dstData.Height * dstData.Width;
            try
            {
                var s = (uint*)dstData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    var o = offset + i;
                    conversion(Xs[o], Ys[o], Zs[o], out var r, out var g, out var b);
                    s[i] = ColorSpace.ComponentToCompound(255, r, g, b);
                }
            }
            finally
            {
                dst.UnlockBits(dstData);
            }
        }
        
        public static unsafe void PlanesToImage_f(Bitmap dst, TripleToTripleSpace conversion, int offset, float[] Xs, float[] Ys, float[] Zs)
        {
            var ri = new Rectangle(Point.Empty, dst.Size);
            var dstData = dst.LockBits(ri, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            var len = dstData.Height * dstData.Width;
            try
            {
                var s = (uint*)dstData.Scan0;
                for (int i = 0; i < len; i++)
                {
                    conversion(Xs[offset + i], Ys[offset + i], Zs[offset + i], out var r, out var g, out var b);
                    s[i] = ColorSpace.ComponentToCompound(255, r, g, b);
                }
            }
            finally
            {
                dst.UnlockBits(dstData);
            }
        }
        
        public static void TranslatePlanes(TripleToTripleSpace conversion, double[] in1, double[] in2, double[] in3, out double[] out1, out double[] out2, out double[] out3)
        {
            out1 = new double[in1.Length];
            out2 = new double[in1.Length];
            out3 = new double[in1.Length];
            for (int i = 0; i < in1.Length; i++)
            {
                conversion(in1[i], in2[i], in3[i], out var a, out var b, out var c);
                out1[i]=a;
                out2[i]=b;
                out3[i]=c;
            }
        }

        public static unsafe void ImageToPlanes_ForcePower2(Bitmap src, TripleToTripleSpace conversion,
            out float[] Y, out float[] U, out float[] V,
            out int width, out int height)
        {
            if (conversion == null) throw new Exception("No data conversion");
            if (src == null) throw new Exception("No source image");
            var ri = new Rectangle(Point.Empty, src.Size);
            var srcData = src.LockBits(ri, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            var srcHeight = srcData.Height;
            var srcWidth = srcData.Width;
            width = Bin.NextPow2(srcData.Width);
            height = Bin.NextPow2(srcData.Height);

            var len = height * width;

            Y = new float[len];
            U = new float[len];
            V = new float[len];
            double yv=0, u=0, v=0;
            int stride = srcData.Stride / sizeof(uint);
            try
            {
                int src_yo;
                var s = (uint*)srcData.Scan0;
                for (int y = 0; y < srcHeight; y++)
                {
                    src_yo = stride * y;
                    var dst_yo = width * y;
                    int src_i;
                    for (int x = 0; x < srcWidth; x++)
                    {
                        src_i = src_yo + x;
                        var dst_i = dst_yo + x;
                        ColorSpace.CompoundToComponent(s[src_i], out _, out var r, out var g, out var b);
                        conversion(r,g,b, out yv, out u, out v);
                        Y[dst_i] = (float)yv;
                        U[dst_i] = (float)u;
                        V[dst_i] = (float)v;
                    }
                    // Continue filling any extra space with a mirror image (reduces ringing)
                    src_i = dst_yo + srcWidth - 1;
                    for (int x = srcWidth; x < width; x++)
                    {
                        var dst_i = dst_yo + x;
                        Y[dst_i] = Y[src_i];
                        U[dst_i] = U[src_i];
                        V[dst_i] = V[src_i];
                        //src_i--; // un-comment for mirror. Comment for smear
                    }
                }
                // fill any remaining rows with copies of the one above (in the planes, so we get the x-smear too)
                src_yo = (srcHeight - 1) * width;
                for (int y = srcHeight; y < height; y++)
                {
                    var dst_yo = width * y;
                    //src_yo -= width; // un-comment for mirror. Comment for smear
                    for (int x = 0; x < width; x++)
                    {
                        var dst_i = dst_yo + x;
                        var src_i = src_yo + x;
                        
                        Y[dst_i] = Y[src_i];
                        U[dst_i] = U[src_i];
                        V[dst_i] = V[src_i];
                    }
                }
            }
            finally
            {
                src.UnlockBits(srcData);
            }
        }

        public static unsafe void PlanesToImage_Slice(Bitmap dst, TripleToTripleSpace conversion,
            int offset, int srcWidth,
            float[] Y, float[] U, float[] V)
        {
            var ri = new Rectangle(Point.Empty, dst.Size);
            var dstData = dst.LockBits(ri, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            var dstHeight = dstData.Height;
            var dstWidth = dstData.Width;
            try
            {
                int stride = dstData.Stride / sizeof(uint);
                var s = (uint*)dstData.Scan0;
                
                for (int y = 0; y < dstHeight; y++)
                {
                    var dst_yo = stride * y;
                    var src_yo = offset + (srcWidth * y);
                    for (int x = 0; x < dstWidth; x++)
                    {
                        var src_i = src_yo + x;
                        var dst_i = dst_yo + x;
                        conversion(Y[src_i], U[src_i], V[src_i], out var r, out var g, out var b);
                        s[dst_i] = ColorSpace.ComponentToCompound(255,r,g,b);
                    }       
                }
            }
            finally
            {
                dst.UnlockBits(dstData);
            }
        }
    }
}