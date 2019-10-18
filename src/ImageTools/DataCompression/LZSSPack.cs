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

        public void Encode(Stream src, Stream dst)
        {
            // Format:
            //  [1 bit : flag; 0 = literal, 1 = reference]
            //  literal -> [8 bits : literal byte]
            //  reference -> [11 bits: unsigned (0..2047) distance back in output to the END of the section being referenced] [5 bits: unsigned length (0..31)]
            // Strategy is:
            // 
        }
    }
}