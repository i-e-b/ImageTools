using System.Drawing;
using System.Drawing.Imaging;

namespace ImageTools.Utilities
{
    /// <summary>
    /// Load image sequence into a planar 3d image
    /// </summary>
    public class Image3d
    {
        public float[] Y;
        public float[] U;
        public float[] V;

        public int yspan, zspan; // xspan is always 1

        public int Width, Depth, Height;

        public int MaxDimension;
        public int MinDimension;

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

                    BitmapTools.ArgbImageToYUVPlanes_f(bmp, out var srcY, out var srcU, out var srcV);
                    for (int i = 0; i < srcY.Length; i++)
                    {
                        Y[zo+i] = srcY[i];
                        U[zo+i] = srcU[i];
                        V[zo+i] = srcV[i];
                    }
                }
                zo += zspan;
            }
        }

        public Image3d(string[] frames, BitmapTools.TripleToTripleSpace colorSpace)
        {
            var frameCount = frames.Length;
            var zo = 0;
            foreach (var frame in frames)
            {
                using (var bmp = Load.FromFile(frame))
                {
                    if (Y == null) { InitPlanes(bmp, frameCount); }

                    BitmapTools.ImageToPlanes(bmp, colorSpace, out var srcY, out var srcU, out var srcV);
                    for (int i = 0; i < srcY.Length; i++)
                    {
                        Y[zo+i] = (float)srcY[i];
                        U[zo+i] = (float)srcU[i];
                        V[zo+i] = (float)srcV[i];
                    }
                }
                zo += zspan;
            }
        }

        public long ByteSize()
        {
            return (Y.LongLength + U.LongLength + V.LongLength) * TypeSize.Of(Y.GetType().GetElementType());
        }

        public long PixelCount() {
            return Y.LongLength;
        }

        public Image3d(int width, int height, int depth)
        {
            Width = width;
            Height = height;
            Depth =  depth;

            MaxDimension = Math.Max(Width, Math.Max(Height, Depth));
            MinDimension = Math.Min(Width, Math.Min(Height, Depth));

            var total = Width * Height * Depth;
            yspan = Width; // planar image
            zspan = yspan * Height;
            Y = new float[total];
            U = new float[total];
            V = new float[total];
        }

        /// <summary>
        /// Read a Z slice back to a 2D bitmap image.
        /// Caller should dispose.
        /// </summary>
        public Bitmap ReadSlice(int z, BitmapTools.TripleToTripleSpace? colorSpace = null)
        {
            var dst = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            var zo = z * zspan;

            if (colorSpace == null)
            {
                BitmapTools.YUVPlanes_To_ArgbImage(dst, zo, Y, U, V);
            }
            else
            {
                BitmapTools.PlanesToImage_f(dst, colorSpace, zo, Y, U, V);
            }

            return dst;
        }

        private void InitPlanes(Bitmap bmp, int frameCount)
        {
            Width = bmp.Width;
            Height = bmp.Height;
            Depth = frameCount;

            MaxDimension = Math.Max(Width, Math.Max(Height, Depth));
            MinDimension = Math.Min(Width, Math.Min(Height, Depth));

            var total = bmp.Width * bmp.Height * frameCount;
            yspan = bmp.Width; // planar image
            zspan = yspan * bmp.Height;
            Y = new float[total];
            U = new float[total];
            V = new float[total];
        }
    }
}