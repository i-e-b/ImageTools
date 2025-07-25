﻿using System.Drawing;
using System.Drawing.Imaging;
using ImageTools.AnalyticalTransforms;
using ImageTools.Utilities;
using ImageTools.WaveletTransforms;

namespace ImageTools
{
	public static class Blur
	{
		/// <summary>
		/// Blur a bitmap into a new bitmap (original is unchanged)
		/// <para></para>
		/// This uses a lookup table to do a fast box blur at arbitrary radius
		/// </summary>
		public static Bitmap FastBlur(Bitmap sourceImage, int radius)
		{
			unchecked
			{
                if (radius < 1) return sourceImage;

                var rct = BitmapTools.BitmapToIntArray(sourceImage, out var source);
                var dest = new int[rct.Width * rct.Height];

				int w = rct.Width;
				int h = rct.Height;
				int wm = w - 1;
				int hm = h - 1;
				int wh = w * h;
				int div = radius + radius + 1;
				var r = new int[wh];
				var g = new int[wh];
				var b = new int[wh];
				int rsum, gsum, bsum, x, y, i, p1, p2, yi;
				var vmin = new int[max(w, h)];
				var vmax = new int[max(w, h)];

				var dv = new int[256 * div];
				for (i = 0; i < 256 * div; i++)
				{
					dv[i] = (i / div);
				}

				int yw = yi = 0;

				for (y = 0; y < h; y++)
				{
					// blur horizontal
					rsum = gsum = bsum = 0;
					for (i = -radius; i <= radius; i++)
					{
						int p = source[yi + min(wm, max(i, 0))];
						rsum += (p & 0xff0000) >> 16;
						gsum += (p & 0x00ff00) >> 8;
						bsum += p & 0x0000ff;
					}
					for (x = 0; x < w; x++)
					{

						r[yi] = dv[rsum];
						g[yi] = dv[gsum];
						b[yi] = dv[bsum];

						if (y == 0)
						{
							vmin[x] = min(x + radius + 1, wm);
							vmax[x] = max(x - radius, 0);
						}
						p1 = source[yw + vmin[x]];
						p2 = source[yw + vmax[x]];

						rsum += ((p1 & 0xff0000) - (p2 & 0xff0000)) >> 16;
						gsum += ((p1 & 0x00ff00) - (p2 & 0x00ff00)) >> 8;
						bsum += (p1 & 0x0000ff) - (p2 & 0x0000ff);
						yi++;
					}
					yw += w;
				}

				for (x = 0; x < w; x++)
				{
					// blur vertical
					rsum = gsum = bsum = 0;
					int yp = -radius * w;
					for (i = -radius; i <= radius; i++)
					{
						yi = max(0, yp) + x;
						rsum += r[yi];
						gsum += g[yi];
						bsum += b[yi];
						yp += w;
					}
					yi = x;
					for (y = 0; y < h; y++)
					{
						dest[yi] = (int)(0xff000000u | (uint)(dv[rsum] << 16) | (uint)(dv[gsum] << 8) | (uint)dv[bsum]);
						if (x == 0)
						{
							vmin[y] = min(y + radius + 1, hm) * w;
							vmax[y] = max(y - radius, 0) * w;
						}
						p1 = x + vmin[y];
						p2 = x + vmax[y];

						rsum += r[p1] - r[p2];
						gsum += g[p1] - g[p2];
						bsum += b[p1] - b[p2];

						yi += w;
					}
				}

				// copy back to image
                return BitmapTools.IntArrayToBitmap(rct, dest);
			}
		}

