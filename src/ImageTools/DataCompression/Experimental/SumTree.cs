using System;
using ImageTools.DataCompression.Encoding;

namespace ImageTools.DataCompression.Experimental
{
    public interface ISumTree
    {
        /// <summary>
        /// Find the source symbol given a scaled probability (for decoding)
        /// </summary>
        SymbolProbability FindSymbol(ulong scaledValue);
        
        /// <summary>
        /// Add delta to element with index i (zero-based)
        /// </summary>
        void IncrementSymbol(int index, ulong value);
        
        /// <summary>
        /// Return the total scaled probability for all symbols
        /// </summary>
        ulong Total();
        
        /// <summary>
        /// Give a scaled probability for the given symbol (for encoding)
        /// </summary>
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

            var length = _count;
            var end = _tree.Length;
            var start = end - length;
            var v = 1UL;
            while (length > 0)
            {
                for (int i = start; i < end; i++)
                {
                    _tree[i] = v;
                }

                v <<= 1;
                length >>= 1;
                end = start;
                start = end - length;
            }
        }

        public SymbolProbability FindSymbol(ulong scaledValue)
        {
            // Any time we go 'left', we subtract the node value
            // Any time we go right, we do nothing.
            
            var start = _tree[0];
            if (scaledValue > start) return new SymbolProbability {terminates = true};
            
            // Look at the node. If:
            //   equal: return the 'max' index under this point
            //   less: go 'right' -> take current value, 
            //   greater: go 'left'
            
            // the target has a sum less than or equal to the total
            // look at each 'left' branch. If it would give us a value:
            //  equal: return the 'max' index at that point
            //  less: follow right
            //  greater: 
            
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