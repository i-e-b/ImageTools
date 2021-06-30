using System;
using System.Drawing;
using System.Drawing.Imaging;
using ImageTools.DistanceFields;
using ImageTools.Utilities;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace ImageTools
{
    public static class Rotate {
        private const double d2rfac = 0.01745329252;
        private static double deg2rad(double deg){
            return deg * d2rfac;
        }

        public static Bitmap ShearRotate(Bitmap source, double angleDegrees) {
            if (source == null) return null;
            
            var shear_x = -Math.Tan(deg2rad(angleDegrees) / 2.0);
            var shear_y = Math.Sin(deg2rad(angleDegrees));

            // Calculate how big the output image has to be
            var width = source.Width;
            var height = source.Height;

            var shear_width = (int)Math.Abs(height * shear_x);
            var bounds_width = (int)Math.Floor((Math.Abs(height * shear_x)+(shear_width*2)) + width + 0.5);
            var bounds_height = (int)Math.Floor(Math.Abs(bounds_width * shear_y) + height + 0.5);

            Console.WriteLine($"Original image size: width={width}; height={height}");
            Console.WriteLine($"Estimated transform bounds: width={bounds_width}; height={bounds_height}");
            Console.WriteLine($"Shear factor: x={shear_x}; y={shear_y}");

            // create an output image, and get source planes
            var dest = new Bitmap(bounds_width, bounds_height, PixelFormat.Format32bppArgb);
            BitmapTools.ArgbImageToYUVPlanes_f(source, out var sY, out var sU, out var sV);
            BitmapTools.ArgbImageToYUVPlanes_f(dest, out var dY, out var dU, out var dV);


            // copy over with an offset and first X shear
            var yofc = bounds_height - height;
            var xofc = (bounds_width - width) - shear_width;
            for (int y = 0; y < height; y++)
            {
                var doy = (y + yofc) * bounds_width;
                var soy = y * width;
                var dx = (int)(y * -shear_x) + xofc;
                for (int x = 0; x < width; x++)
                {
                    dY[doy + x + dx] = sY[soy + x];
                    dU[doy + x + dx] = sU[soy + x];
                    dV[doy + x + dx] = sV[soy + x];
                }
            }

            // apply Y shear
            for (int x = 0; x < bounds_width; x++)
            {
                var yoff = (int)(x * shear_y);
                for (int y = yoff; y < bounds_height; y++)
                {
                    var soy = (y) * bounds_width;
                    var doy = (y - yoff) * bounds_width;
                    dY[doy + x] = dY[soy + x];
                    dU[doy + x] = dU[soy + x];
                    dV[doy + x] = dV[soy + x];
                }
                // clean up run-off
                for (int y = bounds_height - yoff; y < bounds_height; y++)
                {
                    var oy = y * bounds_width;
                    dY[oy + x] = 0;
                    dU[oy + x] = 127;
                    dV[oy + x] = 127;
                }
            }

    
            // apply X shear again
            for (int y = 0; y < bounds_height; y++)
            {
                var oy = y * bounds_width;
                var ny = bounds_height - y;
                var dx = (int)(ny * -shear_x);
                if (dx < 0) break;
                for (int x = 0; x < bounds_width - dx; x++)
                {
                    dY[oy + x] = dY[oy + x + dx];
                    dU[oy + x] = dU[oy + x + dx];
                    dV[oy + x] = dV[oy + x + dx];
                }
                for (int x = bounds_width - dx; x < bounds_width; x++)
                {
                    dY[oy + x] = 0;
                    dU[oy + x] = 127;
                    dV[oy + x] = 127;
                }
            }


            // pack back into bitmap
            BitmapTools.YUVPlanes_To_ArgbImage(dest, 0, dY, dU, dV);

            return dest;
        }

        public static Bitmap SelectRotate(Bitmap source, double angleDegrees)
        {
            if (source == null) return null;
            
            var ar = deg2rad(angleDegrees);
            var rot = new Matrix2(
                Math.Cos(ar), -Math.Sin(ar),
                Math.Sin(ar),  Math.Cos(ar)
                );

            // Calculate how big the output image has to be
            var width = (double)source.Width;
            var height = (double)source.Height;
            
            var halfWidth = width / 2.0;
            var halfHeight = height / 2.0;

            var tl = new Vector2(-halfWidth, -halfHeight) * rot;
            var tr = new Vector2(halfWidth, -halfHeight) * rot;
            var bl = new Vector2(-halfWidth, halfHeight) * rot;
            var br = new Vector2(halfWidth, halfHeight) * rot;
            
            var ty = Math.Min(Math.Min(tl.Dy, tr.Dy), Math.Min(bl.Dy, br.Dy));
            var by = Math.Max(Math.Max(tl.Dy, tr.Dy), Math.Max(bl.Dy, br.Dy));
            var lx = Math.Min(Math.Min(tl.Dx, tr.Dx), Math.Min(bl.Dx, br.Dx));
            var rx = Math.Max(Math.Max(tl.Dx, tr.Dx), Math.Max(bl.Dx, br.Dx));
            
            var bounds_width = (int)Math.Ceiling(rx - lx);
            var bounds_height = (int)Math.Ceiling(by - ty);

            Console.WriteLine($"Original image size: width={width}; height={height}");
            Console.WriteLine($"Estimated transform bounds: width={bounds_width}; height={bounds_height}");

            // create an output image, and get source planes
            var dest = new Bitmap(bounds_width, bounds_height, PixelFormat.Format32bppArgb);
            BitmapTools.ArgbImageToYUVPlanes_f(source, out var sY, out var sU, out var sV);
            BitmapTools.ArgbImageToYUVPlanes_f(dest, out var dY, out var dU, out var dV);
            
            // Invert the matrix, and look up a source location for each output pixel
            // This prevents any 'drop out' positions
            var half_bound_height = bounds_height / 2;
            var half_bound_width = bounds_width / 2;
            var invmat = rot.Inverse();
            for (var dy = -half_bound_height; dy < half_bound_height; dy++)
            {
                for (var dx = -half_bound_width; dx < half_bound_width; dx++)
                {
                    var sp = new Vector2(dx, dy) * invmat;
                    var sx = sp.Dx + halfWidth;
                    var sy = sp.Dy + halfHeight;
                    
                    // sample multiple points blended by sx/sy fractions
                    CopySubsampled(/*from*/ (int)width,(int)height, sx, sy,   /* to */ bounds_width, dx - lx, dy - ty,  /* buffers */ sY,sU,sV,  dY,dU,dV );
                }
            }

            // pack back into bitmap
            BitmapTools.YUVPlanes_To_ArgbImage(dest, 0, dY, dU, dV);

            return dest;
        }

        private static double Fractional(double real) => real - Math.Floor(real);
        
        /// <summary>
        /// sample 4 input points, and write to a single output point
        /// </summary>
        private static void CopySubsampled(int sw, int sh, double sx, double sy, int dw, double dx, double dy, float[] sY, float[] sU, float[] sV, float[] dY, float[] dU, float[] dV)
        {
            // bounds check
            if (sy < 0 || sy > sw) return;
            if (sx < 0 || sx > sh) return;
            
            // work out the sample weights
            var fx2 = Fractional(sx);
            var fx1 = 1.0 - fx2;
            var fy2 = Fractional(sy);
            var fy1 = 1.0 - fy2;
            
            var f0 = fx1 * fy1;
            var f1 = fx2 * fy1;
            var f2 = fx1 * fy2;
            var f3 = fx2 * fy2;
            
            var ox = sx < (sw-1) ? 1 : 0;
            var oy = sy < (sh-1) ? sw : 0;
            
            var sm0 = sw*(int)sy + (int)sx;
            if (sm0 < 0) sm0 = 0;
            var sm1 = sm0 + ox;
            var sm2 = sm0 + oy;
            var sm3 = sm2 + ox;
            
            var dm = dw*(int)dy + (int)dx;
            if (dm < 0 || dm >= dY!.Length) return;

            dY[dm] = (float) (sY[sm0] * f0 + sY[sm1] * f1 + sY[sm2] * f2 + sY[sm3] * f3);
            dU[dm] = (float) (sU[sm0] * f0 + sU[sm1] * f1 + sU[sm2] * f2 + sU[sm3] * f3);
            dV[dm] = (float) (sV[sm0] * f0 + sV[sm1] * f1 + sV[sm2] * f2 + sV[sm3] * f3);
        }
    }
}