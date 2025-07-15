using System.Runtime.CompilerServices;

namespace ImageTools.GeneralTypes
{
    public struct Matrix2
    {
        double[] m;
        
        public Matrix2(double m00, double m01, double m10, double m11)
        {
            m = new double[4];
            m[0] = m00;
            m[1] = m01;
            m[2] = m10;
            m[3] = m11;
        }
        
        public static Vector2 operator* (Matrix2 a, Vector2 b) {
            return new Vector2{ 
                X = a.m[0] * b.X + a.m[2] * b.Y,
                Y = a.m[1] * b.X + a.m[3] * b.Y
            };
        }
        
        public static Vector2 operator* (Vector2 b, Matrix2 a) {
            return new Vector2{ 
                X = a.m[0] * b.X + a.m[2] * b.Y,
                Y = a.m[1] * b.X + a.m[3] * b.Y
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Mul(double bX, double bY, out double x, out double y)
        {
            x = m[0] * bX + m[2] * bY;
            y = m[1] * bX + m[3] * bY;
        }

        public Matrix2 Inverse()
        {
            var det = 1 / (m[0]*m[3] - m[1]*m[2]);
            return new Matrix2(
                 m[3] * det, -m[1] * det,
                -m[2] * det,  m[0] * det
            );
        }

        public static Matrix2 Rotation(double radians)
        {
            var cr = Math.Cos(radians);
            var sr = Math.Sin(radians);
            return new Matrix2(
                cr, -sr,
                sr,  cr
            );
        }
    }
}