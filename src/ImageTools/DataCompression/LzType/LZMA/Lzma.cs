using System;
using System.IO;

// Adapted from https://github.com/dotnet/cli/tree/rel/1.0.0/src/Microsoft.DotNet.Archive/LZMA

namespace ImageTools.DataCompression.LZMA
{
 /// <summary>
/// Compression utility for LZMA (7z) format
/// </summary>
public static class LzmaCompressor
{
    /// <summary>
    /// Compress data from an input stream to an output stream
    /// </summary>
    /// <param name="inStream">data to compress</param>
    /// <param name="outStream">output for compressed data</param>
    public static void Compress(Stream inStream, Stream outStream)
    {
        var encoder = new LzmaEncoder();

        CoderPropId[] propIDs =
        {
            CoderPropId.DictionarySize, CoderPropId.PosStateBits, CoderPropId.LitContextBits,
            CoderPropId.LitPosBits, CoderPropId.Algorithm, CoderPropId.NumFastBytes, CoderPropId.MatchFinder,
            CoderPropId.EndMarker
        };

        object[] properties = { 1 << 26, 1, 8, 0, 2, 96, "bt4", false };

        encoder.SetCoderProperties(propIDs, properties);
        encoder.WriteCoderProperties(outStream);

        var inSize = inStream.Length;
        for (var i = 0; i < 8; i++)
        {
            outStream.WriteByte((byte)(inSize >> (8 * i)));
        }

        encoder.Code(inStream, outStream);
    }

    /// <summary>
    /// Decompress an LZMA input stream into an output stream
    /// </summary>
    /// <param name="inStream">LZMA data to decompress</param>
    /// <param name="outStream">output for restored data</param>
    public static void Decompress(Stream inStream, Stream outStream)
    {
        var properties = new byte[5];

        if (inStream.Read(properties, 0, 5) != 5)
            throw (new Exception("input .lzma is too short"));

        var decoder = new LzmaDecoder();
        decoder.SetDecoderProperties(properties);

        long outSize = 0;
        for (var i = 0; i < 8; i++)
        {
            var v = inStream.ReadByte();
            if (v < 0)
                throw (new Exception("Can't Read 1"));
            outSize |= ((long)(byte)v) << (8 * i);
        }

        decoder.Code(inStream, outStream, outSize);
    }
}

internal abstract class Base
{
    public const uint NumRepDistances = 4;
    public const uint NumStates = 12;

    public struct State
    {
        public uint Index;

        public void Init()
        {
            Index = 0;
        }

        public void UpdateChar()
        {
            if (Index < 4) Index = 0;
            else if (Index < 10) Index -= 3;
            else Index -= 6;
        }

        public void UpdateMatch()
        {
            Index = (uint)(Index < 7 ? 7 : 10);
        }

        public void UpdateRep()
        {
            Index = (uint)(Index < 7 ? 8 : 11);
        }

        public void UpdateShortRep()
        {
            Index = (uint)(Index < 7 ? 9 : 11);
        }

        public bool IsCharState()
        {
            return Index < 7;
        }
    }

    public const int NumPosSlotBits = 6;
    public const int DicLogSizeMin = 0;

    public const int NumLenToPosStatesBits = 2; // it's for speed optimization
    public const uint NumLenToPosStates = 1 << NumLenToPosStatesBits;

    public const uint MatchMinLen = 2;

    public static uint GetLenToPosState(uint len)
    {
        len -= MatchMinLen;
        if (len < NumLenToPosStates)
            return len;
        return NumLenToPosStates - 1;
    }

    public const int NumAlignBits = 4;
    public const uint AlignTableSize = 1 << NumAlignBits;
    public const uint AlignMask = (AlignTableSize - 1);

    public const uint StartPosModelIndex = 4;
    public const uint EndPosModelIndex = 14;

    public const uint NumFullDistances = 1 << ((int)EndPosModelIndex / 2);

    public const uint NumLitPosStatesBitsEncodingMax = 4;
    public const uint NumLitContextBitsMax = 8;

    public const int NumPosStatesBitsMax = 4;
    public const uint NumPosStatesMax = (1 << NumPosStatesBitsMax);
    public const int NumPosStatesBitsEncodingMax = 4;
    public const uint NumPosStatesEncodingMax = (1 << NumPosStatesBitsEncodingMax);

    public const int NumLowLenBits = 3;
    public const int NumMidLenBits = 3;
    public const int NumHighLenBits = 8;
    public const uint NumLowLenSymbols = 1 << NumLowLenBits;
    public const uint NumMidLenSymbols = 1 << NumMidLenBits;

    public const uint NumLenSymbols = NumLowLenSymbols + NumMidLenSymbols +
                                      (1 << NumHighLenBits);

    public const uint MatchMaxLen = MatchMinLen + NumLenSymbols - 1;
}

internal class LzmaEncoder
{
    private enum EMatchFinderType
    {
        Bt2,
        Bt4
    }

    private const uint InfinityPrice = 0xFFFFFFF;

    private static readonly byte[] _gFastPos = new byte[1 << 11];

    static LzmaEncoder()
    {
        const byte kFastSlots = 22;
        var c = 2;
        _gFastPos[0] = 0;
        _gFastPos[1] = 1;
        for (byte slotFast = 2; slotFast < kFastSlots; slotFast++)
        {
            var k = ((uint)1 << ((slotFast >> 1) - 1));
            for (uint j = 0; j < k; j++, c++)
                _gFastPos[c] = slotFast;
        }
    }

    private static uint GetPosSlot(uint pos)
    {
        if (pos < (1 << 11)) return _gFastPos[pos];
        if (pos < (1 << 21)) return (uint)(_gFastPos[pos >> 10] + 20);
        return (uint)(_gFastPos[pos >> 20] + 40);
    }

    private static uint GetPosSlot2(uint pos)
    {
        if (pos < (1 << 17)) return (uint)(_gFastPos[pos >> 6] + 12);
        if (pos < (1 << 27)) return (uint)(_gFastPos[pos >> 16] + 32);
        return (uint)(_gFastPos[pos >> 26] + 52);
    }

    private Base.State _state;
    private byte _previousByte;
    private readonly uint[] _repDistances = new uint[Base.NumRepDistances];

    private void BaseInit()
    {
        _state.Init();
        _previousByte = 0;
        for (uint i = 0; i < Base.NumRepDistances; i++) _repDistances[i] = 0;
    }

    private const int DefaultDictionaryLogSize = 22;
    private const uint NumFastBytesDefault = 0x20;

    private class LiteralEncoder
    {
        public struct Encoder2
        {
            private BitEncoder[] _encoders;

            public void Create()
            {
                _encoders = new BitEncoder[0x300];
            }

            public void Init()
            {
                for (var i = 0; i < 0x300; i++) _encoders[i].Init();
            }

            public void Encode(RangeCoderEncoder rangeEncoder, byte symbol)
            {
                uint context = 1;
                for (var i = 7; i >= 0; i--)
                {
                    var bit = (uint)((symbol >> i) & 1);
                    _encoders[context].Encode(rangeEncoder, bit);
                    context = (context << 1) | bit;
                }
            }

            public void EncodeMatched(RangeCoderEncoder rangeEncoder, byte matchByte, byte symbol)
            {
                uint context = 1;
                var same = true;
                for (var i = 7; i >= 0; i--)
                {
                    var bit = (uint)((symbol >> i) & 1);
                    var state = context;
                    if (same)
                    {
                        var matchBit = (uint)((matchByte >> i) & 1);
                        state += ((1 + matchBit) << 8);
                        same = (matchBit == bit);
                    }

                    _encoders[state].Encode(rangeEncoder, bit);
                    context = (context << 1) | bit;
                }
            }

            public uint GetPrice(bool matchMode, byte matchByte, byte symbol)
            {
                uint price = 0;
                uint context = 1;
                var i = 7;
                if (matchMode)
                {
                    for (; i >= 0; i--)
                    {
                        var matchBit = (uint)(matchByte >> i) & 1;
                        var bit = (uint)(symbol >> i) & 1;
                        price += _encoders[((1 + matchBit) << 8) + context].GetPrice(bit);
                        context = (context << 1) | bit;
                        if (matchBit != bit)
                        {
                            i--;
                            break;
                        }
                    }
                }

                for (; i >= 0; i--)
                {
                    var bit = (uint)(symbol >> i) & 1;
                    price += _encoders[context].GetPrice(bit);
                    context = (context << 1) | bit;
                }

                return price;
            }
        }

        private Encoder2[]? _coders;
        private int _numPrevBits;
        private int _numPosBits;
        private uint _posMask;

        public void Create(int numPosBits, int numPrevBits)
        {
            if (_coders != null && _numPrevBits == numPrevBits && _numPosBits == numPosBits) return;
            _numPosBits = numPosBits;
            _posMask = ((uint)1 << numPosBits) - 1;
            _numPrevBits = numPrevBits;
            var numStates = (uint)1 << (_numPrevBits + _numPosBits);
            _coders = new Encoder2[numStates];
            for (uint i = 0; i < numStates; i++)
                _coders[i].Create();
        }

        public void Init()
        {
            if (_coders is null) throw new Exception("Must call Create() before Init()");
            var numStates = (uint)1 << (_numPrevBits + _numPosBits);
            for (uint i = 0; i < numStates; i++) _coders[i].Init();
        }

        public Encoder2 GetSubCoder(uint pos, byte prevByte)
        {
            if (_coders is null) throw new Exception("Must call Create() before GetSubCoder()");
            return _coders[((pos & _posMask) << _numPrevBits) + (uint)(prevByte >> (8 - _numPrevBits))];
        }
    }

    private class LenEncoder
    {
        private BitEncoder _choice;
        private BitEncoder _choice2;
        private readonly BitTreeEncoder[] _lowCoder = new BitTreeEncoder[Base.NumPosStatesEncodingMax];
        private readonly BitTreeEncoder[] _midCoder = new BitTreeEncoder[Base.NumPosStatesEncodingMax];
        private readonly BitTreeEncoder _highCoder = new(Base.NumHighLenBits);

        protected LenEncoder()
        {
            for (uint posState = 0; posState < Base.NumPosStatesEncodingMax; posState++)
            {
                _lowCoder[posState] = new BitTreeEncoder(Base.NumLowLenBits);
                _midCoder[posState] = new BitTreeEncoder(Base.NumMidLenBits);
            }
        }

        public void Init(uint numPosStates)
        {
            _choice.Init();
            _choice2.Init();
            for (uint posState = 0; posState < numPosStates; posState++)
            {
                _lowCoder[posState].Init();
                _midCoder[posState].Init();
            }

            _highCoder.Init();
        }

        protected void Encode(RangeCoderEncoder rangeEncoder, uint symbol, uint posState)
        {
            if (symbol < Base.NumLowLenSymbols)
            {
                _choice.Encode(rangeEncoder, 0);
                _lowCoder[posState].Encode(rangeEncoder, symbol);
            }
            else
            {
                symbol -= Base.NumLowLenSymbols;
                _choice.Encode(rangeEncoder, 1);
                if (symbol < Base.NumMidLenSymbols)
                {
                    _choice2.Encode(rangeEncoder, 0);
                    _midCoder[posState].Encode(rangeEncoder, symbol);
                }
                else
                {
                    _choice2.Encode(rangeEncoder, 1);
                    _highCoder.Encode(rangeEncoder, symbol - Base.NumMidLenSymbols);
                }
            }
        }

        protected void SetPrices(uint posState, uint numSymbols, uint[] prices, uint st)
        {
            var a0 = _choice.GetPrice0();
            var a1 = _choice.GetPrice1();
            var b0 = a1 + _choice2.GetPrice0();
            var b1 = a1 + _choice2.GetPrice1();
            uint i;
            for (i = 0; i < Base.NumLowLenSymbols; i++)
            {
                if (i >= numSymbols)
                    return;
                prices[st + i] = a0 + _lowCoder[posState].GetPrice(i);
            }

            for (; i < Base.NumLowLenSymbols + Base.NumMidLenSymbols; i++)
            {
                if (i >= numSymbols)
                    return;
                prices[st + i] = b0 + _midCoder[posState].GetPrice(i - Base.NumLowLenSymbols);
            }

            for (; i < numSymbols; i++)
                prices[st + i] = b1 + _highCoder.GetPrice(i - Base.NumLowLenSymbols - Base.NumMidLenSymbols);
        }
    }

