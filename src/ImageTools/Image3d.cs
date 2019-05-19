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

                    // data order is X,Y,Z; (data from 2nd image is entirely after first)
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        var yo = y * yspan;
                        for (int x = 0; x < bmp.Width; x++)
                        {
                            var c = bmp.GetPixel(x,y);
                            ColorSpace.CompoundToComponent(ColorSpace.RGB32_To_Ycocg32((uint) c.ToArgb()),
                                out _,  out var yv, out var cov, out var cgv);
                            var px = yo+x+zo;
                            Y[px] = yv;
                            Co[px] = cov;
                            Cg[px] = cgv;
                        }
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

            for (int y = 0; y < Height; y++)
            {
                var yo = y * yspan;
                for (int x = 0; x < Width; x++)
                {
                    var px = yo + x + zo;

                    var ycc = ColorSpace.ComponentToCompound(255.0, Y[px], Co[px], Cg[px]);
                    var rgb = ColorSpace.Ycocg32_To_RGB32(ycc);

                    dst.SetPixel(x,y, Color.FromArgb((int)rgb));
                }
            }
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