using ImageTools.ImageDataFormats;

namespace ImageTools.DataCompression.Experimental
{
    /// <summary>
    /// A basic moving probability compressor.
    /// Very simple and low memory use, but very limited compression.
    /// </summary>
    public class PushToFrontEncoder
    {
        /// <summary>
        /// Read original data from <c>src</c>, write compressed data into <c>encoded</c>
        /// </summary>
        public static void Compress(Stream src, Stream encoded)
        {
            // The encoded stream is a reference to positions in a priority list.
            // Any reference that is outside the list means the next byte of data
            // is a new value to add to the list.
            // When we add a new value to the list, it goes at the end
            // When we reference an existing value in the list, it gets moved closer
            // to the start.
            // Values closer to the start should get shorter codes.
            
            //DataEncoding.FibonacciEncodeOne(uint value, BitwiseStreamWrapper output)
            //DataEncoding.FibonacciDecodeOne(BitwiseStreamWrapper input)
            
            if (!encoded.CanWrite || !src.CanRead) throw new Exception("Invalid streams. Must be able to read input and write to output");
            var output = new BitwiseStreamWrapper(encoded, 8);
            var priority = new BytePriorityList();

            while (true)
            {
                var next = src.ReadByte();
                if (next < 0) break;
                
                var pos = priority.GetPosition((byte)next);
                if (pos >= 0)
                {
                    DataEncoding.FibonacciEncodeOne((uint)pos, output);
                }
                else
                {
                    pos = priority.AddValue((byte)next);
                    DataEncoding.FibonacciEncodeOne((uint)pos, output); // we could save 2 bits by assuming this for first value
                    output.WriteByteUnaligned((byte)next);
                }
            }
            
            output.Flush();
        }

        /// <summary>
        /// Read compressed data from <c>encoded</c>, write original data to <c>dst</c>
        /// </summary>
        public static void Decompress(Stream encoded, Stream dst)
        {
            if (!dst.CanWrite || !encoded.CanRead) throw new Exception("Invalid streams. Must be able to read input and write to output");
            var input = new BitwiseStreamWrapper(encoded, 0);
            var priority = new BytePriorityList();
            
            while (!input.IsEmpty())
            {
                var pos = (int)DataEncoding.FibonacciDecodeOne(input);
                
                var next = priority.ReadPosition(pos);
                if (next >= 0)
                {
                    dst.WriteByte((byte)next);
                }
                else
                {
                    if (input.IsEmpty()) break;
                    var add = input.ReadByteUnaligned();
                    priority.AddValue(add);
                    dst.WriteByte(add);
                }
            }
            
            dst.Flush();
        }
    }

    public class BytePriorityList
    {
        private readonly List<byte> _list = new(256);

        public override string ToString()
        {
            return System.Text.Encoding.ASCII.GetString(_list.ToArray());
        }

        /// <summary>
        /// Get (and internally update) position of given value.
        /// Returns <c>-1</c> if value is not in the list
        /// </summary>
        public int GetPosition(byte value)
        {
            for (int i = 0; i < _list.Count; i++)
            {
                if (_list[i] != value) continue;
                
                // Found value. If not in top position, swap it up one
                if (i > 0)
                {
                    _list[i] = _list[i-1];
                    _list[i-1] = value;
                }

                return i;
            }
            return -1;
        }

        /// <summary>
        /// Add a new value to the list.
        /// Returns position of new item
        /// </summary>
        public int AddValue(byte value)
        {
            _list.Add(value);
            return _list.Count - 1;
        }

        /// <summary>
        /// Read value at position. If position is not in the list, returns <c>-1</c>
        /// </summary>
        public int ReadPosition(int pos)
        {
            if (pos < 0 || pos >= _list.Count) return -1;
            
            // Found value. If not in top position, swap it up one
            var result = _list[pos];
            if (pos > 0)
            {
                _list[pos] = _list[pos-1];
                _list[pos-1] = result;
            }
            return result;
        }
    }
}