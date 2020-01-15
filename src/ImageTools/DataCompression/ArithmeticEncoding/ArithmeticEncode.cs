using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ImageTools.ImageDataFormats;

// ReSharper disable BuiltInTypeReferenceStyle

namespace ImageTools.DataCompression.Encoding
{
    /// <summary>
    /// A byte-wise arithmetic encoder. Bring your own probability model.
    /// </summary>
    public class ArithmeticEncode
    {
        // Assuming a code value of long

        public const int BIT_SIZE = sizeof(long) * 8;
        public const int CODE_VALUE_BITS = BIT_SIZE / 2;
        public const int PRECISION = CODE_VALUE_BITS;
        public const int FREQUENCY_BITS = BIT_SIZE - CODE_VALUE_BITS;
        public const ulong MAX_CODE = (1ul << CODE_VALUE_BITS) - 1;
        public const ulong MAX_FREQ = (1ul << FREQUENCY_BITS) - 1;
        public const ulong ONE_QUARTER = 1ul << (CODE_VALUE_BITS - 2);
        public const ulong ONE_HALF = 2 * ONE_QUARTER;
        public const ulong THREE_QUARTERS = 3 * ONE_QUARTER;


        private readonly IProbabilityModel _model;
        private readonly int _terminationSymbol;

        /// <summary>
        /// Create a coder with a model and termination code
        /// </summary>
        /// <param name="model">Probability model</param>
        /// <param name="terminationSymbol">End of stream symbol code. This *MUST* be the largest value in the symbol set</param>
        public ArithmeticEncode(IProbabilityModel model, int terminationSymbol = 256)
        {
            _model = model ?? throw new Exception("Probability model must be supplied");
            _terminationSymbol = terminationSymbol;
            if (PRECISION < model.RequiredSymbolBits()) throw new Exception($"Probability model requires more symbol bits than this encoder provides (Requires {model.RequiredSymbolBits()}, {PRECISION} available)");
        }

        
        /// <summary>
        /// Encode the data from an enumerator into the supplied bitwise IO container
        /// </summary>
        public void Encode(IEnumerable<int> source, Stream destination)
        {
            if (source == null) throw new Exception("Invalid input stream passed to encoder");
            if (destination == null || destination.CanWrite == false) throw new Exception("Invalid output stream passed to encoder");

            var target = new BitwiseStreamWrapper(destination, BIT_SIZE);

            using (var src = source.GetEnumerator())
            {
                ulong high = MAX_CODE;
                ulong low = 0;
                int pending_bits = 0;

                while (src.MoveNext()) // data loop
                {
                    int c = src.Current;
                    if (c < 0) c = _terminationSymbol; // EOF symbol

                    var p = _model.GetCurrentProbability(c);
                    var range = high - low + 1;
                    high = low + (range * p.high / p.count) - 1;
                    low = low + (range * p.low / p.count);

                    while (true) // symbol encoding loop
                    {
                        if (high < ONE_HALF)
                        { // Converging
                            output_bit_plus_pending(target, 0, ref pending_bits);
                        }
                        else if (low >= ONE_HALF)
                        { // Converging
                            output_bit_plus_pending(target, 1, ref pending_bits);
                        }
                        else if (low >= ONE_QUARTER && high < THREE_QUARTERS)
                        { // Near converging
                            pending_bits++;
                            low -= ONE_QUARTER;
                            high -= ONE_QUARTER;
                        }
                        else
                        { // Not converging. Move to next input
                            break;
                        }
                        high <<= 1;
                        high++;
                        low <<= 1;
                        high &= MAX_CODE;
                        low &= MAX_CODE;
                    } // end of symbol encoding loop

                    if (c >= _terminationSymbol)
                    { // EOF symbol
                        break;
                    }
                } // end of data loop

                // Ensure we pump out enough bits to have an unambiguous result
                pending_bits++;
                if (low < ONE_QUARTER) output_bit_plus_pending(target, 0, ref pending_bits);
                else output_bit_plus_pending(target, 1, ref pending_bits);

                // Done. Write out final data
                target.Flush();
            }
        }

        /// <summary>
        /// Encode the data from a stream into the supplied bitwise IO container
        /// </summary>
        public void Encode(Stream source, Stream destination)
        {
            if (source == null || source.CanRead == false) throw new Exception("Invalid input stream passed to encoder");
            var srcWrapper = new StreamEnumerationWrapper(source);
            Encode(srcWrapper, destination);
        }
        
