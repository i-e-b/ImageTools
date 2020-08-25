using System;
using System.Drawing;
using ImageTools.Utilities;
using ImageTools.WaveletTransforms;

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
                    var v = field[x,y].Pin(-255, 255);
                    if (v < 0) { bmp.SetPixel(x,y,Color.FromArgb(0,0, 255+v)); }
                    else { bmp.SetPixel(x,y,Color.FromArgb(0,v,0)); }
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
                    var vf = vertField[x,y];
                    var hf = horzField[x,y];
                    var dy = Math.Abs(vf).Pin(0, 255);
                    var dx = Math.Abs(hf).Pin(0, 255);
                    var ni = (vf < 0 || hf < 0) ? 255 : 0;
                    bmp.SetPixel(x,y,Color.FromArgb(dy, dx, ni));
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
                    var vf = vertField[x,y];
                    var hf = horzField[x,y];
                    var outp = Math.Min(vf, hf) < threshold ? Color.Black : Color.White;
                    bmp.SetPixel(x, y, outp);
                }
            }
            
            return bmp;
        }

        public static Vector[,] ReduceToVectors(int rounds, int[,] horzField, int[,] vertField)
        {
            // write to temp, CDF that, then copy out to final
            if (horzField == null || horzField.Length < 4) return null;
            if (vertField == null || vertField.Length < 4) return null;
            
            var width = horzField.GetLength(0);
            var height = horzField.GetLength(1);
            
            var smallWidth = width >> rounds;
            var smallHeight = height >> rounds;
            
            var maxDim = Math.Max(width, height);
            var xBuffer = new float[maxDim];
            var yBuffer = new float[maxDim];
            var temp = new float[maxDim];
            
            var stage = new Vector[width, height];
            
            // shrink in X
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++) { 
                    xBuffer[x] = horzField[x,y];
                    yBuffer[x] = vertField[x,y];
                }
                
                var length = width;
                for (int i = 0; i < rounds; i++)
                {
                    CDF.Fwt53(xBuffer, temp, length, 0, 1);
                    CDF.Fwt53(yBuffer, temp, length, 0, 1);
                    length >>= 1;
                }
                
                for (int x = 0; x < smallWidth; x++) {
                    stage[x,y].Dx = xBuffer[x];
                    stage[x,y].Dy = yBuffer[x];
                }
            }
            
            // shrink in Y
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++) {
                    xBuffer[y] = (float)stage[x,y].Dx;
                    yBuffer[y] = (float)stage[x,y].Dy;
                }
                
                var length = height;
                for (int i = 0; i < rounds; i++)
                {
                    CDF.Fwt53(xBuffer, temp, length, 0, 1);
                    CDF.Fwt53(yBuffer, temp, length, 0, 1);
                    length >>= 1;
                }
                
                for (int y = 0; y < smallHeight; y++) {
                    stage[x,y].Dx = xBuffer[y];
                    stage[x,y].Dy = yBuffer[y];
                }
            }
            
            var final = new Vector[smallWidth, smallHeight];
            for (int y = 0; y < smallHeight; y++)
            {
                for (int x = 0; x < smallWidth; x++)
                {
                    final[x,y] = stage[x,y];
                }
            }
            return final;
        }

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
                    var v = vectors[x,y];
                    var outp = Math.Min(v.Dx, v.Dy) < threshold ? Color.Black : Color.White;
                    bmp.SetPixel(x, y, outp);
                }
            }
            
            return bmp;
        }
    }

    public struct Vector
    {
        public double Dx,Dy;
    }
}