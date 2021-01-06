using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ImageTools.AnalyticalTransforms;
using ImageTools.Utilities;
// ReSharper disable PossibleNullReferenceException
// ReSharper disable UnusedMember.Global

// ReSharper disable InconsistentNaming

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

        /// <summary>
        /// Measure signed distance to shape edges.
        /// Dark pixel are considered 'inside' (negative).
        /// Also stores 2-vector of normal to the nearest surface.
        /// </summary>
        public static Vector1_2[,] DistanceAndGradient(Bitmap bmp)
        {
            if (bmp == null || bmp.Width < 1 || bmp.Height < 1) return new Vector1_2[0, 0];

            int x, y, j;
            var width = bmp.Width;
            var height = bmp.Height;
            var outp = new Vector1_2[width, height];
            var nearest_edge = new EdgeInfo[width, height]; //X,Y location of nearest edge point
            var maxDim = Math.Max(width, height);
            var maxDistSqr = (double) (maxDim * maxDim);

            // First, fill up the 'nearest_edge' matrix from the image
            // surfaces at the edge of the image are allowed to act weird.
            for (y = 0; y < height; y++)
            {
                var t = (y - 1).PinXu(0, height);
                var b = (y + 1).PinXu(0, height);
                for (x = 0; x < width; x++)
                {
                    var l = (x - 1).PinXu(0, width);
                    var r = (x + 1).PinXu(0, width);

                    var pixIn = IsInObj(bmp, x, y);
                    var it = IsInObj(bmp, x, t);
                    var ib = IsInObj(bmp, x, b);
                    var il = IsInObj(bmp, l, y);
                    var ir = IsInObj(bmp, r, y);

                    var edge = pixIn && (!it || !ib || !il || !ir);
                    nearest_edge[x, y] = new EdgeInfo {Inside = pixIn, IsEdge = edge, X = x, Y = y};
                }
            }

            // 'jump-flood' fill the space to (somewhat) efficiently calculate
            // the exact distance and normal values
            for (j = maxDim / 2; j >= 1; j /= 2)
            {
                for (y = 0; y < height; y++)
                {
                    var t = (y - j).PinXu(0, height);
                    var b = (y + j).PinXu(0, height);

                    for (x = 0; x < width; x++)
                    {
                        var l = (x - j).PinXu(0, width);
                        var r = (x + j).PinXu(0, width);
                        // Every round we merge 9 samples, keeping only the minimum distance
                        // Note: we don't need the real distance, just the relative -- so we can square and not square-root.

                        var s1 = nearest_edge[l, t];
                        var s2 = nearest_edge[x, t];
                        var s3 = nearest_edge[r, t];
                        var s4 = nearest_edge[l, y];
                        var s5 = nearest_edge[x, y];
                        var s6 = nearest_edge[r, y];
                        var s7 = nearest_edge[l, b];
                        var s8 = nearest_edge[x, b];
                        var s9 = nearest_edge[r, b];

                        nearest_edge[x, y] = MergeSamples(maxDistSqr, x, y, s5.Inside,
                            s1, s2, s3,
                            s4, s5, s6,
                            s7, s8, s9);
                        
                    }
                }
            }

            // Finally, calculate the true distances and normals into the output:
            for (y = 0; y < height; y++)
            {
                for (x = 0; x < width; x++)
                {
                    var s = nearest_edge[x, y];
                    var dx = (double)s.X - x;
                    var dy = (double)s.Y - y;
                    var dist = Math.Max(1, Math.Sqrt(dx*dx + dy*dy));
                    dx /= dist;
                    dy /= dist;
                    if (s.Inside) dist = -dist;
                    outp[x, y] = new Vector1_2 {Dist = dist, Gx = dx, Gy = dy};
                }
            }

            return outp;
        }

        private static EdgeInfo MergeSamples(double max, double x, double y, bool inside, params EdgeInfo[] neighborSamples)
        {
            var result = new EdgeInfo{Inside = inside};
            var bestDistance = max; // active sample point
            foreach (var candidateSample in neighborSamples)
            {
                if (!candidateSample.IsEdge) continue;

                var dx = candidateSample.X - x;
                var dy = candidateSample.Y - y;
                var dc = (dx * dx) + (dy * dy);
                if (dc >= bestDistance) continue;
                
                // new sample is better
                bestDistance = dc;
                result.X = candidateSample.X;
                result.Y = candidateSample.Y;
                result.IsEdge = true;
            }
            return result;
        }

        private static bool IsInObj(Bitmap bmp, int x, int y)
        {
            return bmp.GetPixel(x, y).GetBrightness() <= 0.5f;
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

        public static Bitmap RenderFieldToImage(Vector1_2[,] field)
        {
            if (field == null || field.Length < 4) return null;

            var width = field.GetLength(0);
            var height = field.GetLength(1);
            var bmp = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var fv = field[x, y];
                    var dx = Math.Abs((int) (fv.Gx * 127) + 127).Pin(0, 200);
                    var dy = Math.Abs((int) (fv.Gy * 127) + 127).Pin(0, 255);
                    var dd = Math.Abs((int)( 127 - fv.Dist)).Pin(0, 255);
                    bmp.SetPixel(x, y, Color.FromArgb(dy, dd, dx));
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
        public static Bitmap RenderToImage(double threshold, double shade, Vector2[,] vectors)
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
                    var g = 255 - ((int) (s * rel)).Pin(0, 255);

                    bmp.SetPixel(x, y, Color.FromArgb(g, g, g));
                }
            }

            return bmp;
        }

        /// <summary>
        /// Render with a shade between the minimum and maximum distance
        /// </summary>
        public static Bitmap RenderToImage(double threshold, Vector2[,] vectors)
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

                    bmp.SetPixel(x, y, Color.FromArgb(s, s, s));
                }
            }

            return bmp;
        }
        
        public static Bitmap RenderToImage(double threshold, Vector1_2[,] signedField)
        {
            if (signedField == null || signedField.Length < 4) return null;

            var width = signedField.GetLength(0);
            var height = signedField.GetLength(1);
            var bmp = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var v = signedField[x, y];
                    var s = (v.Dist < threshold) ? 0 : 255;
                    bmp.SetPixel(x, y, Color.FromArgb(s, s, s));
                }
            }

            return bmp;
        }

        public static Bitmap RenderToImage(double threshold, double[,] signedField)
        {
            if (signedField == null || signedField.Length < 4) return null;

            var width = signedField.GetLength(0);
            var height = signedField.GetLength(1);
            var bmp = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var v = signedField[x, y];
                    var s = (v < threshold) ? 0 : 255;
                    bmp.SetPixel(x, y, Color.FromArgb(s, s, s));
                }
            }

            return bmp;
        }

        /// <summary>
        /// Scale a pair of scalar fields into a smaller vector field.
        /// Uses cubic interpolation
        /// </summary>
        public static Vector2[,] ReduceToVectors_cubicSpline(int rounds, int[,] horzField, int[,] vertField)
        {
            // write to temp, scale that, then copy out to final
            if (horzField == null || horzField.Length < 4) return null;
            if (vertField == null || vertField.Length < 4) return null;

            var width = horzField.GetLength(0);
            var height = horzField.GetLength(1);

            var smallWidth = width >> rounds;
            var smallHeight = height >> rounds;

            var stage = new Vector2[width, height];

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
                var samples = Enumerable.Range(0, smallWidth).Select(j => j * dx).ToArray();
                xBuffer = CubicSplines.Resample1D(xBuffer, samples);
                yBuffer = CubicSplines.Resample1D(yBuffer, samples);

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
                var samples = Enumerable.Range(0, smallHeight).Select(j => j * dy).ToArray();
                xBuffer = CubicSplines.Resample1D(xBuffer, samples);
                yBuffer = CubicSplines.Resample1D(yBuffer, samples);

                for (int y = 0; y < smallHeight; y++)
                {
                    stage[x, y].Dx = xBuffer[y];
                    stage[x, y].Dy = yBuffer[y];
                }
            }

            var final = new Vector2[smallWidth, smallHeight];
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
        /// Rescale a distance field. Currently loses normals
        /// </summary>
        public static double[,] ReduceToDistance_cubicSpline(int rounds, Vector1_2[,] field)
        {
            // write to temp, scale that, then copy out to final
            if (field == null || field.Length < 4) return null;

            var width = field.GetLength(0);
            var height = field.GetLength(1);

            var smallWidth = width >> rounds;
            var smallHeight = height >> rounds;

            var stage = new double[width, height];

            // shrink in X
            for (int y = 0; y < height; y++)
            {
                var buffer = new double[width];

                for (int x = 0; x < width; x++) { buffer[x] = field[x, y].Dist; }

                var dx = (double) width / smallWidth;
                var samples = Enumerable.Range(0, smallWidth).Select(j => j * dx).ToArray();
                buffer = CubicSplines.Resample1D(buffer, samples);

                for (int x = 0; x < smallWidth; x++) { stage[x, y] = buffer[x]; }
            }

            // shrink in Y
            for (int x = 0; x < width; x++)
            {
                var buffer = new double[height];

                for (int y = 0; y < height; y++) { buffer[y] = (float) stage[x, y]; }

                var dy = (double) height / smallHeight;
                var samples = Enumerable.Range(0, smallHeight).Select(j => j * dy).ToArray();
                buffer = CubicSplines.Resample1D(buffer, samples);

                for (int y = 0; y < smallHeight; y++) { stage[x, y] = buffer[y]; }
            }

            var final = new double[smallWidth, smallHeight];
            for (int y = 0; y < smallHeight; y++)
            {
                for (int x = 0; x < smallWidth; x++)
                {
                    final[x, y] = stage[x, y];
                }
            }

            return final;
        }

        public static double[,] ReduceToVectors_boxZero(int rounds, Vector1_2[,] field)
        {
            if (field == null || field.Length < 4) return null;

            var width = field.GetLength(0);
            var height = field.GetLength(1);

            var smallWidth = width >> rounds;
            var smallHeight = height >> rounds;

            var final = new double[smallWidth, smallHeight];

            var dx = (double) width / smallWidth;
            var dy = (double) height / smallHeight;


            for (int y = 0; y < smallHeight; y++)
            {
                var sy = (int) (y * dy);
                for (int x = 0; x < smallWidth; x++)
                {
                    var sx = (int) (x * dx);

                    var hf = 5120.0;
                    for (int ddx = 0; ddx < dx; ddx++)
                    {
                        var sample_x = (sx + ddx).Pin(0, width - 1);
                        for (int ddy = 0; ddy < dy; ddy++)
                        {
                            // this is like a box filter, but rather than averaging,
                            // we keep the closest to zero
                            var sample_y = (sy + ddy).Pin(0, height - 1);
                            hf = AbsLowest(hf, field[sample_x, sample_y].Dist);
                        }
                    }

                    final[x, y] = hf;
                }
            }

            return final;
        }

        /// <summary>
        /// Scale a pair of scalar fields into a smaller vector field.
        /// Uses nearest-neighbour interpolation
        /// </summary>
        public static Vector2[,] ReduceToVectors_nearest(int rounds, int[,] horzField, int[,] vertField)
        {
            if (horzField == null || horzField.Length < 4) return null;
            if (vertField == null || vertField.Length < 4) return null;

            var width = horzField.GetLength(0);
            var height = horzField.GetLength(1);

            var smallWidth = width >> rounds;
            var smallHeight = height >> rounds;

            var final = new Vector2[smallWidth, smallHeight];

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
        /// Uses a custom box filter to try to prevent drop-outs
        /// </summary>
        public static Vector2[,] ReduceToVectors_boxZero(int rounds, int[,] horzField, int[,] vertField)
        {
            if (horzField == null || horzField.Length < 4) return null;
            if (vertField == null || vertField.Length < 4) return null;

            var width = horzField.GetLength(0);
            var height = horzField.GetLength(1);

            var smallWidth = width >> rounds;
            var smallHeight = height >> rounds;

            var final = new Vector2[smallWidth, smallHeight];

            var dx = (double) width / smallWidth;
            var dy = (double) height / smallHeight;


            for (int y = 0; y < smallHeight; y++)
            {
                var sy = (int) (y * dy);
                for (int x = 0; x < smallWidth; x++)
                {
                    var sx = (int) (x * dx);

                    var hf = 5120;
                    var vf = 5120;
                    for (int ddx = 0; ddx < dx; ddx++)
                    {
                        var sample_x = (sx + ddx).Pin(0, width - 1);
                        for (int ddy = 0; ddy < dy; ddy++)
                        {
                            // this is like a box filter, but rather than averaging,
                            // we keep the closest to zero
                            var sample_y = (sy + ddy).Pin(0, height - 1);
                            hf = AbsLowest(hf, horzField[sample_x, sample_y]);
                            vf = AbsLowest(vf, vertField[sample_x, sample_y]);
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
        
        private static double AbsLowest(double s1, double s2)
        {
            return (Math.Abs(s1) < Math.Abs(s2)) ? s1 : s2;
        }

        public static Vector2[,] RescaleVectors_nearest(Vector2[,] original, int dstWidth, int dstHeight)
        {
            if (original == null || original.Length < 4) return null;

            var srcWidth = original.GetLength(0);
            var srcHeight = original.GetLength(1);

            var final = new Vector2[dstWidth, dstHeight];

            var dx = (double) srcWidth / dstWidth;
            var dy = (double) srcHeight / dstHeight;

            for (int y = 0; y < dstHeight; y++)
            {
                var sy = (int) (y * dy);
                for (int x = 0; x < dstWidth; x++)
                {
                    var sx = (int) (x * dx);
                    final[x, y].Dx = original[sx, sy].Dx;
                    final[x, y].Dy = original[sx, sy].Dy;
                }
            }

            return final;
        }

        public static Vector2[,] RescaleVectors_bilinear(Vector2[,] original, int dstWidth, int dstHeight)
        {
            if (original == null || original.Length < 4) return null;

            var srcWidth = original.GetLength(0);
            var srcHeight = original.GetLength(1);

            var final = new Vector2[dstWidth, dstHeight];

            var dx = (double) srcWidth / dstWidth;
            var dy = (double) srcHeight / dstHeight;

            for (int y = 0; y < dstHeight; y++)
            {
                var sy = y * dy;
                var ty = (int) Math.Floor(sy);
                var by = (ty + 1).Pin(0, srcHeight - 1);
                var Ly = sy - ty;
                var ly = 1.0 - Ly;

                for (int x = 0; x < dstWidth; x++)
                {
                    var sx = x * dx;
                    var tx = (int) Math.Floor(sx);
                    var bx = (tx + 1).Pin(0, srcWidth - 1);
                    var Lx = sx - tx;
                    var lx = 1.0 - Lx;

                    var fx =
                        (original[tx, ty].Dx * ly * lx) +
                        (original[bx, ty].Dx * ly * Lx) +
                        (original[tx, by].Dx * Ly * lx) +
                        (original[bx, by].Dx * Ly * Lx);

                    var fy =
                        (original[tx, ty].Dy * ly * lx) +
                        (original[bx, ty].Dy * ly * Lx) +
                        (original[tx, by].Dy * Ly * lx) +
                        (original[bx, by].Dy * Ly * Lx);

                    final[x, y].Dx = fx;
                    final[x, y].Dy = fy;
                }
            }

            return final;
        }

        public static Vector2[,] RescaleVectors_cubic(Vector2[,] original, int width, int height)
        {
            if (original == null || original.Length < 4) return null;

            var srcWidth = original.GetLength(0);
            var srcHeight = original.GetLength(1);

            var maxWidth = Math.Max(width, srcWidth);
            var maxHeight = Math.Max(height, srcHeight);

            var stage = new Vector2[maxWidth, maxHeight];

            // TODO: separate the dx and dy scaling, so we do each in its
            //       prime direction first

            #region Scale DX

            // scale in X
            for (int y = 0; y < srcHeight; y++)
            {
                var xBuffer = new double[srcWidth];

                for (int x = 0; x < srcWidth; x++)
                {
                    xBuffer[x] = original[x, y].Dx;
                }

                xBuffer = CubicSplines.Resample1D(xBuffer, width);
                for (int x = 0; x < width; x++)
                {
                    stage[x, y].Dx = xBuffer[x];
                }
            }

            // scale in Y
            for (int x = 0; x < width; x++)
            {
                var xBuffer = new double[srcHeight];

                for (int y = 0; y < srcHeight; y++)
                {
                    xBuffer[y] = (float) stage[x, y].Dx;
                }

                xBuffer = CubicSplines.Resample1D(xBuffer, height);
                for (int y = 0; y < height; y++)
                {
                    stage[x, y].Dx = xBuffer[y];
                }
            }

            #endregion

            #region Scale DY

            // scale in Y
            for (int x = 0; x < srcWidth; x++)
            {
                var yBuffer = new double[srcHeight];

                for (int y = 0; y < srcHeight; y++)
                {
                    yBuffer[y] = (float) original[x, y].Dy;
                }

                yBuffer = CubicSplines.Resample1D(yBuffer, height);
                for (int y = 0; y < height; y++)
                {
                    stage[x, y].Dy = yBuffer[y];
                }
            }

            // scale in X
            for (int y = 0; y < height; y++)
            {
                var yBuffer = new double[srcWidth];

                for (int x = 0; x < srcWidth; x++)
                {
                    yBuffer[x] = stage[x, y].Dy;
                }

                yBuffer = CubicSplines.Resample1D(yBuffer, width);
                for (int x = 0; x < width; x++)
                {
                    stage[x, y].Dy = yBuffer[x];
                }
            }

            #endregion

            if (width >= srcWidth && height >= srcHeight) return stage;

            var final = new Vector2[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    final[x, y] = stage[x, y];
                }
            }

            return final;
        }

        public static double[,] RescaleDistance_bilinear(double[,] original, int dstWidth, int dstHeight)
        {
            if (original == null || original.Length < 4) return null;

            var srcWidth = original.GetLength(0);
            var srcHeight = original.GetLength(1);

            var final = new double[dstWidth, dstHeight];

            var dx = (double) srcWidth / dstWidth;
            var dy = (double) srcHeight / dstHeight;

            for (int y = 0; y < dstHeight; y++)
            {
                var sy = y * dy;
                var ty = (int) Math.Floor(sy);
                var by = (ty + 1).Pin(0, srcHeight - 1);
                var Ly = sy - ty;
                var ly = 1.0 - Ly;

                for (int x = 0; x < dstWidth; x++)
                {
                    var sx = x * dx;
                    var tx = (int) Math.Floor(sx);
                    var bx = (tx + 1).Pin(0, srcWidth - 1);
                    var Lx = sx - tx;
                    var lx = 1.0 - Lx;

                    var fx =
                        (original[tx, ty] * ly * lx) +
                        (original[bx, ty] * ly * Lx) +
                        (original[tx, by] * Ly * lx) +
                        (original[bx, by] * Ly * Lx);

                    final[x, y] = fx;
                }
            }

            return final;
        }

        public static Bitmap RenderPointVisibility(Vector1_2[,] field, int px, int py, int limit = 32767)
        {
            if (field == null || field.Length < 4) return null;

            var width = field.GetLength(0);
            var height = field.GetLength(1);
            var bmp = new Bitmap(width, height);

            // Plan: - Keep a list of points, add the initial one to it.
            //       - Each round, for each point
            //           1. add each edge point, if no existing pixel on the bitmap, add point to the list.
            //              - don't add point if at edge of image, or distance is < 1;
            //           2. render a circle at its distance into the bitmap (if distance > 0);
            //              - only write pixels if not already written.
            
            var q = new Queue<Point>(); // using a stack does roughly depth-first edge tracing.
                                        // a queue does breadth-first exploration.
                                        // stack explores in the least steps, queue looks better when limited.
            q.Enqueue(new Point(px,py));

            // TODO: Don't use the bitmap as temp data
            // TODO: Add step-wise distance from origin to data
            // TODO: increase distance when changing direction?
            
            
            var seen = Color.FromArgb(255,0,0);
            var hit = Color.FromArgb(255,127,0);
            var reject = Color.FromArgb(255,0,255);
            var rounds = 0L;
            var jump_points = 0L;
            var max = limit;
            while (q.Count > 0 && max > 0)
            {
                rounds++;
                var p = q.Dequeue();
                var rad = (int)field[p.X, p.Y].Dist;
                if (rad < 3) continue; // close to the edge
                if (bmp.GetPixel(p.X, p.Y).R > 127)
                {
                    bmp.SetPixel(p.X, p.Y, reject);
                    continue; // already seen
                }

                max--;
                bmp.SetPixel(p.X, p.Y, hit); // show we've processed this point
                jump_points++;
                
                var next = ScanlineEllipse(p.X, p.Y, rad - 1, rad - 1);
                
                // set next steps (just the top, left, bottom, right extremes)
                var l = p.X - rad;
                var r = p.X + rad;
                var t = p.Y - rad;
                var b = p.Y + rad;
                if (l > 0 && bmp.GetPixel(l,p.Y).R < 127) { q.Enqueue(new Point(l,p.Y)); }
                if (r < width && bmp.GetPixel(r,p.Y).R < 127) { q.Enqueue(new Point(r,p.Y)); }
                if (t > 0 && bmp.GetPixel(p.X,t).R < 127) { q.Enqueue(new Point(p.X,t)); }
                if (b < height && bmp.GetPixel(p.X,b).R < 127) { q.Enqueue(new Point(p.X,b)); }
                
                // using color channels to keep track of things
                foreach (var scanLine in next)
                {
                    var y = scanLine.Y;
                    if (y < 0 || y >= height) continue;

                    for (int i = scanLine.Left; i < scanLine.Right; i++)
                    {
                        if (i < 0 || i >= width) continue;
                        if (bmp.GetPixel(i, y).R > 127) continue;
                        bmp.SetPixel(i, y, seen);
                    }
                }
            }
            
            Console.WriteLine($"Made {rounds} point inspections, found {jump_points} jump points");

            // white spot for the initiator:
            bmp.SetPixel(px, py, Color.White);
            return bmp;
        }
        
        
        
        private static List<ScanLine> ScanlineEllipse(int xc, int yc, int width, int height)
        {
            int a2 = width * width;
            int b2 = height * height;
            int fa2 = 4 * a2, fb2 = 4 * b2;
            int x, y, sigma;
            
            var outp = new List<ScanLine>();
            if (width < 1 || height < 1) return outp;
    
            // Top and bottom
            for (x = 0, y = height, sigma = 2 * b2 + a2 * (1 - 2 * height); b2*x <= a2 * y; x++) {
                if (sigma >= 0) {
                    sigma += fa2 * (1 - y);
                    outp.Add(new ScanLine{Y = yc + y, Left = xc - x, Right = xc + x });
                    outp.Add(new ScanLine{Y = yc - y, Left = xc - x, Right = xc + x });
                    y--;
                }
                sigma += b2 * ((4 * x) + 6);
            }
            var ty = y;

            // Left and right
            outp.Add(new ScanLine{Y = yc, Left = xc - width, Right = xc + width});
            for (x = width, y = 1, sigma = 2 * a2 + b2 * (1 - 2 * width); a2*y < b2 * x; y++) {
                if (y > ty) break; // started to overlap 'top-and-bottom'
                
                outp.Add(new ScanLine{Y = yc + y, Left = xc - x, Right = xc + x });
                outp.Add(new ScanLine{Y = yc - y, Left = xc - x, Right = xc + x });

                if (sigma >= 0) {
                    sigma += fb2 * (1 - x);
                    x--;
                }
                sigma += a2 * ((4 * y) + 6);
            }
            return outp;
        }
    }

    public struct ScanLine
    {
        public int Y, Left, Right;
    }

    public struct Vector1_2
    {
        public double Dist;
        public double Gx, Gy;
    }

    public struct Vector2
    {
        public double Dx, Dy;
    }

    public struct EdgeInfo
    {
        /// <summary>True if we are inside an object (so distance should be negative) </summary>
        public bool Inside;

        /// <summary> True if the X and Y represent a known edge location</summary>
        public bool IsEdge;

        /// <summary>Location of nearest edge</summary>
        public int X, Y;
    }
}