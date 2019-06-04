namespace ImageTools
{
    /// <summary>
    /// https://en.wikipedia.org/wiki/Cohen%E2%80%93Daubechies%E2%80%93Feauveau_wavelet
    /// </summary>
    public static class CDF {

        /// <summary>
        ///  fwt97 - Forward biorthogonal 9/7 wavelet transform (lifting implementation)
        ///<para></para><list type="bullet">
        ///  <item><description>buf is an input signal, which will be replaced by its output transform.</description></item>
        ///  <item><description>x is a temporary buffer and is the length of the signal, and must be a power of 2.</description></item>
        ///  <item><description>s is the stride across the signal (for multi dimensional signals)</description></item>
        /// </list>
        ///<para></para>
        ///  The first half part of the output signal contains the approximation coefficients.
        ///  The second half part contains the detail coefficients (aka. the wavelets coefficients).
        ///<para></para>
        ///  See also iwt97.
        /// </summary>
        public static void Fwt97(float[] buf, float[] x, int offset, int stride)
        {
            float a;
            int i;

            // pick out stride data
            var n = x.Length;
            for (i = 0; i < n; i++) { x[i] = buf[i * stride + offset]; }

            // Predict 1
            a = -1.586134342f;
            for (i = 1; i < n - 2; i += 2)
            {
                x[i] += a * (x[i - 1] + x[i + 1]);
            }
            x[n - 1] += 2 * a * x[n - 2];

            // Update 1
            a = -0.05298011854f;
            for (i = 2; i < n; i += 2)
            {
                x[i] += a * (x[i - 1] + x[i + 1]);
            }
            x[0] += 2 * a * x[1];

            // Predict 2
            a = 0.8829110762f;
            for (i = 1; i < n - 2; i += 2)
            {
                x[i] += a * (x[i - 1] + x[i + 1]);
            }
            x[n - 1] += 2 * a * x[n - 2];

            // Update 2
            a = 0.4435068522f;
            for (i = 2; i < n; i += 2)
            {
                x[i] += a * (x[i - 1] + x[i + 1]);
            }
            x[0] += 2 * a * x[1];

            // Scale
            a = 1.0f / 1.149604398f;
            var b = 1.149604398f;
            for (i = 0; i < n; i+=2)
            {
                x[i] *= a;
                x[i+1] *= b;
            }

            // Pack into buffer (using stride and offset)
            // The raw output is like [DC][AC][DC][AC]...
            // we want it as          [DC][DC]...[AC][AC]
            var hn = n/2;
            for (i = 0; i < hn; i++) {
                buf[i * stride + offset] = x[i*2];
                buf[(i + hn) * stride + offset] = x[1 + i * 2];
            }
        }

        /// <summary>
        /// iwt97 - Inverse biorthogonal 9/7 wavelet transform
        /// <para></para>
        /// This is the inverse of fwt97 so that iwt97(fwt97(x,n),n)=x for every signal x of length n.
        /// <para></para>
        /// See also fwt97.
        /// </summary>
        public static void Iwt97(float[] buf, float[] x, int offset, int stride)
        {
            float a;
            int i;
                        
            // Unpack from stride into working buffer
            // The raw input is like [DC][DC]...[AC][AC]
            // we want it as         [DC][AC][DC][AC]...
            var n = x.Length;
            var hn = n/2;
            for (i = 0; i < hn; i++) {
                x[i*2] = buf[i * stride + offset];
                x[1 + i * 2] = buf[(i + hn) * stride + offset];
            }

            // Undo scale
            a = 1.149604398f;
            var b = 1.0f / 1.149604398f;
            for (i = 0; i < n; i+=2)
            {
                x[i] *= a;
                x[i+1] *= b;
            }

            // Undo update 2
            a = -0.4435068522f;
            for (i = 2; i < n; i += 2)
            {
                x[i] += a * (x[i - 1] + x[i + 1]);
            }
            x[0] += 2 * a * x[1];

            // Undo predict 2
            a = -0.8829110762f;
            for (i = 1; i < n - 2; i += 2)
            {
                x[i] += a * (x[i - 1] + x[i + 1]);
            }
            x[n - 1] += 2 * a * x[n - 2];

            // Undo update 1
            a = 0.05298011854f;
            for (i = 2; i < n; i += 2)
            {
                x[i] += a * (x[i - 1] + x[i + 1]);
            }
            x[0] += 2 * a * x[1];

            // Undo predict 1
            a = 1.586134342f;
            for (i = 1; i < n - 2; i += 2)
            {
                x[i] += a * (x[i - 1] + x[i + 1]);
            }
            x[n - 1] += 2 * a * x[n - 2];
            

            // write back stride data
            for (i = 0; i < n; i++) { buf[i * stride + offset] = x[i]; }
        }

    }
}