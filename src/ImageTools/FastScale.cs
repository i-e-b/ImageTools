using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace ImageTools
{
	internal unsafe delegate void FenceScale (byte* Src, byte* Dst, int SrcStart, int SrcStride, int SrcLength, int DstStart, int DstStride, int DstLength);

    /// <summary>
	/// Pure .Net scaling -- mainly just for interest.
	/// </summary>
	public static class FastScale
	{
		/// <summary>
		/// Create a rescaled copy of an image. Original image may be used as temp space
		/// Image aspect ration is always maintained
		/// </summary>
		public static Bitmap MaintainAspect(Bitmap src, int maxWidth, int maxHeight)
		{
            if (src == null) return null;
			float xScale = Math.Min(1.0f, (float)(maxWidth) / src.Width);
			float yScale = Math.Min(1.0f, (float)(maxHeight) / src.Height);

			float scale = Math.Min(xScale, yScale);
			var w = (int)(src.Width * scale);
			var h = (int)(src.Height * scale);

			return DisregardAspect(src, w, h);
		}

		/// <summary>
		/// Create a rescaled copy of an image. Original image may be used as temp space
		/// Output is exactly the height and width specified, even if it means 'squashing' the image.
		/// </summary>
		public static unsafe Bitmap DisregardAspect(Bitmap src_orig, int targetWidth, int targetHeight)
		{
			using (var src = UpscaleIfNeeded(src_orig, targetWidth, targetHeight))
			{
				var stride = src.Width * 4;
				var fmt = PixelFormat.Format32bppArgb;
				var outx = new Bitmap(Math.Max(src.Width, targetWidth), Math.Max(src.Height, targetHeight), fmt);
				var ro = new Rectangle(Point.Empty, outx.Size);
				var dstData = outx.LockBits(ro, ImageLockMode.ReadWrite, fmt);

				Bitmap final;
				try
				{
					var ri = new Rectangle(Point.Empty, src.Size);
					var srcData = src.LockBits(ri, ImageLockMode.ReadWrite, fmt);

					try
					{
						var s = (byte*)srcData.Scan0;
						var d = (byte*)dstData.Scan0;

						InterleavedScale(4, s, d, src.Width, src.Height, targetWidth, targetHeight);
					}
					finally
					{
						src.UnlockBits(srcData);
					}

					var restride = new Bitmap(targetWidth, targetHeight, stride, fmt, dstData.Scan0);
					final = new Bitmap(restride);
				}
				finally
				{
					outx.UnlockBits(dstData);
				}

				outx.Dispose();
				return final;
			}
		}

		static Bitmap UpscaleIfNeeded(Bitmap src, int targetWidth, int targetHeight)
		{
			if (src.Width >= targetWidth && src.Height >= targetHeight)
				return new Bitmap(src);

			return new Bitmap(src, targetWidth, targetHeight);
		}

		/// <summary>
		/// Scale an interleaved image
		/// </summary>
		static unsafe void InterleavedScale(int componentCount, byte* src, byte* dest, int width, int height, int targetWidth, int targetHeight)
		{
			// scale by power of two (half, quarter, double, etc.)
			var dim = new Size(width, height);
			var nextDim = new Size(width, height);
			var aBuf = src;
			var bBuf = dest;

			while (Aspect(dim.Width, targetWidth) > 2 && Aspect(dim.Height, targetHeight) > 2)
			{
				// shrink by half
				for (int i = 0; i < componentCount; i++)
				{
					nextDim = PowerOfTwoScale(aBuf, bBuf, dim.Width, width, dim.Height, targetWidth, targetHeight,
											  i, componentCount);
				}
				dim = nextDim;
				// flip buffers
				var tmp = bBuf;
				bBuf = aBuf;
				aBuf = tmp; // aBuf is last written-to buffer
			}

			// scale by less-than-power-of-two
			if (dim.Width != targetWidth || dim.Height != targetHeight)
			{
				for (int i = 0; i < componentCount; i++)
				{
					GeneralScale(RescaleFence_SMALL_DOWN, aBuf, bBuf, dim.Width, width, dim.Height, targetWidth, targetHeight, i, componentCount);
				}
				aBuf = bBuf; // aBuf is last written-to buffer
			}


			if (aBuf != dest) // if last write was to source, copy to dest
			{
				CopyBytes(aBuf, dest, width, dim.Height, componentCount); // copy all data (a bit wasteful)
			}
		}

		static unsafe Size PowerOfTwoScale(byte* Src, byte* Dst, int SrcWidth, int SrcStride, int SrcHeight, int DstWidth, int DstHeight,
			int componentIndex, int componentCount)
		{
			var outHeight = SrcHeight / 2;
			var outWidth = SrcWidth / 2;

			// down scaling
			for (int y = 0; y < SrcHeight; y++)
			{
				RescaleFence_HALF(Src, Dst,
						(y * SrcStride * componentCount) + componentIndex, // start at left of row
						componentCount, // Move 1 pixel at a time
						SrcWidth, // source length

						(y * SrcStride * componentCount) + componentIndex,
						componentCount, // Move 1 column at a time (1 pixel)
						outWidth); // dest length
			}
			for (int x = 0; x < outWidth; x++)
			{
				var pixX = (x * componentCount) + componentIndex;
				RescaleFence_HALF(Dst, Dst, // copy Src to dst, while scaling vertically
						pixX, // start at top of column
						SrcStride * componentCount, // Move 1 row at a time through source
						SrcHeight, // source length

						pixX, // start at top of column
						SrcStride * componentCount, // Move 1 row at a time in dest (equally spaced to src)
						outHeight); // dest length
			}

			return new Size(outWidth, outHeight); // TODO: only scale dimensions that need it.
		}

		static int Aspect(int width, int targetWidth)
		{
			return Math.Max(width, targetWidth) / Math.Min(width, targetWidth);
		}


		/// <summary>
		/// Rescale a single plane or component of an image.
		/// Assumes components are 1 byte each, and in XYZXYZ... order.
		/// For Planar images, call on each plane with count of 1 and index of 0.
		/// For Interleaved images (e.g. RGB), call on whole image 3 times, with count of 3 and index from 0 to 2 incl.
		/// </summary>
		static unsafe void GeneralScale(FenceScale scaler, byte* Src, byte* Dst, int SrcWidth, int SrcStride, int SrcHeight, int DstWidth, int DstHeight,
			int componentIndex, int componentCount)
		{
			// down scaling
			for (int y = 0; y < SrcHeight; y++)
			{
				scaler(Src, Dst,
						(y * SrcStride * componentCount) + componentIndex, // start at left of row
						componentCount, // Move 1 pixel at a time
						SrcWidth, // source length

						(y * SrcStride * componentCount) + componentIndex,
						componentCount, // Move 1 column at a time (1 pixel)
						DstWidth); // dest length
			}
			for (int x = 0; x < DstWidth; x++)
			{
				var pixX = (x * componentCount) + componentIndex;
				scaler(Dst, Dst, // copy Src to dst, while scaling vertically
						pixX, // start at top of column
						SrcStride * componentCount, // Move 1 row at a time through source
						SrcHeight, // source length

						pixX, // start at top of column
						SrcStride * componentCount, // Move 1 row at a time in dest (equally spaced to src)
						DstHeight); // dest length
			}
		}

		static unsafe void CopyBytes(byte* Src, byte* Dst, int SrcStride, int SrcHeight, int componentCount)
		{
			int x = (SrcHeight * SrcStride * componentCount);
			int length = (x >> 3);
			int spSt = length << 3;
			int spare = spSt + (x % 8);

			var sl = (long*)Src;
			var dl = (long*)Dst;

			for (int i = 0; i < length; i++) dl[i] = sl[i];
			for (int i = spSt; i < spare; i++) Dst[i] = Src[i];
		}

		/// <summary>
		/// Nearest-neighbour interpolation with mid-points.
		/// Fast, and reasonable quality for small changes (less halfing or doubling)
		/// </summary>
		static unsafe void RescaleFence_SMALL_DOWN(byte* Src, byte* Dst, int SrcStart, int SrcStride, int SrcLength, int DstStart, int DstStride, int DstLength)
		{
			int NumPixels = SrcLength;
			int srcw = DstLength;
			int dstw = SrcLength;
			int Mid = NumPixels >> 1;
			int o = DstStart;
			int i = SrcStart;
			int v = Src[i];
			int E = 0;

			int p = NumPixels - 1;
			while (p-- > 0)
			{
				if (E < Mid) v = (short)((v + Src[i + SrcStride]) >> 1);
				E += srcw;
				Dst[o] = (byte)v;
				i += SrcStride;

				if (E < dstw) continue;
				E -= dstw;
				o += DstStride;
				v = Src[i];
			}
			Dst[o] = (byte)v;
		}

		// Generate lookup tables
		static FastScale()
		{
			ThreeTimes = Enumerable.Range(0, 256).Select(x => x * 3).ToArray();
		}
		static readonly int[] ThreeTimes;

		/// <summary>
		/// Smooth halfing of a signal's resolution
		/// </summary>
		static unsafe void RescaleFence_HALF(byte* Src, byte* Dst, int SrcStart, int SrcStride, int SrcLength, int DstStart, int DstStride, int DstLength)
		{
			int NumPixels = SrcLength / 2;
			int o = DstStart;
			int i = SrcStart;

			int ss1 = SrcStride;
			int ss2 = SrcStride * 2;
			int ss3 = SrcStride * 3;

			unchecked
			{
				// we add 2*(byte), 2*(3*byte), so all values need (x/8), or (x >> 3)
				// first and last pixels are special cases
				Dst[o] = (byte)((Src[i] + ThreeTimes[Src[i]] + ThreeTimes[Src[i + ss1]] + Src[i + ss2]) >> 3);
				o += DstStride;
				i += ss1;

				int p = NumPixels - 2;
				while (p-- > 0)
				{
					Dst[o] = (byte)((Src[i] + ThreeTimes[Src[i + ss1]] + ThreeTimes[Src[i + ss2]] + Src[i + ss3]) >> 3);

					o += DstStride;
					i += ss2;
				}

				// first and last pixels are special cases
				Dst[o] = (byte)((Src[i] + ThreeTimes[Src[i + ss1]] + ThreeTimes[Src[i + ss2]] + Src[i + ss2]) >> 3);
			}
		}

	}
}