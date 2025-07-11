namespace ImageTools.DataCompression.Experimental
{
    /// <summary>
    /// Run-length encoding.
    /// <ul>
    /// <li>Repeated chunks</li>
    /// <li>Each chunk starts with a byte</li>
    /// <li>If 0x40 bit is set, it's a run-length block</li>
    /// <li>If 0x80 bit is set, it's a long block</li>
    /// <li>For short blocks, lower 6 bits are length (1..64)</li>
    /// <li>For long blocks, an extra byte follows for length (6 lower, 7 upper)</li>
    /// <li>If the extra length byte has its top bit set, there is another byte to follow</li>
    /// <li>If 0x80 set, Next byte is repeated for length, otherwise next `length` bytes are literals</li>
    /// </ul>
    /// Bytes must be repeated 3 times to break-even
    /// </summary>
    public static class BytewiseRle
    {
        public static byte[] Compress(byte[] input)
        {
            if (input.Length < 3) return input;
            
            var lengths = new int[input.Length]; // length of run-so-far
            lengths[0] = 1;
            
            // Scan the input for lengths
            for (int i = 1; i < input.Length; i++)
            {
                if (input[i] == input[i-1]) lengths[i] = lengths[i-1]+1;
                else lengths[i] = 1;
            }

            // Now go backward, build a stack of chunks (regardless of max length)
            var runs = new Stack<Chunk>();
            var literalLength = 0;
            for (int i = input.Length - 1; i >= 0; i--)
            {
                if (lengths[i] > 3) // worth being a run
                {
                    if (literalLength > 0) runs.Push(new Chunk{Length = literalLength, IsRun = false});

                    literalLength = 0;
                    runs.Push(new Chunk{Length = lengths[i], IsRun = true});
                    i -= lengths[i] - 1;
                }
                else
                {
                    literalLength++;
                }
            }
            if (literalLength > 0) runs.Push(new Chunk{Length = literalLength, IsRun = false});

            // Now build output
            var checksum = 0;
            var cursor = 0;
            var outp = new List<byte>();
            while (runs.Count > 0)
            {
                var r = runs.Pop();
                checksum += r.Length;

                byte v1 = 0;
                if (r.IsRun) v1 += 0b0100_0000;
                if (r.Length <= 0) throw new Exception("Invalid run length");
                
                var len = r.Length - 1;
                var len1 =  len        & 0b0011_1111;
                var len2 = (len >>  6) & 0b0111_1111;
                var len3 = (len >> 13) & 0b1111_1111;
                if (len2 > 0) v1 += 0b1000_0000;
                if (len3 > 0) len2 += 0b1000_0000;
                if (len3 > 200) throw new Exception($"Over capacity length ({r.Length})");
                
                // Write header
                v1 |= (byte)len1;
                outp.Add(v1);
                if (len2 > 0) outp.Add((byte)len2);
                if (len3 > 0) outp.Add((byte)len3);

                if (r.IsRun)
                {
                    // Write single byte
                    outp.Add(input[cursor]);
                    cursor += r.Length;
                }
                else
                {
                    // write literal data
                    for (var i = 0; i < r.Length; i++)
                    {
                        outp.Add(input[cursor++]);
                    }
                }

            }

            if (checksum != input.Length) throw new Exception("Invalid RLE build");
            
            return outp.ToArray();
        }

        private struct Chunk
        {
            public int Length;
            public bool IsRun;
        }

        public static byte[] Decompress(byte[] input)
        {
            var outp = new List<byte>();
            
            var cursor = 0;
            while (cursor < input.Length && cursor < 600000)
            {
                byte v2 = 0, v3 = 0;
                // Read chunk:
                byte v1 = input[cursor++];
                if ((v1 & 0b1000_0000) != 0) v2 = input[cursor++];
                if ((v2 & 0b1000_0000) != 0) v3 = input[cursor++];
                
                var isRun = (v1 & 0b0100_0000) != 0;
                var length = 1 + (v1 & 0b0011_1111) + ((v2 & 0b0111_1111) << 6) + (v3 << 13);

                if (isRun)
                {
                    var b = input[cursor++];
                    for (int i = 0; i < length; i++)
                    {
                        outp.Add(b);
                    }
                }
                else
                {
                    for (int i = 0; i < length; i++)
                    {
                        outp.Add(input[cursor++]);
                    }
                }
            }
            
            
            return outp.ToArray();
        }
    }
}