        /// <summary>
		/// Blur a bitmap into a new bitmap (original is unchanged)
        /// <para></para>
        /// This uses bit-shifts to do a box blur at fixed radius intervals (power-of-two)
        /// This is not as accurate as 'FastBlur', but it uses less memory, and is much faster
        /// at very large blur radiuses
		/// </summary>
		public static Bitmap ShiftBlur(Bitmap sourceImage, int radius)
		{
			unchecked
			{
                if (radius < 1) return sourceImage;

				var rct = BitmapTools.BitmapToIntArray(sourceImage, out var source);
                var dest = new int[rct.Width * rct.Height];

                radius = (int)Bin.NextPow2((uint)radius);
                int shift = (int)Math.Log(radius * 2, 2);

                int w = rct.Width;
				int h = rct.Height;
				int wm = w - 1;
				int hm = h - 1;
				int wh = w * h;
				var r = new int[wh];
				var g = new int[wh];
				var b = new int[wh];
				int rsum, gsum, bsum, x, y, i, p1, p2, yi;
				var vmin = new int[max(w, h)];
				var vmax = new int[max(w, h)];

				int yw = yi = 0;

				for (y = 0; y < h; y++)
				{
					// blur horizontal
					rsum = gsum = bsum = 0;
					for (i = -radius; i <= radius; i++)
					{
						int p = source[yi + min(wm, max(i, 0))];
						rsum += (p & 0xff0000) >> 16;
						gsum += (p & 0x00ff00) >> 8;
						bsum += p & 0x0000ff;
					}
					for (x = 0; x < w; x++)
					{

						r[yi] = rsum >> shift;
						g[yi] = gsum >> shift;
						b[yi] = bsum >> shift;

						if (y == 0)
						{
							vmin[x] = min(x + radius + 1, wm);
							vmax[x] = max(x - radius, 0);
						}
						p1 = source[yw + vmin[x]];
						p2 = source[yw + vmax[x]];

						rsum += ((p1 & 0xff0000) - (p2 & 0xff0000)) >> 16;
						gsum += ((p1 & 0x00ff00) - (p2 & 0x00ff00)) >> 8;
						bsum += (p1 & 0x0000ff) - (p2 & 0x0000ff);
						yi++;
					}
					yw += w;
				}

				for (x = 0; x < w; x++)
				{
					// blur vertical
					rsum = gsum = bsum = 0;
					int yp = -radius * w;
					for (i = -radius; i <= radius; i++)
					{
						yi = max(0, yp) + x;
						rsum += r[yi];
						gsum += g[yi];
						bsum += b[yi];
						yp += w;
					}
					yi = x;
					for (y = 0; y < h; y++)
					{
                        var rv = clip(rsum >> shift);
                        var gv = clip(gsum >> shift);
                        var bv = clip(bsum >> shift);
						dest[yi] = (int)(0xff000000u | (uint)(rv<< 16) | (uint)(gv << 8) | (uint)bv);
						if (x == 0)
						{
							vmin[y] = min(y + radius + 1, hm) * w;
							vmax[y] = max(y - radius, 0) * w;
						}
						p1 = x + vmin[y];
						p2 = x + vmax[y];

						rsum += r[p1] - r[p2];
						gsum += g[p1] - g[p2];
						bsum += b[p1] - b[p2];

						yi += w;
					}
				}

				// copy back to image
                return BitmapTools.IntArrayToBitmap(rct, dest);
			}
		}

        /// <summary>
        /// Apply a soft-focus effect into a new bitmap (original is unchanged).
        /// NOTE: This currently assumes a square power-of-two sized source image
        /// <para></para>
        /// This is done by a wavelet transform where the high-frequency coefficients
        /// are reduced but not removed
        /// </summary>
        public static Bitmap SoftFocus(Bitmap sourceImage)
        {
            // Blur factors (these are specific to make the soft-focus effect)
            var factors = new[] { 1, 2, 3 };

            if (sourceImage == null) return null;
            BitmapTools.ArgbImageToYUVPlanes_f(sourceImage, out var Y, out var U, out var V);

            var buffers = new[] { Y, U, V };
            int rounds = (int)Math.Log(sourceImage.Width, 2);
            Console.WriteLine(rounds);

            foreach (var buffer in buffers)
            {
                // DC to AC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] -= 127.5f; }

                // Transform
                for (int i = 0; i < rounds; i++)
                {
                    var height = sourceImage.Height >> i;
                    var width = sourceImage.Width >> i;

                    var hx = new float[height];
                    var wx = new float[width];

                    // Wavelet decompose vertical
                    for (int x = 0; x < width; x++) // each column
                    {
                        CDF.Fwt97(buffer, hx, height, x, sourceImage.Width);
                    }

                    // Wavelet decompose HALF horizontal
                    for (int y = 0; y < height / 2; y++) // each row
                    {
                        CDF.Fwt97(buffer, wx, width, y * sourceImage.Width, 1);
                    }
                }

                // this puts coefficients in frequency order
                WaveletCompress.ToStorageOrder2D(buffer, sourceImage.Width, sourceImage.Height, rounds, sourceImage.Width, sourceImage.Height);

                // Reduce coefficients
                for (int r = 0; r < rounds; r++)
                {
                    float factor = (r >= factors.Length) ? factors[factors.Length - 1] : factors[r];
                    factor = 1 / factor;

                    var len = buffer.Length >> r;
                    for (int i = len >> 1; i < len; i++)
                    {
                        buffer[i] *= factor; // reduce
                    }
                }

                // Restore
                WaveletCompress.FromStorageOrder2D(buffer, sourceImage.Width, sourceImage.Height, rounds, sourceImage.Width, sourceImage.Height);
                
                // Restore
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var height = sourceImage.Height >> i;
                    var width = sourceImage.Width >> i;

                    var hx = new float[height];
                    var wx = new float[width];

                    // Wavelet restore HALF horizontal
                    for (int y = 0; y < height / 2; y++) // each row
                    {
                        CDF.Iwt97(buffer, wx, width, y * sourceImage.Width, 1);
                    }

                    // Wavelet restore vertical
                    for (int x = 0; x < width; x++) // each column
                    {
                        CDF.Iwt97(buffer, hx, height, x, sourceImage.Width);
                    }
                }
                
                // AC to DC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5f; }
            }