    private class LenPriceTableEncoder : LenEncoder
    {
        private readonly uint[] _prices = new uint[Base.NumLenSymbols << Base.NumPosStatesBitsEncodingMax];
        private uint _tableSize;
        private readonly uint[] _counters = new uint[Base.NumPosStatesEncodingMax];

        public void SetTableSize(uint tableSize)
        {
            _tableSize = tableSize;
        }

        public uint GetPrice(uint symbol, uint posState)
        {
            return _prices[posState * Base.NumLenSymbols + symbol];
        }

        private void UpdateTable(uint posState)
        {
            SetPrices(posState, _tableSize, _prices, posState * Base.NumLenSymbols);
            _counters[posState] = _tableSize;
        }

        public void UpdateTables(uint numPosStates)
        {
            for (uint posState = 0; posState < numPosStates; posState++)
                UpdateTable(posState);
        }

        public new void Encode(RangeCoderEncoder rangeEncoder, uint symbol, uint posState)
        {
            base.Encode(rangeEncoder, symbol, posState);
            if (--_counters[posState] == 0)
                UpdateTable(posState);
        }
    }

    private const uint NumOpts = 1 << 12;

    private class Optimal
    {
        public Base.State State;

        public bool Prev1IsChar;
        public bool Prev2;

        public uint PosPrev2;
        public uint BackPrev2;

        public uint Price;
        public uint PosPrev;
        public uint BackPrev;

        public uint Backs0;
        public uint Backs1;
        public uint Backs2;
        public uint Backs3;

        public void MakeAsChar()
        {
            BackPrev = 0xFFFFFFFF;
            Prev1IsChar = false;
        }

        public void MakeAsShortRep()
        {
            BackPrev = 0;
            Prev1IsChar = false;
        }

        public bool IsShortRep()
        {
            return (BackPrev == 0);
        }
    }

    private readonly Optimal[] _optimum = new Optimal[NumOpts];
    private BinTree? _matchFinder;
    private readonly RangeCoderEncoder _rangeEncoder = new();

    private readonly BitEncoder[] _isMatch = new BitEncoder[Base.NumStates << Base.NumPosStatesBitsMax];
    private readonly BitEncoder[] _isRep = new BitEncoder[Base.NumStates];
    private readonly BitEncoder[] _isRepG0 = new BitEncoder[Base.NumStates];
    private readonly BitEncoder[] _isRepG1 = new BitEncoder[Base.NumStates];
    private readonly BitEncoder[] _isRepG2 = new BitEncoder[Base.NumStates];
    private readonly BitEncoder[] _isRep0Long = new BitEncoder[Base.NumStates << Base.NumPosStatesBitsMax];

    private readonly BitTreeEncoder[] _posSlotEncoder = new BitTreeEncoder[Base.NumLenToPosStates];

    private readonly BitEncoder[] _posEncoders = new BitEncoder[Base.NumFullDistances - Base.EndPosModelIndex];
    private readonly BitTreeEncoder _posAlignEncoder = new(Base.NumAlignBits);

    private readonly LenPriceTableEncoder _lenEncoder = new();
    private readonly LenPriceTableEncoder _repMatchLenEncoder = new();

    private readonly LiteralEncoder _literalEncoder = new();

    private readonly uint[] _matchDistances = new uint[Base.MatchMaxLen * 2 + 2];

    private uint _numFastBytes = NumFastBytesDefault;
    private uint _longestMatchLength;
    private uint _numDistancePairs;

    private uint _additionalOffset;

    private uint _optimumEndIndex;
    private uint _optimumCurrentIndex;

    private bool _longestMatchWasFound;

    private readonly uint[] _posSlotPrices = new uint[1 << (Base.NumPosSlotBits + Base.NumLenToPosStatesBits)];
    private readonly uint[] _distancesPrices = new uint[Base.NumFullDistances << Base.NumLenToPosStatesBits];
    private readonly uint[] _alignPrices = new uint[Base.AlignTableSize];
    private uint _alignPriceCount;

    private uint _distTableSize = (DefaultDictionaryLogSize * 2);

    private int _posStateBits = 2;
    private uint _posStateMask = (4 - 1);
    private int _numLiteralPosStateBits;
    private int _numLiteralContextBits = 3;

    private uint _dictionarySize = (1 << DefaultDictionaryLogSize);
    private uint _dictionarySizePrev = 0xFFFFFFFF;
    private uint _numFastBytesPrev = 0xFFFFFFFF;

    private long _nowPos64;
    private bool _finished;
    private Stream? _inStream;

    private EMatchFinderType _matchFinderType = EMatchFinderType.Bt4;
    private bool _writeEndMark;

    private bool _needReleaseMfStream;

    private void Create()
    {
        if (_matchFinder == null)
        {
            var bt = new BinTree();
            var numHashBytes = 4;
            if (_matchFinderType == EMatchFinderType.Bt2)
                numHashBytes = 2;
            bt.SetType(numHashBytes);
            _matchFinder = bt;
        }

        _literalEncoder.Create(_numLiteralPosStateBits, _numLiteralContextBits);

        if (_dictionarySize == _dictionarySizePrev && _numFastBytesPrev == _numFastBytes)
            return;
        _matchFinder.Create(_dictionarySize, NumOpts, _numFastBytes, Base.MatchMaxLen + 1);
        _dictionarySizePrev = _dictionarySize;
        _numFastBytesPrev = _numFastBytes;
    }

    public LzmaEncoder()
    {
        for (var i = 0; i < NumOpts; i++) _optimum[i] = new Optimal();
        for (var i = 0; i < Base.NumLenToPosStates; i++) _posSlotEncoder[i] = new BitTreeEncoder(Base.NumPosSlotBits);
    }

    private void SetWriteEndMarkerMode(bool writeEndMarker)
    {
        _writeEndMark = writeEndMarker;
    }

    private void Init()
    {
        BaseInit();
        _rangeEncoder.Init();

        uint i;
        for (i = 0; i < Base.NumStates; i++)
        {
            for (uint j = 0; j <= _posStateMask; j++)
            {
                var complexState = (i << Base.NumPosStatesBitsMax) + j;
                _isMatch[complexState].Init();
                _isRep0Long[complexState].Init();
            }

            _isRep[i].Init();
            _isRepG0[i].Init();
            _isRepG1[i].Init();
            _isRepG2[i].Init();
        }

        _literalEncoder.Init();
        for (i = 0; i < Base.NumLenToPosStates; i++)
            _posSlotEncoder[i].Init();
        for (i = 0; i < Base.NumFullDistances - Base.EndPosModelIndex; i++)
            _posEncoders[i].Init();

        _lenEncoder.Init((uint)1 << _posStateBits);
        _repMatchLenEncoder.Init((uint)1 << _posStateBits);

        _posAlignEncoder.Init();

        _longestMatchWasFound = false;
        _optimumEndIndex = 0;
        _optimumCurrentIndex = 0;
        _additionalOffset = 0;
    }

    private void ReadMatchDistances(out uint lenRes, out uint numDistancePairs)
    {
        if (_matchFinder is null) throw new Exception();
        lenRes = 0;
        numDistancePairs = _matchFinder.GetMatches(_matchDistances);
        if (numDistancePairs > 0)
        {
            lenRes = _matchDistances[numDistancePairs - 2];
            if (lenRes == _numFastBytes)
                lenRes += _matchFinder.GetMatchLen((int)lenRes - 1, _matchDistances[numDistancePairs - 1],
                    Base.MatchMaxLen - lenRes);
        }

        _additionalOffset++;
    }


    private void MovePos(uint num)
    {
        if (_matchFinder is null) throw new Exception();
        if (num <= 0) return;

        _matchFinder.Skip(num);
        _additionalOffset += num;
    }

    private uint GetRepLen1Price(Base.State state, uint posState)
    {
        return _isRepG0[state.Index].GetPrice0() +
               _isRep0Long[(state.Index << Base.NumPosStatesBitsMax) + posState].GetPrice0();
    }

    private uint GetPureRepPrice(uint repIndex, Base.State state, uint posState)
    {
        uint price;
        if (repIndex == 0)
        {
            price = _isRepG0[state.Index].GetPrice0();
            price += _isRep0Long[(state.Index << Base.NumPosStatesBitsMax) + posState].GetPrice1();
        }
        else
        {
            price = _isRepG0[state.Index].GetPrice1();
            if (repIndex == 1)
                price += _isRepG1[state.Index].GetPrice0();
            else
            {
                price += _isRepG1[state.Index].GetPrice1();
                price += _isRepG2[state.Index].GetPrice(repIndex - 2);
            }
        }

        return price;
    }

    private uint GetRepPrice(uint repIndex, uint len, Base.State state, uint posState)
    {
        var price = _repMatchLenEncoder.GetPrice(len - Base.MatchMinLen, posState);
        return price + GetPureRepPrice(repIndex, state, posState);
    }

    private uint GetPosLenPrice(uint pos, uint len, uint posState)
    {
        uint price;
        var lenToPosState = Base.GetLenToPosState(len);
        if (pos < Base.NumFullDistances)
            price = _distancesPrices[(lenToPosState * Base.NumFullDistances) + pos];
        else
            price = _posSlotPrices[(lenToPosState << Base.NumPosSlotBits) + GetPosSlot2(pos)] +
                    _alignPrices[pos & Base.AlignMask];
        return price + _lenEncoder.GetPrice(len - Base.MatchMinLen, posState);
    }

    private uint Backward(out uint backRes, uint cur)
    {
        _optimumEndIndex = cur;
        var posMem = _optimum[cur].PosPrev;
        var backMem = _optimum[cur].BackPrev;
        do
        {
            if (_optimum[cur].Prev1IsChar)
            {
                _optimum[posMem].MakeAsChar();
                _optimum[posMem].PosPrev = posMem - 1;
                if (_optimum[cur].Prev2)
                {
                    _optimum[posMem - 1].Prev1IsChar = false;
                    _optimum[posMem - 1].PosPrev = _optimum[cur].PosPrev2;
                    _optimum[posMem - 1].BackPrev = _optimum[cur].BackPrev2;
                }
            }

            var posPrev = posMem;
            var backCur = backMem;

            backMem = _optimum[posPrev].BackPrev;
            posMem = _optimum[posPrev].PosPrev;

            _optimum[posPrev].BackPrev = backCur;
            _optimum[posPrev].PosPrev = cur;
            cur = posPrev;
        } while (cur > 0);

        backRes = _optimum[0].BackPrev;
        _optimumCurrentIndex = _optimum[0].PosPrev;
        return _optimumCurrentIndex;
    }

    private readonly uint[] _reps = new uint[Base.NumRepDistances];
    private readonly uint[] _repLens = new uint[Base.NumRepDistances];


