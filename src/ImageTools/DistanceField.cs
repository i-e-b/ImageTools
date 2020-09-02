using System;
using System.Drawing;
using System.Linq;
using ImageTools.AnalyticalTransforms;
using ImageTools.Utilities;

namespace ImageTools
{
    public class DistanceField
    {
        /// <summary>
        /// Measure signed distance to shape edges.
        /// Dark pixel are considered 'inside' (negative).
        /// </summary>
        public static int[,] HorizontalDistance(Bitmap bmp)
        {
            if (bmp == null || bmp.Width < 1 || bmp.Height < 1) return new int[0, 0];

            var outp = new int[bmp.Width, bmp.Height];

            // each row can be calculated separately
            for (int y = 0; y < bmp.Height; y++)
            {
                // we scan one way, and count up the distance, resetting when we cross a border.
                // we then scan the other, and keep the one closest to zero.
                var inside = bmp.GetPixel(0, y).GetBrightness() <= 0.5f;
                var dist = inside ? -bmp.Width : bmp.Width;

                for (int x = 0; x < bmp.Width; x++)
                {
                    var pixIn = bmp.GetPixel(x, y).GetBrightness() <= 0.5f;
                    if (pixIn != inside) dist = 0;
                    inside = pixIn;
                    dist += inside ? -1 : 1; // avoid zero, so we always have a value crossing

                    outp[x, y] = dist;
                }

                // now opposite way. The right column value should be correct as possible now.
                for (int x = bmp.Width - 1; x >= 0; x--)
                {
                    var pixIn = bmp.GetPixel(x, y).GetBrightness() <= 0.5f;
                    if (pixIn != inside) dist = 0;
                    inside = pixIn;
                    dist += inside ? -1 : 1; // avoid zero, so we always have a value crossing

                    if (Math.Abs(dist) < Math.Abs(outp[x, y])) outp[x, y] = dist;
                }
            }

            return outp;
        }

        public static int[,] VerticalDistance(Bitmap bmp)
        {
            if (bmp == null || bmp.Width < 1 || bmp.Height < 1) return new int[0, 0];

            var outp = new int[bmp.Width, bmp.Height];

            // each row can be calculated separately
            for (int x = 0; x < bmp.Width; x++)
            {
                // we scan one way, and count up the distance, resetting when we cross a border.
                // we then scan the other, and keep the one closest to zero.
                var inside = bmp.GetPixel(x, 0).GetBrightness() <= 0.5f;
                var dist = inside ? -bmp.Height : bmp.Height;

                for (int y = 0; y < bmp.Height; y++)
                {
                    var pixIn = bmp.GetPixel(x, y).GetBrightness() <= 0.5f;
                    if (pixIn != inside) dist = 0;
                    inside = pixIn;
                    dist += inside ? -1 : 1; // avoid zero, so we always have a value crossing

                    outp[x, y] = dist;
                }

                // now opposite way. The right column value should be correct as possible now.
                for (int y = bmp.Height - 1; y >= 0; y--)
                {
                    var pixIn = bmp.GetPixel(x, y).GetBrightness() <= 0.5f;
                    if (pixIn != inside) dist = 0;
                    inside = pixIn;
                    dist += inside ? -1 : 1; // avoid zero, so we always have a value crossing

                    if (Math.Abs(dist) < Math.Abs(outp[x, y])) outp[x, y] = dist;
                }
            }

            return outp;
        }

        public static Bitmap RenderFieldToImage(int[,] field)
        {
            if (field == null || field.Length < 4) return null;

            var width = field.GetLength(0);
            var height = field.GetLength(1);
            var bmp = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var v = field[x, y].Pin(-255, 255);
                    if (v < 0)
                    {
                        bmp.SetPixel(x, y, Color.FromArgb(0, 0, 255 + v));
                    }
                    else
                    {
                        bmp.SetPixel(x, y, Color.FromArgb(0, v, 0));
                    }
                }
            }

