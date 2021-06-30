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

            // TEMP: show the sample points:
            // This is the WRONG way to do it.
            for (int sy = 0; sy < height; sy++)
            {
                for (int sx = 0; sx < width; sx++)
                {
                    var dp = new Vector2(sx-halfWidth, sy-halfHeight) * rot;
                    Set(bounds_width, 255, dY, dp.Dx - lx, dp.Dy - ty);
                }
            }

            // pack back into bitmap
            BitmapTools.YUVPlanes_To_ArgbImage(dest, 0, dY, dU, dV);

            return dest;
        }

        private static void Set(int w, float c, float[] v, double x, double y)
        {
            v![w*(int)y + (int)x] = c;
        }
    }
}