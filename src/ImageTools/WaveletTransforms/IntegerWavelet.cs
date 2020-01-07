namespace ImageTools.WaveletTransforms
{
    /// <summary>
    /// Experimental wavelet transform using only addition, subtraction and bitshifts
    /// </summary>
    public class IntegerWavelet
    {
        private static readonly int[] x = new int[2048];

        public static void Forward (float[] buf, float[] x_ign_, int n, int offset, int stride) {
            int i;

            // pick out stride data
            for (i = 0; i < n; i++) { x[i] = (int)buf[i * stride + offset]; }

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
            var hn = n/2;
            for (i = 0; i < hn; i++) {
                buf[i * stride + offset] = x[i*2];
                buf[(i + hn) * stride + offset] = x[1 + i * 2];
            }
        }

        public static void Inverse (float[] buf, float[] x_ign_, int n, int offset, int stride) {
            int i;

            // Unpack from stride into working buffer
            // The raw input is like [DC][DC]...[AC][AC]
            // we want it as         [DC][AC][DC][AC]...
            var hn = n/2;
            for (i = 0; i < hn; i++) {
                x[i*2] = (int)buf[i * stride + offset];
                x[1 + i * 2] = (int)buf[(i + hn) * stride + offset];
            }

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
            for (i = 0; i < n; i++) { buf[i * stride + offset] = x[i]; }
        }
    }
}