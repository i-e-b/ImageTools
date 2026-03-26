using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace ImageTools.AnalyticalTransforms;

/// <summary>
/// Discrete Cosine Transform
/// </summary>
/// <remarks>Derived from https://github.com/orrollo/DCTLib</remarks>
public class DCT
{
    /// <summary>
    /// Create a DCT processor for a given size
    /// </summary>
    public DCT(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public readonly int Width;
    public readonly int Height;

    private const int NormOffset = 128;

    /// <summary>
    /// Turn DCT matrices into an RGB bitmap for output
    /// </summary>
    /// <param name="matrices"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public Bitmap MatricesToBitmap(double[][,] matrices, bool offset = true)
    {
        var bitmap = new Bitmap(Width, Height);
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                var r = matrices[0][x, y];
                var g = matrices[1][x, y];
                var b = matrices[2][x, y];

                var R = (byte)(NormOut(r, offset));
                var G = (byte)(NormOut(g, offset));
                var B = (byte)(NormOut(b, offset));
                bitmap.SetPixel(x, y, Color.FromArgb(R, G, B));
            }
        }

        return bitmap;
    }

    private double NormOut(double a, bool offset)
    {
        var o = offset ? NormOffset : 0d;

        return Math.Min(Math.Max(a + o, 0), 255);
    }

    /// <summary>
    /// Create matrices from an RGB bitmap
    /// </summary>
    /// <param name="b"></param>
    /// <returns></returns>
    public double[][,] BitmapToMatrices(Bitmap b)
    {
        var matrices = new double[3][,];

        for (var i = 0; i < 3; i++)
        {
            matrices[i] = new double[Width, Height];
        }

        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                matrices[0][x, y] = b.GetPixel(x, y).R - NormOffset;
                matrices[1][x, y] = b.GetPixel(x, y).G - NormOffset;
                matrices[2][x, y] = b.GetPixel(x, y).B - NormOffset;
            }
        }

        return matrices;
    }

    /// <summary>
    /// Run the DCT2D on 3-channeled group of matrices.
    /// This is a O(n²) naive algorithm. Don't use it on anything over about 64x64
    /// </summary>
    public double[][,] DCTMatrices(double[][,] matrices)
    {
        var outMatrices = new double[3][,];
        Parallel.For(0, 3, i => { outMatrices[i] = DCT2D(matrices[i]); });
        return outMatrices;
    }

    /// <summary>
    /// Run the inverse DCT2D on 3-channeled group of matrices
    /// </summary>
    public double[][,] IDCTMatrices(double[][,] matrices)
    {
        var outMatrices = new double[3][,];
        Parallel.For(0, 3, i => { outMatrices[i] = IDCT2D(matrices[i]); });
        return outMatrices;
    }

    /// <summary>
    /// Run a DCT2D on a single matrix.
    /// This is a O(n²) naive algorithm. Don't use it on anything over about 64x64
    /// </summary>
    public double[,] DCT2D(double[,] input)
    {
        var coeffs = new double[Width, Height];

        //To initialise every [u,v] value in the coefficient table...
        for (var u = 0; u < Width; u++)
        {
            for (var v = 0; v < Height; v++)
            {
                //...sum the basis function for every [x,y] value in the bitmap input
                var sum = 0d;


                for (var x = 0; x < Width; x++)
                {
                    for (var y = 0; y < Height; y++)
                    {
                        var a = input[x, y];
                        sum += BasisFunction(a, u, v, x, y);
                    }
                }

                coeffs[u, v] = sum * Beta * Alpha(u) * Alpha(v);
            }
        }

        return coeffs;
    }

    /// <summary>
    /// Run an inverse DCT2D on a single matrix
    /// </summary>
    public double[,] IDCT2D(double[,] coeffs)
    {
        var output = new double[Width, Height];

        //To initialise every [x,y] value in the bitmap output...
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                //...sum the basis function for every [u,v] value in the coefficient table
                var sum = 0d;

                for (var u = 0; u < Width; u++)
                {
                    for (var v = 0; v < Height; v++)
                    {
                        var a = coeffs[u, v];
                        sum += BasisFunction(a, u, v, x, y) * Alpha(u) * Alpha(v);
                    }
                }

                output[x, y] = sum * Beta;
            }
        }

        return output;
    }

    public double BasisFunction(double a, double u, double v, double x, double y)
    {
        var b = Math.Cos(((2d * x + 1d) * u * Math.PI) / (2 * Width));
        var c = Math.Cos(((2d * y + 1d) * v * Math.PI) / (2 * Height));

        return a * b * c;
    }

    /// <summary>
    /// return 1/sqrt(2) if u is not 0
    /// </summary>
    private static double Alpha(int u) => u == 0 ? 1 / Math.Sqrt(2) : 1;

    /// <summary>
    /// normalising value
    /// </summary>
    private double Beta => (1d / Width + 1d / Height);
}