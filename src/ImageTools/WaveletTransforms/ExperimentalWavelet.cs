namespace ImageTools.WaveletTransforms
{
    public class ExperimentalWavelet
    {
        public static void Forward (float[] buf, float[] x, int n, int offset, int stride) {
            float a;
            int i;

            // pick out stride data
            for (i = 0; i < n; i++) { x[i] = buf[i * stride + offset]; }

            // Predict 1
            a = -1.8f;//-1.586134342f;
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
            var b = 1.149604398f;
            a = 1.0f / b;
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

        public static void Inverse (float[] buf, float[] x, int n, int offset, int stride) {
            return;
        }
    }
}