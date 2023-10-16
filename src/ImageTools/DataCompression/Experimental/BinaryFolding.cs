namespace ImageTools.DataCompression.Experimental
{
    /// <summary>
    /// Experiment with lossless wavelet encoding
    /// </summary>
    public class BinaryFolding
    {
        /// <summary>
        /// Perform a single round Haar transform on a dataset, in place.
        /// </summary>
        public static void Encode(byte[] data)
        {
            // Split AC and DC (interleaved)
            for (int i = 0; i < data.Length; i+=2)
            {
                var left = data[i];
                var right = data[i+1];

                var ave = (byte)((left + right) / 2);
                var diff = (byte)(right - ave);
                data[i] = ave;    // DC on left
                data[i+1] = diff; // AC on right
            }
            
            // Pack into buffer (using stride and offset)
            // The raw output is like [DC][AC][DC][AC]...
            // we want it as          [DC][DC]...[AC][AC]
            // TODO: figure out an efficient in-place for this
            var output = new byte[data.Length];
            var hn = data.Length / 2;
            for (var i = 0; i < hn; i++) {
                output[i] = data[i*2];
                output[i + hn] = data[1 + i * 2];
            }
            
            // copy back
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = output[i];
            }
        }
    }
}