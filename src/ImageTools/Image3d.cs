using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace ImageTools
{
    /// <summary>
    /// Load image sequence into a planar 3d image
    /// </summary>
    public class Image3d
    {
        public double[] Y;
        public double[] Co;
        public double[] Cg;

        public int yspan, zspan; // xspan is always 1

        public int Width, Depth, Height;

        public int MaxDimension;

        /// <summary>
        /// Assumes every frame is the same size as the first
        /// </summary>
        /// <param name="frames">paths to frame images</param>
        public Image3d(string[] frames)
        {
            var frameCount = frames.Length;
            var zo = 0;
            foreach (var frame in frames)
            {
                using (var bmp = Load.FromFile(frame))
                {
                    if (Y == null) { InitPlanes(bmp, frameCount); }

                    Bitmangle.ArgbImageToYCoCgPlanes(bmp, out var srcY, out var srcCo, out var srcCg);
                    for (int i = 0; i < srcY.Length; i++)
                    {
                        Y[zo+i] = srcY[i];
                        Co[zo+i] = srcCo[i];
                        Cg[zo+i] = srcCg[i];
                    }
                }
                zo += zspan;
            }
        }

        /// <summary>
        /// Read a Z slice back to a 2D bitmap image.
        /// Caller should dispose.
        /// </summary>
        public Bitmap ReadSlice(int z)
        {
            var dst = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            var zo = z * zspan;

            Bitmangle.YCoCgPlanes_To_ArgbImage(dst, zo, Y, Co, Cg);

            return dst;
        }

        private void InitPlanes(Bitmap bmp, int frameCount)
        {
            Width = bmp.Width;
            Height = bmp.Height;
            Depth = frameCount;

            MaxDimension = Math.Max(Width, Math.Max(Height, Depth));

            var total = bmp.Width * bmp.Height * frameCount;
            yspan = bmp.Width; // planar image
            zspan = yspan * bmp.Height;
            Y = new double[total];
            Co = new double[total];
            Cg = new double[total];
        }
    }
}