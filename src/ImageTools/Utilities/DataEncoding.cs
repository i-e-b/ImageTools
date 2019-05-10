using System;
using System.Collections.Generic;

namespace ImageTools.Utilities
{
    /// <summary>
    /// Tools to convert arrays to different encodings
    /// </summary>
    public static class DataEncoding
    {
        public static byte[] DoubleToByte_EncodeBytes(double[] buffer)
        {
            var b = new byte[buffer.Length];
            for (int i = 0; i < buffer.Length; i++)
            {
                b[i] = (byte)Saturate(buffer[i]);
            }
            return b;
        }
        
        public static byte[] DoubleToShort_EncodeBytes(double[] buffer)
        {
            var b = new byte[buffer.Length * 2];
            for (int i = 0; i < buffer.Length; i++)
            {
                var s = (short)buffer[i];
                var j = i*2;
                b[j] = (byte)(s >> 8);
                b[j+1] = (byte)(s & 0xff);
            }
            return b;
        }

        public static void ByteToDouble_DecodeBytes(byte[] src, double[] buffer)
        {
            for (int i = 0; i < src.Length; i++)
            {
                buffer[i] = src[i];
            }
        }

        public static void ShortToDouble_DecodeBytes(byte[] src, double[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                var j = i * 2;
                short s = (short)((src[j] << 8) | (src[j+1]));
                buffer[i] = s;
            }
        }
        


        
        /// <summary>
        /// Reverse of RLZ_Encode
        /// </summary>
        private static void RLZ_Decode(byte[] packedSource, double[] buffer)
        {
            int DC = 0;
            var p = 0;
            var lim = buffer.Length - 1;
            bool err = false;
            for (int i = 0; i < packedSource.Length; i++)
            {
                if (p > lim) { err = true; break; }
                var value = packedSource[i];

                if (value == DC) {
                    if (i >= packedSource.Length) { err = true; break; }
                    // next byte is run length
                    for (int j = 0; j < packedSource[i+1]; j++)
                    {
                        buffer[p++] = value;
                        if (p > lim) { err = true; break; }
                    }
                    i++;
                    continue;
                }
                buffer[p++] = value;
            }
            if (err) Console.WriteLine("RLZ Decode did not line up correctly");
            else Console.WriteLine("RLZ A-OK");
        }

        /// <summary>
        /// Run length of zeros
        /// </summary>
        private static byte[] RLZ_Encode(double[] buffer)
        {
            var samples = DataEncoding.DoubleToByte_EncodeBytes(buffer);
            byte DC = 0;

            // the run length encoding is ONLY for zeros...
            // [data] ?[runlength] in bytes.
            //

            var runs = new List<byte>();
            var dcLength = 0;

            for (int i = 0; i < samples.Length; i++)
            {
                var samp = samples[i];
                if (samp == DC && dcLength < 252)
                {
                    dcLength++;
                    continue;
                }

                // non zero sample...
                if (dcLength > 0)
                {
                    runs.Add(DC);
                    runs.Add((byte)dcLength);
                }
                dcLength = 0;
                runs.Add(samp);
            }

            if (dcLength > 0)
            {
                runs.Add(DC);
                runs.Add((byte)dcLength);
            }

            return runs.ToArray();
        }


        private static int Saturate(double value)
        {
            if (value < 0) return 0;
            if (value > 255) return 255;
            return (int)value;
        }
        
    }
}