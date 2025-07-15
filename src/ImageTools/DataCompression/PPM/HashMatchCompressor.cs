namespace ImageTools.DataCompression.PPM
{
/// <summary>
/// General purpose compressor by PPM (Prediction by Partial Matching)
/// A predictor/corrector using a hash table.
/// This implementation is very basic, but is also simple to follow.
/// This does NOT guarantee a smaller-or-equal output size.
/// <p></p>
/// Based on
/// https://bugfix-66.com/5e54291e3c874c3d25ba72a8766e63d4b33e0c9674d607d4fe24d1367b90b298
/// https://bugfix-66.com/d548f3abf6faa823a829e4c770a8babca648a5fdcbf69f9a7ba385bb3199f8f0
/// <p></p>
/// See
/// https://en.wikipedia.org/wiki/Prediction_by_partial_matching
/// </summary>
public class HashMatchCompressor
{
    private readonly int _numBits;  // how many entries in hash table (2^numBits)
    private readonly int _order;    // how many *bytes* of context to use between rounds [1..3]
    private readonly ulong _mixer;  // Hash mixer value (see https://en.wikipedia.org/wiki/Hash_function#Multiplicative_hashing )

    /// <summary>
    /// Create a new compressor with default settings
    /// </summary>
    public HashMatchCompressor()
    {
        _numBits = 12;
        _order = 3;
        _mixer = 0xff51afd7ed558ccdUL;
    }
    
    /// <summary>
    /// Create a new compressor with custom settings
    /// </summary>
    /// <param name="numBits">how many entries in hash table (2^numBits) default:12, range:[2..15]</param>
    /// <param name="order">how many *bytes* of context to use between rounds default:3, range:[1..7]</param>
    /// <param name="mixer">Hash mixer value</param>
    public HashMatchCompressor(int numBits, int order, ulong mixer = 0xff51afd7ed558ccdUL)
    {
        _numBits = numBits;
        _order = order;
        _mixer = mixer;
    }
    
    public byte[] Compress(byte[] from)
    {
        var ctrl = 0;                     // bit flags for predicted or not
        var bit = 128;                    // bit mask for ctrl
        var loc = 0;                      // where we are updating the control byte
        var to = new List<byte>();        // output bytes (needs at least 8 bytes of random access)
        var ctx = 0UL;                    // Hash context. This is data mixed together for prediction
        var lut = new byte[1 << _numBits]; // Hash table, will be built from input data

        to.Add(0);                    // place a byte to hold the ctrl flags
        foreach (var next in from)    // For each byte, processed in order
        {
            var hash = ctx * _mixer;       // Mix the context to derive a hash key
            hash >>= 64 - _numBits;        // Limit hash key length to fit in `lut`
            var pred = lut[hash];         // Look up the predicted value

            if (pred == next)             // If we predicted the value correctly
            {
                ctrl += bit;              // Then set the control bit, but don't write the data value
            }
            else                          // If the prediction was wrong
            {
                lut[hash] = next;         // Then update our hash table for next prediction
                to.Add(next);             // And append the data value, without setting the control bit
            }
            bit >>= 1;                    // move to next control bit
            if (bit == 0)                 // If we've run out of space in the control byte
            {
                to[loc] = (byte)ctrl;     // write the control byte into the output
                ctrl = 0;                 // clear the control bits
                bit = 128;                // reset the current bit
                to.Add(0);                // write a new control byte (we will update later)
                loc = to.Count - 1;       // record the location of the control byte
            }
            ctx <<= 8;                    // make space in context for data
            ctx += next;                  // add data to context
            ctx &= (1ul << (_order * 8)) - 1;  // mask context
        }
        
        to[loc] = (byte)ctrl;         // write final control byte
        return to.ToArray();          // return final output
    }

    public byte[] Decompress(byte[] from)
    {
        var at = 0;                       // cursor in 'from' array
        var ct = from.Length;             // end of input
        var ctx = 0UL;                    // hash context
        var lut = new byte[1 << _numBits]; // Hash table, will be built from output data
        var to = new List<byte>();        // output bytes

        while (at < ct)                                // while cursor not past end
        {
            var ctrl = (int)from[at++];                // read a control byte

            for (var bit = 128; bit > 0; bit >>= 1)    // for each bit in the control byte
            {
                var hash = ctx * _mixer;                // Mix the context to derive a hash key
                hash >>= 64 - _numBits;                 // Limit hash key length to fit in `lut`

                byte next;
                if ((ctrl & bit) == 0)                 // if control bit says not predicted
                {
                    if (at >= ct) return to.ToArray(); // if we're at the end of data, return
                    next = from[at];                   // read literal value from input
                    at++;                              // move cursor forward
                    lut[hash] = next;                  // store value in prediction table
                }
                else                                   // if control bit says prediction correct
                {
                    next = lut[hash];                  // use the predicted value
                }
                ctx <<= 8;                             // make space in context for new value
                ctx += next;                           // add new value to context
                ctx &= (1ul << (_order * 8)) - 1;       // mask context
                to.Add(next);                          // add value to output
            }
        }
        return to.ToArray();
    }
}
}