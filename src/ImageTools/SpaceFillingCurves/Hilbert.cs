namespace ImageTools.SpaceFillingCurves
{
    /// <summary>
    /// Hilbert curve encoding and decoding
    /// </summary>
    public class Hilbert
    {
        /// <summary>
        /// convert (x,y) to d
        /// NOTE: `n` MUST be a power of 2
        /// </summary>
        /// <param name="n">Side length of the grid ((max value - 1) of x and y)</param>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        public static int xy2d (int n, int x, int y) {
            int rx, ry, s, d=0;
            for (s=n/2; s>0; s/=2) {
                rx = ((x & s) > 0) ? 1 : 0;
                ry = ((y & s) > 0) ? 1 : 0;
                d += s * s * ((3 * rx) ^ ry);
                rot(n, ref x, ref y, rx, ry);
            }
            return d;
        }

        /// <summary>
        /// convert d to (x,y)
        /// NOTE: `n` MUST be a power of 2
        /// </summary>
        /// <param name="n">Side length of the grid ((max value - 1) of x and y)</param>
        /// <param name="d">Hilbert coordinate</param>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        public static void d2xy(int n, int d, out int x, out int y)
        {
            int rx, ry, s, t = d;
            x = y = 0;
            for (s=1; s<n; s*=2) {
                rx = 1 & (t/2);
                ry = 1 & (t ^ rx);
                rot(s, ref x, ref y, rx, ry);
                x += s * rx;
                y += s * ry;
                t /= 4;
            }
        }

        //rotate/flip a quadrant appropriately
        private static void rot(int n, ref int x, ref int y, int rx, int ry) {
            if (ry != 0) return;
            if (rx == 1) {
                x = n-1 - x;
                y = n-1 - y;
            }

            //Swap x and y
            int t  = x;
            x = y;
            y = t;
        }
    }
}