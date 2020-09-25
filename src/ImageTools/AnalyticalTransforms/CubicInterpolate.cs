using System;
using System.Linq;

// ReSharper disable InconsistentNaming

namespace ImageTools.AnalyticalTransforms
{
    public class CubicSplines
    {
        public static double[] Resample1D(double[] inputs, int sampleCount)
        {
            if (inputs == null) return Array.Empty<double>();
            var dx = (double) inputs.Length / sampleCount;
            var samples = Enumerable.Range(0, sampleCount).Select(j=>j*dx).ToArray();
            return Resample1D(inputs, samples);
        }

        public static double[] Resample1D(double[] inputs, double[] newXPoints)
        {
            if (inputs == null || newXPoints == null) return Array.Empty<double>();
            var buffer = new double[newXPoints.Length];
            Resample1DInto(inputs, newXPoints, buffer);
            return buffer;
        }

        /// <summary>
        /// Resample a function using a cubic spline series
        /// </summary>
        /// <param name="inputs">Function values at integral 'x' co-ordinates (0..n)</param>
        /// <param name="newXPoints">The new 'x' co-ordinates to sample. Each value should be between 0 and n</param>
        /// <param name="outputs">buffer to write into. Must be at least as large as 'newXPoints'</param>
        /// <returns>The interpolated function values for the new 'x' co-ordinates</returns>
        public static void Resample1DInto(double[] inputs, double[] newXPoints, double[] outputs)
        {
            if (inputs == null || newXPoints == null || outputs == null) return;
            if (inputs.Length < 3) throw new Exception("Input too small");
            var n = inputs.Length;
            var sourceX = Enumerable.Range(0, inputs.Length).ToArray();

            /*
             * Spline[i] = f[i] + b[i]*(x - x[i]) + c[i]*(x - x[i])^2 + d[i]*(x - x[i])^3
             * First: We prepare data for algorithm by calculate dx[i]. If dx[i] equal to zero then function return null.
             * Second: We need calculate coefficients b[i]. 
             * b[i] = 3 * ( (f[i] - f[i - 1])*dx[i]/dx[i - 1] + (f[i + 1] - f[i])*dx[i - 1]/dx[i] ),  i = 1, ... , N - 2
             * Calculation of b[0] to b[N - 1] you can see below. And b can be find by means of tri-diagonal matrix A[N, N].
             * 
             * A[N, N] - Tri-diagonal Matrix:
             *      beta(0)     gamma(0)    0           0           0   ...
             *      alpha(1)    beta(1)     gamma(1)    0           0   ...
             *      0           alpha(2)    beta(2)     gamma(2)    0
             *      ...
             * A*x=b
             * We calculate inverse of tri-diagonal matrix by Gauss method and transforming equation A*x=b to the form I*x=b, where I - Identity matrix.
             * Third: Now we can find coefficients c[i], d[i] where i = 0, ... , N - 2
             */

            long nx = n - 1;
            var dx = new double[nx];

            var b = new double[n];
            var alpha = new double[n];
            var beta = new double[n];
            var gamma = new double[n];

            var triDiagonalCoefficients = new double[4,nx];

            for (long i = 0; i + 1 <= nx; i++)
            {
                dx[i] = sourceX[i + 1] - sourceX[i];
                if (dx[i] == 0.0) return;
            }

            for (long i = 1; i + 1 <= nx; i++)
            {
                b[i] = 3.0 * (dx[i] * ((inputs[i] - inputs[i - 1]) / dx[i - 1]) + dx[i - 1] * ((inputs[i + 1] - inputs[i]) / dx[i]));
            }

            b[0] = ((dx[0] + 2.0 * (sourceX[2] - sourceX[0])) * dx[1] * ((inputs[1] - inputs[0]) / dx[0]) +
                        Math.Pow(dx[0], 2.0) * ((inputs[2] - inputs[1]) / dx[1])) / (sourceX[2] - sourceX[0]);

            b[n - 1] = (Math.Pow(dx[nx - 1], 2.0) * ((inputs[n - 2] - inputs[n - 3]) / dx[nx - 2]) + (2.0 * (sourceX[n - 1] - sourceX[n - 3])
                + dx[nx - 1]) * dx[nx - 2] * ((inputs[n - 1] - inputs[n - 2]) / dx[nx - 1])) / (sourceX[n - 1] - sourceX[n - 3]);

            beta[0] = dx[1];
            gamma[0] = sourceX[2] - sourceX[0];
            beta[n - 1] = dx[nx - 1];
            alpha[n - 1] = sourceX[n - 1] - sourceX[n - 3];
            for (long i = 1; i < n - 1; i++)
            {
                beta[i] = 2.0 * (dx[i] + dx[i - 1]);
                gamma[i] = dx[i];
                alpha[i] = dx[i - 1];
            }
            
            double c;
            for (long i = 0; i < n - 1; i++)
            {
                c = beta[i];
                b[i] /= c;
                beta[i] /= c;
                gamma[i] /= c;

                c = alpha[i + 1];
                b[i + 1] -= c * b[i];
                alpha[i + 1] -= c * beta[i];
                beta[i + 1] -= c * gamma[i];
            }

            b[n - 1] /= beta[n - 1];
            beta[n - 1] = 1.0;
            for (long i = n - 2; i >= 0; i--)
            {
                c = gamma[i];
                b[i] -= c * b[i + 1];
                gamma[i] -= c * beta[i];
            }

            for (long i = 0; i < nx; i++)
            {
                var ddz_dx = (inputs[i + 1] - inputs[i]) / Math.Pow(dx[i], 2.0) - b[i] / dx[i];
                var dz_ddx = b[i + 1] / dx[i] - (inputs[i + 1] - inputs[i]) / Math.Pow(dx[i], 2.0);
                triDiagonalCoefficients[0, i] = (dz_ddx - ddz_dx) / dx[i];
                triDiagonalCoefficients[1, i] = (2.0 * ddz_dx - dz_ddx);
                triDiagonalCoefficients[2, i] = b[i];
                triDiagonalCoefficients[3, i] = inputs[i];
            }

            var newY = outputs;
            long j = 0;
            for (long i = 0; i < n - 1; i++)
            {
                if (j >= newXPoints.Length) break;
                while (newXPoints[j] < sourceX[i + 1])
                {
                    var h = newXPoints[j] - sourceX[i];
                    newY[j] = triDiagonalCoefficients[3, i] + h * (triDiagonalCoefficients[2, i] + h * (triDiagonalCoefficients[1, i] + h * triDiagonalCoefficients[0, i] / 3.0) / 2.0);
                    j++;
                    if (j >= newXPoints.Length)
                        break;
                }
                if (j >= newXPoints.Length)
                    break;
            }

            newY[newY.Length - 1] = inputs[n - 1];
        }
        
        
        /// <summary>
        /// Takes an array of 4 equidistant values, returns an interpolated value from
        /// a sample point that can be at a fractional index.
        /// </summary>
        public static double SampleInterpolate1D (double x, params double[] p) {
            return p[1] + 0.5 * x*(p[2] - p[0] + x*(2.0*p[0] - 5.0*p[1] + 4.0*p[2] - p[3] + x*(3.0*(p[1] - p[2]) + p[3] - p[0])));
        }

        /// <summary>
        /// Takes a 4x4 array of equidistant values, returns an interpolated value from
        /// a sample point that can be at a fractional indexes.
        /// </summary>
        public static double SampleInterpolate2D (double[,] p, double x, double y) {
            return SampleInterpolate1D(
                x,
                SampleInterpolate1D(y, p[0,0], p[0,1], p[0,2], p[0,3]),
                SampleInterpolate1D(y, p[1,0], p[1,1], p[1,2], p[1,3]),
                SampleInterpolate1D(y, p[2,0], p[2,1], p[2,2], p[2,3]),
                SampleInterpolate1D(y, p[3,0], p[3,1], p[3,2], p[3,3])
                );
        }
        
        
    }
}