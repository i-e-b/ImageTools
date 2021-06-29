using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImageTools
{
    /// <summary>
    /// Represents a 24bit 888 RGB image
    /// </summary>
    public class ByteImage
    {
        public byte[] PixelBytes;
        public int RowBytes;
        public Rectangle Bounds;

        public static ByteImage FromBitmap(Bitmap bmp)
        {
            if (bmp == null) throw new Exception("Invalid source");
            var result = new ByteImage {Bounds = RectOf(bmp)};
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
    }
}