using System;

namespace ImageTools.DataCompression.Experimental
{
    /// <summary>
    /// Schindler Transform, a sort-based transform similar to BWT.
    /// <p/>
    /// Memory use for either transform is <c>src length + 65798</c> bytes.
    /// <p/>
    /// References:<ul>
    /// <li>https://ieeexplore.ieee.org/document/1607307</li>
    /// <li>http://www.compressconsult.com/st/</li>
    /// </ul>
    /// By Michael Schindler
    /// </summary>
    public static class Stx
    {
        private const int SymbolCount = 256; // This is count of distinct symbols. Memory use is 65'796 bytes.
        
        /// <summary>
        /// Transform source to sorted/clustered output.
        /// Output will be two bytes longer than input.
        /// </summary>
        public static byte[] ForwardTransform(byte[] src)
        {
            var srcLen = src.Length;
            var dst = new byte[srcLen+2];
            
            uint i,c;

            var o2 = new uint[SymbolCount*SymbolCount + 2];

            uint p = src[srcLen-2];
            uint q = src[srcLen-1]; 
            for( i=0; i<srcLen; i++ ) {
                c = src[i];
                
                var idx = 1+p+(q<<8);
                o2[idx]++;
                p = q; q = c;
            }

            for (i = 0, c = 2; i < SymbolCount * SymbolCount; i++)
            {
                o2[i] = c;
                c += o2[i + 1];
            }

            p = src[srcLen-2]; dst[0] = (byte)p;
            q = src[srcLen-1]; dst[1] = (byte)q;
            for( i=0; i<srcLen; i++ ) {
                c = dst[ o2[p+(q<<8)]++ ] = src[i]; 
                p = q; q = c; 
            }

            return dst;
        }

        /// <summary>
        /// Transform source to sorted/clustered output.
        /// Output will be two bytes longer than input.
        /// </summary>
        public static byte[] ForwardTransform(byte[] src, int offset, int length)
        {
            var srcLen = length;
            var dst    = new byte[srcLen+2];

            uint i,c;

            var o2 = new uint[SymbolCount*SymbolCount + 2];

            uint p = src[offset+srcLen-2];
            uint q = src[offset+srcLen-1];
            for( i=0; i<srcLen; i++ ) {
                c = src[offset+i];

                var idx = 1+p+(q<<8);
                o2[idx]++;
                p = q; q = c;
            }

            for (i = 0, c = 2; i < SymbolCount * SymbolCount; i++)
            {
                o2[i] = c;
                c += o2[i + 1];
            }

            p = src[offset+srcLen-2]; dst[0] = (byte)p;
            q = src[offset+srcLen-1]; dst[1] = (byte)q;
            for( i=0; i<srcLen; i++ ) {
                c = dst[ o2[p+(q<<8)]++ ] = src[offset+i];
                p = q; q = c;
            }

            return dst;
        }

        public static byte[] ReverseTransform(byte[] transformData)
        {
            var len = transformData.Length;
            var dst = new byte[len-2];
            
            uint i,j,c, q;

            var o1 = new uint[SymbolCount + 2];
            var o2 = new uint[SymbolCount*SymbolCount + 2];

            for (i = 2; i < len; i++)
            {
                c = transformData[i];
                o1[1 + c]++; // symbol freqs
            }

            for (i = 0, c = 2; i < SymbolCount; i++) // now offsets
            {
                o1[i] = c;
                c += o1[i + 1];
            }
            o1[SymbolCount] = c;

            for( j=0; j < SymbolCount; j++ ) { // symbol loop
                q = j;
                for( i=o1[j]; i<o1[j+1]; i++ ) {
                    c = (uint)(transformData[i]<<8);
                    o2[1+q+c]++; // byte freqs
                }
            }

            for (i = 0, c = 2; i < SymbolCount * SymbolCount; i++)
            {
                o2[i] = c;
                c += o2[i + 1];
            }

            uint p = transformData[0]; 
            q = transformData[1]; 
            for( i=0; i<len-2; i++ ) {
                c = dst[i] = transformData[ o2[p+(q<<8)]++ ];
                p = q; q = c; 
            }

            return dst;
        }
    }
}