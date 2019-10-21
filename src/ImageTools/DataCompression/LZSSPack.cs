using System;
using System.IO;

namespace ImageTools.DataCompression
{
    /// <summary>
    /// A rough implementation of the LZSS algorithm
    /// https://en.wikipedia.org/wiki/Lempel%E2%80%93Ziv%E2%80%93Storer%E2%80%93Szymanski
    /// </summary>
    /// <remarks>This currently makes no attempt to optimise performance</remarks>
    public class LZSSPack {


        public void Decode(Stream src, Stream dst)
        {

        }

        
        const uint PRIME_BASE = 0x101;
        const uint PRIME_MOD = 0x3B9ACA07;

        public void Encode(Stream src, Stream dst)
        {
            // Format:
            //  [1 bit : flag; 0 = literal, 1 = reference]
            //  literal -> [8 bits : literal byte]
            //  reference -> [11 bits: unsigned (0..2047) distance back in output to the END of the section being referenced] [5 bits: unsigned length (0..31)]
            // Strategy is:
            //  (?) Multiple scans, varying window size -- look for same hash result.
            //      Keep increasing the window size until we get to a safety limit, or no matches.

            // Minimum match size. Tune so matches are longer that the backref data
            var minSize = 3;

            // get a static buffer. TODO: make this a proper streaming impl
            var ms = new MemoryStream();
            src.CopyTo(ms);
            var buffer = ms.ToArray();

            // EXP 1: rolling hash @ size = 4 (catch "and ","the " etc)
            var len = (int)src.Length;
            //var size = 4;

            // values for back references. We keep the longest
            var backRefLen = new int[len];
            var backRefPos = new int[len];
            var hashVals = new uint[len];

            for (int i = 0; i < len; i++) { backRefPos[i]=int.MaxValue; }

            for (int size = minSize; size < 256; size++)
            {
                var anyFound = false;
                // Build up the hashVals array for this window size:
                long power = 1;
                long hash2 = 0;
                for (int i = 0; i < size; i++) { power = (power * PRIME_BASE) % PRIME_MOD; } //calculate the correct 'power' value
                for (int i = 0; i < len; i++)
                {
                    // add the last letter
                    hash2 = hash2 * PRIME_BASE + buffer[i];
                    hash2 %= PRIME_MOD;

                    // remove the first character, if needed
                    if (i >= size) {
                        hash2 -= power * buffer[i - size] % PRIME_MOD;
                        if (hash2 < 0) hash2 += PRIME_MOD;
                    }

                    // store the hash at this point
                    hashVals[i] = (uint)hash2;
                }

                // compare
                // -- any match values are probably a back reference
                for (int fwd = len - 1; fwd >= 0; fwd--)
                {
                    for (int bkw = fwd - 1; bkw >= 0; bkw--)
                    {
                        // record the longest, closest matches
                        if (backRefLen[fwd] >= size || hashVals[fwd] != hashVals[bkw]) continue;
                        anyFound = true;
                        backRefLen[fwd] = size;
                        backRefPos[fwd] = fwd - bkw;
                        fwd -= size - 1; // skip?
                        break;
                    }
                }
                if (!anyFound) {
                    Console.WriteLine($"Ran out of matches at length {size}");
                    break;
                }
            }

            // Now, mark any characters covered by a backreference, so we know not to output
            var rem = 0;
            for (int i = len - 1; i >= 0; i--)
            {
                if (backRefLen[i] > rem) rem = backRefLen[i];
                hashVals[i] = (rem > 0) ? 0u : 1u; // use the hash values to store the output flag
                rem--;
            }


            // Now the backRef* arrays show have all the back ref length and distance values
            // TEST OUTPUT:
            for (int i = 0; i < len; i++)
            {
                if (backRefLen[i] > 0) Console.Write($"({backRefPos[i]:X2},{backRefLen[i]:X2})");
                else if (hashVals[i] > 0) Console.Write((char)buffer[i]);
            }
        }

    }
}