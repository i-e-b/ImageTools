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
}