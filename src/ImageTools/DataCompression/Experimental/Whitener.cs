namespace ImageTools.DataCompression.Experimental
{
    /// <summary>
    /// Transforms that try to change the data correlation.
    /// </summary>
    public static class Whitener
    {
        /// <summary>
        /// Bluetooth LE transform
        /// </summary>
        public static void BtLeWhiten(byte[] data, byte whitenCoefficient){
            int idx = 0;
            var len = data.Length;
            while(len-->0){
                byte  m;
                for(m = 1; m != 0; m <<= 1){
		
                    if((whitenCoefficient & 0x80) != 0){
				
                        whitenCoefficient ^= 0x11;
                        data[idx] ^= m;
                    }
                    whitenCoefficient <<= 1;
                }
                idx++;
            }
        }

    }
}