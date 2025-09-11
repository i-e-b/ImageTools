using ImageTools.GeneralTypes;

namespace ImageTools.DataCompression.Experimental;

/// <summary>
/// The idea here is to start with a virtual list of all 16 bit values.
/// They are in order from 0x0000..0xFFFF.
/// Each encoded value is the position in the list.
///
/// When an item is first used, it gets copied to the start of the list,
/// as a 'mobile' item.
/// When a mobile item is next used, it gets moved to the front of the list.
///
/// Encoded output is a set of indexes into the list (which need to be able to reach 0x1_FFFF)
/// Probably use Elias/Fib/LEB128 values.
///
/// Counting positions of non mobile items is (value + mobile list length)
/// </summary>
public static class WidePushToFrontEncoder
{
    public static byte[] Compress(byte[] src)
    {
        var result = new List<byte>();
        var mobile = new List<int>();

        var len = src.Length - (src.Length % 2); // TODO: handle end byte

        for (var index = 0; index < len; index += 2)
        {
            var v = (src[index] << 8) | src[index + 1];

            var i = mobile.IndexOf(v);

            if (i < 0) // not in mobile set
            {
                i = v + mobile.Count; // offset into virtual table
                result.AddRange(Leb128.EncodeLeb128ListFromUInt64((ulong)i));

                CopyToMobile(mobile, v);
            }
            else // in mobile set
            {
                result.AddRange(Leb128.EncodeLeb128ListFromUInt64((ulong)i));

                PushToFront(mobile, i, v);
            }
        }

        Console.WriteLine($"Total unique 16 bit values: {mobile.Count}");

        return result.ToArray();
    }

    public static byte[] Decompress(byte[] encoded)
    {
        var result = new List<byte>();
        var mobile = new List<int>();

        int offset = 0;
        // go through every symbol in the encoding
        while (Leb128.DecodeLeb128ToUInt64(encoded, ref offset, encoded.Length, out var i))
        {
            if (i < (ulong)mobile.Count) // in mobile set
            {
                int v = mobile[(int)i];
                result.Add((byte)(v >> 8));
                result.Add((byte)(v & 0xFF));

                PushToFront(mobile, (int)i, v);
            }
            else // not in mobile set
            {
                int v = (int)i - mobile.Count; // offset from virtual table
                result.Add((byte)(v >> 8));
                result.Add((byte)(v & 0xFF));

                CopyToMobile(mobile, v);
            }
        }

        return result.ToArray();
    }

    private static void PushToFront(List<int> mobile, int index, int value)
    {
        mobile.RemoveAt(index);
        mobile.Insert(0, value);
    }

    private static void CopyToMobile(List<int> mobile, int value)
    {
        mobile.Insert(0, value);
    }
}