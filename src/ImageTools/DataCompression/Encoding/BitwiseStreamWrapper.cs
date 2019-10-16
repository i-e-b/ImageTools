using System;
using System.IO;

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
}