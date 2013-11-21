using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace ImageTools
{
	/// <summary>
	/// Pure .Net scaling -- mainly just for interest.
	/// </summary>
	public static class FastScale
	{


		public static Bitmap MaintainAspect(Bitmap src, int maxWidth, int maxHeight)
		{
			return null;
		}
		/// <summary>
		/// Create a rescaled copy of an image.
		/// Output is exactly the height and width specified, even if it means 'squashing' the image.
		/// </summary>
		public static unsafe Bitmap DisregardAspect(Bitmap src, int targetWidth, int targetHeight)
		{
			var stride = src.Width * 4;
			var fmt = PixelFormat.Format32bppArgb;

			var outx = new Bitmap(src.Width, src.Height, fmt);
			var ro = new Rectangle(Point.Empty, outx.Size);
			var dstData = outx.LockBits(ro, ImageLockMode.ReadWrite, fmt);
			
			Bitmap final;
			try
			{
				var ri = new Rectangle(Point.Empty, src.Size);
				var srcData = src.LockBits(ri, ImageLockMode.ReadOnly, fmt);

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

		/// <summary>
		/// Scale an interleaved image
		/// </summary>
		static unsafe void InterleavedScale(int componentCount, byte* src, byte* dest, int width, int height, int targetWidth, int targetHeight)
		{			
			// scale by power of two (half, quarter, double, etc.)
			if (Aspect(width, targetWidth) >= 2 && Aspect(height, targetHeight) >= 2)
			{
				var dim = new Size(width, height);
				// shrink by half
				for (int i = 0; i < componentCount; i++)
				{
					dim = PowerOfTwoScale(src, dest, width, height, targetWidth, targetHeight, i, componentCount);
				}
				// flip buffers back
				CopyBytes(src, dest, width, height);
				// recurse until less than power-of-two
				InterleavedScale(componentCount, dest, src, dim.Width, dim.Height, targetWidth, targetHeight);
			}

			// scale by less-than-power-of-two
			for (int i = 0; i < componentCount; i++)
			{
				GeneralScale(src, dest, width, height, targetWidth, targetHeight, i, componentCount);
			}
		}

		static unsafe Size PowerOfTwoScale(byte* Src, byte* Dst, int SrcWidth, int SrcHeight, int DstWidth, int DstHeight,
			int componentIndex, int componentCount)
		{
			if (DstWidth > SrcWidth || DstHeight > SrcHeight) 
				throw new NotImplementedException("Upscale is not yet implemented");

			// down scaling
			for (int y = 0; y < SrcHeight - componentCount; y++)
			{
				RescaleFence_HALF(Src, Dst,
						(y * SrcWidth * componentCount) + componentIndex, // start at left of row
						componentCount, // Move 1 pixel at a time
						SrcWidth * componentCount, // source length

						(y * SrcWidth * componentCount) + componentIndex,
						componentCount, // Move 1 column at a time (1 pixel)
						DstWidth * componentCount); // dest length
			}
			for (int x = 0; x < DstWidth; x++)
			{
				var pixX = (x * componentCount) + componentIndex;
				RescaleFence_HALF(Dst, Dst, // copy Src to dst, while scaling vertically
						pixX, // start at top of column
						SrcWidth * componentCount, // Move 1 row at a time through source
						SrcHeight, // source length

						pixX, // start at top of column
						SrcWidth * componentCount, // Move 1 row at a time in dest (equally spaced to src)
						DstHeight + componentCount); // dest length
			}
		}

		static int Aspect(int width, int targetWidth)
		{
			return Math.Max(width,targetWidth) / Math.Min(width, targetWidth);
		}


		/// <summary>
		/// Rescale a single plane or component of an image.
		/// Assumes components are 1 byte each, and in XYZXYZ... order.
		/// For Planar images, call on each plane with count of 1 and index of 0.
		/// For Interleaved images (e.g. RGB), call on whole image 3 times, with count of 3 and index from 0 to 2 incl.
		/// </summary>
		static unsafe void GeneralScale(byte* Src, byte* Dst, int SrcWidth, int SrcHeight, int DstWidth, int DstHeight,
			int componentIndex, int componentCount)
		{
			if (SrcHeight == DstHeight && SrcWidth == DstHeight)
			{
				// Exactly the same size. Just copy.
				CopyBytes(Src, Dst, SrcWidth, SrcHeight);
				return;
			}

			if (SrcHeight > DstHeight && SrcWidth > DstWidth)
			{
				// down scaling
				for (int y = 0; y < SrcHeight - componentCount; y++)
				{
					RescaleFence_SMALL_DOWN(Src, Dst,
							(y * SrcWidth * componentCount) + componentIndex, // start at left of row
							componentCount, // Move 1 pixel at a time
							SrcWidth * componentCount, // source length

							(y * SrcWidth * componentCount) + componentIndex, 
							componentCount, // Move 1 column at a time (1 pixel)
							DstWidth * componentCount); // dest length
				}
				for (int x = 0; x < DstWidth; x++)
				{
					var pixX = (x * componentCount) + componentIndex;
					RescaleFence_SMALL_DOWN(Dst, Dst, // copy Src to dst, while scaling vertically
							pixX, // start at top of column
							SrcWidth * componentCount, // Move 1 row at a time through source
							SrcHeight, // source length

							pixX, // start at top of column
							SrcWidth * componentCount, // Move 1 row at a time in dest (equally spaced to src)
							DstHeight + componentCount); // dest length
				}
			}
			else
			{
				throw new NotImplementedException("Upscaling isn't done yet");
			}

		}

		static unsafe void CopyBytes(byte* Src, byte* Dst, int SrcWidth, int SrcHeight)
		{
			int length = SrcHeight*SrcWidth;
			for (int i = 0; i < length; i++) Dst[i] = Src[i];
		}

		/// <summary>
		/// Nearest-neighbour interpolation with mid-points.
		/// Fast, and reasonable quality for small changes (less halfing or doubling)
		/// </summary>
		static unsafe void RescaleFence_SMALL_DOWN(byte* Src, byte* Dst, int SrcStart, int SrcStride, int SrcLength, int DstStart, int DstStride, int DstLength)
		{
			int NumPixels = SrcLength;
			int E = 0;
			int srcw = DstLength;
			int dstw = SrcLength;
			int Mid = NumPixels >> 1;
			int o = DstStart;
			int i = SrcStart;
			int v = Src[i];

			int p = NumPixels - 1;
			while (p-- > 0)
			{
				if (E < Mid)
				{ // do interpolation
					v = (short)((v + Src[i + SrcStride]) >> 1);
				}
				E += srcw;
				Dst[o] = (byte)v;
				i += SrcStride;

				if (E < dstw) continue;
				E -= dstw;
				o += DstStride;
				v = Src[i];
			}
		}

		/// <summary>
		/// Smooth (ish) halfing of a signal's resolution
		/// </summary>
		static unsafe void RescaleFence_HALF(byte* Src, byte* Dst, int SrcStart, int SrcStride, int SrcLength, int DstStart, int DstStride, int DstLength)
		{
			int NumPixels = SrcLength;
			int srcw = DstLength;
			int dstw = SrcLength;
			int o = DstStart;
			int i = SrcStart;
			int v = Src[i];

			int p = NumPixels - 1;
			while (p-- > 0)
			{
				Dst[o] = (byte)v;
				i += SrcStride;

				// todo: implement!
				//http://johncostella.webs.com/magic/

				o += DstStride;
				v = Src[i];
			}
		}
	}
}