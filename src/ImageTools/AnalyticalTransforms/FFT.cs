namespace ImageTools.AnalyticalTransforms;

// Source - https://stackoverflow.com/a/7817663
// Posted by Gerry Beauregard, modified by community. See post 'Timeline' for change history
// Retrieved 2026-03-26, License - CC BY-SA 4.0

/**
 * Performs an in-place complex FFT.
 */
public class FFT
{
    private readonly double[] _re; // Real components
    private readonly double[] _im; // Imaginary components
    private readonly int[]    _next; // Next element in linked list
    private readonly int[]    _revTgt; // Target position post bit-reversal

    private readonly int _logN; // log2 of FFT size
    private readonly int _n; // FFT size

    /// <summary>
    /// Set up a FFT transform for given scale
    /// </summary>
    /// <param name="logN">log2 of FFT size</param>
    public FFT(int logN)
    {
        _logN = logN;
        _n = 1 << _logN;

        // Allocate elements for linked list of complex numbers.
        _re = new double[_n];
        _im = new double[_n];
        _next = new int[_n];
        _revTgt = new int[_n];


        //_elements = new FFTElement[_n];
        //for (TInt k = 0; k < _n; k++)
        //    _elements[k] = new FFTElement(null);

        // Set up "next" pointers.
        for (var k = 0; k < _n - 1; k++)
        {
            //_elements[k]!.Next = _elements[k + 1];
            _next[k] = k + 1;
        }
        _next[^1] = -1;

        // Specify target for bit reversal re-ordering.
        for (int k = 0; k < _n; k++)
        {
            //_elements[k]!.RevTgt = BitReverse(k, logN);
            _revTgt[k] = BitReverse(k, logN);
        }
    }

    /// <summary>
    /// Basic row/column transposition
    /// </summary>
    public static double[] Transpose(double[] samples, int width)
    {
        var length = samples.Length;
        var result = new double[length];
        var end = samples.Length - 1;

        var src = 0;
        var dst = 0;
        while (src < length)
        {
            result[dst] = samples[src++];
            dst += width;
            if (dst > end) dst -= end;
        }

        return result;
    }

    /// <summary>
    /// Performs in-place complex FFT
    /// </summary>
    /// <param name="xRe">Real part of input/output</param>
    /// <param name="xIm">Imaginary part of input/output</param>
    public void SpaceToFrequency(double[] xRe, double[] xIm)
    {
        Run(xRe, xIm, false);
    }

    /// <summary>
    /// Performs in-place complex iFFT
    /// </summary>
    /// <param name="xRe">Real part of input/output</param>
    /// <param name="xIm">Imaginary part of input/output</param>
    public void FrequencyToSpace(double[] xRe, double[] xIm)
    {
        Run(xRe, xIm, true);
    }

