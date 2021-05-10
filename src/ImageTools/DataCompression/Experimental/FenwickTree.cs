using System;
using ImageTools.DataCompression.Encoding;

namespace ImageTools.DataCompression.Experimental
{
    /// <summary>
    /// Also known as a binary indexed tree.
    /// Stores, retrieves and updates a set of cumulative symbol probabilities in log time.
    /// </summary>
    /// <remarks>
    /// Derived from code posted to https://en.wikipedia.org/wiki/Fenwick_tree
    /// For useful descriptions, see:
    ///     https://cs.stackexchange.com/a/10541
    ///     https://www.topcoder.com/thrive/articles/Binary%20Indexed%20Trees
    /// </remarks>
    public class FenwickTree: ISumTree
    {
        private readonly ulong[] _data;
        private readonly int _size;
        private readonly int _indexHead;
        private readonly int _endSymbol;
        
        private ulong _sum; // a cache of the total sum, as it's used very often

        /// <summary>
        /// Construct a new cumulative probability set, where each symbol
        /// start with the same non-zero probability.
        /// </summary>
        /// <param name="size">Number of symbols in the set</param>
        /// <param name="endSymbol">Symbol which indicates end-of-data</param>
        public FenwickTree(int size, int endSymbol)
        {
            if (size < 2 || size > 32767) throw new Exception("Invalid tree size");
            _size = size;
            
            var ih = size;
            while (ih != LeastBit(ih)) ih -= LeastBit(ih);
            _indexHead = ih; // _indexHead is now the highest power of 2 <= size
            
            _endSymbol = endSymbol;
            _data = new ulong[size];
            for (int i = 0; i < size; i++) { _data[i] = 1; }
            _sum = (ulong)size;
            Init();
        }

        private static int LeastBit(int i) => (i) & (-i);   // Return the least-significant set bit in i
                                                            // The following identities allow additional optimization,
                                                            // but are omitted from this example code for clarity:
                                                            // i - LeastBit(i)   == i & (i - 1)
                                                            // i + LeastBit(i+1) == i | (i + 1)

        /// <summary>
        /// Returns the sum of the first i elements (indices 0 to i-1)
        /// Equivalent to range_sum(0, i)
        /// </summary>
        public ulong PrefixSum(int i)
        {
            ulong sum = 0;
            for (; i > 0; i -= LeastBit(i)) sum += _data![i - 1];
            return sum;
        }

        /// <summary>
        /// Add delta to element with index i (zero-based)
        /// </summary>
        public void IncrementSymbol(int i, ulong delta)
        {
            _sum += delta;
            for (; i < _size; i += LeastBit(i + 1))
                _data![i] += delta;
        }


        public SymbolProbability FindSymbol(ulong scaledValue)
        {
            FindBoundaries(scaledValue, out var idx, out var lower, out var upper);
            return new SymbolProbability
            {
                symbol = idx,
                terminates = idx == _endSymbol,
                low = lower,
                high = upper,
                count = _sum
            };
        }
        
        public ulong Total() => _sum;

        public SymbolProbability EncodeSymbol(int symbol)
        {
            RangeBounds(symbol, out var lower, out var upper);
            
            return new SymbolProbability
            {
                symbol = symbol,
                terminates = symbol == _endSymbol,
                low = lower,
                high = upper,
                count = _sum,
            };
        }

        /// <summary>
        /// This is Find, then RangeBounds.
        /// Contains very compacted code, so see the other methods for definitions
        /// </summary>
        /// <param name="scaledInput">Symbol value from AE decoder</param>
        /// <param name="index">decoded symbol index</param>
        /// <param name="lowerBound">lower bound for that symbol</param>
        /// <param name="upperBound">upper bound for that symbol</param>
        private void FindBoundaries(ulong scaledInput, out int index, out ulong lowerBound, out ulong upperBound)
        {
            int i = 0, j = _indexHead;

            for (; j > 0; j >>= 1)
            {
                if (i + j > _size || _data![i + j - 1] > scaledInput) continue;
                scaledInput -= _data[i + j - 1];
                i += j;
            }
            index = i;
            
            j = i + 1;
            var k = i;
            lowerBound = 0;
            for (; k > 0; k = k & (k - 1)) lowerBound += _data![k - 1];
            
            upperBound = lowerBound;
            for (; j > i; j = j & (j - 1)) upperBound += _data![j - 1];
            for (; i > j; i = i & (i - 1)) upperBound -= _data![i - 1];
        }

        /// <summary>
        /// Returns the sum of elements from i and to i+1
        /// Equivalent to prefix_sum(i), prefix_sum(i+1), but faster
        /// </summary>
        public void RangeBounds(int i, out ulong lower, out ulong upper)
        {
            var j = i + 1;
            var k = i;
            
            lower = 0;
            for (; k > 0; k = k & (k - 1)) lower += _data![k - 1];
            
            upper = lower;
            for (; j > i; j = j & (j - 1)) upper += _data![j - 1];
            for (; i > j; i = i & (i - 1)) upper -= _data![i - 1];
        }

        /// <summary>
        /// Returns the sum of elements from i to j-1
        /// Equivalent to prefix_sum(j) - prefix_sum(i), but faster
        /// </summary>
        public ulong RangeSum(int i, int j)
        {
            ulong sum = 0;
            for (; j > i; j -= LeastBit(j))
                sum += _data![j - 1];
            for (; i > j; i -= LeastBit(i))
                sum -= _data![i - 1];
            return sum;
        }

// Additional helper functions

        /// <summary>
        /// Convert data in place to Fenwick tree form
        /// </summary>
        private void Init()
        {
            for (var i = 0; i < _size; i++)
            {
                var j = i + LeastBit(i + 1);
                if (j < _size) _data![j] += _data[i];
            }
        }

        /// <summary>
        /// Convert back to array of per-element counts
        /// </summary>
        public ulong[] Histogram()
        {
            var outp = new ulong[_size];
            for (var i = 0; i < _size; i++) { outp[i] = _data![i]; }
            for (var i = _size; i-- > 0;)
            {
                var j = i + LeastBit(i + 1);
                if (j < _size) outp[j] -= outp[i];
            }
            return outp;
        }

        /// <summary>
        /// Return a single element's individual probability
        /// </summary>
        public ulong GetSymbolCount(int i)
        {
            return RangeSum(i, i + 1);
        }

        /// <summary>
        /// Set (as opposed to adjust) a single element's individual probability
        /// </summary>
        public void SetSymbolCount(int i, ulong value)
        {
            IncrementSymbol(i, value - GetSymbolCount(i));
        }

        /// <summary>
        /// Find the largest i with prefix_sum(i) &lt;= value.
        /// NOTE: Requires that all values are non-negative
        /// </summary>
        /// <param name="value">Decoded probability for the symbol we want to find</param>
        /// <returns>Index of matching entry</returns>
        public int Find(ulong value)
        {
            int i = 0, j = _indexHead;

            for (; j > 0; j >>= 1)
            {
                if (i + j > _size || _data![i + j - 1] > value) continue;
                value -= _data[i + j - 1];
                i += j;
            }

            return i;
        }
    }
}