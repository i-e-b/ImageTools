﻿using System;
using System.Diagnostics;
using System.IO;

namespace ImageTools
{
    /// <summary>
    /// DON'T USE THIS.
    /// It causes SEGFAULTs. Need to move it from pointers to arrays to track the issue down.
    /// </summary>
    public static class LzoCompression
    {
        private const uint M2_MAX_LEN = 8;
        private const uint M4_MAX_LEN = 9;
        private const byte M3_MARKER = 32;
        private const byte M4_MARKER = 16;
        private const uint M2_MAX_OFFSET = 0x0800;
        private const uint M3_MAX_OFFSET = 0x4000;
        private const uint M4_MAX_OFFSET = 0xbfff;
        private const byte BITS = 14;
        private const uint D_MASK = (1 << BITS) - 1;
        private const uint DICT_SIZE = 65536 + 3;

        private const int BlockLength = 0xF000;

        public static void Compress(byte[] src, Stream dst) {
            var chunks = Math.DivRem(src.Length, BlockLength, out var remains);
            if (chunks > 0) {
                var inbuf = new byte[BlockLength];
                for (int i = 0; i < chunks; i++)
                {
                    Array.Copy(src, i*BlockLength, inbuf, 0, BlockLength);
                    CompressInternal(inbuf, out var outbuf);
                    dst.Write(outbuf, 0, outbuf.Length);
                }
            }
            if (remains > 0) {
                var inbuf = new byte[remains];
                Array.Copy(src, chunks * BlockLength, inbuf, 0, remains);
                CompressInternal(inbuf, out var outbuf);
                dst.Write(outbuf, 0, outbuf.Length);
            }
        }

        
        public static void Decompress(byte[] src, byte[] dst) {
            DecompressInternal(src, dst);
        }