    private uint GetOptimum(uint position, out uint backRes)
    {
        if (_matchFinder is null) throw new Exception();

        if (_optimumEndIndex != _optimumCurrentIndex)
        {
            var lenRes = _optimum[_optimumCurrentIndex].PosPrev - _optimumCurrentIndex;
            backRes = _optimum[_optimumCurrentIndex].BackPrev;
            _optimumCurrentIndex = _optimum[_optimumCurrentIndex].PosPrev;
            return lenRes;
        }

        _optimumCurrentIndex = _optimumEndIndex = 0;

        uint lenMain, numDistancePairs;
        if (!_longestMatchWasFound)
        {
            ReadMatchDistances(out lenMain, out numDistancePairs);
        }
        else
        {
            lenMain = _longestMatchLength;
            numDistancePairs = _numDistancePairs;
            _longestMatchWasFound = false;
        }

        var numAvailableBytes = _matchFinder.GetNumAvailableBytes() + 1;
        if (numAvailableBytes < 2)
        {
            backRes = 0xFFFFFFFF;
            return 1;
        }

        uint repMaxIndex = 0;
        uint i;
        for (i = 0; i < Base.NumRepDistances; i++)
        {
            _reps[i] = _repDistances[i];
            _repLens[i] = _matchFinder.GetMatchLen(0 - 1, _reps[i], Base.MatchMaxLen);
            if (_repLens[i] > _repLens[repMaxIndex])
                repMaxIndex = i;
        }

        if (_repLens[repMaxIndex] >= _numFastBytes)
        {
            backRes = repMaxIndex;
            var lenRes = _repLens[repMaxIndex];
            MovePos(lenRes - 1);
            return lenRes;
        }

        if (lenMain >= _numFastBytes)
        {
            backRes = _matchDistances[numDistancePairs - 1] + Base.NumRepDistances;
            MovePos(lenMain - 1);
            return lenMain;
        }

        var currentByte = _matchFinder.GetIndexByte(0 - 1);
        var matchByte = _matchFinder.GetIndexByte((int)(0 - _repDistances[0] - 1 - 1));

        if (lenMain < 2 && currentByte != matchByte && _repLens[repMaxIndex] < 2)
        {
            backRes = 0xFFFFFFFF;
            return 1;
        }

        _optimum[0].State = _state;

        var posState = (position & _posStateMask);

        _optimum[1].Price = _isMatch[(_state.Index << Base.NumPosStatesBitsMax) + posState].GetPrice0() +
                            _literalEncoder.GetSubCoder(position, _previousByte).GetPrice(!_state.IsCharState(), matchByte, currentByte);
        _optimum[1].MakeAsChar();

        var matchPrice = _isMatch[(_state.Index << Base.NumPosStatesBitsMax) + posState].GetPrice1();
        var repMatchPrice = matchPrice + _isRep[_state.Index].GetPrice1();

        if (matchByte == currentByte)
        {
            var shortRepPrice = repMatchPrice + GetRepLen1Price(_state, posState);
            if (shortRepPrice < _optimum[1].Price)
            {
                _optimum[1].Price = shortRepPrice;
                _optimum[1].MakeAsShortRep();
            }
        }

        var lenEnd = ((lenMain >= _repLens[repMaxIndex]) ? lenMain : _repLens[repMaxIndex]);

        if (lenEnd < 2)
        {
            backRes = _optimum[1].BackPrev;
            return 1;
        }

        _optimum[1].PosPrev = 0;

        _optimum[0].Backs0 = _reps[0];
        _optimum[0].Backs1 = _reps[1];
        _optimum[0].Backs2 = _reps[2];
        _optimum[0].Backs3 = _reps[3];

        var len = lenEnd;
        do
            _optimum[len--].Price = InfinityPrice;
        while (len >= 2);

        for (i = 0; i < Base.NumRepDistances; i++)
        {
            var repLen = _repLens[i];
            if (repLen < 2)
                continue;
            var price = repMatchPrice + GetPureRepPrice(i, _state, posState);
            do
            {
                var curAndLenPrice = price + _repMatchLenEncoder.GetPrice(repLen - 2, posState);
                var optimum = _optimum[repLen];
                if (curAndLenPrice < optimum.Price)
                {
                    optimum.Price = curAndLenPrice;
                    optimum.PosPrev = 0;
                    optimum.BackPrev = i;
                    optimum.Prev1IsChar = false;
                }
            } while (--repLen >= 2);
        }

        var normalMatchPrice = matchPrice + _isRep[_state.Index].GetPrice0();

        len = ((_repLens[0] >= 2) ? _repLens[0] + 1 : 2);
        if (len <= lenMain)
        {
            uint offs = 0;
            while (len > _matchDistances[offs])
                offs += 2;
            for (;; len++)
            {
                var distance = _matchDistances[offs + 1];
                var curAndLenPrice = normalMatchPrice + GetPosLenPrice(distance, len, posState);
                var optimum = _optimum[len];
                if (curAndLenPrice < optimum.Price)
                {
                    optimum.Price = curAndLenPrice;
                    optimum.PosPrev = 0;
                    optimum.BackPrev = distance + Base.NumRepDistances;
                    optimum.Prev1IsChar = false;
                }

                if (len == _matchDistances[offs])
                {
                    offs += 2;
                    if (offs == numDistancePairs)
                        break;
                }
            }
        }

        uint cur = 0;

        while (true)
        {
            cur++;
            if (cur == lenEnd) return Backward(out backRes, cur);

            ReadMatchDistances(out var newLen, out numDistancePairs);
            if (newLen >= _numFastBytes)
            {
                _numDistancePairs = numDistancePairs;
                _longestMatchLength = newLen;
                _longestMatchWasFound = true;
                return Backward(out backRes, cur);
            }

            position++;
            var posPrev = _optimum[cur].PosPrev;
            Base.State state;
            if (_optimum[cur].Prev1IsChar)
            {
                posPrev--;
                if (_optimum[cur].Prev2)
                {
                    state = _optimum[_optimum[cur].PosPrev2].State;
                    if (_optimum[cur].BackPrev2 < Base.NumRepDistances)
                        state.UpdateRep();
                    else
                        state.UpdateMatch();
                }
                else
                    state = _optimum[posPrev].State;

                state.UpdateChar();
            }
            else
                state = _optimum[posPrev].State;

            if (posPrev == cur - 1)
            {
                if (_optimum[cur].IsShortRep())
                    state.UpdateShortRep();
                else
                    state.UpdateChar();
            }
            else
            {
                uint pos;
                if (_optimum[cur].Prev1IsChar && _optimum[cur].Prev2)
                {
                    posPrev = _optimum[cur].PosPrev2;
                    pos = _optimum[cur].BackPrev2;
                    state.UpdateRep();
                }
                else
                {
                    pos = _optimum[cur].BackPrev;
                    if (pos < Base.NumRepDistances)
                        state.UpdateRep();
                    else
                        state.UpdateMatch();
                }

                var opt = _optimum[posPrev];
                if (pos < Base.NumRepDistances)
                {
                    if (pos == 0)
                    {
                        _reps[0] = opt.Backs0;
                        _reps[1] = opt.Backs1;
                        _reps[2] = opt.Backs2;
                        _reps[3] = opt.Backs3;
                    }
                    else if (pos == 1)
                    {
                        _reps[0] = opt.Backs1;
                        _reps[1] = opt.Backs0;
                        _reps[2] = opt.Backs2;
                        _reps[3] = opt.Backs3;
                    }
                    else if (pos == 2)
                    {
                        _reps[0] = opt.Backs2;
                        _reps[1] = opt.Backs0;
                        _reps[2] = opt.Backs1;
                        _reps[3] = opt.Backs3;
                    }
                    else
                    {
                        _reps[0] = opt.Backs3;
                        _reps[1] = opt.Backs0;
                        _reps[2] = opt.Backs1;
                        _reps[3] = opt.Backs2;
                    }
                }
                else
                {
                    _reps[0] = (pos - Base.NumRepDistances);
                    _reps[1] = opt.Backs0;
                    _reps[2] = opt.Backs1;
                    _reps[3] = opt.Backs2;
                }
            }

            _optimum[cur].State = state;
            _optimum[cur].Backs0 = _reps[0];
            _optimum[cur].Backs1 = _reps[1];
            _optimum[cur].Backs2 = _reps[2];
            _optimum[cur].Backs3 = _reps[3];
            var curPrice = _optimum[cur].Price;

            currentByte = _matchFinder.GetIndexByte(0 - 1);
            matchByte = _matchFinder.GetIndexByte((int)(0 - _reps[0] - 1 - 1));

            posState = (position & _posStateMask);

            var curAnd1Price = curPrice +
                               _isMatch[(state.Index << Base.NumPosStatesBitsMax) + posState].GetPrice0() +
                               _literalEncoder.GetSubCoder(position, _matchFinder.GetIndexByte(0 - 2)).GetPrice(!state.IsCharState(), matchByte, currentByte);

            var nextOptimum = _optimum[cur + 1];

            var nextIsChar = false;
            if (curAnd1Price < nextOptimum.Price)
            {
                nextOptimum.Price = curAnd1Price;
                nextOptimum.PosPrev = cur;
                nextOptimum.MakeAsChar();
                nextIsChar = true;
            }

            matchPrice = curPrice + _isMatch[(state.Index << Base.NumPosStatesBitsMax) + posState].GetPrice1();
            repMatchPrice = matchPrice + _isRep[state.Index].GetPrice1();

            if (matchByte == currentByte &&
                !(nextOptimum.PosPrev < cur && nextOptimum.BackPrev == 0))
            {
                var shortRepPrice = repMatchPrice + GetRepLen1Price(state, posState);
                if (shortRepPrice <= nextOptimum.Price)
                {
                    nextOptimum.Price = shortRepPrice;
                    nextOptimum.PosPrev = cur;
                    nextOptimum.MakeAsShortRep();
                    nextIsChar = true;
                }
            }

            var numAvailableBytesFull = _matchFinder.GetNumAvailableBytes() + 1;
            numAvailableBytesFull = Math.Min(NumOpts - 1 - cur, numAvailableBytesFull);
            numAvailableBytes = numAvailableBytesFull;

            if (numAvailableBytes < 2)
                continue;
            if (numAvailableBytes > _numFastBytes)
                numAvailableBytes = _numFastBytes;
            if (!nextIsChar && matchByte != currentByte)
            {
                // try Literal + rep0
                var t = Math.Min(numAvailableBytesFull - 1, _numFastBytes);
                var lenTest2 = _matchFinder.GetMatchLen(0, _reps[0], t);
                if (lenTest2 >= 2)
                {
                    var state2 = state;
                    state2.UpdateChar();
                    var posStateNext = (position + 1) & _posStateMask;
                    var nextRepMatchPrice = curAnd1Price +
                                            _isMatch[(state2.Index << Base.NumPosStatesBitsMax) + posStateNext].GetPrice1() +
                                            _isRep[state2.Index].GetPrice1();
                    {
                        var offset = cur + 1 + lenTest2;
                        while (lenEnd < offset)
                            _optimum[++lenEnd].Price = InfinityPrice;
                        var curAndLenPrice = nextRepMatchPrice + GetRepPrice(
                            0, lenTest2, state2, posStateNext);
                        var optimum = _optimum[offset];
                        if (curAndLenPrice < optimum.Price)
                        {
                            optimum.Price = curAndLenPrice;
                            optimum.PosPrev = cur + 1;
                            optimum.BackPrev = 0;
                            optimum.Prev1IsChar = true;
                            optimum.Prev2 = false;
                        }
                    }
                }
            }

            uint startLen = 2; // speed optimization 

            for (uint repIndex = 0; repIndex < Base.NumRepDistances; repIndex++)
            {
                var lenTest = _matchFinder.GetMatchLen(0 - 1, _reps[repIndex], numAvailableBytes);
                if (lenTest < 2)
                    continue;
                var lenTestTemp = lenTest;
                do
                {
                    while (lenEnd < cur + lenTest)
                        _optimum[++lenEnd].Price = InfinityPrice;
                    var curAndLenPrice = repMatchPrice + GetRepPrice(repIndex, lenTest, state, posState);
                    var optimum = _optimum[cur + lenTest];
                    if (curAndLenPrice < optimum.Price)
                    {
                        optimum.Price = curAndLenPrice;
                        optimum.PosPrev = cur;
                        optimum.BackPrev = repIndex;
                        optimum.Prev1IsChar = false;
                    }
                } while (--lenTest >= 2);

                lenTest = lenTestTemp;

                if (repIndex == 0)
                    startLen = lenTest + 1;

                // if (_maxMode)
                if (lenTest < numAvailableBytesFull)
                {
                    var t = Math.Min(numAvailableBytesFull - 1 - lenTest, _numFastBytes);
                    var lenTest2 = _matchFinder.GetMatchLen((int)lenTest, _reps[repIndex], t);
                    if (lenTest2 >= 2)
                    {
                        var state2 = state;
                        state2.UpdateRep();
                        var posStateNext = (position + lenTest) & _posStateMask;
                        var curAndLenCharPrice =
                            repMatchPrice + GetRepPrice(repIndex, lenTest, state, posState) +
                            _isMatch[(state2.Index << Base.NumPosStatesBitsMax) + posStateNext].GetPrice0() +
                            _literalEncoder.GetSubCoder(position + lenTest,
                                _matchFinder.GetIndexByte((int)lenTest - 1 - 1)).GetPrice(true,
                                _matchFinder.GetIndexByte((int)lenTest - 1 - (int)(_reps[repIndex] + 1)),
                                _matchFinder.GetIndexByte((int)lenTest - 1));
                        state2.UpdateChar();
                        posStateNext = (position + lenTest + 1) & _posStateMask;
                        var nextMatchPrice = curAndLenCharPrice + _isMatch[(state2.Index << Base.NumPosStatesBitsMax) + posStateNext].GetPrice1();
                        var nextRepMatchPrice = nextMatchPrice + _isRep[state2.Index].GetPrice1();

                        // for(; lenTest2 >= 2; lenTest2--)
                        {
                            var offset = lenTest + 1 + lenTest2;
                            while (lenEnd < cur + offset)
                                _optimum[++lenEnd].Price = InfinityPrice;
                            var curAndLenPrice = nextRepMatchPrice + GetRepPrice(0, lenTest2, state2, posStateNext);
                            var optimum = _optimum[cur + offset];
                            if (curAndLenPrice < optimum.Price)
                            {
                                optimum.Price = curAndLenPrice;
                                optimum.PosPrev = cur + lenTest + 1;
                                optimum.BackPrev = 0;
                                optimum.Prev1IsChar = true;
                                optimum.Prev2 = true;
                                optimum.PosPrev2 = cur;
                                optimum.BackPrev2 = repIndex;
                            }
                        }
                    }
                }
            }

            if (newLen > numAvailableBytes)
            {
                newLen = numAvailableBytes;
                for (numDistancePairs = 0; newLen > _matchDistances[numDistancePairs]; numDistancePairs += 2)
                {
                    // ??? This was wrong before?
                    _matchDistances[numDistancePairs] = newLen;
                }

                numDistancePairs += 2;
            }

            if (newLen < startLen) continue;

            normalMatchPrice = matchPrice + _isRep[state.Index].GetPrice0();
            while (lenEnd < cur + newLen)
                _optimum[++lenEnd].Price = InfinityPrice;

            uint offs = 0;
            while (startLen > _matchDistances[offs]) offs += 2;

            for (var lenTest = startLen;; lenTest++)
            {
                var curBack = _matchDistances[offs + 1];
                var curAndLenPrice = normalMatchPrice + GetPosLenPrice(curBack, lenTest, posState);
                var optimum = _optimum[cur + lenTest];
                if (curAndLenPrice < optimum.Price)
                {
                    optimum.Price = curAndLenPrice;
                    optimum.PosPrev = cur;
                    optimum.BackPrev = curBack + Base.NumRepDistances;
                    optimum.Prev1IsChar = false;
                }

                if (lenTest != _matchDistances[offs]) continue;

                if (lenTest < numAvailableBytesFull)
                {
                    var t = Math.Min(numAvailableBytesFull - 1 - lenTest, _numFastBytes);
                    var lenTest2 = _matchFinder.GetMatchLen((int)lenTest, curBack, t);
                    if (lenTest2 >= 2)
                    {
                        var state2 = state;
                        state2.UpdateMatch();
                        var posStateNext = (position + lenTest) & _posStateMask;
                        var curAndLenCharPrice = curAndLenPrice +
                                                 _isMatch[(state2.Index << Base.NumPosStatesBitsMax) + posStateNext].GetPrice0() +
                                                 _literalEncoder.GetSubCoder(position + lenTest,
                                                     _matchFinder.GetIndexByte((int)lenTest - 1 - 1)).GetPrice(true,
                                                     _matchFinder.GetIndexByte((int)lenTest - (int)(curBack + 1) - 1),
                                                     _matchFinder.GetIndexByte((int)lenTest - 1));
                        state2.UpdateChar();
                        posStateNext = (position + lenTest + 1) & _posStateMask;
                        var nextMatchPrice = curAndLenCharPrice + _isMatch[(state2.Index << Base.NumPosStatesBitsMax) + posStateNext].GetPrice1();
                        var nextRepMatchPrice = nextMatchPrice + _isRep[state2.Index].GetPrice1();

                        var offset = lenTest + 1 + lenTest2;
                        while (lenEnd < cur + offset)
                            _optimum[++lenEnd].Price = InfinityPrice;
                        curAndLenPrice = nextRepMatchPrice + GetRepPrice(0, lenTest2, state2, posStateNext);
                        optimum = _optimum[cur + offset];
                        if (curAndLenPrice < optimum.Price)
                        {
                            optimum.Price = curAndLenPrice;
                            optimum.PosPrev = cur + lenTest + 1;
                            optimum.BackPrev = 0;
                            optimum.Prev1IsChar = true;
                            optimum.Prev2 = true;
                            optimum.PosPrev2 = cur;
                            optimum.BackPrev2 = curBack + Base.NumRepDistances;
                        }
                    }
                }

                offs += 2;
                if (offs == numDistancePairs) break;
            }
        }
    }

