using System;
using System.Drawing;
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
                var inside = bmp.GetPixel(0,y).GetBrightness() <= 0.5f;
                var dist = inside ? -bmp.Width : bmp.Width;
                
                for (int x = 0; x < bmp.Width; x++)
                {
                    var pixIn = bmp.GetPixel(x,y).GetBrightness() <= 0.5f;
                    if (pixIn != inside) dist = 0;
                    inside = pixIn;
                    dist += inside ? -1 : 1; // avoid zero, so we always have a value crossing
                    
                    outp[x,y] = dist;
                }
                
                // now opposite way. The right column value should be correct as possible now.
                for (int x = bmp.Width - 1; x >= 0; x--)
                {
                    var pixIn = bmp.GetPixel(x,y).GetBrightness() <= 0.5f;
                    if (pixIn != inside) dist = 0;
                    inside = pixIn;
                    dist += inside ? -1 : 1; // avoid zero, so we always have a value crossing
                    
                    if (Math.Abs(dist) < Math.Abs(outp[x,y])) outp[x,y] = dist;
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
                var inside = bmp.GetPixel(x,0).GetBrightness() <= 0.5f;
                var dist = inside ? -bmp.Height : bmp.Height;
                
                for (int y = 0; y < bmp.Height; y++)
                {
                    var pixIn = bmp.GetPixel(x,y).GetBrightness() <= 0.5f;
                    if (pixIn != inside) dist = 0;
                    inside = pixIn;
                    dist += inside ? -1 : 1; // avoid zero, so we always have a value crossing
                    
                    outp[x,y] = dist;
                }
                
                // now opposite way. The right column value should be correct as possible now.
                for (int y = bmp.Height - 1; y >= 0; y--)
                {
                    var pixIn = bmp.GetPixel(x,y).GetBrightness() <= 0.5f;
                    if (pixIn != inside) dist = 0;
                    inside = pixIn;
                    dist += inside ? -1 : 1; // avoid zero, so we always have a value crossing
                    
                    if (Math.Abs(dist) < Math.Abs(outp[x,y])) outp[x,y] = dist;
                }
            }
            
            return outp;
        }

        public static Bitmap RenderToImage(int[,] field)
        {
            if (field == null || field.Length < 4) return null;
            
            var width = field.GetLength(0);
            var height = field.GetLength(1);
            var bmp = new Bitmap(width, height);
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var v = field[x,y].Pin(-255, 255);
                    if (v < 0) { bmp.SetPixel(x,y,Color.FromArgb(0,0, 255+v)); }
                    else { bmp.SetPixel(x,y,Color.FromArgb(0,v,0)); }
                }
            }
            
            return bmp;
        }
    }
}