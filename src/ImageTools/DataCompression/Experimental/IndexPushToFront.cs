namespace ImageTools.DataCompression.Experimental
{
    /// <summary>
    /// Simple push-to-front transform for byte arrays
    /// </summary>
    public static class IndexPushToFront
    {
        public static byte[] Transform(byte[] input)
        {
            var output = new byte[input.Length];
            var dict = new byte[256];
            
            // Initial dictionary
            for (int i = 0; i < 256; i++)
            {
                dict[i] = (byte)i;
            }
            
            // transform
            for (int i = 0; i < input.Length; i++)
            {
                var b = input[i];
                var idx = 0;
                for (int j = 0; j < dict.Length; j++)
                {
                    idx = j;
                    if (dict[j] == b) break;
                }
                
                output[i] = (byte)idx;

                if (idx > 0)
                {
                    // Swap to front
                    //(dict[idx], dict[0]) = (dict[0], dict[idx]);
                    
                    // Bump 1
                    (dict[idx], dict[idx-1]) = (dict[idx-1], dict[idx]);
                }
            }
            
            return output;
        }
    }
}