    private void WriteEndMarker(uint posState)
    {
        if (!_writeEndMark)
            return;

        _isMatch[(_state.Index << Base.NumPosStatesBitsMax) + posState].Encode(_rangeEncoder, 1);
        _isRep[_state.Index].Encode(_rangeEncoder, 0);
        _state.UpdateMatch();
        var len = Base.MatchMinLen;
        _lenEncoder.Encode(_rangeEncoder, len - Base.MatchMinLen, posState);
        uint posSlot = (1 << Base.NumPosSlotBits) - 1;
        var lenToPosState = Base.GetLenToPosState(len);
        _posSlotEncoder[lenToPosState].Encode(_rangeEncoder, posSlot);
        var footerBits = 30;
        var posReduced = (((uint)1) << footerBits) - 1;
        _rangeEncoder.EncodeDirectBits(posReduced >> Base.NumAlignBits, footerBits - Base.NumAlignBits);
        _posAlignEncoder.ReverseEncode(_rangeEncoder, posReduced & Base.AlignMask);
    }

    private void Flush(uint nowPos)
    {
        ReleaseMfStream();
        WriteEndMarker(nowPos & _posStateMask);
        _rangeEncoder.FlushData();
        _rangeEncoder.FlushStream();
    }

    public void CodeOneBlock(out bool finished)
    {
        if (_matchFinder is null) throw new Exception("match finder null in CodeOneBlock");
        finished = true;

        if (_inStream != null)
        {
            _matchFinder.SetStream(_inStream);
            _matchFinder.Init();
            _needReleaseMfStream = true;
            _inStream = null;
        }

        if (_finished)
            return;
        _finished = true;


        var progressPosValuePrev = _nowPos64;
        if (_nowPos64 == 0)
        {
            if (_matchFinder.GetNumAvailableBytes() == 0)
            {
                Flush((uint)_nowPos64);
                return;
            }

            ReadMatchDistances(out _, out _);
            var posState = (uint)(_nowPos64) & _posStateMask;
            _isMatch[(_state.Index << Base.NumPosStatesBitsMax) + posState].Encode(_rangeEncoder, 0);
            _state.UpdateChar();
            var curByte = _matchFinder.GetIndexByte((int)(0 - _additionalOffset));
            _literalEncoder.GetSubCoder((uint)(_nowPos64), _previousByte).Encode(_rangeEncoder, curByte);
            _previousByte = curByte;
            _additionalOffset--;
            _nowPos64++;
        }

        if (_matchFinder.GetNumAvailableBytes() == 0)
        {
            Flush((uint)_nowPos64);
            return;
        }

        while (true)
        {
            var len = GetOptimum((uint)_nowPos64, out var pos);

            var posState = ((uint)_nowPos64) & _posStateMask;
            var complexState = (_state.Index << Base.NumPosStatesBitsMax) + posState;
            if (len == 1 && pos == 0xFFFFFFFF)
            {
                _isMatch[complexState].Encode(_rangeEncoder, 0);
                var curByte = _matchFinder.GetIndexByte((int)(0 - _additionalOffset));
                var subCoder = _literalEncoder.GetSubCoder((uint)_nowPos64, _previousByte);
                if (!_state.IsCharState())
                {
                    var matchByte = _matchFinder.GetIndexByte((int)(0 - _repDistances[0] - 1 - _additionalOffset));
                    subCoder.EncodeMatched(_rangeEncoder, matchByte, curByte);
                }
                else
                    subCoder.Encode(_rangeEncoder, curByte);

                _previousByte = curByte;
                _state.UpdateChar();
            }
            else
            {
                _isMatch[complexState].Encode(_rangeEncoder, 1);
                if (pos < Base.NumRepDistances)
                {
                    _isRep[_state.Index].Encode(_rangeEncoder, 1);
                    if (pos == 0)
                    {
                        _isRepG0[_state.Index].Encode(_rangeEncoder, 0);
                        if (len == 1)
                            _isRep0Long[complexState].Encode(_rangeEncoder, 0);
                        else
                            _isRep0Long[complexState].Encode(_rangeEncoder, 1);
                    }
                    else
                    {
                        _isRepG0[_state.Index].Encode(_rangeEncoder, 1);
                        if (pos == 1)
                            _isRepG1[_state.Index].Encode(_rangeEncoder, 0);
                        else
                        {
                            _isRepG1[_state.Index].Encode(_rangeEncoder, 1);
                            _isRepG2[_state.Index].Encode(_rangeEncoder, pos - 2);
                        }
                    }

                    if (len == 1)
                        _state.UpdateShortRep();
                    else
                    {
                        _repMatchLenEncoder.Encode(_rangeEncoder, len - Base.MatchMinLen, posState);
                        _state.UpdateRep();
                    }

                    var distance = _repDistances[pos];
                    if (pos != 0)
                    {
                        for (var i = pos; i >= 1; i--)
                            _repDistances[i] = _repDistances[i - 1];
                        _repDistances[0] = distance;
                    }
                }
                else
                {
                    _isRep[_state.Index].Encode(_rangeEncoder, 0);
                    _state.UpdateMatch();
                    _lenEncoder.Encode(_rangeEncoder, len - Base.MatchMinLen, posState);
                    pos -= Base.NumRepDistances;
                    var posSlot = GetPosSlot(pos);
                    var lenToPosState = Base.GetLenToPosState(len);
                    _posSlotEncoder[lenToPosState].Encode(_rangeEncoder, posSlot);

                    if (posSlot >= Base.StartPosModelIndex)
                    {
                        var footerBits = (int)((posSlot >> 1) - 1);
                        var baseVal = ((2 | (posSlot & 1)) << footerBits);
                        var posReduced = pos - baseVal;

                        if (posSlot < Base.EndPosModelIndex)
                            BitTreeEncoder.ReverseEncode(_posEncoders,
                                baseVal - posSlot - 1, _rangeEncoder, footerBits, posReduced);
                        else
                        {
                            _rangeEncoder.EncodeDirectBits(posReduced >> Base.NumAlignBits, footerBits - Base.NumAlignBits);
                            _posAlignEncoder.ReverseEncode(_rangeEncoder, posReduced & Base.AlignMask);
                            _alignPriceCount++;
                        }
                    }

                    var distance = pos;
                    for (var i = Base.NumRepDistances - 1; i >= 1; i--)
                        _repDistances[i] = _repDistances[i - 1];
                    _repDistances[0] = distance;
                    _matchPriceCount++;
                }

                _previousByte = _matchFinder.GetIndexByte((int)(len - 1 - _additionalOffset));
            }

            _additionalOffset -= len;
            _nowPos64 += len;
            if (_additionalOffset == 0)
            {
                // if (!_fastMode)
                if (_matchPriceCount >= (1 << 7)) FillDistancesPrices();
                if (_alignPriceCount >= Base.AlignTableSize) FillAlignPrices();

                if (_matchFinder.GetNumAvailableBytes() == 0)
                {
                    Flush((uint)_nowPos64);
                    return;
                }

                if (_nowPos64 - progressPosValuePrev >= (1 << 12))
                {
                    _finished = false;
                    finished = false;
                    return;
                }
            }
        }
    }

    private void ReleaseMfStream()
    {
        if (_matchFinder != null && _needReleaseMfStream)
        {
            _matchFinder.ReleaseStream();
            _needReleaseMfStream = false;
        }
    }

    private void SetOutStream(Stream outStream)
    {
        _rangeEncoder.SetStream(outStream);
    }

    private void ReleaseOutStream()
    {
        _rangeEncoder.ReleaseStream();
    }

    private void ReleaseStreams()
    {
        ReleaseMfStream();
        ReleaseOutStream();
    }

