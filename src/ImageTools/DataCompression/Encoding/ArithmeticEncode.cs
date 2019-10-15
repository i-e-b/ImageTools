using System;
using System.Collections.Generic;
using System.IO;

// ReSharper disable BuiltInTypeReferenceStyle

namespace ImageTools.DataCompression.Encoding
{
    public class ArithmeticEncode
    {
        // Assuming a code value of UInt32

        public const int BIT_SIZE = sizeof(UInt32) * 8;
        public const int PRECISION = BIT_SIZE;
        public const int CODE_VALUE_BITS = (BIT_SIZE + 3) / 2;
        public const int FREQUENCY_BITS = BIT_SIZE - CODE_VALUE_BITS;
        public const uint MAX_CODE = (uint) ((1ul << CODE_VALUE_BITS) - 1);
        public const uint MAX_FREQ = (uint) ((1ul << FREQUENCY_BITS) - 1);
        public const uint ONE_QUARTER = 1u << (CODE_VALUE_BITS - 2);
        public const uint ONE_HALF = 2 * ONE_QUARTER;
        public const uint THREE_QUARTERS = 3 * ONE_QUARTER;


        private readonly IProbabilityModel _model;
        private readonly IBitwiseIO _inout;

        public ArithmeticEncode(IProbabilityModel model, IBitwiseIO inout)
        {
            _model = model ?? throw new Exception("Probability model must be supplied");
            _inout = inout ?? throw new Exception("Bitwise I/O must be supplied");
        }

        public void Encode(Stream data)
        {
            if (data == null || data.CanRead == false) throw new Exception("Invalid stream passed to encoder");

            UInt32 high = MAX_CODE;
            UInt32 low = 0;
            int pending_bits = 0;

            while (true) // data loop
            {
                int c = data.ReadByte();
                if (c < 0) c = 256; // EOF symbol

                var range = high - low + 1;
                var p = _model.GetCurrentProbability(c);
                high = low + (range * p.high / p.count) - 1;
                low = low + (range * p.low / p.count);

                while (true) // symbol encoding loop
                {
                    #region Description
                    //
                    // On each pass there are six possible configurations of high/low,
                    // each of which has its own set of actions. When high or low
                    // is converging, we output their MSB and upshift high and low.
                    // When they are in a near-convergent state, we upshift over the
                    // next-to-MSB, increment the pending count, leave the MSB intact,
                    // and don't output anything. If we are not converging, we do
                    // no shifting and no output.
                    // high: 0xxx, low anything : converging (output 0)
                    // low: 1xxx, high anything : converging (output 1)
                    // high: 10xxx, low: 01xxx : near converging
                    // high: 11xxx, low: 01xxx : not converging
                    // high: 11xxx, low: 00xxx : not converging
                    // high: 10xxx, low: 00xxx : not converging
                    //
                    #endregion

                    if ( high < ONE_HALF ) { // Converging
                        output_bit_plus_pending(0, ref pending_bits);
                    }
                    else if ( low >= ONE_HALF ) { // Converging
                        output_bit_plus_pending(1, ref pending_bits);
                    }
                    else if ( low >= ONE_QUARTER && high < THREE_QUARTERS ) { // Near converging
                        pending_bits++;
                        low -= ONE_QUARTER;  
                        high -= ONE_QUARTER;  
                    } else { // Not converging. Move to next input
                        break;
                    }
                    high <<= 1;
                    high++;
                    low <<= 1;
                    high &= MAX_CODE;
                    low &= MAX_CODE;
                } // end of symbol encoding loop

                if (c >= 256) { // EOF symbol
                    break;
                }
            } // end of data loop
            
            // Ensure we pump out enough bits to have an unambiguous result
            pending_bits++;
            if ( low < ONE_QUARTER ) output_bit_plus_pending(0, ref pending_bits);
            else output_bit_plus_pending(1, ref pending_bits);

            // Done
        }
        
        void output_bit_plus_pending(int bit, ref int pending_bits)
        {
            _inout.OutputBit( bit == 1 );
            while ( pending_bits-- > 0 ) _inout.OutputBit( bit == 0 );
        }
    }

    public struct SymbolProbability { 
        public UInt32 low;
        public UInt32 high; 
        public UInt32 count;
    };

    public interface IBitwiseIO
    {
        void OutputBit(bool value);
    }

    public interface IProbabilityModel
    {
        SymbolProbability GetCurrentProbability(int symbol);
        SymbolProbability GetChar(UInt32 scaledValue, ref int decodedSymbol);

        void Reset();
    }
}