            var dest = new Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format32bppArgb);
            BitmapTools.YUVPlanes_To_ArgbImage(dest, 0, Y, U, V);
            return dest;
        }

        public static Bitmap LowpassBlur(Bitmap sourceImage, int radius)
        {
	        if (sourceImage == null) return null;
	        BitmapTools.ArgbImageToYUVPlanes_f(sourceImage, out var Y, out var U, out var V);
	        
	        var height = sourceImage.Height;
	        var width = sourceImage.Width;
	        
	        var resonance = (float) Math.Sqrt(2);
	        var fY = new ButterworthBandpass(0.25f, radius, ButterworthBandpass.PassType.Lowpass, resonance);
	        var fU = new ButterworthBandpass(0.25f, radius, ButterworthBandpass.PassType.Lowpass, resonance);
	        var fV = new ButterworthBandpass(0.25f, radius, ButterworthBandpass.PassType.Lowpass, resonance);
	        
	        // lowpass in X
	        for (int y = 0; y < height; y++)
	        {
		        var oy = y * width;
		        fY.Prime(Y[oy]); fU.Prime(U[oy]); fV.Prime(V[oy]);
		        
		        for (int x = 0; x < width; x++)
		        {
			        var i = oy + x;
			        Y[i] = fY.Update(Y[i]);
			        U[i] = fU.Update(U[i]);
			        V[i] = fV.Update(V[i]);
		        }
	        }
	        
	        // lowpass in Y
			for (int x = 0; x < width; x++)
	        {
		        fY.Prime(Y[x]); fU.Prime(U[x]); fV.Prime(V[x]);
		        
		        for (int y = 0; y < height; y++)
		        {
			        var i = y*width + x;
			        Y[i] = fY.Update(Y[i]);
			        U[i] = fU.Update(U[i]);
			        V[i] = fV.Update(V[i]);
		        }
	        }
	        
	        var dest = new Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format32bppArgb);
	        BitmapTools.YUVPlanes_To_ArgbImage(dest, 0, Y, U, V);
	        return dest;
        }
        
        /// <summary>
        /// Apply a hard-focus effect into a new bitmap (original is unchanged).
        /// NOTE: This currently assumes a square power-of-two sized source image
        /// <para></para>
        /// This is done by a wavelet transform where the low-frequency coefficients
        /// are reduced but not removed
        /// </summary>
        public static Bitmap Sharpen(Bitmap sourceImage)
        {
            // These are the parameters of the transform:
            var factors = new[] { 1, 2, 3, 2, 1 };

            if (sourceImage == null) return null;
            BitmapTools.ArgbImageToYUVPlanes_f(sourceImage, out var Y, out var U, out var V);

            var buffers = new[] { Y, U, V };
            int rounds = (int)Math.Log(sourceImage.Width, 2);
            Console.WriteLine(rounds);

            foreach (var buffer in buffers)
            {
                // DC to AC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] -= 127.5f; }

                // Transform
                for (int i = 0; i < rounds; i++)
                {
                    var height = sourceImage.Height >> i;
                    var width = sourceImage.Width >> i;

                    var hx = new float[height];
                    var wx = new float[width];

                    // Wavelet decompose vertical
                    for (int x = 0; x < width; x++) // each column
                    {
                        CDF.Fwt97(buffer, hx, height, x, sourceImage.Width);
                    }

                    // Wavelet decompose HALF horizontal
                    for (int y = 0; y < height / 2; y++) // each row
                    {
                        CDF.Fwt97(buffer, wx, width, y * sourceImage.Width, 1);
                    }
                }

                // this puts coefficients in frequency order
                WaveletCompress.ToStorageOrder2D(buffer, sourceImage.Width, sourceImage.Height, rounds, sourceImage.Width, sourceImage.Height);

                // expand coefficients.
                for (int r = 0; r < rounds; r++)
                {
                    float factor = (r >= factors.Length) ? factors[factors.Length - 1] : factors[r];

                    var len = buffer.Length >> r;
                    for (int i = len >> 1; i < len; i++)
                    {
                        buffer[i] *= factor; // reduce
                    }
                }

                // Restore
                WaveletCompress.FromStorageOrder2D(buffer, sourceImage.Width, sourceImage.Height, rounds, sourceImage.Width, sourceImage.Height);
                
                // Restore
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var height = sourceImage.Height >> i;
                    var width = sourceImage.Width >> i;

                    var hx = new float[height];
                    var wx = new float[width];

                    // Wavelet restore HALF horizontal
                    for (int y = 0; y < height / 2; y++) // each row
                    {
                        CDF.Iwt97(buffer, wx, width, y * sourceImage.Width, 1);
                    }

                    // Wavelet restore vertical
                    for (int x = 0; x < width; x++) // each column
                    {
                        CDF.Iwt97(buffer, hx, height, x, sourceImage.Width);
                    }
                }
                
                // AC to DC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5f; }
            }

            var dest = new Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format32bppArgb);
            BitmapTools.YUVPlanes_To_ArgbImage(dest, 0, Y, U, V);
            return dest;
        }


        /// <summary>
        /// Apply a matrix kernel to a bitmap, output into a new bitmap (original is unchanged).
        /// Edges are handled by reflection
        /// </summary>
        /// <param name="sourceImage">Source bitmap</param>
        /// <param name="srcColorSpace">Color space for calculation</param>
        /// <param name="dstColorSpace">Color space for output</param>
        /// <param name="kernel">convolution kernel</param>
        public static Bitmap Convolve(Bitmap sourceImage, BitmapTools.TripleToTripleSpace srcColorSpace, BitmapTools.TripleToTripleSpace dstColorSpace, double[,] kernel)
        {
	        var kWidth = kernel.GetLength(0);
	        var kHeight = kernel.GetLength(1);
	        if (kWidth < 1 || kHeight < 1) throw new Exception("Invalid kernel");
	        
	        BitmapTools.ImageToPlanes(sourceImage, srcColorSpace, out var srcX, out var srcY, out var srcZ);
	        var width = sourceImage.Width;
	        var height = sourceImage.Height;
	        
	        var dstX = new double[srcX.Length];
	        var dstY = new double[srcY.Length];
	        var dstZ = new double[srcZ.Length];
	        
	        var sourcePlanes = new []{srcX,srcY,srcZ};
	        var destPlanes = new []{dstX,dstY,dstZ};
	        
	        var kXOffsets = KernelSampleOffsets(kWidth);
	        var kYOffsets = KernelSampleOffsets(kHeight);
	        
	        var wLimit = width - 1;
	        var hLimit = height - 1;
	        
	        for (int planeNum = 0; planeNum < sourcePlanes.Length; planeNum++)
	        {
		        var src = sourcePlanes[planeNum];
		        var dst = destPlanes[planeNum];

		        for (int y = 0; y < height; y++)
		        for (int x = 0; x < width; x++)
		        {
			        var idx = y*width + x;
			        var sum = 0.0;
			        
			        // x,y is the source and dest position
			        // need to scan the kernel reading the values

					for (int kY = 0; kY < kHeight; kY++)
			        for (int kX = 0; kX < kWidth; kX++)
			        {
				        var sx = x - kXOffsets[kX];
				        var sy = y - kYOffsets[kY];
				        
				        // reflect if sample off edge
				        if (sx < 0) sx = -sx;
				        else if (sx > wLimit) sx = wLimit - (sx - wLimit); // w=256, x=256->254;  256 - 255=1; 255-1=254
				        if (sy < 0) sy = -sy;
				        else if (sy > hLimit) sy = hLimit - (sy - hLimit);
				        
				        sum += kernel[kX,kY] * src[sy*width + sx];
			        }
			        
			        dst[idx] = sum;
		        }
	        }
	        
	        var destImage = new Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format32bppArgb);
	        BitmapTools.PlanesToImage(destImage, dstColorSpace, 0, dstX, dstY, dstZ);
	        return destImage;
        }

        /// <summary>
        /// Calculate source sample offsets for kernel
        /// </summary>
        public static int[] KernelSampleOffsets(int size)
        {
	        // For odd-widths:
	        //   -1    idx    +1    <- src
	        //  s[0]  s[1]   s[2]
	        //         idx          <- dst
	        // For even-widths:
	        //   -1           +1    <- src
	        //  s[0]         s[1]
	        //         idx          <- dst
			        
			        
	        // 5-wide:  0=-2; 1=-1; 2= 0; 3= 1; 4= 2;    (4/2 -> 2) Half=2.5
	        // 4-wide:  0=-2; 1=-1; 2= 1; 3= 2;          (3/2 -> 1) Half=2
	        // 3-wide:  0=-1; 1= 0; 2=+1;   Half = 1.5;; 1 (2/2)
	        // 2-wide:  0=-1; 1= 1;         Half = 1.0;; 0 (1/2)
	        
	        var half = size / 2;
	        var end = size - 1;
	        var offsets = new int[size];

	        for (int i = 0; i < half; i++)
	        {
		        offsets[i] = -half + i;
		        offsets[end - i] = half - i;
	        }
	        
	        return offsets;
        }

        private static int clip(int v)
        {
            if (v > 255) return 255;
            if (v < 0) return 0;
            return v;
        }
        private static int min(int a, int b) { return Math.Min(a, b); }
		private static int max(int a, int b) { return Math.Max(a, b); }
	}
}