    private void SetStreams(Stream inStream, Stream outStream)
    {
        _inStream = inStream;
        _finished = false;
        Create();
        SetOutStream(outStream);
        Init();

        // if (!_fastMode)
        {
            FillDistancesPrices();
            FillAlignPrices();
        }

        _lenEncoder.SetTableSize(_numFastBytes + 1 - Base.MatchMinLen);
        _lenEncoder.UpdateTables((uint)1 << _posStateBits);
        _repMatchLenEncoder.SetTableSize(_numFastBytes + 1 - Base.MatchMinLen);
        _repMatchLenEncoder.UpdateTables((uint)1 << _posStateBits);

        _nowPos64 = 0;
    }

    public void Code(Stream inStream, Stream outStream)
    {
        _needReleaseMfStream = false;
        try
        {
            SetStreams(inStream, outStream);
            while (true)
            {
                CodeOneBlock(out var finished);
                if (finished) return;
            }
        }
        finally
        {
            ReleaseStreams();
        }
    }

    private const int PropSize = 5;
    private readonly byte[] _properties = new byte[PropSize];

    public void WriteCoderProperties(Stream outStream)
    {
        _properties[0] = (byte)((_posStateBits * 5 + _numLiteralPosStateBits) * 9 + _numLiteralContextBits);
        for (var i = 0; i < 4; i++)
            _properties[1 + i] = (byte)((_dictionarySize >> (8 * i)) & 0xFF);
        outStream.Write(_properties, 0, PropSize);
    }

    private readonly uint[] _tempPrices = new uint[Base.NumFullDistances];
    private uint _matchPriceCount;

    private void FillDistancesPrices()
    {
        for (var i = Base.StartPosModelIndex; i < Base.NumFullDistances; i++)
        {
            var posSlot = GetPosSlot(i);
            var footerBits = (int)((posSlot >> 1) - 1);
            var baseVal = ((2 | (posSlot & 1)) << footerBits);
            _tempPrices[i] = BitTreeEncoder.ReverseGetPrice(_posEncoders,
                baseVal - posSlot - 1, footerBits, i - baseVal);
        }

        for (uint lenToPosState = 0; lenToPosState < Base.NumLenToPosStates; lenToPosState++)
        {
            uint posSlot;
            var encoder = _posSlotEncoder[lenToPosState];

            var st = (lenToPosState << Base.NumPosSlotBits);
            for (posSlot = 0; posSlot < _distTableSize; posSlot++)
                _posSlotPrices[st + posSlot] = encoder.GetPrice(posSlot);
            for (posSlot = Base.EndPosModelIndex; posSlot < _distTableSize; posSlot++)
                _posSlotPrices[st + posSlot] += ((((posSlot >> 1) - 1) - Base.NumAlignBits) << BitEncoder.NumBitPriceShiftBits);

            var st2 = lenToPosState * Base.NumFullDistances;
            uint i;
            for (i = 0; i < Base.StartPosModelIndex; i++)
                _distancesPrices[st2 + i] = _posSlotPrices[st + i];
            for (; i < Base.NumFullDistances; i++)
                _distancesPrices[st2 + i] = _posSlotPrices[st + GetPosSlot(i)] + _tempPrices[i];
        }

        _matchPriceCount = 0;
    }

    private void FillAlignPrices()
    {
        for (uint i = 0; i < Base.AlignTableSize; i++)
            _alignPrices[i] = _posAlignEncoder.ReverseGetPrice(i);
        _alignPriceCount = 0;
    }

    private static readonly string[] _matchFinderIDs = { "BT2", "BT4" };

    private static int FindMatchFinder(string s)
    {
        for (var m = 0; m < _matchFinderIDs.Length; m++)
            if (s == _matchFinderIDs[m])
                return m;
        return -1;
    }

    public void SetCoderProperties(CoderPropId[] propIDs, object[] setProps)
    {
        for (uint i = 0; i < setProps.Length; i++)
        {
            var prop = setProps[i];
            switch (propIDs[i])
            {
                case CoderPropId.NumFastBytes:
                {
                    if (prop is not int numFastBytes) throw new InvalidParamException();
                    if (numFastBytes < 5 || numFastBytes > Base.MatchMaxLen) throw new InvalidParamException();
                    _numFastBytes = (uint)numFastBytes;
                    break;
                }
                case CoderPropId.Algorithm:
                {
                    break;
                }
                case CoderPropId.MatchFinder:
                {
                    if (prop is not string str) throw new InvalidParamException();

                    var matchFinderIndexPrev = _matchFinderType;
                    var m = FindMatchFinder(str.ToUpper());
                    if (m < 0) throw new InvalidParamException();

                    _matchFinderType = (EMatchFinderType)m;
                    if (_matchFinder != null && matchFinderIndexPrev != _matchFinderType)
                    {
                        _dictionarySizePrev = 0xFFFFFFFF;
                        _matchFinder = null;
                    }

                    break;
                }
                case CoderPropId.DictionarySize:
                {
                    const int kDicLogSizeMaxCompress = 30;
                    if (prop is not int dictionarySize) throw new InvalidParamException();

                    if (dictionarySize < (uint)(1 << Base.DicLogSizeMin) || dictionarySize > (uint)(1 << kDicLogSizeMaxCompress))
                        throw new InvalidParamException();

                    _dictionarySize = (uint)dictionarySize;
                    int dicLogSize;
                    for (dicLogSize = 0; dicLogSize < (uint)kDicLogSizeMaxCompress; dicLogSize++)
                    {
                        if (dictionarySize <= ((uint)(1) << dicLogSize)) break;
                    }

                    _distTableSize = (uint)dicLogSize * 2;
                    break;
                }
                case CoderPropId.PosStateBits:
                {
                    if (prop is not int bits) throw new InvalidParamException();
                    if (bits < 0 || bits > (uint)Base.NumPosStatesBitsEncodingMax) throw new InvalidParamException();
                    _posStateBits = bits;
                    _posStateMask = (((uint)1) << _posStateBits) - 1;
                    break;
                }
                case CoderPropId.LitPosBits:
                {
                    if (prop is not int bits) throw new InvalidParamException();
                    if (bits < 0 || bits > Base.NumLitPosStatesBitsEncodingMax) throw new InvalidParamException();
                    _numLiteralPosStateBits = bits;
                    break;
                }
                case CoderPropId.LitContextBits:
                {
                    if (prop is not int bits) throw new InvalidParamException();
                    if (bits < 0 || bits > Base.NumLitContextBitsMax) throw new InvalidParamException();
                    _numLiteralContextBits = bits;
                    break;
                }
                case CoderPropId.EndMarker:
                {
                    if (prop is not bool b) throw new InvalidParamException();
                    SetWriteEndMarkerMode(b);
                    break;
                }
                default:
                    throw new InvalidParamException();
            }
        }
    }
}

/// <summary>
/// Exception thrown when invalid compression parameters are supplied, either
/// by caller, or by an LZMA stream headers
/// </summary>
public class InvalidParamException : Exception
{
}

/// <summary>
/// Provides the fields that represent properties identifiers for compressing.
/// </summary>
public enum CoderPropId
{
    /// <summary>
    /// Specifies size of dictionary.
    /// </summary>
    DictionarySize,

    /// <summary>
    /// Specifies number of position state bits for LZMA (1..4)
    /// </summary>
    PosStateBits,

    /// <summary>
    /// Specifies number of literal context bits for LZMA (0..8)
    /// </summary>
    LitContextBits,

    /// <summary>
    /// Specifies number of literal position bits for LZMA (0..4)
    /// </summary>
    LitPosBits,

    /// <summary>
    /// Specifies number of fast bytes for LZ*.
    /// </summary>
    NumFastBytes,

    /// <summary>
    /// Specifies match finder. LZMA: "BT2", "BT4" or "BT4B".
    /// </summary>
    MatchFinder,

    /// <summary>
    /// Specifies number of algorithm.
    /// </summary>
    Algorithm,

    /// <summary>
    /// Specifies mode with end marker.
    /// </summary>
    EndMarker
}

/// <summary>
/// Exception thrown when input data is invalid. Either corrupted, or
/// not the correct format.
/// </summary>
public class DataErrorException : Exception
{
}

internal class LzmaDecoder
{
    private class LenDecoder
    {
        private BitDecoder _choice;
        private BitDecoder _choice2;
        private readonly BitTreeDecoder[] _lowCoder = new BitTreeDecoder[Base.NumPosStatesMax];
        private readonly BitTreeDecoder[] _midCoder = new BitTreeDecoder[Base.NumPosStatesMax];
        private readonly BitTreeDecoder _highCoder = new(Base.NumHighLenBits);
        private uint _numPosStates;

        public void Create(uint numberPosStates)
        {
            for (var posState = _numPosStates; posState < numberPosStates; posState++)
            {
                _lowCoder[posState] = new BitTreeDecoder(Base.NumLowLenBits);
                _midCoder[posState] = new BitTreeDecoder(Base.NumMidLenBits);
            }

            _numPosStates = numberPosStates;
        }

        public void Init()
        {
            _choice.Init();
            for (uint posState = 0; posState < _numPosStates; posState++)
            {
                _lowCoder[posState].Init();
                _midCoder[posState].Init();
            }

            _choice2.Init();
            _highCoder.Init();
        }

        public uint Decode(RangeCoderDecoder rangeDecoder, uint posState)
        {
            if (_choice.Decode(rangeDecoder) == 0)
                return _lowCoder[posState].Decode(rangeDecoder);
            var symbol = Base.NumLowLenSymbols;
            if (_choice2.Decode(rangeDecoder) == 0)
                symbol += _midCoder[posState].Decode(rangeDecoder);
            else
            {
                symbol += Base.NumMidLenSymbols;
                symbol += _highCoder.Decode(rangeDecoder);
            }

            return symbol;
        }
    }

    private class LiteralDecoder
    {
        private struct Decoder2
        {
            private BitDecoder[] _decoders;

            public void Create()
            {
                _decoders = new BitDecoder[0x300];
            }

            public void Init()
            {
                for (var i = 0; i < 0x300; i++) _decoders[i].Init();
            }

            public byte DecodeNormal(RangeCoderDecoder rangeDecoder)
            {
                uint symbol = 1;
                do
                {
                    symbol = (symbol << 1) | _decoders[symbol].Decode(rangeDecoder);
                } while (symbol < 0x100);

                return (byte)(symbol & 0xFF);
            }

            public byte DecodeWithMatchByte(RangeCoderDecoder rangeDecoder, byte matchByte)
            {
                uint symbol = 1;
                do
                {
                    var matchBit = (uint)(matchByte >> 7) & 1;
                    matchByte <<= 1;
                    var bit = _decoders[((1 + matchBit) << 8) + symbol].Decode(rangeDecoder);
                    symbol = (symbol << 1) | bit;
                    if (matchBit != bit)
                    {
                        while (symbol < 0x100)
                            symbol = (symbol << 1) | _decoders[symbol].Decode(rangeDecoder);
                        break;
                    }
                } while (symbol < 0x100);

                return (byte)(symbol & 0xFF);
            }
        }

        private Decoder2[]? _coders;
        private int _numPrevBits;
        private int _numPosBits;
        private uint _posMask;

        public void Create(int numPosBits, int numPrevBits)
        {
            if (_coders != null && this._numPrevBits == numPrevBits && this._numPosBits == numPosBits) return;

            this._numPosBits = numPosBits;
            _posMask = ((uint)1 << numPosBits) - 1;
            this._numPrevBits = numPrevBits;
            var numStates = (uint)1 << (this._numPrevBits + this._numPosBits);
            _coders = new Decoder2[numStates];

            for (uint i = 0; i < numStates; i++) _coders[i].Create();
        }

        // Copied from the encoder
        private const int NumLiteralPosStateBits = 0;
        private const int NumLiteralContextBits = 3;

        public void Init()
        {
            if (_coders is null) Create(NumLiteralPosStateBits, NumLiteralContextBits);

            var numStates = (uint)1 << (_numPrevBits + _numPosBits);

            for (uint i = 0; i < numStates; i++) _coders?[i].Init();
        }

        private uint GetState(uint pos, byte prevByte)
        {
            return ((pos & _posMask) << _numPrevBits) + (uint)(prevByte >> (8 - _numPrevBits));
        }

        public byte DecodeNormal(RangeCoderDecoder rangeDecoder, uint pos, byte prevByte)
        {
            return _coders![GetState(pos, prevByte)].DecodeNormal(rangeDecoder);
        }