        private static unsafe void CompressInternal(byte[] src, out byte[] dst)
        {
            if (src == null) {
                dst = null;
                return;
            }
            uint tmp;
            uint dstlen = (uint)(src.Length + (src.Length / 16) + 64 + 3);
            dst = new byte[dstlen];
            if (src.Length <= M2_MAX_LEN + 5)
            {
                tmp = (uint)src.Length;
                dstlen = 0;
            }
            else
            {
                byte[] workmem = new byte[DICT_SIZE];
                fixed (byte* work = workmem, input = src, output = dst)
                {
                    byte** dict = (byte**)work;
                    byte* in_end = input + src.Length;
                    byte* ip_end = input + src.Length - M2_MAX_LEN - 5;
                    byte* ii = input;
                    byte* ip = input + 4;
                    byte* op = output;
                    bool literal = false;
                    bool match = false;

                    for (; ; )
                    {
                        uint offset = 0;
                        var index = D_INDEX1(ip);
                        Debug.Assert(index < DICT_SIZE);
                        var pos = ip - (ip - dict[index]);
                        if (pos < input || (offset = (uint)(ip - pos)) <= 0 || offset > M4_MAX_OFFSET)
                            literal = true;
                        else if (offset <= M2_MAX_OFFSET || pos[3] == ip[3])
                        {
                        }
                        else
                        {
                            index = D_INDEX2(index);
                            Debug.Assert(index < DICT_SIZE);
                            pos = ip - (ip - dict[index]);
                            if (pos < input || (offset = (uint)(ip - pos)) <= 0 || offset > M4_MAX_OFFSET)
                                literal = true;
                            else if (offset <= M2_MAX_OFFSET || pos[3] == ip[3])
                            {
                            }
                            else
                                literal = true;
                        }

                        if (!literal)
                        {
                            if (*((ushort*)pos) == *((ushort*)ip) && pos[2] == ip[2])
                                match = true;
                        }

                        literal = false;
                        if (!match)
                        {
                            Debug.Assert(index < DICT_SIZE);
                            dict[index] = ip;
                            ++ip;
                            if (ip >= ip_end)
                                break;
                            continue;
                        }
                        match = false;
                        
                        Debug.Assert(index < DICT_SIZE);
                        dict[index] = ip;
                        if (ip - ii > 0)
                        {
                            uint t = (uint)(ip - ii);
                            if (t <= 3)
                            {
                                Debug.Assert(op - 2 > output);
                                op[-2] |= (byte)(t);
                            }
                            else if (t <= 18)
                                *op++ = (byte)(t - 3);
                            else
                            {
                                uint tt = t - 18;
                                *op++ = 0;
                                while (tt > 255)
                                {
                                    tt -= 255;
                                    *op++ = 0;
                                }
                                Debug.Assert(tt > 0);
                                *op++ = (byte)(tt);
                            }
                            do
                            {
                                *op++ = *ii++;
                            } while (--t > 0);
                        }
                        Debug.Assert(ii == ip);
                        ip += 3;
                        uint length;
                        if (pos[3] != *ip++ || pos[4] != *ip++ || pos[5] != *ip++
                        || pos[6] != *ip++ || pos[7] != *ip++ || pos[8] != *ip++)
                        {
                            --ip;
                            length = (uint)(ip - ii);
                            Debug.Assert(length >= 3);
                            Debug.Assert(length <= M2_MAX_LEN);
                            if (offset <= M2_MAX_OFFSET)
                            {
                                --offset;
                                *op++ = (byte)(((length - 1) << 5) | ((offset & 7) << 2));
                                *op++ = (byte)(offset >> 3);
                            }
                            else if (offset <= M3_MAX_OFFSET)
                            {
                                --offset;
                                *op++ = (byte)(M3_MARKER | (length - 2));
                                *op++ = (byte)((offset & 63) << 2);
                                *op++ = (byte)(offset >> 6);
                            }
                            else
                            {
                                offset -= 0x4000;
                                Debug.Assert(offset > 0);
                                Debug.Assert(offset <= 0x7FFF);
                                *op++ = (byte)(M4_MARKER | ((offset & 0x4000) >> 11) | (length - 2));
                                *op++ = (byte)((offset & 63) << 2);
                                *op++ = (byte)(offset >> 6);
                            }
                        }
                        else
                        {
                            byte* m = pos + M2_MAX_LEN + 1;
                            while (ip < in_end && *m == *ip)
                            {
                                ++m;
                                ++ip;
                            }
                            length = (uint)(ip - ii);
                            Debug.Assert(length > M2_MAX_LEN);
                            if (offset <= M3_MAX_OFFSET)
                            {
                                --offset;
                                if (length <= 33)
                                    *op++ = (byte)(M3_MARKER | (length - 2));
                                else
                                {
                                    length -= 33;
                                    *op++ = M3_MARKER | 0;
                                    while (length > 255)
                                    {
                                        length -= 255;
                                        *op++ = 0;
                                    }
                                    Debug.Assert(length > 0);
                                    *op++ = (byte)(length);
                                }
                            }
                            else
                            {
                                offset -= 0x4000;
                                Debug.Assert(offset > 0);
                                Debug.Assert(offset <= 0x7FFF);
                                if (length <= M4_MAX_LEN)
                                    *op++ = (byte)(M4_MARKER | ((offset & 0x4000) >> 11) | (length - 2));
                                else
                                {
                                    length -= M4_MAX_LEN;
                                    *op++ = (byte)(M4_MARKER | ((offset & 0x4000) >> 11));
                                    while (length > 255)
                                    {
                                        length -= 255;
                                        *op++ = 0;
                                    }
                                    Debug.Assert(length > 0);
                                    *op++ = (byte)(length);
                                }
                            }
                            *op++ = (byte)((offset & 63) << 2);
                            *op++ = (byte)(offset >> 6);
                        }
                        ii = ip;
                        if (ip >= ip_end)
                            break;
                    }
                    dstlen = (uint)(op - output);
                    tmp = (uint)(in_end - ii);
                }
            }
            if (tmp > 0)
            {
                uint ii = (uint)src.Length - tmp;
                if (dstlen == 0 && tmp <= 238)
                {
                    dst[dstlen++] = (byte)(17 + tmp);
                }
                else if (tmp <= 3)
                {
                    dst[dstlen - 2] |= (byte)(tmp);
                }
                else if (tmp <= 18)
                {
                    dst[dstlen++] = (byte)(tmp - 3);
                }
                else
                {
                    uint tt = tmp - 18;
                    dst[dstlen++] = 0;
                    while (tt > 255)
                    {
                        tt -= 255;
                        dst[dstlen++] = 0;
                    }
                    Debug.Assert(tt > 0);
                    dst[dstlen++] = (byte)(tt);
                }
                do
                {
                    dst[dstlen++] = src[ii++];
                } while (--tmp > 0);
            }
            dst[dstlen++] = M4_MARKER | 1;
            dst[dstlen++] = 0;
            dst[dstlen++] = 0;

            if (dst.Length != dstlen)
            {
                byte[] final = new byte[dstlen];
                Buffer.BlockCopy(dst, 0, final, 0, (int)dstlen);
                dst = final;
            }
        }
        private static unsafe void DecompressInternal(byte[] src, byte[] dst)
        {
            if (src == null || dst == null) return;
            uint t = 0;
            fixed (byte* input = src, output = dst)
            {
                byte* ip_end = input + src.Length;
                byte* op_end = output + dst.Length;
                byte* ip = input;
                byte* op = output;
                bool match = false;
                bool match_next = false;
                bool match_done = false;
                bool copy_match = false;
                bool first_literal_run = false;
                bool eof_found = false;

                if (*ip > 17)
                {
                    t = (uint)(*ip++ - 17);
                    if (t < 4)
                        match_next = true;
                    else
                    {
                        Debug.Assert(t > 0);
                        if ((op_end - op) < t)
                            throw new OverflowException("Output Overrun");
                        if ((ip_end - ip) < t + 1)
                            throw new OverflowException("Input Overrun");
                        do
                        {
                            *op++ = *ip++;
                        } while (--t > 0);
                        first_literal_run = true;
                    }
                }
                while (!eof_found && ip < ip_end)
                {
                    if (!match_next && !first_literal_run)
                    {
                        t = *ip++;
                        if (t >= 16)
                            match = true;
                        else
                        {
                            if (t == 0)
                            {
                                if ((ip_end - ip) < 1)
                                    throw new OverflowException("Input Overrun");
                                while (*ip == 0)
                                {
                                    t += 255;
                                    ++ip;
                                    if ((ip_end - ip) < 1)
                                        throw new OverflowException("Input Overrun");
                                }
                                t += (uint)(15 + *ip++);
                            }
                            Debug.Assert(t > 0);
                            if ((op_end - op) < t + 3)
                                throw new OverflowException("Output Overrun");
                            if ((ip_end - ip) < t + 4)
                                throw new OverflowException("Input Overrun");
                            for (int x = 0; x < 4; ++x, ++op, ++ip)
                                *op = *ip;
                            if (--t > 0)
                            {
                                if (t >= 4)
                                {
                                    do
                                    {
                                        for (int x = 0; x < 4; ++x, ++op, ++ip)
                                            *op = *ip;
                                        t -= 4;
                                    } while (t >= 4);
                                    if (t > 0)
                                    {
                                        do
                                        {
                                            *op++ = *ip++;
                                        } while (--t > 0);
                                    }
                                }
                                else
                                {
                                    do
                                    {
                                        *op++ = *ip++;
                                    } while (--t > 0);
                                }
                            }
                        }
                    }

                    byte* pos;
                    if (!match && !match_next)
                    {
                        first_literal_run = false;

                        t = *ip++;
                        if (t < 16)
                        {
                            pos = op - (1 + M2_MAX_OFFSET);
                            pos -= t >> 2;
                            pos -= *ip++ << 2;
                            if (pos < output || pos >= op)
                                throw new OverflowException("Lookbehind Overrun");
                            if ((op_end - op) < 3)
                                throw new OverflowException("Output Overrun");
                            *op++ = *pos++;
                            *op++ = *pos++;
                            *op++ = *pos;
                            match_done = true;
                        }
                    }
                    match = false;
                    do
                    {
                        if (t >= 64)
                        {
                            pos = op - 1;
                            pos -= (t >> 2) & 7;
                            pos -= *ip++ << 3;
                            t = (t >> 5) - 1;
                            if (pos < output || pos >= op)
                                throw new OverflowException("Lookbehind Overrun");
                            if ((op_end - op) < t + 2)
                                throw new OverflowException("Output Overrun");
                            copy_match = true;
                        }
                        else if (t >= 32)
                        {
                            t &= 31;
                            if (t == 0)
                            {
                                if ((ip_end - ip) < 1)
                                    throw new OverflowException("Input Overrun");
                                while (*ip == 0)
                                {
                                    t += 255;
                                    ++ip;
                                    if ((ip_end - ip) < 1)
                                        throw new OverflowException("Input Overrun");
                                }
                                t += (uint)(31 + *ip++);
                            }
                            pos = op - 1;
                            pos -= (*(ushort*)ip) >> 2;
                            ip += 2;
                        }
                        else if (t >= 16)
                        {
                            pos = op;
                            pos -= (t & 8) << 11;

                            t &= 7;
                            if (t == 0)
                            {
                                if ((ip_end - ip) < 1)
                                    throw new OverflowException("Input Overrun");
                                while (*ip == 0)
                                {
                                    t += 255;
                                    ++ip;
                                    if ((ip_end - ip) < 1)
                                        throw new OverflowException("Input Overrun");
                                }
                                t += (uint)(7 + *ip++);
                            }
                            pos -= (*(ushort*)ip) >> 2;
                            ip += 2;
                            if (pos == op)
                                eof_found = true;
                            else
                                pos -= 0x4000;
                        }
                        else
                        {
                            pos = op - 1;
                            pos -= t >> 2;
                            pos -= *ip++ << 2;
                            if (pos < output || pos >= op)
                                throw new OverflowException("Lookbehind Overrun");
                            if ((op_end - op) < 2)
                                throw new OverflowException("Output Overrun");
                            *op++ = *pos++;
                            *op++ = *pos++;
                            match_done = true;
                        }
                        if (!eof_found && !match_done && !copy_match)
                        {
                            if (pos < output || pos >= op)
                                throw new OverflowException("Lookbehind Overrun");
                            Debug.Assert(t > 0);
                            if ((op_end - op) < t + 2)
                                throw new OverflowException("Output Overrun");
                        }
                        if (!eof_found && t >= 2 * 4 - 2 && (op - pos) >= 4 && !match_done && !copy_match)
                        {
                            for (int x = 0; x < 4; ++x, ++op, ++pos)
                                *op = *pos;
                            t -= 2;
                            do
                            {
                                for (int x = 0; x < 4; ++x, ++op, ++pos)
                                    *op = *pos;
                                t -= 4;

                            } while (t >= 4);
                            if (t > 0)
                            {
                                do
                                {
                                    *op++ = *pos++;
                                } while (--t > 0);
                            }
                        }
                        else if(!eof_found && !match_done)
                        {
                            copy_match = false;

                            *op++ = *pos++;
                            *op++ = *pos++;
                            do
                            {
                                *op++ = *pos++;
                            } while (--t > 0);
                        }

                        if (!eof_found && !match_next)
                        {
                            match_done = false;

                            t = (uint)(ip[-2] & 3);
                            if (t == 0)
                                break;
                        }
                        if (!eof_found)
                        {
                            match_next = false;
                            Debug.Assert(t > 0);
                            Debug.Assert(t < 4);
                            if ((op_end - op) < t)
                                throw new OverflowException("Output Overrun");
                            if ((ip_end - ip) < t + 1)
                                throw new OverflowException("Input Overrun");
                            *op++ = *ip++;
                            if (t > 1)
                            {
                                *op++ = *ip++;
                                if (t > 2)
                                    *op++ = *ip++;
                            }
                            t = *ip++;
                        }
                    } while (!eof_found && ip < ip_end);
                }
                if (!eof_found)
                    throw new OverflowException("EOF Marker Not Found");
                else
                {
                    Debug.Assert(t == 1);
                    if (ip > ip_end)
                        throw new OverflowException("Input Overrun");
                    else if (ip < ip_end)
                        throw new OverflowException("Input Not Consumed");
                }
            }
        }

        private static unsafe uint D_INDEX1(byte * input)
        {
            return D_MS(D_MUL(0x21, D_X3(input, 5, 5, 6)) >> 5, 0);
        }
        private static uint D_INDEX2(uint idx)
        {
            return (idx & D_MASK & 0x7FF) ^ (((D_MASK >> 1) + 1) | 0x1F);
        }
        private static uint D_MS(uint v, byte s)
        {
            return (v & (D_MASK >> s)) << s;
        }
        private static uint D_MUL(uint a, uint b)
        {
            return a * b;
        }
        private static unsafe uint D_X2(byte* input, byte s1, byte s2)
        {
            return (uint)((((input[2] << s2) ^ input[1]) << s1) ^ input[0]);
        }
        private static unsafe uint D_X3(byte* input, byte s1, byte s2, byte s3)
        {
            return (D_X2(input + 1, s2, s3) << s1) ^ input[0];
        }
    }
}
