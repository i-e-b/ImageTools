using System;
using System.Collections.Generic;

namespace ImageTools.DataCompression.BijectiveEC
{

    public interface IArithmeticEncoder {
        void Encode
        (
            UInt32 p1,  //probability 1 ( 0 - 16384 )
            UInt32 psymlow,
            UInt32 psymhigh
        );
    }

    public class IOByteRun
    {
        public byte c;
        public ulong len;
    };

    public class ArithmeticEncoder : ArithmeticBase, IArithmeticEncoder
    {
        
        //our current interval is [low,low+range)
        UInt32 low,range;
        //the number of bits we have in low.  When this gets to 24, we output
        //a byte, reducing it to 16
        int intervalbits;
        //a low-order mask.  All the free ends have these bits 0
        UInt32 freeendeven;
        //the next free end in the current range.  This number
        //is either 0 (the most even number possible, or
        //(freeendeven+1)*(2x+1)
        UInt32 nextfreeend;


        //input processing
        IArithmeticModel model;
        IByteStream m_in;

        //we delay output of strings matching [\x00-\xfe] \xff*, because
        //we might have to do carry propogation
        byte carrybyte;
        ulong carrybuf;

        Queue<IOByteRun> runqout;

        private void _ASSERT(bool condition)
        {
            if (!condition) throw new Exception("assertion failed");
        }

        void ByteWithCarry(UInt32 outbyte)
        {
            IOByteRun d;
            if (carrybuf != 0)
            {
                if (outbyte>=256)
                {
                    //write carrybuf bytes
                    runqout.Enqueue(new IOByteRun{c = (byte)(carrybyte+1), len = 1 });
                    runqout.Enqueue(new IOByteRun{c = 0, len = carrybuf-1 });
                    carrybuf=1;
                    carrybyte=(byte)outbyte;
                }
                else if (outbyte<255)
                {
                    //write carrybuf bytes
                    runqout.Enqueue(new IOByteRun{c = carrybyte, len = 1 });
                    runqout.Enqueue(new IOByteRun{c = 255, len = carrybuf-1 });
                    carrybuf=1;
                    carrybyte=(byte)outbyte;
                }
                else  //add the 0xFF to carry buf
                    ++carrybuf;
            }
            else
            {
                carrybyte=(byte)outbyte;
                carrybuf=1;
            }
        }

        /// <inheritdoc />
        public void Encode(uint p1, uint psymlow, uint psymhigh)
        {
            UInt32 newh,newl;

  //nextfreeend is known to be in the range here.

  //narrow the interval to encode the symbol

  // I would write this:
  // newl=psymlow*range/p1;
  // newh=psymhigh*range/p1;
  // but that could overflow, so we do it this way

  newl=MulAddDiv24(psymlow,range,0,p1);
  newh=MulAddDiv24(psymhigh,range,0,p1);

  //adjust range
  range=newh-newl;
  low+=newl;

  //make sure nextfreeend is at least low
  if (nextfreeend<low)
    nextfreeend=((low+freeendeven)&~freeendeven)|(freeendeven+1);

  //adjust range
  if (range<=(MAXRANGE>>1))
    {
    //scale range once
    _ASSERT(low<=0x7FFFFFFFL);
    _ASSERT(range<=0x7FFFFFFFL);
    low+=low;
    range+=range;
    nextfreeend+=nextfreeend;
    freeendeven+=freeendeven+1;

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
        //need to output a byte
        //adjust and output
        newl=low&~MAXRANGEMASK;
        low-=newl;
        nextfreeend-=newl;
        //there can only be one number this even in the range.
        //nextfreeend is in the range
        //if nextfreeend is this even, next step must reduce evenness
        freeendeven&=MAXRANGEMASK;

        ByteWithCarry(newl>>MAXRANGEBITS);
        intervalbits-=8;
        }

      if (range>(MAXRANGE>>1))
        break;  //finished scaling range

      //scale again
      _ASSERT(low<=0x7FFFFFFFL);
      _ASSERT(range<=0x7FFFFFFFL);
      low+=low;
      range+=range;
      nextfreeend+=nextfreeend;
      freeendeven+=freeendeven+1;
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
        }
    }
}