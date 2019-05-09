using System;

namespace ImageTools.DataCompression.BijectiveEC
{
    public interface IArithmeticDecoder {
        //callbacks for model
        UInt32 GetP(UInt32 p1);

        void Narrow //narrow the interval after decoding
        (
            UInt32 p1,  //probability 1 ( 0 - 16384 )
            UInt32 psymlow,
            UInt32 psymhigh
        );

    }

    public class ArithmeticDecoder : ArithmeticBase, IArithmeticDecoder
    {

        //our current interval is [low,low+range)
        UInt32 low,range;
        //the number of bits we have in low.  When this gets to 24, we output
        //drop the top 8, reducing it to 16.  This matches the encoder behaviour
        int intervalbits;
        //a low-order mask.  All the free ends have these bits 0
        UInt32 freeendeven;
        //the next free end in the current range.  This number
        //is either 0 (the most even number possible, or
        //(freeendeven+1)*(2x+1)
        UInt32 nextfreeend;

        //the current value is low+(value>>valueshift)
        //when valueshift is <0, we shift it left by 8 and read
        //a byte into the low-order bits
        UInt32 value;
        int valueshift;

        IArithmeticModel model;
        IByteStream m_in;

        /// <inheritdoc />
        public uint GetP(uint p1)
        {
            return (MulAddDiv24(value >> valueshift, p1, p1 - 1, range));
        }

        /// <inheritdoc />
        public void Narrow(uint p1, uint psymlow, uint psymhigh)
        {
            UInt32 newh,newl;

            //newl=psymlow*range/p1;
            //newh=psymhigh*range/p1;
            newl=MulAddDiv24(psymlow,range,0,p1);
            newh=MulAddDiv24(psymhigh,range,0,p1);

            range=newh-newl;
            value-=(newl<<valueshift);
            low+=newl;

            //make sure nextfreeend>=low
            if (nextfreeend<low)
                nextfreeend=((low+freeendeven)&~freeendeven)|(freeendeven+1);


            //adjust range
            if (range<=(MAXRANGE>>1))
            {
                //scale range once
                low+=low;
                range+=range;
                nextfreeend+=nextfreeend;
                freeendeven+=freeendeven+1;
                --valueshift;

                //ensure that nextfreeend is in the range
                while(nextfreeend-low>=range)
                {
                    freeendeven>>=1;
                    //smallest number of the required oddness >= low
                    nextfreeend=((low+freeendeven)&~freeendeven)|(freeendeven+1);
                }

                for(;;)
                {
                    if (++intervalbits==(MAXRANGEBITS+8))
                    {
                        //need to drop a byte
                        newl=low&~MAXRANGEMASK;
                        low-=newl;
                        nextfreeend-=newl;
                        //there can only be one number this even in the range.
                        //nextfreeend is in the range
                        //if nextfreeend is this even, next step must reduce evenness
                        freeendeven&=MAXRANGEMASK;
                        intervalbits-=8;
                    }

                    if (range>(MAXRANGE>>1))
                        break;  //finished scaling range

                    //scale again
                    low+=low;
                    range+=range;
                    nextfreeend+=nextfreeend;
                    freeendeven+=freeendeven+1;
                    --valueshift;
                }
            }
            else
            {
                //ensure that nextfreeend is in the range
                while(nextfreeend-low>=range)
                {
                    freeendeven>>=1;
                    //smallest number of the required oddness >= low
                    nextfreeend=((low+freeendeven)&~freeendeven)|(freeendeven+1);
                }
            }

            //read input until we have enough significant bits for next operation
            while(valueshift<=0)
            {
                //need new bytes
                valueshift+=8;
                value=(value<<8)|m_in.Get();
            }

            return;
        }
    }
}