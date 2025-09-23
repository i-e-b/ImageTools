using ImageTools.DataCompression.Experimental;

namespace ImageTools.DataCompression.Encoding;

/// <summary>
/// LZ-type encoder as a symbol-stream for the AC encoder
/// </summary>
public class LzSymbolStream: ISymbolStream
{
    // 0..255: literal byte
    // Reference distance is to the end of the match, so start of match is position - distance - length.
    // Total backref window is 1037 bytes.
    private const int ShortReference = 256; //   0..258 distance, 3..261 length
    private const int MedReference   = 257; // 259..517 distance, 3..261 length
    private const int LongReference  = 258; // 518..776 distance, 3..261 length

    private const int MaxBackRefWindow = 1037;
    private const int MinBackRefLength = 3;
    private const int MaxBackRefLength = 261;

    public const int SymbolCount = 259; // must be highest symbol + 1

    private readonly List<int>  _symbols      = new();
    private readonly List<byte> _decoded      = new();
    private          int        _readPosition = 0;

    /// <summary>
    /// Prepare to decode data
    /// </summary>
    public LzSymbolStream()
    { }

    /// <summary>
    /// Encode the supplied data
    /// </summary>
    public LzSymbolStream(byte[] src)
    {
        int endpoint = 0; // effective length of what we've encoded

        while (endpoint < src.Length)
        {
            if (FoundBackRef(src, endpoint, out var distance, out var length))
            {
                // Encode a back-reference
                if (length is < 3 or > 261) throw new Exception("Backref length is invalid");

                int type, subtract;
                if (distance <= 258)
                {
                    type = ShortReference;
                    subtract = 0;
                }
                else if (distance <= 517)
                {
                    type = MedReference;
                    subtract = 259;
                }
                else if (distance <= 776)
                {
                    type = LongReference;
                    subtract = 518;
                }
                else
                {
                    throw new Exception("Backref is too long");
                }

                _symbols.Add(type);
                _symbols.Add(distance - subtract);
                _symbols.Add(length - 3);
                endpoint += length;
            }
            else
            {
                // Encode a literal byte
                _symbols.Add(src[endpoint]);
                endpoint++;
            }
        }

        Console.WriteLine($"Encoded as {_symbols.Count} symbols");
    }

    private static bool FoundBackRef(byte[] src, int endpoint, out int dist, out int length)
    {
        dist = 0;
        length = 0;

        var start = endpoint - MinBackRefLength; // minimum match size
        if (start < 0) return false;
        var reach = endpoint - MaxBackRefWindow; // maximum reference reach
        if (reach < 0) reach = 0;

        int bestMatchLength = 0;
        int bestMatchEnd    = 0;

        for (int i = start; i >= reach; i--) // scan back through the window
        {
            if (src[i] != src[endpoint]) continue; // no start-of-match here

            for (int j = 1; j <= MaxBackRefLength; j++) // walk forward
            {
                if (i + j >= endpoint) break; // possible reference reached end of known data
                if (endpoint + j >= src.Length) break; // possible match went off end of all data
                if (src[i + j] != src[endpoint + j]) break; // end of matches

                if (j > bestMatchLength) // this is the longest match so far
                {
                    var thisDist = endpoint - (i+j);
                    if (thisDist <= MaxBackRefLength) // and we can encode it
                    {
                        // set the match
                        bestMatchLength = j;
                        bestMatchEnd = i + j;
                    }
                }
            }
        }

        if (bestMatchLength < 3) return false;

        dist = endpoint - bestMatchEnd;
        length = bestMatchLength;
        return true;
    }

    public void WriteSymbol(int symbol)
    {
        _symbols.Add(symbol);
    }

    public void Flush()
    {
        if (_symbols.Count < 1) throw new Exception("Empty data!");
        Console.WriteLine($"Decoding from {_symbols.Count} symbols");

        for (var index = 0; index < _symbols.Count; index++)
        {
            var symbol = _symbols[index];
            if (symbol < 256) // literal
            {
                _decoded.Add((byte)symbol);
                continue;
            }

            // decode back reference
            int lenPlus;
            if (symbol == ShortReference) lenPlus = 0;
            else if (symbol == MedReference) lenPlus = 259;
            else if (symbol == LongReference) lenPlus = 518;
            else throw new Exception($"Invalid ref symbol: {symbol}");

            var refDist   = _symbols[index + 1] + lenPlus;
            var refLength = _symbols[index + 2] + 3;
            var refStart  = _decoded.Count - (refDist + refLength);

            // copy back reference
            for (int i = 0; i < refLength; i++)
            {
                _decoded.Add(_decoded[refStart + i]);
            }

            index += 2;
        }
    }

    public byte[] GetDecoded()
    {
        return _decoded.ToArray();
    }

    public int ReadSymbol()
    {
        if (_readPosition >= _symbols.Count) return -1;
        return _symbols[_readPosition++];
    }
}