using System;
using ImageTools.DataCompression.Encoding;

namespace ImageTools.DataCompression.Experimental
{
    /// <summary>
    /// This is a obvious and slow way to track the probability sum for the arithmetic encoder.
    /// This is used to test the SumTree behaviour
    /// </summary>
    public class DumbTree : ISumTree
    {
        private readonly ulong[] _cumulative;
        private readonly int _countEntry;
        private readonly int _endSymbol;

        /// <summary>
        /// Range for symbol count. for symbols 0..255, give 256.
        /// </summary>
        public DumbTree(int symbolCount, int endSymbol)
        {
            _cumulative = new ulong[symbolCount + 1];
            _countEntry = symbolCount;
            _endSymbol = endSymbol;
            for (int i = 0; i < _cumulative.Length; i++)
            {
                _cumulative[i] = (ulong) i;
            }
        }

        public SymbolProbability FindSymbol(ulong scaledValue)
        {
            // Check range
            if (scaledValue > _cumulative![_countEntry]) return new SymbolProbability {terminates = true};

            // Binary search to find likely symbol
            var stride = _countEntry / 2;
            var idx = stride;
            while (stride > 0)
            {
                var cmp = _cumulative[idx];
                if (scaledValue == cmp)
                {
                    idx++;
                    break;
                }

                if (scaledValue > cmp) idx += stride;
                else idx -= stride;
                stride >>= 1;
            }

            idx--;

            // double check
            if (scaledValue >= _cumulative[idx + 1]) idx++;

            return new SymbolProbability
            {
                symbol = idx,
                terminates = idx == _endSymbol,
                low = _cumulative[idx],
                high = _cumulative[idx + 1],
                count = _cumulative[_countEntry]
            };
        }

        public void IncrementSymbol(int index, ulong value)
        {
            for (int i = index + 1; i < _cumulative!.Length; i++) _cumulative[i] += value;
        }

        public ulong Total()
        {
            return _cumulative![_countEntry];
        }

        public SymbolProbability EncodeSymbol(int symbol)
        {
            if (_cumulative == null) throw new InvalidOperationException("Symbol table missing");
            if (symbol >= _countEntry) throw new Exception("Invalid symbol value");
            
            return new SymbolProbability
            {
                symbol = symbol,
                terminates = symbol == _endSymbol,
                low = _cumulative[symbol],
                high = _cumulative[symbol+1],
                count = _cumulative[_countEntry]
            };
        }
    }
}