        public byte DecodeWithMatchByte(RangeCoderDecoder rangeDecoder, uint pos, byte prevByte, byte matchByte)
        {
            return _coders![GetState(pos, prevByte)].DecodeWithMatchByte(rangeDecoder, matchByte);
        }
    }

    private readonly OutWindow _outWindow = new();
    private readonly RangeCoderDecoder _rangeDecoder = new();

    private readonly BitDecoder[] _isMatchDecoders = new BitDecoder[Base.NumStates << Base.NumPosStatesBitsMax];
    private readonly BitDecoder[] _isRepDecoders = new BitDecoder[Base.NumStates];
    private readonly BitDecoder[] _isRepG0Decoders = new BitDecoder[Base.NumStates];
    private readonly BitDecoder[] _isRepG1Decoders = new BitDecoder[Base.NumStates];
    private readonly BitDecoder[] _isRepG2Decoders = new BitDecoder[Base.NumStates];
    private readonly BitDecoder[] _isRep0LongDecoders = new BitDecoder[Base.NumStates << Base.NumPosStatesBitsMax];

    private readonly BitTreeDecoder[] _posSlotDecoder = new BitTreeDecoder[Base.NumLenToPosStates];
    private readonly BitDecoder[] _posDecoders = new BitDecoder[Base.NumFullDistances - Base.EndPosModelIndex];

    private readonly BitTreeDecoder _posAlignDecoder = new(Base.NumAlignBits);

    private readonly LenDecoder _lenDecoder = new();
    private readonly LenDecoder _repLenDecoder = new();

    private readonly LiteralDecoder _literalDecoder = new();

    private uint _dictionarySize;
    private uint _dictionarySizeCheck;

    private uint _posStateMask;

    public LzmaDecoder()
    {
        _dictionarySize = 0xFFFFFFFF;
        for (var i = 0; i < Base.NumLenToPosStates; i++)
            _posSlotDecoder[i] = new BitTreeDecoder(Base.NumPosSlotBits);
    }

    private void SetDictionarySize(uint dictionarySize)
    {
        if (_dictionarySize != dictionarySize)
        {
            _dictionarySize = dictionarySize;
            _dictionarySizeCheck = Math.Max(_dictionarySize, 1);
            var blockSize = Math.Max(_dictionarySizeCheck, (1 << 12));
            _outWindow.Create(blockSize);
        }
    }

    private void SetLiteralProperties(int lp, int lc)
    {
        if (lp > 8)
            throw new InvalidParamException();
        if (lc > 8)
            throw new InvalidParamException();
        _literalDecoder.Create(lp, lc);
    }

    private void SetPosBitsProperties(int pb)
    {
        if (pb > Base.NumPosStatesBitsMax)
            throw new InvalidParamException();
        var numPosStates = (uint)1 << pb;
        _lenDecoder.Create(numPosStates);
        _repLenDecoder.Create(numPosStates);
        _posStateMask = numPosStates - 1;
    }

    private void Init(Stream inStream, Stream outStream)
    {
        _rangeDecoder.Init(inStream);
        _outWindow.Init(outStream, false);

        uint i;
        for (i = 0; i < Base.NumStates; i++)
        {
            for (uint j = 0; j <= _posStateMask; j++)
            {
                var index = (i << Base.NumPosStatesBitsMax) + j;
                _isMatchDecoders[index].Init();
                _isRep0LongDecoders[index].Init();
            }

            _isRepDecoders[i].Init();
            _isRepG0Decoders[i].Init();
            _isRepG1Decoders[i].Init();
            _isRepG2Decoders[i].Init();
        }

        _literalDecoder.Init();
        for (i = 0; i < Base.NumLenToPosStates; i++)
            _posSlotDecoder[i].Init();
        // m_PosSpecDecoder.Init();
        for (i = 0; i < Base.NumFullDistances - Base.EndPosModelIndex; i++)
            _posDecoders[i].Init();

        _lenDecoder.Init();
        _repLenDecoder.Init();
        _posAlignDecoder.Init();
    }

    public void Code(Stream inStream, Stream outStream, long outSize)
    {
        Init(inStream, outStream);

        var state = new Base.State();
        state.Init();
        uint rep0 = 0, rep1 = 0, rep2 = 0, rep3 = 0;

        ulong nowPos64 = 0;
        var outSize64 = (ulong)outSize;
        if (nowPos64 < outSize64)
        {
            if (_isMatchDecoders[state.Index << Base.NumPosStatesBitsMax].Decode(_rangeDecoder) != 0)
                throw new DataErrorException();
            state.UpdateChar();
            var b = _literalDecoder.DecodeNormal(_rangeDecoder, 0, 0);
            _outWindow.PutByte(b);
            nowPos64++;
        }

        while (nowPos64 < outSize64)
        {
            var posState = (uint)nowPos64 & _posStateMask;
            if (_isMatchDecoders[(state.Index << Base.NumPosStatesBitsMax) + posState].Decode(_rangeDecoder) == 0)
            {
                byte b;
                var prevByte = _outWindow.GetByte(0);
                if (!state.IsCharState())
                    b = _literalDecoder.DecodeWithMatchByte(_rangeDecoder,
                        (uint)nowPos64, prevByte, _outWindow.GetByte(rep0));
                else
                    b = _literalDecoder.DecodeNormal(_rangeDecoder, (uint)nowPos64, prevByte);
                _outWindow.PutByte(b);
                state.UpdateChar();
                nowPos64++;
            }
            else
            {
                uint len;
                if (_isRepDecoders[state.Index].Decode(_rangeDecoder) == 1)
                {
                    if (_isRepG0Decoders[state.Index].Decode(_rangeDecoder) == 0)
                    {
                        if (_isRep0LongDecoders[(state.Index << Base.NumPosStatesBitsMax) + posState].Decode(_rangeDecoder) == 0)
                        {
                            state.UpdateShortRep();
                            _outWindow.PutByte(_outWindow.GetByte(rep0));
                            nowPos64++;
                            continue;
                        }
                    }
                    else
                    {
                        uint distance;
                        if (_isRepG1Decoders[state.Index].Decode(_rangeDecoder) == 0)
                        {
                            distance = rep1;
                        }
                        else
                        {
                            if (_isRepG2Decoders[state.Index].Decode(_rangeDecoder) == 0)
                                distance = rep2;
                            else
                            {
                                distance = rep3;
                                rep3 = rep2;
                            }

                            rep2 = rep1;
                        }

                        rep1 = rep0;
                        rep0 = distance;
                    }

                    len = _repLenDecoder.Decode(_rangeDecoder, posState) + Base.MatchMinLen;
                    state.UpdateRep();
                }
                else
                {
                    rep3 = rep2;
                    rep2 = rep1;
                    rep1 = rep0;
                    len = Base.MatchMinLen + _lenDecoder.Decode(_rangeDecoder, posState);
                    state.UpdateMatch();
                    var posSlot = _posSlotDecoder[Base.GetLenToPosState(len)].Decode(_rangeDecoder);
                    if (posSlot >= Base.StartPosModelIndex)
                    {
                        var numDirectBits = (int)((posSlot >> 1) - 1);
                        rep0 = ((2 | (posSlot & 1)) << numDirectBits);
                        if (posSlot < Base.EndPosModelIndex)
                            rep0 += BitTreeDecoder.ReverseDecode(_posDecoders,
                                rep0 - posSlot - 1, _rangeDecoder, numDirectBits);
                        else
                        {
                            rep0 += (_rangeDecoder.DecodeDirectBits(
                                numDirectBits - Base.NumAlignBits) << Base.NumAlignBits);
                            rep0 += _posAlignDecoder.ReverseDecode(_rangeDecoder);
                        }
                    }
                    else
                        rep0 = posSlot;
                }

                if (rep0 >= _outWindow.TrainSize + nowPos64 || rep0 >= _dictionarySizeCheck)
                {
                    if (rep0 == 0xFFFFFFFF) break;
                    throw new DataErrorException();
                }

                _outWindow.CopyBlock(rep0, len);
                nowPos64 += len;
            }
        }

        _outWindow.Flush();
        _outWindow.ReleaseStream();
        _rangeDecoder.ReleaseStream();
    }

    public void SetDecoderProperties(byte[] properties)
    {
        if (properties.Length < 5)
            throw new InvalidParamException();
        var lc = properties[0] % 9;
        var remainder = properties[0] / 9;
        var lp = remainder % 5;
        var pb = remainder / 5;
        if (pb > Base.NumPosStatesBitsMax)
            throw new InvalidParamException();
        uint dictionarySize = 0;
        for (var i = 0; i < 4; i++)
            dictionarySize += ((uint)(properties[1 + i])) << (i * 8);
        SetDictionarySize(dictionarySize);
        SetLiteralProperties(lp, lc);
        SetPosBitsProperties(pb);
    }
}

internal class RangeCoderEncoder
{
    public const uint TopValue = (1 << 24);

    private Stream? _stream;

    public ulong Low;
    public uint Range;
    private uint _cacheSize;
    private byte _cache;

    public void SetStream(Stream stream)
    {
        _stream = stream;
    }

    public void ReleaseStream()
    {
        _stream = null;
    }

    public void Init()
    {
        Low = 0;
        Range = 0xFFFFFFFF;
        _cacheSize = 1;
        _cache = 0;
    }

    public void FlushData()
    {
        for (var i = 0; i < 5; i++)
            ShiftLow();
    }

    public void FlushStream()
    {
        _stream?.Flush();
    }

    public void ShiftLow()
    {
        if ((uint)Low < 0xFF000000 || (uint)(Low >> 32) == 1)
        {
            var temp = _cache;
            do
            {
                _stream?.WriteByte((byte)(temp + (Low >> 32)));
                temp = 0xFF;
            } while (--_cacheSize != 0);

            _cache = (byte)(((uint)Low) >> 24);
        }

        _cacheSize++;
        Low = ((uint)Low) << 8;
    }

    public void EncodeDirectBits(uint v, int numTotalBits)
    {
        for (var i = numTotalBits - 1; i >= 0; i--)
        {
            Range >>= 1;
            if (((v >> i) & 1) == 1)
                Low += Range;
            if (Range < TopValue)
            {
                Range <<= 8;
                ShiftLow();
            }
        }
    }
}

internal class RangeCoderDecoder
{
    public const uint TopValue = (1 << 24);
    public uint Range;

    public uint Code;

    // public Buffer.InBuffer Stream = new Buffer.InBuffer(1 << 16);
    public Stream? Stream;

    public void Init(Stream stream)
    {
        // Stream.Init(stream);
        Stream = stream;

        Code = 0;
        Range = 0xFFFFFFFF;
        for (var i = 0; i < 5; i++)
            Code = (Code << 8) | (byte)Stream.ReadByte();
    }

    public void ReleaseStream()
    {
        // Stream.ReleaseStream();
        Stream = null;
    }

    public uint DecodeDirectBits(int numTotalBits)
    {
        if (Stream is null) throw new Exception();
        var range = Range;
        var code = Code;
        uint result = 0;
        for (var i = numTotalBits; i > 0; i--)
        {
            range >>= 1;

            var t = (code - range) >> 31;
            code -= range & (t - 1);
            result = (result << 1) | (1 - t);

            if (range < TopValue)
            {
                code = (code << 8) | (byte)Stream.ReadByte();
                range <<= 8;
            }
        }

        Range = range;
        Code = code;
        return result;
    }
}

internal struct BitEncoder
{
    public const int NumBitModelTotalBits = 11;
    public const uint BitModelTotal = (1 << NumBitModelTotalBits);
    private const int NumMoveBits = 5;
    private const int NumMoveReducingBits = 2;
    public const int NumBitPriceShiftBits = 6;

    private uint _prob;

    public void Init()
    {
        _prob = BitModelTotal >> 1;
    }

    public void Encode(RangeCoderEncoder encoder, uint symbol)
    {
        // encoder.EncodeBit(Prob, kNumBitModelTotalBits, symbol);
        // UpdateModel(symbol);
        var newBound = (encoder.Range >> NumBitModelTotalBits) * _prob;
        if (symbol == 0)
        {
            encoder.Range = newBound;
            _prob += (BitModelTotal - _prob) >> NumMoveBits;
        }
        else
        {
            encoder.Low += newBound;
            encoder.Range -= newBound;
            _prob -= (_prob) >> NumMoveBits;
        }

        if (encoder.Range < RangeCoderEncoder.TopValue)
        {
            encoder.Range <<= 8;
            encoder.ShiftLow();
        }
    }

