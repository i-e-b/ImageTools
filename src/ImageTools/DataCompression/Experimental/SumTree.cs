using System;
using ImageTools.DataCompression.Encoding;

namespace ImageTools.DataCompression.Experimental
{
    public interface ISumTree
    {
        SymbolProbability FindSymbol(ulong scaledValue);
        void IncrementSymbol(int index, ulong value);
        ulong Total();
        SymbolProbability EncodeSymbol(int symbol);
    }

    /// <summary>
    /// Similar to a Fenwick tree, but without the compact and clever indexing.
    /// This makes some operations faster, and others slower -- all operations are
    /// at worst log n.
    /// </summary>
    public class SumTree : ISumTree
    {
        private readonly ulong[] _tree;
        private readonly int _count;

        public SumTree(int symbolCount)
        {
            _count = symbolCount;
            _tree = new ulong[(2 * symbolCount) - 1];


            // TODO: build initial tree here with all symbols = 1
            var start = _tree.Length - _count;
            var end = _tree.Length;
            var v = 1UL;
            while (end - start > 0)
            {
                for (int i = start - 1; i < end; i++)
                {
                    _tree[i] = v;
                }

                v <<= 1;
                end = start - 1;
                start >>= 1;
            }
        }

        public SymbolProbability FindSymbol(ulong scaledValue)
        {
            // TODO: calculate the lower & upper range, and the count -- which is actually the sum of all values + 1
            return new SymbolProbability
            {
                low = 0, //map[lastSymbol, idx],
                high = 1, //map[lastSymbol, idx + 1],
                count = 2 //map[lastSymbol, COUNT_ENTRY]
            };
        }

        public void IncrementSymbol(int index, ulong value)
        {
            // TODO: increment the count at the index, work back up the tree to re-calculate the sums
        }

        public ulong Total()
        {
            return _tree![0];
        }

        public SymbolProbability EncodeSymbol(int symbol)
        {
            throw new NotImplementedException();
        }
    }
}