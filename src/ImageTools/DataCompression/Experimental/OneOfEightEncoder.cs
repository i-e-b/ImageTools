using ImageTools.ImageDataFormats;

namespace ImageTools.DataCompression.Experimental;

/// <summary>
/// Simple bitwise predictor experiment
/// </summary>
public static class OneOfEightEncoder
{
    public static byte[] Compress(byte[] src)
    {
        using var stream = new MemoryStream(src);
        using var buffer = new MemoryStream();

        var inp = new BitwiseStreamWrapper(stream, 0);
        var outp = new BitwiseStreamWrapper(buffer, 0);

        var dict    = new bool[256];
        var context = 0;

        // This can't compress at the moment, as it's one bit in, one bit out.
        // If the prediction is any good, we can use an arithmetic encoder and
        // set the probability of matching to higher than not matching.
        var good = 0;
        var bad  = 0;

        while (inp.TryReadBit(out var bit))
        {
            var guess = dict[context];

            if ((bit == 1) == guess) // guess is good, write a 1 bit
            {
                good++;
                outp.WriteBit(1);
            }
            else // guess is bad, write a 0 bit.
            {
                bad++;
                outp.WriteBit(0);
            }

            // move context forward
            dict[context] = bit == 1;
            context = ((context << 1) + bit) & 0xFF;
        }

        var total   = good + bad;
        var percent = 100.0 * good / total;
        Console.WriteLine($"Good guesses: {good}; Bad guesses: {bad}; ({percent:0.0}%)");

        outp.Flush();
        buffer.Seek(0, SeekOrigin.Begin);
        return buffer.ToArray();
    }

    public static byte[] Decompress(byte[] encoded)
    {
        using var stream = new MemoryStream(encoded);
        using var buffer = new MemoryStream();

        var inp  = new BitwiseStreamWrapper(stream, 0);
        var outp = new BitwiseStreamWrapper(buffer, 0);

        var dict    = new bool[256];
        var context = 0;

        while (inp.TryReadBit(out var flag))
        {
            var guess = dict[context];
            var bit   = guess ? 1 : 0;

            if (flag != 1) bit = 1 - bit; // guess was bad, flip it.

            outp.WriteBit(bit);

            // move context forward
            dict[context] = bit == 1;
            context = ((context << 1) + bit) & 0xFF;
        }

        outp.Flush();
        buffer.Seek(0, SeekOrigin.Begin);
        return buffer.ToArray();
    }
}