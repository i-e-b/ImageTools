using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageTools.GeneralTypes
{
    /// <summary>
    /// Encode integer values as a variable-length compact representation.
    /// <p/>
    /// "LEB128" compact representation for signed integer
    /// See https://en.wikipedia.org/wiki/LEB128 and https://jsfiddle.net/i_e_b/t8hsrLyq/
    /// </summary>
    public class Leb128
    {
        /// <summary>
        /// Encode an unsigned value into a buffer, using a variable length encoding.
        /// </summary>
        /// <param name="buffer">buffer to write into</param>
        /// <param name="offset">offset into buffer to start writing at. On success, this will be updated to the position after the last byte written</param>
        /// <param name="bufferLength">buffer length</param>
        /// <param name="value">value to encode</param>
        /// <returns>true if encoded successfully. False if ran out of result space</returns>
        public static bool EncodeLeb128FromUInt64(IList<byte> buffer, ref int offset, int bufferLength, UInt64 value)
        {
            var idx = offset;
            while (true)
            {
                var b = (byte)(value & 0x7f);
                value >>= 7;
                if (value == 0 && (b & 0x40) == 0)
                {
                    buffer[idx++] = b;
                    offset = idx;
                    return true;
                }

                buffer[idx++] = (byte)(b | 0x80);
                if (idx >= bufferLength) return false;
            }
        }

        /// <summary>
        /// Encode an unsigned 64 bit value into a LEB128 byte array
        /// </summary>
        public static byte[] EncodeLeb128ListFromUInt64(ulong value)
        {
            var buffer = new byte[16];
            int offset = 0;
            EncodeLeb128FromUInt64(buffer, ref offset, 10, value);
            return buffer.Take(offset).ToArray();
        }

        /// <summary>
        /// Encode a signed value into a buffer, using a variable length encoding.
        /// </summary>
        /// <param name="buffer">buffer to write into</param>
        /// <param name="offset">offset into buffer to start writing at. On success, this will be updated to the position after the last byte written</param>
        /// <param name="bufferLength">buffer length</param>
        /// <param name="value">value to encode</param>
        /// <returns>true if encoded successfully. False if ran out of result space</returns>
        public static bool EncodeLeb128FromInt64(IList<byte> buffer, ref int offset, int bufferLength, Int64 value)
        {
            int idx = offset;
            while (true)
            {
                var b = (byte)(value & 0x7f);
                value >>= 7;
                if ((value == 0 && (b & 0x40) == 0) || (value == -1 && (b & 0x40) != 0))
                {
                    buffer[idx++] = b;
                    offset = idx;
                    return true;
                }

                buffer[idx++] = (byte)(b | 0x80);
                if (idx >= bufferLength) return false;
            }
        }

        /// <summary>
        /// Encode a signed 64 bit value into a LEB128 byte array
        /// </summary>
        public static byte[] EncodeLeb128ListFromInt64(long value)
        {
            var buffer = new byte[16];
            int offset = 0;
            EncodeLeb128FromInt64(buffer, ref offset, 10, value);
            return buffer.Take(offset).ToArray();
        }

        /// <summary>
        /// Decode a signed value from a buffer containing a LEB variable length encoding.
        /// </summary>
        /// <param name="buffer">buffer to read from</param>
        /// <param name="offset">offset into buffer to start reading at. On success, this will be updated to the position after the last byte read</param>
        /// <param name="bufferLength">buffer length</param>
        /// <param name="value">target for decoded value</param>
        /// <returns>true if decoded successfully, false if ran out of buffer</returns>
        public static bool DecodeLeb128ToInt64(IList<byte> buffer, ref int offset, int bufferLength, out Int64 value)
        {
            value = 0;
            int shift = 0;
            int idx = offset;
            while (true)
            {
                if (idx >= bufferLength) return false;

                byte b = buffer[idx++];
                value |= ((Int64)(b & 0x7f)) << shift;
                shift += 7;
                if ((0x80 & b) == 0)
                {
                    offset = idx;
                    if (shift < 64 && (b & 0x40) != 0)
                    {
                        value |= (-1L << shift); // apply signing
                        return true;
                    }

                    return true;
                }
            }
        }

        /// <summary>
        /// Decode an unsigned value from a buffer containing a LEB variable length encoding.
        /// </summary>
        /// <param name="buffer">buffer to read from</param>
        /// <param name="offset">offset into buffer to start reading at. On success, this will be updated to the position after the last byte read</param>
        /// <param name="bufferLength">buffer length</param>
        /// <param name="value">target for decoded value</param>
        /// <returns>true if decoded successfully, false if ran out of buffer</returns>
        public static bool DecodeLeb128ToUInt64(IList<byte> buffer, ref int offset, int bufferLength, out UInt64 value)
        {
            value = 0;
            int shift = 0;
            int idx = offset;
            while (true)
            {
                if (idx >= bufferLength) return false;

                byte b = buffer[idx++];
                value |= ((UInt64)(b & 0x7f)) << shift;
                shift += 7;
                if ((0x80 & b) == 0)
                {
                    offset = idx;
                    return true;
                }
            }
        }
    }
}