        /// <summary>
        /// Decode the data from a supplied bitwise IO container into a byte stream.
        /// Any symbols outside the range of a byte will cause the decode to stop.
        /// </summary>
        public void Decode(Stream source, Stream destination) {
            if (destination == null || destination.CanWrite == false) throw new Exception("Invalid output stream passed to decoder");
            foreach (var code in Decode(source))
            {
                if (code > 255 || code < 0) break;
                destination.WriteByte((byte) code);
            }
        }
        
        /// <summary>
        /// Decode the data from a supplied bitwise IO container into a symbol code enumerator
        /// </summary>
        public IEnumerable<int> Decode(Stream source) {
            if (source == null || source.CanRead == false) throw new Exception("Invalid input stream passed to encoder");

            var src = new BitwiseStreamWrapper(source, BIT_SIZE);

            ulong high = MAX_CODE;
            ulong low = 0;
            ulong value = 0;

            for ( int i = 0 ; i < CODE_VALUE_BITS ; i++ ) {
                value <<= 1;
                value += (ulong)src.ReadBit();
            }

            while (src.CanRead()) { // data loop
                var range = high - low + 1;
                var scaled_value = ((value - low + 1) * _model.GetCount() - 1) / range;
                int c = 0;
                var p = _model.GetChar( scaled_value, ref c );
                if (c == _terminationSymbol) { yield break; }
                if (c > _terminationSymbol) { break; } // something went wrong
                yield return c;

                high = low + (range * p.high) / p.count - 1;
                low = low + (range * p.low) / p.count;

                while (true) { // symbol decoding loop
                    if ( high < ONE_HALF ) {
                        //do nothing, bit is a zero
                    } else if ( low >= ONE_HALF ) {
                        value -= ONE_HALF;  //subtract one half from all three code values
                        low -= ONE_HALF;
                        high -= ONE_HALF;
                    } else if ( low >= ONE_QUARTER && high < THREE_QUARTERS ) {
                        value -= ONE_QUARTER;
                        low -= ONE_QUARTER;
                        high -= ONE_QUARTER;
                    } else {
                        break;
                    }

                    if (!src.CanRead()) { // Unexpected truncation
                        break;
                    }

                    low <<= 1;
                    high <<= 1;
                    high++;
                    value <<= 1;
                    value += (ulong)src.ReadBit();

                } // end of symbol decoding loop
            } // end of data loop

            
            // Unexpected truncation. We should end with a termination symbol.
            yield return -1; // signal the failure
        }

        /// <summary>
        /// Reset the probability model.
        /// Do this when you start a new dataset, or switch from encode to decode
        /// </summary>
        public void Reset()
        {
            _model.Reset();
        }
        
        void output_bit_plus_pending(BitwiseStreamWrapper target, int bit, ref int pending_bits)
        {
            target.WriteBit( bit == 1 );
            while ( pending_bits-- > 0 ) target.WriteBit( bit == 0 );
            pending_bits = 0;
        }
    }

    public class StreamEnumerationWrapper : IEnumerable<int>
    {
        private readonly Stream _source;
        private bool _isOver;

        public StreamEnumerationWrapper(Stream source)
        {
            _source = source;
            _isOver = false;
        }

        /// <inheritdoc />
        public IEnumerator<int> GetEnumerator()
        {
            while (!_isOver)
            {
                var read = _isOver ? -1 : _source.ReadByte();
                if (read < 0) _isOver = true;
                yield return read;
            }
            yield return -1;
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public struct SymbolProbability { 
        public ulong low;
        public ulong high; 
        public ulong count;
    };

    public interface IBitwiseIO
    {
        void OutputBit(bool value);
        uint GetBit();
    }

    public interface IProbabilityModel
    {
        /// <summary>
        /// Encoding step
        /// </summary>
        SymbolProbability GetCurrentProbability(int symbol);

        /// <summary>
        /// Decoding step
        /// </summary>
        SymbolProbability GetChar(ulong scaledValue, ref int decodedSymbol);

        /// <summary>
        /// Reset between encode/decode
        /// </summary>
        void Reset();

        /// <summary>
        /// Current maximum cumulative frequency value
        /// </summary>
        ulong GetCount();

        /// <summary>
        /// Minimum number of symbol bits needed
        /// </summary>
        int RequiredSymbolBits();

        /// <summary>
        /// Any fixed headers that must be supplied with the encoding
        /// </summary>
        byte[] Preamble();

        /// <summary>
        /// Write preamble to stream
        /// </summary>
        void WritePreamble(Stream dest);

        /// <summary>
        /// Read preamble from stream
        /// </summary>
        void ReadPreamble(Stream src);
    }
}

