// ReSharper disable PossibleNullReferenceException
// ReSharper disable InconsistentNaming
namespace ImageTools.WaveletTransforms
{
    /// <summary>
    /// Experimental wavelet transform using only addition, subtraction and bit-shifts.
    /// Specifically requires no multiplies, divides, or float values.
    /// </summary>
    public class IntegerWavelet
    {
        private static int[] x = new int[2048];

        public static void Forward (float[] buf, float[] _ignored_, int n, int offset, int stride) {
            int i,j;
            // little safety...
            if (n > x.Length) x = new int[n+1];

            // pick out stride data
            for (i = 0, j=offset; i < n; i++, j+=stride) { x[i] = (int)buf[j]; }

            // Predict 1
            for (i = 1; i < n - 2; i += 2)
            {
                x[i] -= (x[i - 1] + x[i + 1]) >> 1;
            }
            x[n - 1] -= x[n - 2];

            // Update 1
            for (i = 2; i < n; i += 2)
            {
                x[i] += (x[i - 1] + x[i + 1]) >> 2;
            }
            x[0] += x[1] >> 1;

            // Pack into buffer (using stride and offset)
            // The raw output is like [DC][AC][DC][AC]...
            // we want it as          [DC][DC]...[AC][AC]
            for (i = 0, j = offset; i < n; i += 2, j += stride) { buf[j] = x[i]; }
            for (i = 1; i < n; i += 2, j += stride) { buf[j] = x[i]; }
        }

        public static void Inverse (float[] buf, float[] _ignored_, int n, int offset, int stride) {
            int i,j;

            // Unpack from stride into working buffer
            // The raw input is like [DC][DC]...[AC][AC]
            // we want it as         [DC][AC][DC][AC]...
            for (i = 0, j = offset; i < n; i += 2, j += stride) { x[i] = (int)buf[j]; }
            for (i = 1; i < n; i += 2, j += stride) { x[i] = (int)buf[j]; }

            // Undo update 1
            for (i = 2; i < n; i += 2)
            {
                x[i] -= (x[i - 1] + x[i + 1]) >> 2;
            }
            x[0] -= x[1] >> 1;

            // Undo predict 1
            for (i = 1; i < n - 2; i += 2)
            {
                x[i] += (x[i - 1] + x[i + 1]) >> 1;
            }
            x[n - 1] += x[n - 2];
            
            // write back stride data
            for (i = 0, j=offset; i < n; i++, j+=stride) { buf[j] = x[i]; }
        }
    }
}