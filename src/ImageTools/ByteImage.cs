﻿using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ImageTools
{
    /// <summary>
    /// Represents a 32bit 8888 ABGR image
    /// </summary>
    public class ByteImage
    {
        public byte[]? PixelBytes;
        public int RowBytes;
        public Rectangle Bounds;
        int heightLimit, widthLimit;

        public static ByteImage FromBitmap(Bitmap bmp)
        {
            if (bmp == null) throw new Exception("Invalid source");
            var result = new ByteImage {Bounds = RectOf(bmp)};
            result.heightLimit = result.Bounds.Height - 1;
            result.widthLimit = result.Bounds.Width - 1;
            var data = bmp.LockBits(result.Bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                result.PixelBytes = new byte[data.Stride*data.Height];
                result.RowBytes = data.Stride;
                Marshal.Copy(data.Scan0, result.PixelBytes, 0, result.PixelBytes.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            return result;
        }
        
        private static Rectangle RectOf(Image bmp) => new Rectangle(0,0, bmp?.Width ?? 0, bmp?.Height ?? 0);

        public void RenderOnBitmap(Bitmap bmp)
        {
            if (bmp == null || PixelBytes == null) return;
            var rect = RectOf(bmp);
            if (Different(rect, Bounds)) throw new Exception("Mismatch bitmaps");
            var bitmapData = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                Marshal.Copy(PixelBytes!, 0, bitmapData.Scan0, PixelBytes.Length);
            }
            finally
            {
                bmp.UnlockBits(bitmapData);
            }
        }

        private bool Different(Rectangle a, Rectangle b) => a.Height != b.Height || a.Width != b.Width;

        /// <summary>
        /// Set a range of pixels on the image
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSpan(PixelSpan span, byte r, byte g, byte b)
        {
            if (span == null || PixelBytes == null) return;
            if (span.Y < 0 || span.Y > heightLimit) return;
            
            var maxRight = widthLimit;
            var right = Math.Min(span.Right, maxRight);
            var left = Math.Max(span.Left, 0);
            var rowOffset = span.Y * RowBytes;
            var pixelOffset = rowOffset + left * 4; // target pixel as byte offset from base
            
            // blend left pixel
            if (span.LeftFraction != 255)
            {
                var antiFraction = 255 - span.LeftFraction;
                PixelBytes[pixelOffset + 0] = (byte) (((PixelBytes[pixelOffset + 0] * antiFraction) >> 8) + ((b * span.LeftFraction) >> 8));
                PixelBytes[pixelOffset + 1] = (byte) (((PixelBytes[pixelOffset + 1] * antiFraction) >> 8) + ((g * span.LeftFraction) >> 8));
                PixelBytes[pixelOffset + 2] = (byte) (((PixelBytes[pixelOffset + 2] * antiFraction) >> 8) + ((r * span.LeftFraction) >> 8));
                pixelOffset+=4;
                left++; // don't over-write
            }

            
            // Fill main span
            var length = right - left;
            if (length >= 8) // unrolled section for long runs
            {
                var unrolled = length >> 3; // blocks of 8
                for (int j = 0; j < unrolled; j++) // unrolled block
                {
                    left += 8;
                    PixelBytes[pixelOffset++] = b; PixelBytes[pixelOffset++] = g; PixelBytes[pixelOffset++] = r; pixelOffset++;
                    PixelBytes[pixelOffset++] = b; PixelBytes[pixelOffset++] = g; PixelBytes[pixelOffset++] = r; pixelOffset++;
                    PixelBytes[pixelOffset++] = b; PixelBytes[pixelOffset++] = g; PixelBytes[pixelOffset++] = r; pixelOffset++;
                    PixelBytes[pixelOffset++] = b; PixelBytes[pixelOffset++] = g; PixelBytes[pixelOffset++] = r; pixelOffset++;

                    PixelBytes[pixelOffset++] = b; PixelBytes[pixelOffset++] = g; PixelBytes[pixelOffset++] = r; pixelOffset++;
                    PixelBytes[pixelOffset++] = b; PixelBytes[pixelOffset++] = g; PixelBytes[pixelOffset++] = r; pixelOffset++;
                    PixelBytes[pixelOffset++] = b; PixelBytes[pixelOffset++] = g; PixelBytes[pixelOffset++] = r; pixelOffset++;
                    PixelBytes[pixelOffset++] = b; PixelBytes[pixelOffset++] = g; PixelBytes[pixelOffset++] = r; pixelOffset++;
                }
            }

            // Fill remainder after unrolled section
            for (int j = left; j <= right; j++)
            {
                PixelBytes[pixelOffset++] = b;
                PixelBytes[pixelOffset++] = g;
                PixelBytes[pixelOffset++] = r;
                pixelOffset++; // skip alpha
            }

            // Blend right pixel (over-runs the integral position)
            if (span.RightFraction != 255 && right < maxRight)
            {
                var antiFraction = 255 - span.RightFraction;
                PixelBytes[pixelOffset + 0] = (byte) (((PixelBytes[pixelOffset + 0] * antiFraction) >> 8) + ((b * span.RightFraction) >> 8));
                PixelBytes[pixelOffset + 1] = (byte) (((PixelBytes[pixelOffset + 1] * antiFraction) >> 8) + ((g * span.RightFraction) >> 8));
                PixelBytes[pixelOffset + 2] = (byte) (((PixelBytes[pixelOffset + 2] * antiFraction) >> 8) + ((r * span.RightFraction) >> 8));
            }
        }

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSpan(int y, int left, int right, byte r, byte g, byte b)
        {
            if (PixelBytes == null) return;
            if (y < 0 || y > heightLimit) return;
            
            var maxRight = widthLimit;
            right = Math.Min(right, maxRight); 
            left = Math.Max(left, 0);
            var rowOffset = y * RowBytes;
            var pixelOffset = rowOffset + left * 4; // target pixel as byte offset from base
            
            // Fill main span
            var length = right - left;
            if (length >= 8) // unrolled section for long runs
            {
                var unrolled = length >> 3; // blocks of 8
                for (int j = 0; j < unrolled; j++) // unrolled block
                {
                    left += 8;
                    PixelBytes[pixelOffset++] = b; PixelBytes[pixelOffset++] = g; PixelBytes[pixelOffset++] = r; pixelOffset++;
                    PixelBytes[pixelOffset++] = b; PixelBytes[pixelOffset++] = g; PixelBytes[pixelOffset++] = r; pixelOffset++;
                    PixelBytes[pixelOffset++] = b; PixelBytes[pixelOffset++] = g; PixelBytes[pixelOffset++] = r; pixelOffset++;
                    PixelBytes[pixelOffset++] = b; PixelBytes[pixelOffset++] = g; PixelBytes[pixelOffset++] = r; pixelOffset++;

                    PixelBytes[pixelOffset++] = b; PixelBytes[pixelOffset++] = g; PixelBytes[pixelOffset++] = r; pixelOffset++;
                    PixelBytes[pixelOffset++] = b; PixelBytes[pixelOffset++] = g; PixelBytes[pixelOffset++] = r; pixelOffset++;
                    PixelBytes[pixelOffset++] = b; PixelBytes[pixelOffset++] = g; PixelBytes[pixelOffset++] = r; pixelOffset++;
                    PixelBytes[pixelOffset++] = b; PixelBytes[pixelOffset++] = g; PixelBytes[pixelOffset++] = r; pixelOffset++;
                }
            }

            // Fill remainder after unrolled section
            for (int j = left; j <= right; j++)
            {
                PixelBytes[pixelOffset++] = b;
                PixelBytes[pixelOffset++] = g;
                PixelBytes[pixelOffset++] = r;
                pixelOffset++; // skip alpha
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPixel(int x, int y, byte r, byte g, byte b)
        {
            if (y < 0 || y > heightLimit) return;
            if (x < 0 || x > widthLimit) return;
            
            var rowOffset = y * RowBytes;
            var pixelOffset = rowOffset + x * 4; // target pixel as byte offset from base
            
            PixelBytes![pixelOffset++] = b;
            PixelBytes[pixelOffset++] = g;
            PixelBytes[pixelOffset] = r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BlendPixel(int x, int y, byte blend, byte r, byte g, byte b)
        {
            if (PixelBytes == null) return;
            if (y < 0 || y > heightLimit) return;
            if (x < 0 || x > widthLimit) return;
            
            var rowOffset = y * RowBytes;
            var pixelOffset = rowOffset + x * 4; // target pixel as byte offset from base
        
            var antiFraction = 255 - blend;
            PixelBytes[pixelOffset + 0] = (byte) (((PixelBytes[pixelOffset + 0] * antiFraction) >> 8) + ((b * blend) >> 8));
            PixelBytes[pixelOffset + 1] = (byte) (((PixelBytes[pixelOffset + 1] * antiFraction) >> 8) + ((g * blend) >> 8));
            PixelBytes[pixelOffset + 2] = (byte) (((PixelBytes[pixelOffset + 2] * antiFraction) >> 8) + ((r * blend) >> 8));
        }

        /// <summary>
        /// Return a cursor, which can be used like scanning through pointers
        /// in old C/C++ code
        /// </summary>
        public ByteImageCursor GetCursor(int x, int y)
        {
            return new ByteImageCursor(this, x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetPixel(int x, int y, out byte r, out byte g, out byte b)
        {
            y = Clamp(y, 0, heightLimit);
            x = Clamp(x, 0, widthLimit);
            
            var rowOffset = y * RowBytes;
            var pixelOffset = rowOffset + x * 4; // target pixel as byte offset from base
            
            b = PixelBytes![pixelOffset++];
            g = PixelBytes[pixelOffset++];
            r = PixelBytes[pixelOffset];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public int GetOffset(int x, int y)
        {
            var rowOffset = y * RowBytes;
            return rowOffset + x * 4; // target pixel as byte offset from base
        }
    }

    public class ByteImageCursor
    {
        private readonly ByteImage _src;
        private int _idx;
        private readonly int _maxIdx;

        public ByteImageCursor(ByteImage src, int x, int y)
        {
            _src = src;
            _idx = src!.GetOffset(x,y);
            _maxIdx = (src.RowBytes * src.Bounds.Height) - 4;
        }
        
        /// <summary>
        /// Move cursor one pixel forward. Wraps in both directions
        /// </summary>
        public void Advance(){
            
            _idx+=4;
            if (_idx > _maxIdx) _idx = 0;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(byte r, byte g, byte b)
        {
            var pixelOffset = _idx;
            _src!.PixelBytes![pixelOffset++] = b; _src!.PixelBytes[pixelOffset++] = g; _src!.PixelBytes[pixelOffset++] = r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Get(out byte r, out byte g, out byte b)
        {
            var pixelOffset = _idx;
            b = _src!.PixelBytes![pixelOffset++]; g = _src!.PixelBytes[pixelOffset++]; r = _src!.PixelBytes[pixelOffset++];
        }
    }

    public class PixelSpan
    {
        public int Right;
        public int Left;
        public int Y;
        
        public byte LeftFraction, RightFraction; // optional blending of the left-most and right-most pixels
    }
}