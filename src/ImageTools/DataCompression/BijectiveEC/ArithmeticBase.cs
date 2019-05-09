using System;

namespace ImageTools.DataCompression.BijectiveEC
{
    public class ArithmeticBase {

        protected const UInt32 BIT16 = 0x10000;
        protected const UInt32 MASK16 = 0x0FFFF;

        protected const int MAXRANGEBITS = 23;
        protected const UInt32 MAXRANGE = ((UInt32)1) << MAXRANGEBITS;
        protected const UInt32 MAXRANGEMASK = MAXRANGE - 1;
        

        protected UInt32 MulAddDiv24(UInt32 a,UInt32 b,UInt32 accum,UInt32 divisor)
        {
            return (UInt32)((((UInt64)a)*b+accum)/divisor);
        }
    }
}