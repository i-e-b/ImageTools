using System.Text;
using ImageTools.ImageDataFormats;

namespace ImageTools.DataCompression
{
    public class LZWPack
    {

        public void Decode(Stream src, Stream dst)
        {
            var dict = new Dictionary<string, uint>();
            InsertStarterEntries(dict);

            var inp = new BitwiseStreamWrapper(src, 1);
            var w = new StringBuilder();
            long prevValue = -1;

            while (true)
            {
                var value = (uint)DataEncoding.FibonacciDecodeOne(inp); // LZW is *all* back-references

                string prevKey = GetKeyByIndex(dict, prevValue) ?? "";
                string key = GetKeyByIndex(dict, value) ?? prevKey;
                if (prevKey.Length == 0) prevKey = key;

                if (inp.IsEmpty()) break;

                var bytes = System.Text.Encoding.UTF8.GetBytes(key);
                dst.Write(bytes, 0, bytes.Length);

                string finalKey = (key.Length > 0) ? (prevKey + key.Substring(0, 1)) : prevKey + prevKey;

                if (!dict.ContainsKey(finalKey))
                {
                    dict.Add(finalKey, (uint)dict.Count);
                }

                prevValue = value;
            }
        }

        private string GetKeyByIndex(IDictionary<string, uint> lookup, long value)
        {
            return (from pair in lookup where pair.Value == value select pair.Key).FirstOrDefault();
        }

        public void Encode(Stream src, Stream dst)
        {
            var dict = new Dictionary<string, uint>();
            InsertStarterEntries(dict);

            var outp = new BitwiseStreamWrapper(dst, 1);
            var w = new StringBuilder();

            // get a static buffer. TODO: make this a proper streaming impl
            var ms = new MemoryStream();
            src.CopyTo(ms);
            var buffer = ms.ToArray();

            int i = 0;
            while (i < buffer.Length)
            {
                w.Clear();
                w.Append((char)buffer[i]);
                i++;

                while (dict.ContainsKey(w.ToString()) && i < buffer.Length)
                {
                    w.Append((char)buffer[i]);
                    i++;
                }

                var key = w.ToString();
                if (!dict.ContainsKey(key))
                {
                    string matchKey = key.Substring(0, w.Length - 1);
                    DataEncoding.FibonacciEncodeOne(dict[matchKey], outp); // OUTPUT

                    dict.Add(key, (uint)dict.Count);
                    i--; // sticking point for streaming. TODO: peek, and pop if match?
                }
                else
                {
                    DataEncoding.FibonacciEncodeOne(dict[key], outp); // OUTPUT
                }
            }
            outp.Flush();
        }

        private void InsertStarterEntries(Dictionary<string, uint> dict)
        {
            for (uint i = 0; i < 256; i++)
            {
                dict.Add(((char)i).ToString(), i);
            }
        }
    }
}