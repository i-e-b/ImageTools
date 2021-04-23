using System;
using System.Diagnostics;

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
    public class FenwickTree
    {
        private readonly int[] _data;
        private readonly int _size;

        /// <summary>
        /// Construct a new cumulative probability set, where each symbol
        /// start with the same non-zero probability.
        /// </summary>
        /// <param name="size">Number of symbols in the set</param>
        public FenwickTree(int size)
        {
            if (size < 2 || size > 32767) throw new Exception("Invalid tree size");
            _size = size;
            _data = new int[size];
            for (int i = 0; i < size; i++) { _data[i] = 1; }
            Init();
        }

        private static void Assert(bool p)
        {
            Debug.Assert(p, "Assertion failed");
        }

        private static int LeastBit(int i) => (i) & (-i);  // Return the least-significant set bit in i
                                                        // The following identities allow additional optimization,
                                                        // but are omitted from this example code for clarity:
                                                        // i - LSBIT(i)   == i & (i - 1)
                                                        // i + LSBIT(i+1) == i | (i + 1)

        /// <summary>
        /// Returns the sum of the first i elements (indices 0 to i-1)
        /// Equivalent to range_sum(0, i)
        /// </summary>
        public int PrefixSum(int i)
        {
            var sum = 0;
            Assert(0 <= i && i <= _size);
            for (; i > 0; i -= LeastBit(i))
                sum += _data![i - 1];
            return sum;
        }

        /// <summary>
        /// Add delta to element with index i (zero-based)
        /// </summary>
        public void IncrementSymbol(int i, int delta)
        {
            Assert(0 <= i && i < _size);
            for (; i < _size; i += LeastBit(i + 1))
                _data![i] += delta;
        }

        /// <summary>
        /// Returns the sum of elements from i to j-1
        /// Equivalent to prefix_sum(j) - prefix_sum(i), but faster
        /// </summary>
        public int RangeSum(int i, int j)
        {
            int sum = 0;
            Assert(0 <= i && i <= j && j <= _size);
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
        public int[] Histogram()
        {
            var outp = new int[_size];
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
        public int GetSymbolCount(int i)
        {
            return RangeSum(i, i + 1);
        }

        /// <summary>
        /// Set (as opposed to adjust) a single element's individual probability
        /// </summary>
        public void SetSymbolCount(int i, int value)
        {
            IncrementSymbol(i, value - GetSymbolCount(i));
        }

        /// <summary>
        /// Find the largest i with prefix_sum(i) &lt;= value.
        /// NOTE: Requires that all values are non-negative
        /// </summary>
        /// <param name="value">Decoded probability for the symbol we want to find</param>
        /// <returns>Index of matching entry</returns>
        public int FindSymbol(int value)
        {
            int i = 0, j = _size;

            // The following could be precomputed, or use find first set
            while (j != LeastBit(j)) j -= LeastBit(j);
            
            // j is now the highest power of 2 <= SIZE
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