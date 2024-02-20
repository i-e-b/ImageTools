namespace ImageTools.DataCompression.Experimental
{
    /// <summary>
    /// Schindler Transform, a sort-based transform similar to BWT.
    /// <p/>
    /// References:<ul>
    /// <li>https://ieeexplore.ieee.org/document/1607307</li>
    /// <li>http://www.compressconsult.com/st/</li>
    /// </ul>
    /// </summary>
    public class Stx
    {
        private const int CNUM = 255; // This is count of distinct symbols. Memory use is 65'796 bytes.
        
        public static byte[] ForwardTransform(byte[] inpbuf)
        {
            var inplen = inpbuf.Length;
            var outbuf = new byte[inplen+2];
            
            uint i,c,p,q;

            var o2 = new uint[CNUM*CNUM + 2];

            p = inpbuf[inplen-2];
            q = inpbuf[inplen-1]; 
            for( i=0; i<inplen; i++ ) {
                c = inpbuf[i];
                o2[1+p+(q<<8)]++;
                p = q; q = c;
            }

            for (i = 0, c = 2; i < CNUM * CNUM; i++)
            {
                o2[i] = c;
                c += o2[i + 1];
            }

            p = inpbuf[inplen-2]; outbuf[0] = (byte)p;
            q = inpbuf[inplen-1]; outbuf[1] = (byte)q;
            for( i=0; i<inplen; i++ ) {
                c = outbuf[ o2[p+(q<<8)]++ ] = inpbuf[i]; 
                p = q; q = c; 
            }

            return outbuf;//inplen+2;
        }

        public static byte[] ReverseTransform(byte[] inpbuf)
        {
            var inplen = inpbuf.Length;
            var outbuf = new byte[inplen-2];
            
            uint i,j,c,p,q;

            var o1 = new uint[CNUM + 2];
            var o2 = new uint[CNUM*CNUM + 2];

            for (i = 2; i < inplen; i++)
            {
                c = inpbuf[i];
                o1[1 + c]++; // symbol freqs
            }

            for (i = 0, c = 2; i < CNUM; i++) // now offsets
            {
                o1[i] = c;
                c += o1[i + 1];
            }
            o1[CNUM] = c;

            for( j=0; j < CNUM; j++ ) { // symbol loop
                q = j;
                for( i=o1[j]; i<o1[j+1]; i++ ) {
                    c = (uint)(inpbuf[i]<<8);
                    o2[1+q+c]++; // byte freqs
                }
            }

            for (i = 0, c = 2; i < CNUM * CNUM; i++)
            {
                o2[i] = c;
                c += o2[i + 1];
            }

            p = inpbuf[0]; 
            q = inpbuf[1]; 
            for( i=0; i<inplen-2; i++ ) {
                c = outbuf[i] = inpbuf[ o2[p+(q<<8)]++ ];
                p = q; q = c; 
            }

            return outbuf; //inplen-2;
        }
    }
}