    private static readonly uint[] _probPrices = new uint[BitModelTotal >> NumMoveReducingBits];

    static BitEncoder()
    {
        const int kNumBits = (NumBitModelTotalBits - NumMoveReducingBits);
        for (var i = kNumBits - 1; i >= 0; i--)
        {
            var start = (uint)1 << (kNumBits - i - 1);
            var end = (uint)1 << (kNumBits - i);
            for (var j = start; j < end; j++)
                _probPrices[j] = ((uint)i << NumBitPriceShiftBits) +
                                 (((end - j) << NumBitPriceShiftBits) >> (kNumBits - i - 1));
        }
    }

    public uint GetPrice(uint symbol)
    {
        return _probPrices[(((_prob - symbol) ^ ((-(int)symbol))) & (BitModelTotal - 1)) >> NumMoveReducingBits];
    }

    public uint GetPrice0()
    {
        return _probPrices[_prob >> NumMoveReducingBits];
    }

    public uint GetPrice1()
    {
        return _probPrices[(BitModelTotal - _prob) >> NumMoveReducingBits];
    }
}

internal struct BitDecoder
{
    public const int NumBitModelTotalBits = 11;
    public const uint BitModelTotal = (1 << NumBitModelTotalBits);
    private const int NumMoveBits = 5;

    private uint _prob;

    public void Init()
    {
        _prob = BitModelTotal >> 1;
    }

    public uint Decode(RangeCoderDecoder rangeDecoder)
    {
        if (rangeDecoder.Stream is null) throw new Exception();

        var newBound = (rangeDecoder.Range >> NumBitModelTotalBits) * _prob;
        if (rangeDecoder.Code < newBound)
        {
            rangeDecoder.Range = newBound;
            _prob += (BitModelTotal - _prob) >> NumMoveBits;
            if (rangeDecoder.Range < RangeCoderDecoder.TopValue)
            {
                rangeDecoder.Code = (rangeDecoder.Code << 8) | (byte)rangeDecoder.Stream.ReadByte();
                rangeDecoder.Range <<= 8;
            }

            return 0;
        }

        rangeDecoder.Range -= newBound;
        rangeDecoder.Code -= newBound;
        _prob -= (_prob) >> NumMoveBits;
        if (rangeDecoder.Range < RangeCoderDecoder.TopValue)
        {
            rangeDecoder.Code = (rangeDecoder.Code << 8) | (byte)rangeDecoder.Stream.ReadByte();
            rangeDecoder.Range <<= 8;
        }

        return 1;
    }
}

internal readonly struct BitTreeEncoder
{
    private readonly BitEncoder[] _models;
    private readonly int _numBitLevels;

    public BitTreeEncoder(int numBitLevels)
    {
        _numBitLevels = numBitLevels;
        _models = new BitEncoder[1 << numBitLevels];
    }

    public void Init()
    {
        for (uint i = 1; i < (1 << _numBitLevels); i++)
            _models[i].Init();
    }

    public void Encode(RangeCoderEncoder rangeEncoder, uint symbol)
    {
        uint m = 1;
        for (var bitIndex = _numBitLevels; bitIndex > 0;)
        {
            bitIndex--;
            var bit = (symbol >> bitIndex) & 1;
            _models[m].Encode(rangeEncoder, bit);
            m = (m << 1) | bit;
        }
    }

    public void ReverseEncode(RangeCoderEncoder rangeEncoder, uint symbol)
    {
        uint m = 1;
        for (uint i = 0; i < _numBitLevels; i++)
        {
            var bit = symbol & 1;
            _models[m].Encode(rangeEncoder, bit);
            m = (m << 1) | bit;
            symbol >>= 1;
        }
    }

    public uint GetPrice(uint symbol)
    {
        uint price = 0;
        uint m = 1;
        for (var bitIndex = _numBitLevels; bitIndex > 0;)
        {
            bitIndex--;
            var bit = (symbol >> bitIndex) & 1;
            price += _models[m].GetPrice(bit);
            m = (m << 1) + bit;
        }

        return price;
    }

    public uint ReverseGetPrice(uint symbol)
    {
        uint price = 0;
        uint m = 1;
        for (var i = _numBitLevels; i > 0; i--)
        {
            var bit = symbol & 1;
            symbol >>= 1;
            price += _models[m].GetPrice(bit);
            m = (m << 1) | bit;
        }

        return price;
    }

    public static uint ReverseGetPrice(BitEncoder[] models, uint startIndex,
        int numBitLevels, uint symbol)
    {
        uint price = 0;
        uint m = 1;
        for (var i = numBitLevels; i > 0; i--)
        {
            var bit = symbol & 1;
            symbol >>= 1;
            price += models[startIndex + m].GetPrice(bit);
            m = (m << 1) | bit;
        }

        return price;
    }

    public static void ReverseEncode(BitEncoder[] models, uint startIndex,
        RangeCoderEncoder rangeEncoder, int numBitLevels, uint symbol)
    {
        uint m = 1;
        for (var i = 0; i < numBitLevels; i++)
        {
            var bit = symbol & 1;
            models[startIndex + m].Encode(rangeEncoder, bit);
            m = (m << 1) | bit;
            symbol >>= 1;
        }
    }
}

internal readonly struct BitTreeDecoder
{
    private readonly BitDecoder[] _models;
    private readonly int _numBitLevels;

    public BitTreeDecoder(int numBitLevels)
    {
        _numBitLevels = numBitLevels;
        _models = new BitDecoder[1 << numBitLevels];
    }

    public void Init()
    {
        for (uint i = 1; i < (1 << _numBitLevels); i++)
            _models[i].Init();
    }

    public uint Decode(RangeCoderDecoder rangeDecoder)
    {
        uint m = 1;
        for (var bitIndex = _numBitLevels; bitIndex > 0; bitIndex--)
            m = (m << 1) + _models[m].Decode(rangeDecoder);
        return m - ((uint)1 << _numBitLevels);
    }

    public uint ReverseDecode(RangeCoderDecoder rangeDecoder)
    {
        uint m = 1;
        uint symbol = 0;
        for (var bitIndex = 0; bitIndex < _numBitLevels; bitIndex++)
        {
            var bit = _models[m].Decode(rangeDecoder);
            m <<= 1;
            m += bit;
            symbol |= (bit << bitIndex);
        }

        return symbol;
    }

    public static uint ReverseDecode(BitDecoder[] models, uint startIndex,
        RangeCoderDecoder rangeDecoder, int numBitLevels)
    {
        uint m = 1;
        uint symbol = 0;
        for (var bitIndex = 0; bitIndex < numBitLevels; bitIndex++)
        {
            var bit = models[startIndex + m].Decode(rangeDecoder);
            m <<= 1;
            m += bit;
            symbol |= (bit << bitIndex);
        }

        return symbol;
    }
}

internal class BinTree
{
    private byte[]? _bufferBase; // pointer to buffer with data
    private Stream? _stream;
    private uint _posLimit; // offset (from _buffer) of first byte when new block reading must be done
    private bool _streamEndWasReached; // if (true) then _streamPos shows real end of stream

    private uint _pointerToLastSafePosition;

    private uint _bufferOffset;

    private uint _blockSize; // Size of Allocated memory block
    private uint _pos; // offset (from _buffer) of current byte
    private uint _keepSizeBefore; // how many BYTEs must be kept in buffer before _pos
    private uint _keepSizeAfter; // how many BYTEs must be kept buffer after _pos
    private uint _streamPos; // offset (from _buffer) of first not read byte from Stream

    private uint _cyclicBufferPos;
    private uint _cyclicBufferSize;
    private uint _matchMaxLen;

    private uint[]? _son;
    private uint[]? _hash;

    private uint _cutValue = 0xFF;
    private uint _hashMask;
    private uint _hashSizeSum;

    private bool _useHashArray = true;

    private const uint Hash2Size = 1 << 10;
    private const uint Hash3Size = 1 << 16;
    private const uint Bt2HashSize = 1 << 16;
    private const uint StartMaxLen = 1;
    private const uint Hash3Offset = Hash2Size;
    private const uint EmptyHashValue = 0;
    private const uint MaxValForNormalize = ((uint)1 << 31) - 1;

    private uint _numHashDirectBytes;
    private uint _minMatchCheck = 4;
    private uint _fixHashSize = Hash2Size + Hash3Size;

    private static readonly uint[] _crcTable = new uint[256];
    private static bool _crcMade;

    private static void MakeCrcTable()
    {
        if (_crcMade) return;
        const uint kPoly = 0xEDB88320;
        for (uint i = 0; i < 256; i++)
        {
            var r = i;
            for (var j = 0; j < 8; j++)
                if ((r & 1) != 0) r = (r >> 1) ^ kPoly;
                else r >>= 1;
            _crcTable[i] = r;
        }

        _crcMade = true;
    }

    public void SetType(int numHashBytes)
    {
        _useHashArray = (numHashBytes > 2);
        if (_useHashArray)
        {
            _numHashDirectBytes = 0;
            _minMatchCheck = 4;
            _fixHashSize = Hash2Size + Hash3Size;
        }
        else
        {
            _numHashDirectBytes = 2;
            _minMatchCheck = 2 + 1;
            _fixHashSize = 0;
        }
    }

    public void SetStream(Stream stream)
    {
        _stream = stream;
    }

    public void ReleaseStream()
    {
        _stream = null;
    }

    public void Init()
    {
        _bufferOffset = 0;
        _pos = 0;
        _streamPos = 0;
        _streamEndWasReached = false;
        ReadBlock();

        for (uint i = 0; i < _hashSizeSum; i++) _hash![i] = EmptyHashValue;
        _cyclicBufferPos = 0;
        ReduceOffsets(-1);
    }

    public void MovePos()
    {
        if (++_cyclicBufferPos >= _cyclicBufferSize) _cyclicBufferPos = 0;

        _pos++;
        if (_pos > _posLimit)
        {
            var pointerToPosition = _bufferOffset + _pos;
            if (pointerToPosition > _pointerToLastSafePosition) MoveBlock();

            ReadBlock();
        }

        if (_pos == MaxValForNormalize) Normalize();
    }

    public byte GetIndexByte(int index)
    {
        if (_bufferBase is null) return 0;
        return _bufferBase[_bufferOffset + _pos + index];
    }

    public uint GetMatchLen(int index, uint distance, uint limit)
    {
        if (_bufferBase is null) return 0;
        if (_streamEndWasReached && (_pos + index) + limit > _streamPos)
        {
            limit = _streamPos - (uint)(_pos + index);
        }

        distance++;
        var pby = _bufferOffset + _pos + (uint)index;

        uint i = 0;
        while (i < limit && _bufferBase[pby + i] == _bufferBase[pby + i - distance])
        {
            i++;
        }

        return i;
    }

    public uint GetNumAvailableBytes()
    {
        return _streamPos - _pos;
    }

    private void Free()
    {
        _bufferBase = null;
    }

    private void SetupBufferBase(uint keepSizeBefore, uint keepSizeAfter, uint keepSizeReserve)
    {
        _keepSizeBefore = keepSizeBefore;
        _keepSizeAfter = keepSizeAfter;
        var blockSize = keepSizeBefore + keepSizeAfter + keepSizeReserve;
        if (_bufferBase == null || _blockSize != blockSize)
        {
            Free();
            _blockSize = blockSize;
            _bufferBase = new byte[_blockSize];
        }

        _pointerToLastSafePosition = _blockSize - keepSizeAfter;
    }