    /// <summary>
    /// Performs in-place complex FFT
    /// </summary>
    /// <param name="xRe">Real part of input/output</param>
    /// <param name="xIm">Imaginary part of input/output</param>
    /// <param name="inverse">If true, do an inverse FFT</param>
    private void Run(
        double[] xRe,
        double[] xIm,
        bool inverse )
    {
        var numFlies   = _n >> 1; // Number of butterflies per sub-FFT
        var span       = _n >> 1; // Width of the butterfly
        var spacing    = _n; // Distance between start of sub-FFTs
        var wIndexStep = 1; // Increment for twiddle table index

        // Copy data into linked complex number objects
        // If it's an iFFT, we divide by N while we're at it
        var x     = 0;
        int k     = 0;
        var scale = inverse ? 1.0 / _n : 1.0;
        while (x >= 0)
        {
            _re[x] = scale*xRe[k];
            _im[x] = scale*xIm[k];
            x = _next[x];
            k++;
        }

        // For each stage of the FFT
        for (var stage = 0; stage < _logN; stage++)
        {
            // Compute a multiplier factor for the "twiddle factors".
            // The twiddle factors are complex unit vectors spaced at
            // regular angular intervals. The angle by which the twiddle
            // factor advances depends on the FFT stage. In many FFT
            // implementations the twiddle factors are cached, but because
            // array lookup is relatively slow in C#, it's just
            // as fast to compute them on the fly.
            var wAngleInc = wIndexStep * 2.0*Math.PI/_n;
            if (inverse == false)
                wAngleInc *= -1;
            var wMulRe = Math.Cos(wAngleInc);
            var wMulIm = Math.Sin(wAngleInc);

            for (var start = 0; start < _n; start += spacing)
            {
                var xTop = start;
                var xBot = start+span;

                var wRe = 1.0;
                var wIm = 0.0;

                // For each butterfly in this stage
                for (var flyCount = 0; flyCount < numFlies; ++flyCount)
                {
                    if (xTop < 0 || xBot < 0) break;

                    // Get the top & bottom values
                    var xTopRe = _re[xTop];//xTop.Re;
                    var xTopIm = _im[xTop];//xTop.Im;
                    var xBotRe = _re[xBot];//xBot.Re;
                    var xBotIm = _im[xBot];//xBot.Im;

                    // Top branch of butterfly has addition
                    /*xTop.Re*/_re[xTop] = xTopRe + xBotRe;
                    /*xTop.Im*/_im[xTop] = xTopIm + xBotIm;

                    // Bottom branch of butterfly has subtraction,
                    // followed by multiplication by twiddle factor
                    xBotRe = xTopRe - xBotRe;
                    xBotIm = xTopIm - xBotIm;
                    /*xBot.Re*/_re[xBot] = xBotRe*wRe - xBotIm*wIm;
                    /*xBot.Im*/_im[xBot] = xBotRe*wIm + xBotIm*wRe;

                    // Advance butterfly to next top & bottom positions
                    xTop = _next[xTop];//xTop.Next;
                    xBot = _next[xBot];//xBot.Next;

                    // Update the twiddle factor, via complex multiply
                    // by unit vector with the appropriate angle
                    // (wRe + j wIm) = (wRe + j wIm) x (wMulRe + j wMulIm)
                    var tRe = wRe;
                    wRe = wRe*wMulRe - wIm*wMulIm;
                    wIm = tRe*wMulIm + wIm*wMulRe;
                }
            }

            numFlies >>= 1;     // Divide by 2 by right shift
            span >>= 1;
            spacing >>= 1;
            wIndexStep <<= 1;   // Multiply by 2 by left shift
        }

        // The algorithm leaves the result in a scrambled order.
        // Unscramble while copying values from the complex
        // linked list elements back to the input/output vectors.
        x = 0;//_elements[0];
        while (x >= 0)
        {
            var target = _revTgt[x];//x.RevTgt;
            xRe[target] = _re[x];//x.Re;
            xIm[target] = _im[x];//x.Im;
            x = _next[x]; //x.Next;
        }
    }

    /// <summary>
    /// Do bit reversal of specified number of places of an int.
    /// For example, 1101 bit-reversed is 1011
    /// </summary>
    /// <param name="x">Number to be bit-reverse</param>
    /// <param name="numBits">Number of bits in the number</param>
    private int BitReverse(
        int x,
        int numBits)
    {
        int y = 0;
        for (int i = 0; i < numBits; i++)
        {
            y <<= 1;
            y |= x & 0x0001;
            x >>= 1;
        }
        return y;
    }

    /// <summary>
    /// Adjust values to fill range
    /// </summary>
    public static void Normalise(double[] values, double max)
    {
        var top = values[0];
        var bot = values[0];

        for (var i = 1; i < values.Length; i++)
        {
            top = Math.Max(top, values[i]);
            bot = Math.Min(bot, values[i]);
        }

        var range = top - bot;
        var scale = max / range;


        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (values[i] - bot) * scale;
        }
    }
}