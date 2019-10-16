using System;
using System.IO;

// ReSharper disable BuiltInTypeReferenceStyle

namespace ImageTools.DataCompression.Encoding
{
    /// <summary>
    /// A bitwise wrapper around a byte stream. Also provides run-out
    /// </summary>
    public class BitwiseStreamWrapper {
        private readonly Stream _original;
        private int _runoutBits;

        private bool inRunOut;
        private byte readMask, writeMask;
        private int nextOut, currentIn;

        public BitwiseStreamWrapper(Stream original, int runoutBits)
        {
            _original = original ?? throw new Exception("Must not wrap a null stream");
            _runoutBits = runoutBits;

            inRunOut = false;
            readMask = 1;
            writeMask = 0x80;
            nextOut = 0;
            currentIn = 0;
        }

        /// <summary>
        /// Write the current pending output byte (if any)
        /// </summary>
        public void Flush() {
            if (writeMask == 0x80) return; // no pending byte
            _original.WriteByte((byte)nextOut);
            writeMask = 0x80;
            nextOut = 0;
        }

        public void WriteBit(bool value){
            if (value) nextOut |= writeMask;
            writeMask >>= 1;

            if (writeMask == 0)
            {
                _original.WriteByte((byte)nextOut);
                writeMask = 0x80;
                nextOut = 0;
            }
        }

        public int ReadBit()
        {
            if (inRunOut)
            {
                if (_runoutBits-- > 0) return 0;
                throw new Exception("End of input stream");
            }

            if (readMask == 1)
            {
                currentIn = _original.ReadByte();
                if (currentIn < 0)
                {
                    inRunOut = true;
                    if (_runoutBits-- > 0) return 0;
                    throw new Exception("End of input stream");
                }
                readMask = 0x80;
            }
            else
            {
                readMask >>= 1;
            }
            return ((currentIn & readMask) != 0) ? 1 : 0;
        }
    }


    public class ArithmeticEncode
    {
        // Assuming a code value of UInt32

        public const int BIT_SIZE = sizeof(UInt32) * 8;
        public const int CODE_VALUE_BITS = (BIT_SIZE + 3) / 2;
        public const int PRECISION = CODE_VALUE_BITS - 1;
        public const int FREQUENCY_BITS = BIT_SIZE - CODE_VALUE_BITS;
        public const uint MAX_CODE = (1u << CODE_VALUE_BITS) - 1;
        public const uint MAX_FREQ = (1u << FREQUENCY_BITS) - 1;
        public const uint ONE_QUARTER = 1u << (CODE_VALUE_BITS - 2);
        public const uint ONE_HALF = 2 * ONE_QUARTER;
        public const uint THREE_QUARTERS = 3 * ONE_QUARTER;


        private readonly IProbabilityModel _model;

        public ArithmeticEncode(IProbabilityModel model)
        {
            _model = model ?? throw new Exception("Probability model must be supplied");
            if (PRECISION < model.RequiredSymbolBits()) throw new Exception($"Probability model requires more symbol bits than this encoder provides (Requires {model.RequiredSymbolBits()}, {PRECISION} available)");
        }


        /// <summary>
        /// Encode the data from a stream into the supplied bitwise IO container
        /// </summary>
        public void Encode(Stream source, Stream destination)
        {
            if (source == null || source.CanRead == false) throw new Exception("Invalid input stream passed to encoder");
            if (destination == null || destination.CanWrite == false) throw new Exception("Invalid output stream passed to encoder");

            var target = new BitwiseStreamWrapper(destination, BIT_SIZE);

            long high = MAX_CODE;
            long low = 0;
            int pending_bits = 0;

            while (true) // data loop
            {
                int c = source.ReadByte();
                if (c < 0) c = 256; // EOF symbol

                var p = _model.GetCurrentProbability(c);
                var range = high - low + 1;
                high = low + (range * p.high / p.count) - 1;
                low = low + (range * p.low / p.count);

                while (true) // symbol encoding loop
                {
                    if ( high < ONE_HALF ) { // Converging
                        output_bit_plus_pending(target, 0, ref pending_bits);
                    }
                    else if ( low >= ONE_HALF ) { // Converging
                        output_bit_plus_pending(target, 1, ref pending_bits);
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
            if ( low < ONE_QUARTER ) output_bit_plus_pending(target, 0, ref pending_bits);
            else output_bit_plus_pending(target, 1, ref pending_bits);

            // Done. Write out final data
            target.Flush();
        }
        
        /// <summary>
        /// Decode the data from a supplied bitwise IO container into a byte stream
        /// </summary>
        public void Decode(Stream source, Stream destination) {
            if (source == null || source.CanRead == false) throw new Exception("Invalid input stream passed to encoder");
            if (destination == null || destination.CanWrite == false) throw new Exception("Invalid output stream passed to decoder");

            var src = new BitwiseStreamWrapper(source, BIT_SIZE);

            long high = MAX_CODE;
            long low = 0;
            long value = 0;

            for ( int i = 0 ; i < CODE_VALUE_BITS ; i++ ) {
                value <<= 1;
                value += src.ReadBit();
            }
            while (true) { // data loop
                var range = high - low + 1;
                var scaled_value = ((value - low + 1) * _model.GetCount() - 1) / range;
                int c = 0;
                var p = _model.GetChar( scaled_value, ref c );
                if ( c >= 256 ) break;
                destination.WriteByte((byte)c);

                high = low + (range * p.high) / p.count - 1;
                low = low + (range * p.low) / p.count;

                while (true) { // symbol decoding loop
                    if ( high < ONE_HALF ) {
                        //do nothing, bit is a zero
                    } else if ( low >= ONE_HALF ) {
                        value -= ONE_HALF;  //subtract one half from all three code values
                        low -= ONE_HALF;
                        high -= ONE_HALF;
                    } else if ( low >= ONE_QUARTER && high < THREE_QUARTERS ) {
                        value -= ONE_QUARTER;
                        low -= ONE_QUARTER;
                        high -= ONE_QUARTER;
                    } else {
                        break;
                    }
                    low <<= 1;
                    high <<= 1;
                    high++;
                    value <<= 1;
                    value += src.ReadBit();

                } // end of symbol decoding loop
            } // end of data loop
        }

        /// <summary>
        /// Reset the probability model.
        /// Do this when you start a new dataset, or switch from encode to decode
        /// </summary>
        public void Reset()
        {
            _model.Reset();
        }
        
        void output_bit_plus_pending(BitwiseStreamWrapper target, int bit, ref int pending_bits)
        {
            target.WriteBit( bit == 1 );
            while ( pending_bits-- > 0 ) target.WriteBit( bit == 0 );
            pending_bits = 0;
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
        uint GetBit();
    }

    public interface IProbabilityModel
    {
        SymbolProbability GetCurrentProbability(int symbol);
        SymbolProbability GetChar(long scaledValue, ref int decodedSymbol);

        void Reset();
        UInt32 GetCount();

        int RequiredSymbolBits();
    }
}

