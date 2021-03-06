﻿namespace ImageTools.GeneralTypes
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
                Dx = a.m[0] * b.Dx + a.m[2] * b.Dy,
                Dy = a.m[1] * b.Dx + a.m[3] * b.Dy
            };
        }
        
        public static Vector2 operator* (Vector2 b, Matrix2 a) {
            return new Vector2{ 
                Dx = a.m[0] * b.Dx + a.m[2] * b.Dy,
                Dy = a.m[1] * b.Dx + a.m[3] * b.Dy
            };
        }

        public Matrix2 Inverse()
        {
            var det = 1 / (m[0]*m[3] - m[1]*m[2]);
            return new Matrix2(
                 m[3] * det, -m[1] * det,
                -m[2] * det,  m[0] * det
            );
        }
    }
}