            return bmp;
        }

        public static Bitmap RenderFieldToImage(int[,] horzField, int[,] vertField)
        {
            if (horzField == null || horzField.Length < 4) return null;
            if (vertField == null || vertField.Length < 4) return null;

            var width = horzField.GetLength(0);
            var height = horzField.GetLength(1);
            var bmp = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var vf = vertField[x, y];
                    var hf = horzField[x, y];
                    var dy = Math.Abs(vf).Pin(0, 255);
                    var dx = Math.Abs(hf).Pin(0, 255);
                    var ni = (vf < 0 || hf < 0) ? 255 : 0;
                    bmp.SetPixel(x, y, Color.FromArgb(dy, dx, ni));
                }
            }

            return bmp;
        }

        public static Bitmap RenderToImage(int threshold, int[,] horzField, int[,] vertField)
        {
            if (horzField == null || horzField.Length < 4) return null;
            if (vertField == null || vertField.Length < 4) return null;

            var width = horzField.GetLength(0);
            var height = horzField.GetLength(1);
            var bmp = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var vf = vertField[x, y];
                    var hf = horzField[x, y];
                    var outp = Math.Min(vf, hf) < threshold ? Color.Black : Color.White;
                    bmp.SetPixel(x, y, outp);
                }
            }

            return bmp;
        }

        /// <summary>
        /// Render with a specified shade across the minimum distance
        /// </summary>
        public static Bitmap RenderToImage(double threshold, double shade, Vector[,] vectors)
        {
            if (vectors == null || vectors.Length < 4) return null;

            var width = vectors.GetLength(0);
            var height = vectors.GetLength(1);
            var bmp = new Bitmap(width, height);

            var rel = 255.0 / shade;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var v = vectors[x, y];
                    var thresh = Math.Min(v.Dx, v.Dy) - threshold;
                    var s = shade - thresh;
                    var g = 255 - ((int)(s * rel)).Pin(0, 255);

                    bmp.SetPixel(x,y,Color.FromArgb(g,g,g));
                }
            }

            return bmp;
        }
        
        /// <summary>
        /// Render with a shade between the minimum and maximum distance
        /// </summary>
        public static Bitmap RenderToImage(double threshold, Vector[,] vectors)
        {
            if (vectors == null || vectors.Length < 4) return null;

            var width = vectors.GetLength(0);
            var height = vectors.GetLength(1);
            var bmp = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var v = vectors[x, y];
                    var s = 255;
                    if (Math.Min(v.Dx, v.Dy) < threshold) s -= 127;
                    if (Math.Max(v.Dx, v.Dy) < threshold) s -= 127;

                    bmp.SetPixel(x,y,Color.FromArgb(s,s,s));
                }
            }

            return bmp;
        }

        /// <summary>
        /// Scale a pair of scalar fields into a smaller vector field.
        /// Uses cubic interpolation
        /// </summary>
        public static Vector[,] ReduceToVectors_cubicSpline(int rounds, int[,] horzField, int[,] vertField)
        {
            // write to temp, scale that, then copy out to final
            if (horzField == null || horzField.Length < 4) return null;
            if (vertField == null || vertField.Length < 4) return null;

            var width = horzField.GetLength(0);
            var height = horzField.GetLength(1);

            var smallWidth = width >> rounds;
            var smallHeight = height >> rounds;

            var maxDim = Math.Max(width, height);

            var stage = new Vector[width, height];

            // shrink in X
            for (int y = 0; y < height; y++)
            {
                var xBuffer = new double[width];
                var yBuffer = new double[width];
                
                for (int x = 0; x < width; x++)
                {
                    xBuffer[x] = horzField[x, y];
                    yBuffer[x] = vertField[x, y];
                }

                var dx = (double) width / smallWidth;
                var samples = Enumerable.Range(0, smallWidth).Select(j=>j*dx).ToArray();
                xBuffer = CubicSplines.Resample(xBuffer, samples);
                yBuffer = CubicSplines.Resample(yBuffer, samples);

                for (int x = 0; x < smallWidth; x++)
                {
                    stage[x, y].Dx = xBuffer[x];
                    stage[x, y].Dy = yBuffer[x];
                }
            }

            // shrink in Y
            for (int x = 0; x < width; x++)
            {
                var xBuffer = new double[height];
                var yBuffer = new double[height];
                
                for (int y = 0; y < height; y++)
                {
                    xBuffer[y] = (float) stage[x, y].Dx;
                    yBuffer[y] = (float) stage[x, y].Dy;
                }

                var dy = (double) height / smallHeight;
                var samples = Enumerable.Range(0, smallHeight).Select(j=>j*dy).ToArray();
                xBuffer = CubicSplines.Resample(xBuffer, samples);
                yBuffer = CubicSplines.Resample(yBuffer, samples);

                for (int y = 0; y < smallHeight; y++)
                {
                    stage[x, y].Dx = xBuffer[y];
                    stage[x, y].Dy = yBuffer[y];
                }
            }

            var final = new Vector[smallWidth, smallHeight];
            for (int y = 0; y < smallHeight; y++)
            {
                for (int x = 0; x < smallWidth; x++)
                {
                    final[x, y] = stage[x, y];
                }
            }

            return final;
        }

        /// <summary>
        /// Scale a pair of scalar fields into a smaller vector field.
        /// Uses nearest-neighbour interpolation
        /// </summary>
        public static Vector[,] ReduceToVectors_nearest(int rounds, int[,] horzField, int[,] vertField)
        {
            if (horzField == null || horzField.Length < 4) return null;
            if (vertField == null || vertField.Length < 4) return null;

            var width = horzField.GetLength(0);
            var height = horzField.GetLength(1);

            var smallWidth = width >> rounds;
            var smallHeight = height >> rounds;

            var final = new Vector[smallWidth, smallHeight];

            var dx = (double) width / smallWidth;
            var dy = (double) height / smallHeight;

            for (int y = 0; y < smallHeight; y++)
            {
                var sy = (int) (y * dy);
                for (int x = 0; x < smallWidth; x++)
                {
                    var sx = (int) (x * dx);
                    final[x, y].Dx = horzField[sx, sy];
                    final[x, y].Dy = vertField[sx, sy];
                }
            }

            return final;
        }
        
        /// <summary>
        /// Scale a pair of scalar fields into a smaller vector field.
        /// Uses experimental interpolation to try to prevent drop-outs
        /// </summary>
        public static Vector[,] ReduceToVectors_experimental(int rounds, int[,] horzField, int[,] vertField)
        {
            if (horzField == null || horzField.Length < 4) return null;
            if (vertField == null || vertField.Length < 4) return null;

            var width = horzField.GetLength(0);
            var height = horzField.GetLength(1);

            var smallWidth = width >> rounds;
            var smallHeight = height >> rounds;

            var final = new Vector[smallWidth, smallHeight];

            var dx = (double) width / smallWidth;
            var dy = (double) height / smallHeight;

            
            
            for (int y = 0; y < smallHeight; y++)
            {
                var sy = (int) (y * dy);
                // this is like a box filter, but rather than averaging,
                // we keep the closest to zero
                for (int x = 0; x < smallWidth; x++)
                {
                    var sx = (int) (x * dx);

                    var hf = 100;
                    var vf = 100;
                    for (int ddx = 0; ddx < dx; ddx++)
                    {
                        for (int ddy = 0; ddy < dy; ddy++)
                        {
                            var sample_x = (sx+ddx).Pin(0, width-1);
                            var sample_y = (sy+ddy).Pin(0, height-1);
                            hf = Math.Min(hf, horzField[sample_x, sample_y]);
                            vf = Math.Min(vf, vertField[sample_x, sample_y]);
                        }
                    }
                    final[x, y].Dx = hf;
                    final[x, y].Dy = vf;
                }
            }

            return final;
        }

        private static int AbsLowest(int s1, int s2)
        {
            return (Math.Abs(s1) < Math.Abs(s2)) ? s1 : s2;
        }
    }
    

    public struct Vector
    {
        public double Dx, Dy;
    }
}