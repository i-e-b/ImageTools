using System;
using System.Collections.Generic;
using System.IO;
using ImageTools.ImageDataFormats;

namespace ImageTools.DataCompression
{
    /// <summary>
    /// A rough implementation of the LZMW algorithm
    /// </summary>
    public class LZMWPack
    {
        private readonly int _sizeLimit;
        private readonly LinkedList<byte[]> _dict;

        public LZMWPack(int sizeLimit)
        {
            _sizeLimit = sizeLimit;
            if (_sizeLimit < 1) _sizeLimit = int.MaxValue;
            _dict = new LinkedList<byte[]>();
        }
        

        public void Decode(MemoryStream src, MemoryStream dst)
        {
            _dict.Clear();
            var pattern = new List<byte>();

            var inp = new BitwiseStreamWrapper(src, 2);

            while (true) {
                // The tricky thing here will be maintaining the order of the implicit dictionary
                // Any time we add an entry, it goes at the front. Any time we *use* an entry, it goes to the front

                var idx = ((int)DataEncoding.FibonacciDecodeOne(inp)) - 1;
                if (inp.IsEmpty()) break;
                var ch = inp.ReadByteUnaligned();

                if (idx >= 0) {
                    // read dictionary entry
                    var buf = ReadAndPushToFront(_dict, idx);
                    dst.Write(buf, 0, buf.Length);
                    pattern.AddRange(buf);
                }

                dst.WriteByte(ch);
                pattern.Add(ch);
                _dict.AddFirst(pattern.ToArray()); // Insert at start
                pattern.Clear();

                // Limit dictionary size
                if (_dict.Count > (_sizeLimit+1)) _dict.RemoveLast();

            }
        }

        private byte[] ReadAndPushToFront(LinkedList<byte[]> dict, int idx)
        {
            var node = dict.First;
            while (idx-- > 0) node = node.Next;

            dict.Remove(node);
            dict.AddFirst(node);

            return node.Value;
        }

        /// <summary>
        /// Compress the input stream into the output stream.
        /// </summary>
        /// <remarks>The output is not byte aligned, and is pairs of fibonacci codes and unaligned bytes</remarks>
        public void Encode(Stream src, Stream dst)
        {
            _dict.Clear();
            var pattern = new List<byte>();

            var outp = new BitwiseStreamWrapper(dst, 1);

            int b;
            while ((b = src.ReadByte()) >= 0) {
                // test for existing pattern match
                // if found, extend the pattern and continue
                // if not, write the dictionary that matches all but the last + the last byte.

                pattern.Add((byte)b);
                if (AnyPrefixMatch(pattern)) { // There are entries for which this is a prefix
                    continue;
                }

                WriteToOutputStream(pattern, outp);

                // Limit dictionary memory size
                if (_dict.Count > (_sizeLimit+1)) _dict.RemoveLast();

                pattern.Clear();
            }

            // last pattern?
            if (pattern.Count > 0)
            {
                WriteToOutputStream(pattern, outp);
            }
            outp.Flush();
        }

        private void WriteToOutputStream(List<byte> pattern, BitwiseStreamWrapper outp)
        {
            // now there should be exactly one entry in the dictionary that is a prefix of the pattern
            var matchIdx = GetMatchIndexAndPushToFront(pattern);

            if (matchIdx >= 0)
            {
                // we have a dictionary match, and should be adding exactly one character.
                DataEncoding.FibonacciEncodeOne((uint) (matchIdx + 1), outp); // backreference (variable length)
                outp.WriteByteUnaligned(LastOf(pattern)); // new extension (fixed length)

                _dict.AddFirst(pattern.ToArray()); // Insert at start
            }
            else
            {
                foreach (var c in pattern)
                {
                    // truncating the dictionary can leave us with extra unmatched characters
                    DataEncoding.FibonacciEncodeOne(0, outp);
                    outp.WriteByteUnaligned(c);

                    _dict.AddFirst(new[] { c }); // Insert at start
                }
            }
        }

        private byte LastOf(List<byte> pattern) { return pattern[pattern.Count - 1]; }

        private int GetMatchIndexAndPushToFront(List<byte> pattern)
        {
            var node = _dict.First;
            int i = -1;
            while (node != null)
            {
                var entry = node.Value;
                var prev = node;
                node = node.Next;

                i++;
                if (i > _sizeLimit) return -1;

                if (entry.Length != pattern.Count - 1) continue;
                if (!EntryIsPrefix(entry, pattern)) continue;

                // pull this match to the front *after* getting the index
                _dict.Remove(prev);
                _dict.AddFirst(prev);

                return i;
            }

            return -1;
        }

        private bool AnyPrefixMatch(List<byte> pattern)
        {
            var node = _dict.First;
            int i = -1;
            while (node != null && i < _sizeLimit)
            {
                i++;
                var entry = node.Value;
                node = node.Next;
                if (i >= (_sizeLimit-1)) return false;

                if (entry.Length < pattern.Count) continue;
                if (!PatternIsPrefix(entry, pattern)) continue;
                return true;
            }
            return false;
        }

        private bool PatternIsPrefix(byte[] entry, List<byte> pattern)
        {
            for (int i = 0; i < pattern.Count; i++)
            {
                if (entry[i] != pattern[i]) return false;
            }
            return true;
        }
        
        private bool EntryIsPrefix(byte[] entry, List<byte> pattern)
        {
            for (int i = 0; i < pattern.Count - 1; i++)
            {
                if (entry[i] != pattern[i]) return false;
            }
            return true;
        }

        public class LZEntry
        {
            public int DictIdx { get; set; }
            public byte Extension { get; set; }
        }
    }
}