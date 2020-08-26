using System;

namespace ImageTools
{
    public class ButterworthBandpass
    {
        private readonly float a1, a2, a3, b1, b2;

        /// <summary>
        /// Array of input values, latest are in front
        /// </summary>
        private readonly float[] inputHistory = new float[2];

        /// <summary>
        /// Array of output values, latest are in front
        /// </summary>
        private readonly float[] outputHistory = new float[3];

        /// <summary>
        /// Setup a streaming Butterworth filter.
        /// </summary>
        /// <param name="frequency">cutoff centre</param>
        /// <param name="sampleRate">units for the frequency</param>
        /// <param name="passType">pass high or low</param>
        /// <param name="resonance">from sqrt(2) to ~ 0.1</param>
        public ButterworthBandpass(float frequency, float sampleRate, PassType passType, float resonance)
        {
            float c;

            switch (passType)
            {
                case PassType.Lowpass:
                    c = 1.0f / (float)Math.Tan(Math.PI * frequency / sampleRate);
                    a1 = 1.0f / (1.0f + resonance * c + c * c);
                    a2 = 2f * a1;
                    a3 = a1;
                    b1 = 2.0f * (1.0f - c * c) * a1;
                    b2 = (1.0f - resonance * c + c * c) * a1;
                    break;
                
                case PassType.Highpass:
                    c = (float)Math.Tan(Math.PI * frequency / sampleRate);
                    a1 = 1.0f / (1.0f + resonance * c + c * c);
                    a2 = -2f * a1;
                    a3 = a1;
                    b1 = 2.0f * (c * c - 1.0f) * a1;
                    b2 = (1.0f - resonance * c + c * c) * a1;
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException(nameof(passType), passType, null);
            }
        }

        public enum PassType
        {
            Highpass,
            Lowpass,
        }

        public float Update(float newInput)
        {
            var newOutput = a1 * newInput + a2 * inputHistory![0] + a3 * inputHistory[1] - b1 * outputHistory![0] - b2 * outputHistory[1];

            inputHistory[1] = inputHistory[0];
            inputHistory[0] = newInput;

            outputHistory[2] = outputHistory[1];
            outputHistory[1] = outputHistory[0];
            outputHistory[0] = newOutput;
            
            return newOutput;
        }

        public float Value => outputHistory![0];

        public void Prime(float value)
        {
            inputHistory[1] = inputHistory[0] = value;
            outputHistory[2] = outputHistory[1] = outputHistory[0] = value;
        }
    }
}