    public void Create(uint historySize, uint keepAddBufferBefore, uint matchMaxLen, uint keepAddBufferAfter)
    {
        if (historySize > MaxValForNormalize - 256) throw new Exception();

        _cutValue = 16 + (matchMaxLen >> 1);

        var windowReserveSize = (historySize + keepAddBufferBefore +
                                 matchMaxLen + keepAddBufferAfter) / 2 + 256;

        SetupBufferBase(historySize + keepAddBufferBefore, matchMaxLen + keepAddBufferAfter, windowReserveSize);

        _matchMaxLen = matchMaxLen;

        var cyclicBufferSize = historySize + 1;
        if (_cyclicBufferSize != cyclicBufferSize)
            _son = new uint[(_cyclicBufferSize = cyclicBufferSize) * 2];

        var hs = Bt2HashSize;

        if (_useHashArray)
        {
            hs = historySize - 1;
            hs |= (hs >> 1);
            hs |= (hs >> 2);
            hs |= (hs >> 4);
            hs |= (hs >> 8);
            hs >>= 1;
            hs |= 0xFFFF;
            if (hs > (1 << 24))
                hs >>= 1;
            _hashMask = hs;
            hs++;
            hs += _fixHashSize;
        }

        if (hs != _hashSizeSum)
            _hash = new uint[_hashSizeSum = hs];
    }

    public uint GetMatches(uint[] distances)
    {
        uint lenLimit;
        if (_pos + _matchMaxLen <= _streamPos)
            lenLimit = _matchMaxLen;
        else
        {
            lenLimit = _streamPos - _pos;
            if (lenLimit < _minMatchCheck)
            {
                MovePos();
                return 0;
            }
        }

        uint offset = 0;
        var matchMinPos = (_pos > _cyclicBufferSize) ? (_pos - _cyclicBufferSize) : 0;
        var cur = _bufferOffset + _pos;
        var maxLen = StartMaxLen; // to avoid items for len < hashSize;
        uint hashValue, hash2Value = 0, hash3Value = 0;

        if (_bufferBase is null || _hash is null) throw new Exception();

        if (_useHashArray)
        {
            MakeCrcTable();
            var temp = _crcTable[_bufferBase[cur]] ^ _bufferBase[cur + 1];
            hash2Value = temp & (Hash2Size - 1);
            temp ^= ((uint)(_bufferBase[cur + 2]) << 8);
            hash3Value = temp & (Hash3Size - 1);
            hashValue = (temp ^ (_crcTable[_bufferBase[cur + 3]] << 5)) & _hashMask;
        }
        else
            hashValue = _bufferBase[cur] ^ ((uint)(_bufferBase[cur + 1]) << 8);

        var curMatch = _hash[_fixHashSize + hashValue];
        if (_useHashArray)
        {
            var curMatch2 = _hash[hash2Value];
            var curMatch3 = _hash[Hash3Offset + hash3Value];
            _hash[hash2Value] = _pos;
            _hash[Hash3Offset + hash3Value] = _pos;
            if (curMatch2 > matchMinPos)
                if (_bufferBase[_bufferOffset + curMatch2] == _bufferBase[cur])
                {
                    distances[offset++] = maxLen = 2;
                    distances[offset++] = _pos - curMatch2 - 1;
                }

            if (curMatch3 > matchMinPos)
                if (_bufferBase[_bufferOffset + curMatch3] == _bufferBase[cur])
                {
                    if (curMatch3 == curMatch2)
                        offset -= 2;
                    distances[offset++] = maxLen = 3;
                    distances[offset++] = _pos - curMatch3 - 1;
                    curMatch2 = curMatch3;
                }

            if (offset != 0 && curMatch2 == curMatch)
            {
                offset -= 2;
                maxLen = StartMaxLen;
            }
        }

        _hash[_fixHashSize + hashValue] = _pos;

        var ptr0 = (_cyclicBufferPos << 1) + 1;
        var ptr1 = (_cyclicBufferPos << 1);

        uint len1;
        var len0 = len1 = _numHashDirectBytes;

        if (_numHashDirectBytes != 0)
        {
            if (curMatch > matchMinPos)
            {
                if (_bufferBase[_bufferOffset + curMatch + _numHashDirectBytes] !=
                    _bufferBase[cur + _numHashDirectBytes])
                {
                    distances[offset++] = maxLen = _numHashDirectBytes;
                    distances[offset++] = _pos - curMatch - 1;
                }
            }
        }

        var count = _cutValue;

        while (true)
        {
            if (curMatch <= matchMinPos || count-- == 0)
            {
                _son![ptr0] = _son[ptr1] = EmptyHashValue;
                break;
            }

            var delta = _pos - curMatch;
            var cyclicPos = ((delta <= _cyclicBufferPos) ? (_cyclicBufferPos - delta) : (_cyclicBufferPos - delta + _cyclicBufferSize)) << 1;

            var pby1 = _bufferOffset + curMatch;
            var len = Math.Min(len0, len1);
            if (_bufferBase[pby1 + len] == _bufferBase[cur + len])
            {
                while (++len != lenLimit)
                    if (_bufferBase[pby1 + len] != _bufferBase[cur + len])
                        break;
                if (maxLen < len)
                {
                    distances[offset++] = maxLen = len;
                    distances[offset++] = delta - 1;
                    if (len == lenLimit)
                    {
                        _son![ptr1] = _son[cyclicPos];
                        _son[ptr0] = _son[cyclicPos + 1];
                        break;
                    }
                }
            }

            if (_bufferBase[pby1 + len] < _bufferBase[cur + len])
            {
                _son![ptr1] = curMatch;
                ptr1 = cyclicPos + 1;
                curMatch = _son[ptr1];
                len1 = len;
            }
            else
            {
                _son![ptr0] = curMatch;
                ptr0 = cyclicPos;
                curMatch = _son[ptr0];
                len0 = len;
            }
        }

        MovePos();
        return offset;
    }

    public void Skip(uint num)
    {
        do
        {
            uint lenLimit;
            if (_pos + _matchMaxLen <= _streamPos)
                lenLimit = _matchMaxLen;
            else
            {
                lenLimit = _streamPos - _pos;
                if (lenLimit < _minMatchCheck)
                {
                    MovePos();
                    continue;
                }
            }

            var matchMinPos = (_pos > _cyclicBufferSize) ? (_pos - _cyclicBufferSize) : 0;
            var cur = _bufferOffset + _pos;

            uint hashValue;

            if (_useHashArray)
            {
                MakeCrcTable();
                var temp = _crcTable[_bufferBase![cur]] ^ _bufferBase[cur + 1];
                var hash2Value = temp & (Hash2Size - 1);
                _hash![hash2Value] = _pos;
                temp ^= ((uint)(_bufferBase[cur + 2]) << 8);
                var hash3Value = temp & (Hash3Size - 1);
                _hash[Hash3Offset + hash3Value] = _pos;
                hashValue = (temp ^ (_crcTable[_bufferBase[cur + 3]] << 5)) & _hashMask;
            }
            else
                hashValue = _bufferBase![cur] ^ ((uint)(_bufferBase[cur + 1]) << 8);

            var curMatch = _hash![_fixHashSize + hashValue];
            _hash[_fixHashSize + hashValue] = _pos;

            var ptr0 = (_cyclicBufferPos << 1) + 1;
            var ptr1 = (_cyclicBufferPos << 1);

            uint len1;
            var len0 = len1 = _numHashDirectBytes;

            var count = _cutValue;
            while (true)
            {
                if (curMatch <= matchMinPos || count-- == 0)
                {
                    _son![ptr0] = _son[ptr1] = EmptyHashValue;
                    break;
                }

                var delta = _pos - curMatch;
                var cyclicPos = ((delta <= _cyclicBufferPos) ? (_cyclicBufferPos - delta) : (_cyclicBufferPos - delta + _cyclicBufferSize)) << 1;

                var pby1 = _bufferOffset + curMatch;
                var len = Math.Min(len0, len1);
                if (_bufferBase[pby1 + len] == _bufferBase[cur + len])
                {
                    while (++len != lenLimit)
                        if (_bufferBase[pby1 + len] != _bufferBase[cur + len])
                            break;
                    if (len == lenLimit)
                    {
                        _son![ptr1] = _son[cyclicPos];
                        _son[ptr0] = _son[cyclicPos + 1];
                        break;
                    }
                }

                if (_bufferBase[pby1 + len] < _bufferBase[cur + len])
                {
                    _son![ptr1] = curMatch;
                    ptr1 = cyclicPos + 1;
                    curMatch = _son[ptr1];
                    len1 = len;
                }
                else
                {
                    _son![ptr0] = curMatch;
                    ptr0 = cyclicPos;
                    curMatch = _son[ptr0];
                    len0 = len;
                }
            }

            MovePos();
        } while (--num != 0);
    }

    private void NormalizeLinks(uint[] items, uint numItems, uint subValue)
    {
        for (uint i = 0; i < numItems; i++)
        {
            var value = items[i];
            if (value <= subValue)
                value = EmptyHashValue;
            else
                value -= subValue;
            items[i] = value;
        }
    }

    private void Normalize()
    {
        var subValue = _pos - _cyclicBufferSize;
        NormalizeLinks(_son!, _cyclicBufferSize * 2, subValue);
        NormalizeLinks(_hash!, _hashSizeSum, subValue);
        ReduceOffsets((int)subValue);
    }

    private void MoveBlock()
    {
        if (_bufferBase is null) throw new Exception();

        var offset = _bufferOffset + _pos - _keepSizeBefore;
        // we need one additional byte, since MovePos moves on 1 byte.
        if (offset > 0)
            offset--;

        var numBytes = _bufferOffset + _streamPos - offset;

        // check negative offset ????
        for (uint i = 0; i < numBytes; i++)
            _bufferBase[i] = _bufferBase[offset + i];
        _bufferOffset -= offset;
    }

    private void ReadBlock()
    {
        if (_streamEndWasReached) return;
        if (_stream is null || _bufferBase is null) return;

        while (true)
        {
            var size = (int)((0 - _bufferOffset) + _blockSize - _streamPos);
            if (size == 0) return;

            var numReadBytes = _stream.Read(_bufferBase, (int)(_bufferOffset + _streamPos), size);
            if (numReadBytes == 0)
            {
                _posLimit = _streamPos;
                var pointerToPosition = _bufferOffset + _posLimit;
                if (pointerToPosition > _pointerToLastSafePosition)
                    _posLimit = _pointerToLastSafePosition - _bufferOffset;

                _streamEndWasReached = true;
                return;
            }

            _streamPos += (uint)numReadBytes;
            if (_streamPos >= _pos + _keepSizeAfter)
                _posLimit = _streamPos - _keepSizeAfter;
        }
    }

    private void ReduceOffsets(int subValue)
    {
        _bufferOffset += (uint)subValue;
        _posLimit -= (uint)subValue;
        _pos -= (uint)subValue;
        _streamPos -= (uint)subValue;
    }
}

internal class OutWindow
{
    private byte[]? _buffer;
    private uint _pos;
    private uint _windowSize;
    private uint _streamPos;
    private Stream? _stream;

    public uint TrainSize;

    public void Create(uint windowSize)
    {
        if (_windowSize != windowSize || _buffer == null)
        {
            _buffer = new byte[windowSize];
        }

        _windowSize = windowSize;
        _pos = 0;
        _streamPos = 0;
    }

    public void Init(Stream stream, bool solid)
    {
        if (_buffer is null) Create(1024 * 1024);
        ReleaseStream();
        _stream = stream;
        if (!solid)
        {
            _streamPos = 0;
            _pos = 0;
            TrainSize = 0;
        }
    }

    public void ReleaseStream()
    {
        Flush();
        _stream = null;
    }

    public void Flush()
    {
        if (_buffer is null || _stream is null) return;

        var size = _pos - _streamPos;
        if (size == 0) return;

        _stream.Write(_buffer, (int)_streamPos, (int)size);
        if (_pos >= _windowSize)
            _pos = 0;
        _streamPos = _pos;
    }

    public void CopyBlock(uint distance, uint len)
    {
        if (_buffer is null) throw new Exception();

        var pos = _pos - distance - 1;
        if (pos >= _windowSize)
            pos += _windowSize;
        for (; len > 0; len--)
        {
            if (pos >= _windowSize)
                pos = 0;
            _buffer[_pos++] = _buffer[pos++];
            if (_pos >= _windowSize)
                Flush();
        }
    }

    public void PutByte(byte b)
    {
        if (_buffer is null) throw new Exception();

        _buffer[_pos++] = b;
        if (_pos >= _windowSize)
            Flush();
    }

    public byte GetByte(uint distance)
    {
        if (_buffer is null) throw new Exception();

        var pos = _pos - distance - 1;
        if (pos >= _windowSize)
            pos += _windowSize;
        return _buffer[pos];
    }
}
}