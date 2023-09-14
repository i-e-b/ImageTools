using System;
using System.IO;

// ReSharper disable InconsistentNaming

// Imported from https://github.com/dotnet/cli/tree/rel/1.0.0/src/Microsoft.DotNet.Archive/LZMA


namespace ImageTools.DataCompression.LZMA
{
    /// <summary>
    /// Compression utility for LZMA (7z) format
    /// </summary>
    public static class LzmaCompressor
    {
        public static void Compress(Stream inStream, Stream outStream)
        {
            var encoder = new LzmaEncoder();

            CoderPropID[] propIDs =
            {
                CoderPropID.DictionarySize, CoderPropID.PosStateBits, CoderPropID.LitContextBits,
                CoderPropID.LitPosBits, CoderPropID.Algorithm, CoderPropID.NumFastBytes, CoderPropID.MatchFinder,
                CoderPropID.EndMarker
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

            var compressedSize = inStream.Length - inStream.Position;
            decoder.Code(inStream, outStream, compressedSize, outSize);
        }
    }

    internal abstract class Base
    {
        public const uint kNumRepDistances = 4;
        public const uint kNumStates = 12;

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

        public const int kNumPosSlotBits = 6;
        public const int kDicLogSizeMin = 0;

        public const int kNumLenToPosStatesBits = 2; // it's for speed optimization
        public const uint kNumLenToPosStates = 1 << kNumLenToPosStatesBits;

        public const uint kMatchMinLen = 2;

        public static uint GetLenToPosState(uint len)
        {
            len -= kMatchMinLen;
            if (len < kNumLenToPosStates)
                return len;
            return kNumLenToPosStates - 1;
        }

        public const int kNumAlignBits = 4;
        public const uint kAlignTableSize = 1 << kNumAlignBits;
        public const uint kAlignMask = (kAlignTableSize - 1);

        public const uint kStartPosModelIndex = 4;
        public const uint kEndPosModelIndex = 14;

        public const uint kNumFullDistances = 1 << ((int)kEndPosModelIndex / 2);

        public const uint kNumLitPosStatesBitsEncodingMax = 4;
        public const uint kNumLitContextBitsMax = 8;

        public const int kNumPosStatesBitsMax = 4;
        public const uint kNumPosStatesMax = (1 << kNumPosStatesBitsMax);
        public const int kNumPosStatesBitsEncodingMax = 4;
        public const uint kNumPosStatesEncodingMax = (1 << kNumPosStatesBitsEncodingMax);

        public const int kNumLowLenBits = 3;
        public const int kNumMidLenBits = 3;
        public const int kNumHighLenBits = 8;
        public const uint kNumLowLenSymbols = 1 << kNumLowLenBits;
        public const uint kNumMidLenSymbols = 1 << kNumMidLenBits;

        public const uint kNumLenSymbols = kNumLowLenSymbols + kNumMidLenSymbols +
                                           (1 << kNumHighLenBits);

        public const uint kMatchMaxLen = kMatchMinLen + kNumLenSymbols - 1;
    }

    public class LzmaEncoder
    {
        private enum EMatchFinderType
        {
            BT2,
            BT4,
        }

        private const uint kInfinityPrice = 0xFFFFFFF;

        private static readonly byte[] g_FastPos = new byte[1 << 11];

        static LzmaEncoder()
        {
            const byte kFastSlots = 22;
            var c = 2;
            g_FastPos[0] = 0;
            g_FastPos[1] = 1;
            for (Byte slotFast = 2; slotFast < kFastSlots; slotFast++)
            {
                var k = ((UInt32)1 << ((slotFast >> 1) - 1));
                for (UInt32 j = 0; j < k; j++, c++)
                    g_FastPos[c] = slotFast;
            }
        }

        private static UInt32 GetPosSlot(UInt32 pos)
        {
            if (pos < (1 << 11)) return g_FastPos[pos];
            if (pos < (1 << 21)) return (UInt32)(g_FastPos[pos >> 10] + 20);
            return (UInt32)(g_FastPos[pos >> 20] + 40);
        }

        private static UInt32 GetPosSlot2(UInt32 pos)
        {
            if (pos < (1 << 17)) return (UInt32)(g_FastPos[pos >> 6] + 12);
            if (pos < (1 << 27)) return (UInt32)(g_FastPos[pos >> 16] + 32);
            return (UInt32)(g_FastPos[pos >> 26] + 52);
        }

        private Base.State _state;
        private Byte _previousByte;
        private readonly UInt32[] _repDistances = new UInt32[Base.kNumRepDistances];

        private void BaseInit()
        {
            _state.Init();
            _previousByte = 0;
            for (UInt32 i = 0; i < Base.kNumRepDistances; i++) _repDistances[i] = 0;
        }

        private const int kDefaultDictionaryLogSize = 22;
        private const UInt32 kNumFastBytesDefault = 0x20;

        private class LiteralEncoder
        {
            public struct Encoder2
            {
                private BitEncoder[] m_Encoders;

                public void Create()
                {
                    m_Encoders = new BitEncoder[0x300];
                }

                public void Init()
                {
                    for (var i = 0; i < 0x300; i++) m_Encoders[i].Init();
                }

                public void Encode(RangeCoderEncoder rangeEncoder, byte symbol)
                {
                    uint context = 1;
                    for (var i = 7; i >= 0; i--)
                    {
                        var bit = (uint)((symbol >> i) & 1);
                        m_Encoders[context].Encode(rangeEncoder, bit);
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

                        m_Encoders[state].Encode(rangeEncoder, bit);
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
                            price += m_Encoders[((1 + matchBit) << 8) + context].GetPrice(bit);
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
                        price += m_Encoders[context].GetPrice(bit);
                        context = (context << 1) | bit;
                    }

                    return price;
                }
            }

            private Encoder2[]? m_Coders;
            private int m_NumPrevBits;
            private int m_NumPosBits;
            private uint m_PosMask;

            public void Create(int numPosBits, int numPrevBits)
            {
                if (m_Coders != null && m_NumPrevBits == numPrevBits && m_NumPosBits == numPosBits) return;
                m_NumPosBits = numPosBits;
                m_PosMask = ((uint)1 << numPosBits) - 1;
                m_NumPrevBits = numPrevBits;
                var numStates = (uint)1 << (m_NumPrevBits + m_NumPosBits);
                m_Coders = new Encoder2[numStates];
                for (uint i = 0; i < numStates; i++)
                    m_Coders[i].Create();
            }

            public void Init()
            {
                if (m_Coders is null) throw new Exception("Must call Create() before Init()");
                var numStates = (uint)1 << (m_NumPrevBits + m_NumPosBits);
                for (uint i = 0; i < numStates; i++) m_Coders[i].Init();
            }

            public Encoder2 GetSubCoder(UInt32 pos, Byte prevByte)
            {
                if (m_Coders is null) throw new Exception("Must call Create() before GetSubCoder()");
                return m_Coders[((pos & m_PosMask) << m_NumPrevBits) + (uint)(prevByte >> (8 - m_NumPrevBits))];
            }
        }

        private class LenEncoder
        {
            private BitEncoder _choice;
            private BitEncoder _choice2;
            private readonly BitTreeEncoder[] _lowCoder = new BitTreeEncoder[Base.kNumPosStatesEncodingMax];
            private readonly BitTreeEncoder[] _midCoder = new BitTreeEncoder[Base.kNumPosStatesEncodingMax];
            private readonly BitTreeEncoder _highCoder = new(Base.kNumHighLenBits);

            protected LenEncoder()
            {
                for (UInt32 posState = 0; posState < Base.kNumPosStatesEncodingMax; posState++)
                {
                    _lowCoder[posState] = new BitTreeEncoder(Base.kNumLowLenBits);
                    _midCoder[posState] = new BitTreeEncoder(Base.kNumMidLenBits);
                }
            }

            public void Init(UInt32 numPosStates)
            {
                _choice.Init();
                _choice2.Init();
                for (UInt32 posState = 0; posState < numPosStates; posState++)
                {
                    _lowCoder[posState].Init();
                    _midCoder[posState].Init();
                }

                _highCoder.Init();
            }

            protected void Encode(RangeCoderEncoder rangeEncoder, UInt32 symbol, UInt32 posState)
            {
                if (symbol < Base.kNumLowLenSymbols)
                {
                    _choice.Encode(rangeEncoder, 0);
                    _lowCoder[posState].Encode(rangeEncoder, symbol);
                }
                else
                {
                    symbol -= Base.kNumLowLenSymbols;
                    _choice.Encode(rangeEncoder, 1);
                    if (symbol < Base.kNumMidLenSymbols)
                    {
                        _choice2.Encode(rangeEncoder, 0);
                        _midCoder[posState].Encode(rangeEncoder, symbol);
                    }
                    else
                    {
                        _choice2.Encode(rangeEncoder, 1);
                        _highCoder.Encode(rangeEncoder, symbol - Base.kNumMidLenSymbols);
                    }
                }
            }

            protected void SetPrices(UInt32 posState, UInt32 numSymbols, UInt32[] prices, UInt32 st)
            {
                var a0 = _choice.GetPrice0();
                var a1 = _choice.GetPrice1();
                var b0 = a1 + _choice2.GetPrice0();
                var b1 = a1 + _choice2.GetPrice1();
                UInt32 i;
                for (i = 0; i < Base.kNumLowLenSymbols; i++)
                {
                    if (i >= numSymbols)
                        return;
                    prices[st + i] = a0 + _lowCoder[posState].GetPrice(i);
                }

                for (; i < Base.kNumLowLenSymbols + Base.kNumMidLenSymbols; i++)
                {
                    if (i >= numSymbols)
                        return;
                    prices[st + i] = b0 + _midCoder[posState].GetPrice(i - Base.kNumLowLenSymbols);
                }

                for (; i < numSymbols; i++)
                    prices[st + i] = b1 + _highCoder.GetPrice(i - Base.kNumLowLenSymbols - Base.kNumMidLenSymbols);
            }
        }

        private class LenPriceTableEncoder : LenEncoder
        {
            private readonly UInt32[] _prices = new UInt32[Base.kNumLenSymbols << Base.kNumPosStatesBitsEncodingMax];
            private UInt32 _tableSize;
            private readonly UInt32[] _counters = new UInt32[Base.kNumPosStatesEncodingMax];

            public void SetTableSize(UInt32 tableSize)
            {
                _tableSize = tableSize;
            }

            public UInt32 GetPrice(UInt32 symbol, UInt32 posState)
            {
                return _prices[posState * Base.kNumLenSymbols + symbol];
            }

            private void UpdateTable(UInt32 posState)
            {
                SetPrices(posState, _tableSize, _prices, posState * Base.kNumLenSymbols);
                _counters[posState] = _tableSize;
            }

            public void UpdateTables(UInt32 numPosStates)
            {
                for (UInt32 posState = 0; posState < numPosStates; posState++)
                    UpdateTable(posState);
            }

            public new void Encode(RangeCoderEncoder rangeEncoder, UInt32 symbol, UInt32 posState)
            {
                base.Encode(rangeEncoder, symbol, posState);
                if (--_counters[posState] == 0)
                    UpdateTable(posState);
            }
        }

        private const UInt32 kNumOpts = 1 << 12;

        private class Optimal
        {
            public Base.State State;

            public bool Prev1IsChar;
            public bool Prev2;

            public UInt32 PosPrev2;
            public UInt32 BackPrev2;

            public UInt32 Price;
            public UInt32 PosPrev;
            public UInt32 BackPrev;

            public UInt32 Backs0;
            public UInt32 Backs1;
            public UInt32 Backs2;
            public UInt32 Backs3;

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

        private readonly Optimal[] _optimum = new Optimal[kNumOpts];
        private BinTree? _matchFinder;
        private readonly RangeCoderEncoder _rangeEncoder = new();

        private readonly BitEncoder[] _isMatch = new BitEncoder[Base.kNumStates << Base.kNumPosStatesBitsMax];
        private readonly BitEncoder[] _isRep = new BitEncoder[Base.kNumStates];
        private readonly BitEncoder[] _isRepG0 = new BitEncoder[Base.kNumStates];
        private readonly BitEncoder[] _isRepG1 = new BitEncoder[Base.kNumStates];
        private readonly BitEncoder[] _isRepG2 = new BitEncoder[Base.kNumStates];
        private readonly BitEncoder[] _isRep0Long = new BitEncoder[Base.kNumStates << Base.kNumPosStatesBitsMax];

        private readonly BitTreeEncoder[] _posSlotEncoder = new BitTreeEncoder[Base.kNumLenToPosStates];

        private readonly BitEncoder[] _posEncoders = new BitEncoder[Base.kNumFullDistances - Base.kEndPosModelIndex];
        private readonly BitTreeEncoder _posAlignEncoder = new(Base.kNumAlignBits);

        private readonly LenPriceTableEncoder _lenEncoder = new();
        private readonly LenPriceTableEncoder _repMatchLenEncoder = new();

        private readonly LiteralEncoder _literalEncoder = new();

        private readonly UInt32[] _matchDistances = new UInt32[Base.kMatchMaxLen * 2 + 2];

        private UInt32 _numFastBytes = kNumFastBytesDefault;
        private UInt32 _longestMatchLength;
        private UInt32 _numDistancePairs;

        private UInt32 _additionalOffset;

        private UInt32 _optimumEndIndex;
        private UInt32 _optimumCurrentIndex;

        private bool _longestMatchWasFound;

        private readonly UInt32[] _posSlotPrices = new UInt32[1 << (Base.kNumPosSlotBits + Base.kNumLenToPosStatesBits)];
        private readonly UInt32[] _distancesPrices = new UInt32[Base.kNumFullDistances << Base.kNumLenToPosStatesBits];
        private readonly UInt32[] _alignPrices = new UInt32[Base.kAlignTableSize];
        private UInt32 _alignPriceCount;

        private UInt32 _distTableSize = (kDefaultDictionaryLogSize * 2);

        private int _posStateBits = 2;
        private UInt32 _posStateMask = (4 - 1);
        private int _numLiteralPosStateBits;
        private int _numLiteralContextBits = 3;

        private UInt32 _dictionarySize = (1 << kDefaultDictionaryLogSize);
        private UInt32 _dictionarySizePrev = 0xFFFFFFFF;
        private UInt32 _numFastBytesPrev = 0xFFFFFFFF;

        private Int64 nowPos64;
        private bool _finished;
        private Stream? _inStream;

        private EMatchFinderType _matchFinderType = EMatchFinderType.BT4;
        private bool _writeEndMark;

        private bool _needReleaseMFStream;

        private void Create()
        {
            if (_matchFinder == null)
            {
                var bt = new BinTree();
                var numHashBytes = 4;
                if (_matchFinderType == EMatchFinderType.BT2)
                    numHashBytes = 2;
                bt.SetType(numHashBytes);
                _matchFinder = bt;
            }

            _literalEncoder.Create(_numLiteralPosStateBits, _numLiteralContextBits);

            if (_dictionarySize == _dictionarySizePrev && _numFastBytesPrev == _numFastBytes)
                return;
            _matchFinder.Create(_dictionarySize, kNumOpts, _numFastBytes, Base.kMatchMaxLen + 1);
            _dictionarySizePrev = _dictionarySize;
            _numFastBytesPrev = _numFastBytes;
        }

        public LzmaEncoder()
        {
            for (var i = 0; i < kNumOpts; i++) _optimum[i] = new Optimal();
            for (var i = 0; i < Base.kNumLenToPosStates; i++) _posSlotEncoder[i] = new BitTreeEncoder(Base.kNumPosSlotBits);
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
            for (i = 0; i < Base.kNumStates; i++)
            {
                for (uint j = 0; j <= _posStateMask; j++)
                {
                    var complexState = (i << Base.kNumPosStatesBitsMax) + j;
                    _isMatch[complexState].Init();
                    _isRep0Long[complexState].Init();
                }

                _isRep[i].Init();
                _isRepG0[i].Init();
                _isRepG1[i].Init();
                _isRepG2[i].Init();
            }

            _literalEncoder.Init();
            for (i = 0; i < Base.kNumLenToPosStates; i++)
                _posSlotEncoder[i].Init();
            for (i = 0; i < Base.kNumFullDistances - Base.kEndPosModelIndex; i++)
                _posEncoders[i].Init();

            _lenEncoder.Init((UInt32)1 << _posStateBits);
            _repMatchLenEncoder.Init((UInt32)1 << _posStateBits);

            _posAlignEncoder.Init();

            _longestMatchWasFound = false;
            _optimumEndIndex = 0;
            _optimumCurrentIndex = 0;
            _additionalOffset = 0;
        }

        private void ReadMatchDistances(out UInt32 lenRes, out UInt32 numDistancePairs)
        {
            if (_matchFinder is null) throw new Exception();
            lenRes = 0;
            numDistancePairs = _matchFinder.GetMatches(_matchDistances);
            if (numDistancePairs > 0)
            {
                lenRes = _matchDistances[numDistancePairs - 2];
                if (lenRes == _numFastBytes)
                    lenRes += _matchFinder.GetMatchLen((int)lenRes - 1, _matchDistances[numDistancePairs - 1],
                        Base.kMatchMaxLen - lenRes);
            }

            _additionalOffset++;
        }


        private void MovePos(UInt32 num)
        {
            if (_matchFinder is null) throw new Exception();
            if (num <= 0) return;
            
            _matchFinder.Skip(num);
            _additionalOffset += num;
        }

        private UInt32 GetRepLen1Price(Base.State state, UInt32 posState)
        {
            return _isRepG0[state.Index].GetPrice0() +
                   _isRep0Long[(state.Index << Base.kNumPosStatesBitsMax) + posState].GetPrice0();
        }

        private UInt32 GetPureRepPrice(UInt32 repIndex, Base.State state, UInt32 posState)
        {
            UInt32 price;
            if (repIndex == 0)
            {
                price = _isRepG0[state.Index].GetPrice0();
                price += _isRep0Long[(state.Index << Base.kNumPosStatesBitsMax) + posState].GetPrice1();
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

        private UInt32 GetRepPrice(UInt32 repIndex, UInt32 len, Base.State state, UInt32 posState)
        {
            var price = _repMatchLenEncoder.GetPrice(len - Base.kMatchMinLen, posState);
            return price + GetPureRepPrice(repIndex, state, posState);
        }

        private UInt32 GetPosLenPrice(UInt32 pos, UInt32 len, UInt32 posState)
        {
            UInt32 price;
            var lenToPosState = Base.GetLenToPosState(len);
            if (pos < Base.kNumFullDistances)
                price = _distancesPrices[(lenToPosState * Base.kNumFullDistances) + pos];
            else
                price = _posSlotPrices[(lenToPosState << Base.kNumPosSlotBits) + GetPosSlot2(pos)] +
                        _alignPrices[pos & Base.kAlignMask];
            return price + _lenEncoder.GetPrice(len - Base.kMatchMinLen, posState);
        }

        private UInt32 Backward(out UInt32 backRes, UInt32 cur)
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

        private readonly UInt32[] reps = new UInt32[Base.kNumRepDistances];
        private readonly UInt32[] repLens = new UInt32[Base.kNumRepDistances];


        private UInt32 GetOptimum(UInt32 position, out UInt32 backRes)
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

            UInt32 lenMain, numDistancePairs;
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

            UInt32 repMaxIndex = 0;
            UInt32 i;
            for (i = 0; i < Base.kNumRepDistances; i++)
            {
                reps[i] = _repDistances[i];
                repLens[i] = _matchFinder.GetMatchLen(0 - 1, reps[i], Base.kMatchMaxLen);
                if (repLens[i] > repLens[repMaxIndex])
                    repMaxIndex = i;
            }

            if (repLens[repMaxIndex] >= _numFastBytes)
            {
                backRes = repMaxIndex;
                var lenRes = repLens[repMaxIndex];
                MovePos(lenRes - 1);
                return lenRes;
            }

            if (lenMain >= _numFastBytes)
            {
                backRes = _matchDistances[numDistancePairs - 1] + Base.kNumRepDistances;
                MovePos(lenMain - 1);
                return lenMain;
            }

            var currentByte = _matchFinder.GetIndexByte(0 - 1);
            var matchByte = _matchFinder.GetIndexByte((Int32)(0 - _repDistances[0] - 1 - 1));

            if (lenMain < 2 && currentByte != matchByte && repLens[repMaxIndex] < 2)
            {
                backRes = 0xFFFFFFFF;
                return 1;
            }

            _optimum[0].State = _state;

            var posState = (position & _posStateMask);

            _optimum[1].Price = _isMatch[(_state.Index << Base.kNumPosStatesBitsMax) + posState].GetPrice0() +
                                _literalEncoder.GetSubCoder(position, _previousByte).GetPrice(!_state.IsCharState(), matchByte, currentByte);
            _optimum[1].MakeAsChar();

            var matchPrice = _isMatch[(_state.Index << Base.kNumPosStatesBitsMax) + posState].GetPrice1();
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

            var lenEnd = ((lenMain >= repLens[repMaxIndex]) ? lenMain : repLens[repMaxIndex]);

            if (lenEnd < 2)
            {
                backRes = _optimum[1].BackPrev;
                return 1;
            }

            _optimum[1].PosPrev = 0;

            _optimum[0].Backs0 = reps[0];
            _optimum[0].Backs1 = reps[1];
            _optimum[0].Backs2 = reps[2];
            _optimum[0].Backs3 = reps[3];

            var len = lenEnd;
            do
                _optimum[len--].Price = kInfinityPrice;
            while (len >= 2);

            for (i = 0; i < Base.kNumRepDistances; i++)
            {
                var repLen = repLens[i];
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

            len = ((repLens[0] >= 2) ? repLens[0] + 1 : 2);
            if (len <= lenMain)
            {
                UInt32 offs = 0;
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
                        optimum.BackPrev = distance + Base.kNumRepDistances;
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

            UInt32 cur = 0;

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
                        if (_optimum[cur].BackPrev2 < Base.kNumRepDistances)
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
                    UInt32 pos;
                    if (_optimum[cur].Prev1IsChar && _optimum[cur].Prev2)
                    {
                        posPrev = _optimum[cur].PosPrev2;
                        pos = _optimum[cur].BackPrev2;
                        state.UpdateRep();
                    }
                    else
                    {
                        pos = _optimum[cur].BackPrev;
                        if (pos < Base.kNumRepDistances)
                            state.UpdateRep();
                        else
                            state.UpdateMatch();
                    }

                    var opt = _optimum[posPrev];
                    if (pos < Base.kNumRepDistances)
                    {
                        if (pos == 0)
                        {
                            reps[0] = opt.Backs0;
                            reps[1] = opt.Backs1;
                            reps[2] = opt.Backs2;
                            reps[3] = opt.Backs3;
                        }
                        else if (pos == 1)
                        {
                            reps[0] = opt.Backs1;
                            reps[1] = opt.Backs0;
                            reps[2] = opt.Backs2;
                            reps[3] = opt.Backs3;
                        }
                        else if (pos == 2)
                        {
                            reps[0] = opt.Backs2;
                            reps[1] = opt.Backs0;
                            reps[2] = opt.Backs1;
                            reps[3] = opt.Backs3;
                        }
                        else
                        {
                            reps[0] = opt.Backs3;
                            reps[1] = opt.Backs0;
                            reps[2] = opt.Backs1;
                            reps[3] = opt.Backs2;
                        }
                    }
                    else
                    {
                        reps[0] = (pos - Base.kNumRepDistances);
                        reps[1] = opt.Backs0;
                        reps[2] = opt.Backs1;
                        reps[3] = opt.Backs2;
                    }
                }

                _optimum[cur].State = state;
                _optimum[cur].Backs0 = reps[0];
                _optimum[cur].Backs1 = reps[1];
                _optimum[cur].Backs2 = reps[2];
                _optimum[cur].Backs3 = reps[3];
                var curPrice = _optimum[cur].Price;

                currentByte = _matchFinder.GetIndexByte(0 - 1);
                matchByte = _matchFinder.GetIndexByte((Int32)(0 - reps[0] - 1 - 1));

                posState = (position & _posStateMask);

                var curAnd1Price = curPrice +
                                   _isMatch[(state.Index << Base.kNumPosStatesBitsMax) + posState].GetPrice0() +
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

                matchPrice = curPrice + _isMatch[(state.Index << Base.kNumPosStatesBitsMax) + posState].GetPrice1();
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
                numAvailableBytesFull = Math.Min(kNumOpts - 1 - cur, numAvailableBytesFull);
                numAvailableBytes = numAvailableBytesFull;

                if (numAvailableBytes < 2)
                    continue;
                if (numAvailableBytes > _numFastBytes)
                    numAvailableBytes = _numFastBytes;
                if (!nextIsChar && matchByte != currentByte)
                {
                    // try Literal + rep0
                    var t = Math.Min(numAvailableBytesFull - 1, _numFastBytes);
                    var lenTest2 = _matchFinder.GetMatchLen(0, reps[0], t);
                    if (lenTest2 >= 2)
                    {
                        var state2 = state;
                        state2.UpdateChar();
                        var posStateNext = (position + 1) & _posStateMask;
                        var nextRepMatchPrice = curAnd1Price +
                                                _isMatch[(state2.Index << Base.kNumPosStatesBitsMax) + posStateNext].GetPrice1() +
                                                _isRep[state2.Index].GetPrice1();
                        {
                            var offset = cur + 1 + lenTest2;
                            while (lenEnd < offset)
                                _optimum[++lenEnd].Price = kInfinityPrice;
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

                UInt32 startLen = 2; // speed optimization 

                for (UInt32 repIndex = 0; repIndex < Base.kNumRepDistances; repIndex++)
                {
                    var lenTest = _matchFinder.GetMatchLen(0 - 1, reps[repIndex], numAvailableBytes);
                    if (lenTest < 2)
                        continue;
                    var lenTestTemp = lenTest;
                    do
                    {
                        while (lenEnd < cur + lenTest)
                            _optimum[++lenEnd].Price = kInfinityPrice;
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
                        var lenTest2 = _matchFinder.GetMatchLen((Int32)lenTest, reps[repIndex], t);
                        if (lenTest2 >= 2)
                        {
                            var state2 = state;
                            state2.UpdateRep();
                            var posStateNext = (position + lenTest) & _posStateMask;
                            var curAndLenCharPrice =
                                repMatchPrice + GetRepPrice(repIndex, lenTest, state, posState) +
                                _isMatch[(state2.Index << Base.kNumPosStatesBitsMax) + posStateNext].GetPrice0() +
                                _literalEncoder.GetSubCoder(position + lenTest,
                                    _matchFinder.GetIndexByte((Int32)lenTest - 1 - 1)).GetPrice(true,
                                    _matchFinder.GetIndexByte((Int32)lenTest - 1 - (Int32)(reps[repIndex] + 1)),
                                    _matchFinder.GetIndexByte((Int32)lenTest - 1));
                            state2.UpdateChar();
                            posStateNext = (position + lenTest + 1) & _posStateMask;
                            var nextMatchPrice = curAndLenCharPrice + _isMatch[(state2.Index << Base.kNumPosStatesBitsMax) + posStateNext].GetPrice1();
                            var nextRepMatchPrice = nextMatchPrice + _isRep[state2.Index].GetPrice1();

                            // for(; lenTest2 >= 2; lenTest2--)
                            {
                                var offset = lenTest + 1 + lenTest2;
                                while (lenEnd < cur + offset)
                                    _optimum[++lenEnd].Price = kInfinityPrice;
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
                    { // ??? This was wrong before?
                        _matchDistances[numDistancePairs] = newLen;
                    }

                    numDistancePairs += 2;
                }

                if (newLen < startLen) continue;

                normalMatchPrice = matchPrice + _isRep[state.Index].GetPrice0();
                while (lenEnd < cur + newLen)
                    _optimum[++lenEnd].Price = kInfinityPrice;

                UInt32 offs = 0;
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
                        optimum.BackPrev = curBack + Base.kNumRepDistances;
                        optimum.Prev1IsChar = false;
                    }
                    if (lenTest != _matchDistances[offs]) continue;
                    
                    if (lenTest < numAvailableBytesFull)
                    {
                        var t = Math.Min(numAvailableBytesFull - 1 - lenTest, _numFastBytes);
                        var lenTest2 = _matchFinder.GetMatchLen((Int32)lenTest, curBack, t);
                        if (lenTest2 >= 2)
                        {
                            var state2 = state;
                            state2.UpdateMatch();
                            var posStateNext = (position + lenTest) & _posStateMask;
                            var curAndLenCharPrice = curAndLenPrice +
                                                     _isMatch[(state2.Index << Base.kNumPosStatesBitsMax) + posStateNext].GetPrice0() +
                                                     _literalEncoder.GetSubCoder(position + lenTest,
                                                         _matchFinder.GetIndexByte((Int32)lenTest - 1 - 1)).GetPrice(true,
                                                         _matchFinder.GetIndexByte((Int32)lenTest - (Int32)(curBack + 1) - 1),
                                                         _matchFinder.GetIndexByte((Int32)lenTest - 1));
                            state2.UpdateChar();
                            posStateNext = (position + lenTest + 1) & _posStateMask;
                            var nextMatchPrice = curAndLenCharPrice + _isMatch[(state2.Index << Base.kNumPosStatesBitsMax) + posStateNext].GetPrice1();
                            var nextRepMatchPrice = nextMatchPrice + _isRep[state2.Index].GetPrice1();

                            var offset = lenTest + 1 + lenTest2;
                            while (lenEnd < cur + offset)
                                _optimum[++lenEnd].Price = kInfinityPrice;
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
                                optimum.BackPrev2 = curBack + Base.kNumRepDistances;
                            }
                        }
                    }

                    offs += 2;
                    if (offs == numDistancePairs) break;
                }
            }
        }

        private void WriteEndMarker(UInt32 posState)
        {
            if (!_writeEndMark)
                return;

            _isMatch[(_state.Index << Base.kNumPosStatesBitsMax) + posState].Encode(_rangeEncoder, 1);
            _isRep[_state.Index].Encode(_rangeEncoder, 0);
            _state.UpdateMatch();
            var len = Base.kMatchMinLen;
            _lenEncoder.Encode(_rangeEncoder, len - Base.kMatchMinLen, posState);
            UInt32 posSlot = (1 << Base.kNumPosSlotBits) - 1;
            var lenToPosState = Base.GetLenToPosState(len);
            _posSlotEncoder[lenToPosState].Encode(_rangeEncoder, posSlot);
            var footerBits = 30;
            var posReduced = (((UInt32)1) << footerBits) - 1;
            _rangeEncoder.EncodeDirectBits(posReduced >> Base.kNumAlignBits, footerBits - Base.kNumAlignBits);
            _posAlignEncoder.ReverseEncode(_rangeEncoder, posReduced & Base.kAlignMask);
        }

        private void Flush(UInt32 nowPos)
        {
            ReleaseMFStream();
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
                _needReleaseMFStream = true;
                _inStream = null;
            }

            if (_finished)
                return;
            _finished = true;


            var progressPosValuePrev = nowPos64;
            if (nowPos64 == 0)
            {
                if (_matchFinder.GetNumAvailableBytes() == 0)
                {
                    Flush((UInt32)nowPos64);
                    return;
                }

                ReadMatchDistances(out _, out _);
                var posState = (UInt32)(nowPos64) & _posStateMask;
                _isMatch[(_state.Index << Base.kNumPosStatesBitsMax) + posState].Encode(_rangeEncoder, 0);
                _state.UpdateChar();
                var curByte = _matchFinder.GetIndexByte((Int32)(0 - _additionalOffset));
                _literalEncoder.GetSubCoder((UInt32)(nowPos64), _previousByte).Encode(_rangeEncoder, curByte);
                _previousByte = curByte;
                _additionalOffset--;
                nowPos64++;
            }

            if (_matchFinder.GetNumAvailableBytes() == 0)
            {
                Flush((UInt32)nowPos64);
                return;
            }

            while (true)
            {
                var len = GetOptimum((UInt32)nowPos64, out var pos);

                var posState = ((UInt32)nowPos64) & _posStateMask;
                var complexState = (_state.Index << Base.kNumPosStatesBitsMax) + posState;
                if (len == 1 && pos == 0xFFFFFFFF)
                {
                    _isMatch[complexState].Encode(_rangeEncoder, 0);
                    var curByte = _matchFinder.GetIndexByte((Int32)(0 - _additionalOffset));
                    var subCoder = _literalEncoder.GetSubCoder((UInt32)nowPos64, _previousByte);
                    if (!_state.IsCharState())
                    {
                        var matchByte = _matchFinder.GetIndexByte((Int32)(0 - _repDistances[0] - 1 - _additionalOffset));
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
                    if (pos < Base.kNumRepDistances)
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
                            _repMatchLenEncoder.Encode(_rangeEncoder, len - Base.kMatchMinLen, posState);
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
                        _lenEncoder.Encode(_rangeEncoder, len - Base.kMatchMinLen, posState);
                        pos -= Base.kNumRepDistances;
                        var posSlot = GetPosSlot(pos);
                        var lenToPosState = Base.GetLenToPosState(len);
                        _posSlotEncoder[lenToPosState].Encode(_rangeEncoder, posSlot);

                        if (posSlot >= Base.kStartPosModelIndex)
                        {
                            var footerBits = (int)((posSlot >> 1) - 1);
                            var baseVal = ((2 | (posSlot & 1)) << footerBits);
                            var posReduced = pos - baseVal;

                            if (posSlot < Base.kEndPosModelIndex)
                                BitTreeEncoder.ReverseEncode(_posEncoders,
                                    baseVal - posSlot - 1, _rangeEncoder, footerBits, posReduced);
                            else
                            {
                                _rangeEncoder.EncodeDirectBits(posReduced >> Base.kNumAlignBits, footerBits - Base.kNumAlignBits);
                                _posAlignEncoder.ReverseEncode(_rangeEncoder, posReduced & Base.kAlignMask);
                                _alignPriceCount++;
                            }
                        }

                        var distance = pos;
                        for (var i = Base.kNumRepDistances - 1; i >= 1; i--)
                            _repDistances[i] = _repDistances[i - 1];
                        _repDistances[0] = distance;
                        _matchPriceCount++;
                    }

                    _previousByte = _matchFinder.GetIndexByte((Int32)(len - 1 - _additionalOffset));
                }

                _additionalOffset -= len;
                nowPos64 += len;
                if (_additionalOffset == 0)
                {
                    // if (!_fastMode)
                    if (_matchPriceCount >= (1 << 7)) FillDistancesPrices();
                    if (_alignPriceCount >= Base.kAlignTableSize) FillAlignPrices();
                    
                    if (_matchFinder.GetNumAvailableBytes() == 0)
                    {
                        Flush((UInt32)nowPos64);
                        return;
                    }

                    if (nowPos64 - progressPosValuePrev >= (1 << 12))
                    {
                        _finished = false;
                        finished = false;
                        return;
                    }
                }
            }
        }

        private void ReleaseMFStream()
        {
            if (_matchFinder != null && _needReleaseMFStream)
            {
                _matchFinder.ReleaseStream();
                _needReleaseMFStream = false;
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
            ReleaseMFStream();
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

            _lenEncoder.SetTableSize(_numFastBytes + 1 - Base.kMatchMinLen);
            _lenEncoder.UpdateTables((UInt32)1 << _posStateBits);
            _repMatchLenEncoder.SetTableSize(_numFastBytes + 1 - Base.kMatchMinLen);
            _repMatchLenEncoder.UpdateTables((UInt32)1 << _posStateBits);

            nowPos64 = 0;
        }

        public void Code(Stream inStream, Stream outStream)
        {
            _needReleaseMFStream = false;
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

        private const int kPropSize = 5;
        private readonly Byte[] properties = new Byte[kPropSize];

        public void WriteCoderProperties(Stream outStream)
        {
            properties[0] = (Byte)((_posStateBits * 5 + _numLiteralPosStateBits) * 9 + _numLiteralContextBits);
            for (var i = 0; i < 4; i++)
                properties[1 + i] = (Byte)((_dictionarySize >> (8 * i)) & 0xFF);
            outStream.Write(properties, 0, kPropSize);
        }

        private readonly UInt32[] tempPrices = new UInt32[Base.kNumFullDistances];
        private UInt32 _matchPriceCount;

        private void FillDistancesPrices()
        {
            for (var i = Base.kStartPosModelIndex; i < Base.kNumFullDistances; i++)
            {
                var posSlot = GetPosSlot(i);
                var footerBits = (int)((posSlot >> 1) - 1);
                var baseVal = ((2 | (posSlot & 1)) << footerBits);
                tempPrices[i] = BitTreeEncoder.ReverseGetPrice(_posEncoders,
                    baseVal - posSlot - 1, footerBits, i - baseVal);
            }

            for (UInt32 lenToPosState = 0; lenToPosState < Base.kNumLenToPosStates; lenToPosState++)
            {
                UInt32 posSlot;
                var encoder = _posSlotEncoder[lenToPosState];

                var st = (lenToPosState << Base.kNumPosSlotBits);
                for (posSlot = 0; posSlot < _distTableSize; posSlot++)
                    _posSlotPrices[st + posSlot] = encoder.GetPrice(posSlot);
                for (posSlot = Base.kEndPosModelIndex; posSlot < _distTableSize; posSlot++)
                    _posSlotPrices[st + posSlot] += ((((posSlot >> 1) - 1) - Base.kNumAlignBits) << BitEncoder.kNumBitPriceShiftBits);

                var st2 = lenToPosState * Base.kNumFullDistances;
                UInt32 i;
                for (i = 0; i < Base.kStartPosModelIndex; i++)
                    _distancesPrices[st2 + i] = _posSlotPrices[st + i];
                for (; i < Base.kNumFullDistances; i++)
                    _distancesPrices[st2 + i] = _posSlotPrices[st + GetPosSlot(i)] + tempPrices[i];
            }

            _matchPriceCount = 0;
        }

        private void FillAlignPrices()
        {
            for (UInt32 i = 0; i < Base.kAlignTableSize; i++)
                _alignPrices[i] = _posAlignEncoder.ReverseGetPrice(i);
            _alignPriceCount = 0;
        }


        private static readonly string[] kMatchFinderIDs =
        {
            "BT2",
            "BT4",
        };

        private static int FindMatchFinder(string s)
        {
            for (var m = 0; m < kMatchFinderIDs.Length; m++)
                if (s == kMatchFinderIDs[m])
                    return m;
            return -1;
        }

        public void SetCoderProperties(CoderPropID[] propIDs, object[] setProps)
        {
            for (UInt32 i = 0; i < setProps.Length; i++)
            {
                var prop = setProps[i];
                switch (propIDs[i])
                {
                    case CoderPropID.NumFastBytes:
                    {
                        if (prop is not Int32 numFastBytes) throw new InvalidParamException();
                        if (numFastBytes < 5 || numFastBytes > Base.kMatchMaxLen) throw new InvalidParamException();
                        _numFastBytes = (UInt32)numFastBytes;
                        break;
                    }
                    case CoderPropID.Algorithm:
                    {
                        break;
                    }
                    case CoderPropID.MatchFinder:
                    {
                        if (prop is not String str) throw new InvalidParamException();
                        
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
                    case CoderPropID.DictionarySize:
                    {
                        const int kDicLogSizeMaxCompress = 30;
                        if (prop is not Int32 dictionarySize) throw new InvalidParamException();
                        
                        if (dictionarySize < (UInt32)(1 << Base.kDicLogSizeMin) || dictionarySize > (UInt32)(1 << kDicLogSizeMaxCompress))
                            throw new InvalidParamException();
                        
                        _dictionarySize = (UInt32)dictionarySize;
                        int dicLogSize;
                        for (dicLogSize = 0; dicLogSize < (UInt32)kDicLogSizeMaxCompress; dicLogSize++)
                        {
                            if (dictionarySize <= ((UInt32)(1) << dicLogSize)) break;
                        }

                        _distTableSize = (UInt32)dicLogSize * 2;
                        break;
                    }
                    case CoderPropID.PosStateBits:
                    {
                        if (prop is not Int32 bits) throw new InvalidParamException();
                        if (bits < 0 || bits > (UInt32)Base.kNumPosStatesBitsEncodingMax) throw new InvalidParamException();
                        _posStateBits = bits;
                        _posStateMask = (((UInt32)1) << _posStateBits) - 1;
                        break;
                    }
                    case CoderPropID.LitPosBits:
                    {
                        if (prop is not Int32 bits) throw new InvalidParamException();
                        if (bits < 0 || bits > Base.kNumLitPosStatesBitsEncodingMax) throw new InvalidParamException();
                        _numLiteralPosStateBits = bits;
                        break;
                    }
                    case CoderPropID.LitContextBits:
                    {
                        if (prop is not Int32 bits) throw new InvalidParamException();
                        if (bits < 0 || bits > Base.kNumLitContextBitsMax) throw new InvalidParamException();
                        _numLiteralContextBits = bits;
                        break;
                    }
                    case CoderPropID.EndMarker:
                    {
                        if (prop is not Boolean b) throw new InvalidParamException();
                        SetWriteEndMarkerMode(b);
                        break;
                    }
                    default:
                        throw new InvalidParamException();
                }
            }
        }
    }

    public class InvalidParamException : Exception
    {
    }

    /// <summary>
    /// Provides the fields that represent properties identifiers for compressing.
    /// </summary>
    public enum CoderPropID
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

    public class DataErrorException : Exception
    {
    }

    public class LzmaDecoder
    {
        private class LenDecoder
        {
            private BitDecoder m_Choice;
            private BitDecoder m_Choice2;
            private readonly BitTreeDecoder[] m_LowCoder = new BitTreeDecoder[Base.kNumPosStatesMax];
            private readonly BitTreeDecoder[] m_MidCoder = new BitTreeDecoder[Base.kNumPosStatesMax];
            private readonly BitTreeDecoder m_HighCoder = new(Base.kNumHighLenBits);
            private uint m_NumPosStates;

            public void Create(uint numPosStates)
            {
                for (var posState = m_NumPosStates; posState < numPosStates; posState++)
                {
                    m_LowCoder[posState] = new BitTreeDecoder(Base.kNumLowLenBits);
                    m_MidCoder[posState] = new BitTreeDecoder(Base.kNumMidLenBits);
                }

                m_NumPosStates = numPosStates;
            }

            public void Init()
            {
                m_Choice.Init();
                for (uint posState = 0; posState < m_NumPosStates; posState++)
                {
                    m_LowCoder[posState].Init();
                    m_MidCoder[posState].Init();
                }

                m_Choice2.Init();
                m_HighCoder.Init();
            }

            public uint Decode(RangeCoderDecoder rangeDecoder, uint posState)
            {
                if (m_Choice.Decode(rangeDecoder) == 0)
                    return m_LowCoder[posState].Decode(rangeDecoder);
                var symbol = Base.kNumLowLenSymbols;
                if (m_Choice2.Decode(rangeDecoder) == 0)
                    symbol += m_MidCoder[posState].Decode(rangeDecoder);
                else
                {
                    symbol += Base.kNumMidLenSymbols;
                    symbol += m_HighCoder.Decode(rangeDecoder);
                }

                return symbol;
            }
        }

        private class LiteralDecoder
        {
            private struct Decoder2
            {
                private BitDecoder[] m_Decoders;

                public void Create()
                {
                    m_Decoders = new BitDecoder[0x300];
                }

                public void Init()
                {
                    for (var i = 0; i < 0x300; i++) m_Decoders[i].Init();
                }

                public byte DecodeNormal(RangeCoderDecoder rangeDecoder)
                {
                    uint symbol = 1;
                    do
                    {
                        symbol = (symbol << 1) | m_Decoders[symbol].Decode(rangeDecoder);
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
                        var bit = m_Decoders[((1 + matchBit) << 8) + symbol].Decode(rangeDecoder);
                        symbol = (symbol << 1) | bit;
                        if (matchBit != bit)
                        {
                            while (symbol < 0x100)
                                symbol = (symbol << 1) | m_Decoders[symbol].Decode(rangeDecoder);
                            break;
                        }
                    } while (symbol < 0x100);

                    return (byte)(symbol & 0xFF);
                }
            }

            private Decoder2[]? m_Coders;
            private int m_NumPrevBits;
            private int m_NumPosBits;
            private uint m_PosMask;

            public void Create(int numPosBits, int numPrevBits)
            {
                if (m_Coders != null && m_NumPrevBits == numPrevBits && m_NumPosBits == numPosBits) return;
                
                m_NumPosBits = numPosBits;
                m_PosMask = ((uint)1 << numPosBits) - 1;
                m_NumPrevBits = numPrevBits;
                var numStates = (uint)1 << (m_NumPrevBits + m_NumPosBits);
                m_Coders = new Decoder2[numStates];
                
                for (uint i = 0; i < numStates; i++) m_Coders[i].Create();
            }

            // Copied from the encoder
            private const int _numLiteralPosStateBits = 0;
            private const int _numLiteralContextBits = 3;

            public void Init()
            {
                if (m_Coders is null) Create(_numLiteralPosStateBits, _numLiteralContextBits);

                var numStates = (uint)1 << (m_NumPrevBits + m_NumPosBits);
                
                for (uint i = 0; i < numStates; i++) m_Coders?[i].Init();
            }

            private uint GetState(uint pos, byte prevByte)
            {
                return ((pos & m_PosMask) << m_NumPrevBits) + (uint)(prevByte >> (8 - m_NumPrevBits));
            }

            public byte DecodeNormal(RangeCoderDecoder rangeDecoder, uint pos, byte prevByte)
            {
                return m_Coders![GetState(pos, prevByte)].DecodeNormal(rangeDecoder);
            }

            public byte DecodeWithMatchByte(RangeCoderDecoder rangeDecoder, uint pos, byte prevByte, byte matchByte)
            {
                return m_Coders![GetState(pos, prevByte)].DecodeWithMatchByte(rangeDecoder, matchByte);
            }
        }

        private readonly OutWindow m_OutWindow = new();
        private readonly RangeCoderDecoder m_RangeDecoder = new();

        private readonly BitDecoder[] m_IsMatchDecoders = new BitDecoder[Base.kNumStates << Base.kNumPosStatesBitsMax];
        private readonly BitDecoder[] m_IsRepDecoders = new BitDecoder[Base.kNumStates];
        private readonly BitDecoder[] m_IsRepG0Decoders = new BitDecoder[Base.kNumStates];
        private readonly BitDecoder[] m_IsRepG1Decoders = new BitDecoder[Base.kNumStates];
        private readonly BitDecoder[] m_IsRepG2Decoders = new BitDecoder[Base.kNumStates];
        private readonly BitDecoder[] m_IsRep0LongDecoders = new BitDecoder[Base.kNumStates << Base.kNumPosStatesBitsMax];

        private readonly BitTreeDecoder[] m_PosSlotDecoder = new BitTreeDecoder[Base.kNumLenToPosStates];
        private readonly BitDecoder[] m_PosDecoders = new BitDecoder[Base.kNumFullDistances - Base.kEndPosModelIndex];

        private readonly BitTreeDecoder m_PosAlignDecoder = new(Base.kNumAlignBits);

        private readonly LenDecoder m_LenDecoder = new();
        private readonly LenDecoder m_RepLenDecoder = new();

        private readonly LiteralDecoder m_LiteralDecoder = new();

        private uint m_DictionarySize;
        private uint m_DictionarySizeCheck;

        private uint m_PosStateMask;

        public LzmaDecoder()
        {
            m_DictionarySize = 0xFFFFFFFF;
            for (var i = 0; i < Base.kNumLenToPosStates; i++)
                m_PosSlotDecoder[i] = new BitTreeDecoder(Base.kNumPosSlotBits);
        }

        private void SetDictionarySize(uint dictionarySize)
        {
            if (m_DictionarySize != dictionarySize)
            {
                m_DictionarySize = dictionarySize;
                m_DictionarySizeCheck = Math.Max(m_DictionarySize, 1);
                var blockSize = Math.Max(m_DictionarySizeCheck, (1 << 12));
                m_OutWindow.Create(blockSize);
            }
        }

        private void SetLiteralProperties(int lp, int lc)
        {
            if (lp > 8)
                throw new InvalidParamException();
            if (lc > 8)
                throw new InvalidParamException();
            m_LiteralDecoder.Create(lp, lc);
        }

        private void SetPosBitsProperties(int pb)
        {
            if (pb > Base.kNumPosStatesBitsMax)
                throw new InvalidParamException();
            var numPosStates = (uint)1 << pb;
            m_LenDecoder.Create(numPosStates);
            m_RepLenDecoder.Create(numPosStates);
            m_PosStateMask = numPosStates - 1;
        }

        private void Init(Stream inStream, Stream outStream)
        {
            m_RangeDecoder.Init(inStream);
            m_OutWindow.Init(outStream, false);

            uint i;
            for (i = 0; i < Base.kNumStates; i++)
            {
                for (uint j = 0; j <= m_PosStateMask; j++)
                {
                    var index = (i << Base.kNumPosStatesBitsMax) + j;
                    m_IsMatchDecoders[index].Init();
                    m_IsRep0LongDecoders[index].Init();
                }

                m_IsRepDecoders[i].Init();
                m_IsRepG0Decoders[i].Init();
                m_IsRepG1Decoders[i].Init();
                m_IsRepG2Decoders[i].Init();
            }

            m_LiteralDecoder.Init();
            for (i = 0; i < Base.kNumLenToPosStates; i++)
                m_PosSlotDecoder[i].Init();
            // m_PosSpecDecoder.Init();
            for (i = 0; i < Base.kNumFullDistances - Base.kEndPosModelIndex; i++)
                m_PosDecoders[i].Init();

            m_LenDecoder.Init();
            m_RepLenDecoder.Init();
            m_PosAlignDecoder.Init();
        }

        public void Code(Stream inStream, Stream outStream,
            Int64 inSize, Int64 outSize)
        {
            Init(inStream, outStream);

            var state = new Base.State();
            state.Init();
            uint rep0 = 0, rep1 = 0, rep2 = 0, rep3 = 0;

            UInt64 nowPos64 = 0;
            var outSize64 = (UInt64)outSize;
            if (nowPos64 < outSize64)
            {
                if (m_IsMatchDecoders[state.Index << Base.kNumPosStatesBitsMax].Decode(m_RangeDecoder) != 0)
                    throw new DataErrorException();
                state.UpdateChar();
                var b = m_LiteralDecoder.DecodeNormal(m_RangeDecoder, 0, 0);
                m_OutWindow.PutByte(b);
                nowPos64++;
            }

            while (nowPos64 < outSize64)
            {
                var posState = (uint)nowPos64 & m_PosStateMask;
                if (m_IsMatchDecoders[(state.Index << Base.kNumPosStatesBitsMax) + posState].Decode(m_RangeDecoder) == 0)
                {
                    byte b;
                    var prevByte = m_OutWindow.GetByte(0);
                    if (!state.IsCharState())
                        b = m_LiteralDecoder.DecodeWithMatchByte(m_RangeDecoder,
                            (uint)nowPos64, prevByte, m_OutWindow.GetByte(rep0));
                    else
                        b = m_LiteralDecoder.DecodeNormal(m_RangeDecoder, (uint)nowPos64, prevByte);
                    m_OutWindow.PutByte(b);
                    state.UpdateChar();
                    nowPos64++;
                }
                else
                {
                    uint len;
                    if (m_IsRepDecoders[state.Index].Decode(m_RangeDecoder) == 1)
                    {
                        if (m_IsRepG0Decoders[state.Index].Decode(m_RangeDecoder) == 0)
                        {
                            if (m_IsRep0LongDecoders[(state.Index << Base.kNumPosStatesBitsMax) + posState].Decode(m_RangeDecoder) == 0)
                            {
                                state.UpdateShortRep();
                                m_OutWindow.PutByte(m_OutWindow.GetByte(rep0));
                                nowPos64++;
                                continue;
                            }
                        }
                        else
                        {
                            UInt32 distance;
                            if (m_IsRepG1Decoders[state.Index].Decode(m_RangeDecoder) == 0)
                            {
                                distance = rep1;
                            }
                            else
                            {
                                if (m_IsRepG2Decoders[state.Index].Decode(m_RangeDecoder) == 0)
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

                        len = m_RepLenDecoder.Decode(m_RangeDecoder, posState) + Base.kMatchMinLen;
                        state.UpdateRep();
                    }
                    else
                    {
                        rep3 = rep2;
                        rep2 = rep1;
                        rep1 = rep0;
                        len = Base.kMatchMinLen + m_LenDecoder.Decode(m_RangeDecoder, posState);
                        state.UpdateMatch();
                        var posSlot = m_PosSlotDecoder[Base.GetLenToPosState(len)].Decode(m_RangeDecoder);
                        if (posSlot >= Base.kStartPosModelIndex)
                        {
                            var numDirectBits = (int)((posSlot >> 1) - 1);
                            rep0 = ((2 | (posSlot & 1)) << numDirectBits);
                            if (posSlot < Base.kEndPosModelIndex)
                                rep0 += BitTreeDecoder.ReverseDecode(m_PosDecoders,
                                    rep0 - posSlot - 1, m_RangeDecoder, numDirectBits);
                            else
                            {
                                rep0 += (m_RangeDecoder.DecodeDirectBits(
                                    numDirectBits - Base.kNumAlignBits) << Base.kNumAlignBits);
                                rep0 += m_PosAlignDecoder.ReverseDecode(m_RangeDecoder);
                            }
                        }
                        else
                            rep0 = posSlot;
                    }

                    if (rep0 >= m_OutWindow.TrainSize + nowPos64 || rep0 >= m_DictionarySizeCheck)
                    {
                        if (rep0 == 0xFFFFFFFF) break;
                        throw new DataErrorException();
                    }

                    m_OutWindow.CopyBlock(rep0, len);
                    nowPos64 += len;
                }
            }

            m_OutWindow.Flush();
            m_OutWindow.ReleaseStream();
            m_RangeDecoder.ReleaseStream();
        }

        public void SetDecoderProperties(byte[] properties)
        {
            if (properties.Length < 5)
                throw new InvalidParamException();
            var lc = properties[0] % 9;
            var remainder = properties[0] / 9;
            var lp = remainder % 5;
            var pb = remainder / 5;
            if (pb > Base.kNumPosStatesBitsMax)
                throw new InvalidParamException();
            UInt32 dictionarySize = 0;
            for (var i = 0; i < 4; i++)
                dictionarySize += ((UInt32)(properties[1 + i])) << (i * 8);
            SetDictionarySize(dictionarySize);
            SetLiteralProperties(lp, lc);
            SetPosBitsProperties(pb);
        }
    }

    internal class RangeCoderEncoder
    {
        public const uint kTopValue = (1 << 24);

        private Stream? Stream;

        public UInt64 Low;
        public uint Range;
        private uint _cacheSize;
        private byte _cache;

        public void SetStream(Stream stream)
        {
            Stream = stream;
        }

        public void ReleaseStream()
        {
            Stream = null;
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
            Stream?.Flush();
        }

        public void ShiftLow()
        {
            if ((uint)Low < 0xFF000000 || (uint)(Low >> 32) == 1)
            {
                var temp = _cache;
                do
                {
                    Stream?.WriteByte((byte)(temp + (Low >> 32)));
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
                if (Range < kTopValue)
                {
                    Range <<= 8;
                    ShiftLow();
                }
            }
        }
    }

    internal class RangeCoderDecoder
    {
        public const uint kTopValue = (1 << 24);
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

                if (range < kTopValue)
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
        public const int kNumBitModelTotalBits = 11;
        public const uint kBitModelTotal = (1 << kNumBitModelTotalBits);
        private const int kNumMoveBits = 5;
        private const int kNumMoveReducingBits = 2;
        public const int kNumBitPriceShiftBits = 6;

        private uint Prob;

        public void Init()
        {
            Prob = kBitModelTotal >> 1;
        }

        public void Encode(RangeCoderEncoder encoder, uint symbol)
        {
            // encoder.EncodeBit(Prob, kNumBitModelTotalBits, symbol);
            // UpdateModel(symbol);
            var newBound = (encoder.Range >> kNumBitModelTotalBits) * Prob;
            if (symbol == 0)
            {
                encoder.Range = newBound;
                Prob += (kBitModelTotal - Prob) >> kNumMoveBits;
            }
            else
            {
                encoder.Low += newBound;
                encoder.Range -= newBound;
                Prob -= (Prob) >> kNumMoveBits;
            }

            if (encoder.Range < RangeCoderEncoder.kTopValue)
            {
                encoder.Range <<= 8;
                encoder.ShiftLow();
            }
        }

        private static readonly UInt32[] ProbPrices = new UInt32[kBitModelTotal >> kNumMoveReducingBits];

        static BitEncoder()
        {
            const int kNumBits = (kNumBitModelTotalBits - kNumMoveReducingBits);
            for (var i = kNumBits - 1; i >= 0; i--)
            {
                var start = (UInt32)1 << (kNumBits - i - 1);
                var end = (UInt32)1 << (kNumBits - i);
                for (var j = start; j < end; j++)
                    ProbPrices[j] = ((UInt32)i << kNumBitPriceShiftBits) +
                                    (((end - j) << kNumBitPriceShiftBits) >> (kNumBits - i - 1));
            }
        }

        public uint GetPrice(uint symbol)
        {
            return ProbPrices[(((Prob - symbol) ^ ((-(int)symbol))) & (kBitModelTotal - 1)) >> kNumMoveReducingBits];
        }

        public uint GetPrice0()
        {
            return ProbPrices[Prob >> kNumMoveReducingBits];
        }

        public uint GetPrice1()
        {
            return ProbPrices[(kBitModelTotal - Prob) >> kNumMoveReducingBits];
        }
    }

    internal struct BitDecoder
    {
        public const int kNumBitModelTotalBits = 11;
        public const uint kBitModelTotal = (1 << kNumBitModelTotalBits);
        private const int kNumMoveBits = 5;

        private uint Prob;

        public void Init()
        {
            Prob = kBitModelTotal >> 1;
        }

        public uint Decode(RangeCoderDecoder rangeDecoder)
        {
            if (rangeDecoder.Stream is null) throw new Exception();
            
            var newBound = (rangeDecoder.Range >> kNumBitModelTotalBits) * Prob;
            if (rangeDecoder.Code < newBound)
            {
                rangeDecoder.Range = newBound;
                Prob += (kBitModelTotal - Prob) >> kNumMoveBits;
                if (rangeDecoder.Range < RangeCoderDecoder.kTopValue)
                {
                    rangeDecoder.Code = (rangeDecoder.Code << 8) | (byte)rangeDecoder.Stream.ReadByte();
                    rangeDecoder.Range <<= 8;
                }

                return 0;
            }

            rangeDecoder.Range -= newBound;
            rangeDecoder.Code -= newBound;
            Prob -= (Prob) >> kNumMoveBits;
            if (rangeDecoder.Range < RangeCoderDecoder.kTopValue)
            {
                rangeDecoder.Code = (rangeDecoder.Code << 8) | (byte)rangeDecoder.Stream.ReadByte();
                rangeDecoder.Range <<= 8;
            }

            return 1;
        }
    }

    internal readonly struct BitTreeEncoder
    {
        private readonly BitEncoder[] Models;
        private readonly int NumBitLevels;

        public BitTreeEncoder(int numBitLevels)
        {
            NumBitLevels = numBitLevels;
            Models = new BitEncoder[1 << numBitLevels];
        }

        public void Init()
        {
            for (uint i = 1; i < (1 << NumBitLevels); i++)
                Models[i].Init();
        }

        public void Encode(RangeCoderEncoder rangeEncoder, UInt32 symbol)
        {
            UInt32 m = 1;
            for (var bitIndex = NumBitLevels; bitIndex > 0;)
            {
                bitIndex--;
                var bit = (symbol >> bitIndex) & 1;
                Models[m].Encode(rangeEncoder, bit);
                m = (m << 1) | bit;
            }
        }

        public void ReverseEncode(RangeCoderEncoder rangeEncoder, UInt32 symbol)
        {
            UInt32 m = 1;
            for (UInt32 i = 0; i < NumBitLevels; i++)
            {
                var bit = symbol & 1;
                Models[m].Encode(rangeEncoder, bit);
                m = (m << 1) | bit;
                symbol >>= 1;
            }
        }

        public UInt32 GetPrice(UInt32 symbol)
        {
            UInt32 price = 0;
            UInt32 m = 1;
            for (var bitIndex = NumBitLevels; bitIndex > 0;)
            {
                bitIndex--;
                var bit = (symbol >> bitIndex) & 1;
                price += Models[m].GetPrice(bit);
                m = (m << 1) + bit;
            }

            return price;
        }

        public UInt32 ReverseGetPrice(UInt32 symbol)
        {
            UInt32 price = 0;
            UInt32 m = 1;
            for (var i = NumBitLevels; i > 0; i--)
            {
                var bit = symbol & 1;
                symbol >>= 1;
                price += Models[m].GetPrice(bit);
                m = (m << 1) | bit;
            }

            return price;
        }

        public static UInt32 ReverseGetPrice(BitEncoder[] Models, UInt32 startIndex,
            int NumBitLevels, UInt32 symbol)
        {
            UInt32 price = 0;
            UInt32 m = 1;
            for (var i = NumBitLevels; i > 0; i--)
            {
                var bit = symbol & 1;
                symbol >>= 1;
                price += Models[startIndex + m].GetPrice(bit);
                m = (m << 1) | bit;
            }

            return price;
        }

        public static void ReverseEncode(BitEncoder[] Models, UInt32 startIndex,
            RangeCoderEncoder rangeEncoder, int NumBitLevels, UInt32 symbol)
        {
            UInt32 m = 1;
            for (var i = 0; i < NumBitLevels; i++)
            {
                var bit = symbol & 1;
                Models[startIndex + m].Encode(rangeEncoder, bit);
                m = (m << 1) | bit;
                symbol >>= 1;
            }
        }
    }

    internal readonly struct BitTreeDecoder
    {
        private readonly BitDecoder[] Models;
        private readonly int NumBitLevels;

        public BitTreeDecoder(int numBitLevels)
        {
            NumBitLevels = numBitLevels;
            Models = new BitDecoder[1 << numBitLevels];
        }

        public void Init()
        {
            for (uint i = 1; i < (1 << NumBitLevels); i++)
                Models[i].Init();
        }

        public uint Decode(RangeCoderDecoder rangeDecoder)
        {
            uint m = 1;
            for (var bitIndex = NumBitLevels; bitIndex > 0; bitIndex--)
                m = (m << 1) + Models[m].Decode(rangeDecoder);
            return m - ((uint)1 << NumBitLevels);
        }

        public uint ReverseDecode(RangeCoderDecoder rangeDecoder)
        {
            uint m = 1;
            uint symbol = 0;
            for (var bitIndex = 0; bitIndex < NumBitLevels; bitIndex++)
            {
                var bit = Models[m].Decode(rangeDecoder);
                m <<= 1;
                m += bit;
                symbol |= (bit << bitIndex);
            }

            return symbol;
        }

        public static uint ReverseDecode(BitDecoder[] Models, UInt32 startIndex,
            RangeCoderDecoder rangeDecoder, int NumBitLevels)
        {
            uint m = 1;
            uint symbol = 0;
            for (var bitIndex = 0; bitIndex < NumBitLevels; bitIndex++)
            {
                var bit = Models[startIndex + m].Decode(rangeDecoder);
                m <<= 1;
                m += bit;
                symbol |= (bit << bitIndex);
            }

            return symbol;
        }
    }

    internal class CRC
    {
        public static readonly uint[] Table;

        static CRC()
        {
            Table = new uint[256];
            const uint kPoly = 0xEDB88320;
            for (uint i = 0; i < 256; i++)
            {
                var r = i;
                for (var j = 0; j < 8; j++)
                    if ((r & 1) != 0)
                        r = (r >> 1) ^ kPoly;
                    else
                        r >>= 1;
                Table[i] = r;
            }
        }
    }

    public class InWindow
    {
        protected Byte[]? _bufferBase; // pointer to buffer with data
        private Stream? _stream;
        private UInt32 _posLimit; // offset (from _buffer) of first byte when new block reading must be done
        private bool _streamEndWasReached; // if (true) then _streamPos shows real end of stream

        private UInt32 _pointerToLastSafePosition;

        protected UInt32 _bufferOffset;

        public UInt32 _blockSize; // Size of Allocated memory block
        protected UInt32 _pos; // offset (from _buffer) of current byte
        private UInt32 _keepSizeBefore; // how many BYTEs must be kept in buffer before _pos
        private UInt32 _keepSizeAfter; // how many BYTEs must be kept buffer after _pos
        protected UInt32 _streamPos; // offset (from _buffer) of first not read byte from Stream

        public void MoveBlock()
        {
            if (_bufferBase is null) throw new Exception();
            
            var offset = _bufferOffset + _pos - _keepSizeBefore;
            // we need one additional byte, since MovePos moves on 1 byte.
            if (offset > 0)
                offset--;

            var numBytes = _bufferOffset + _streamPos - offset;

            // check negative offset ????
            for (UInt32 i = 0; i < numBytes; i++)
                _bufferBase[i] = _bufferBase[offset + i];
            _bufferOffset -= offset;
        }

        public void ReadBlock()
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

                _streamPos += (UInt32)numReadBytes;
                if (_streamPos >= _pos + _keepSizeAfter)
                    _posLimit = _streamPos - _keepSizeAfter;
            }
        }

        private void Free()
        {
            _bufferBase = null;
        }

        protected void Create(UInt32 keepSizeBefore, UInt32 keepSizeAfter, UInt32 keepSizeReserve)
        {
            _keepSizeBefore = keepSizeBefore;
            _keepSizeAfter = keepSizeAfter;
            var blockSize = keepSizeBefore + keepSizeAfter + keepSizeReserve;
            if (_bufferBase == null || _blockSize != blockSize)
            {
                Free();
                _blockSize = blockSize;
                _bufferBase = new Byte[_blockSize];
            }

            _pointerToLastSafePosition = _blockSize - keepSizeAfter;
        }

        protected void SetStream(Stream stream)
        {
            _stream = stream;
        }

        protected void ReleaseStream()
        {
            _stream = null;
        }

        protected void Init()
        {
            _bufferOffset = 0;
            _pos = 0;
            _streamPos = 0;
            _streamEndWasReached = false;
            ReadBlock();
        }

        protected void MovePos()
        {
            _pos++;
            if (_pos <= _posLimit) return;
            
            var pointerToPosition = _bufferOffset + _pos;
            if (pointerToPosition > _pointerToLastSafePosition) MoveBlock();
                
            ReadBlock();
        }

        protected Byte GetIndexByte(Int32 index)
        {
            if (_bufferBase is null) throw new Exception();
            return _bufferBase[_bufferOffset + _pos + index];
        }

        // index + limit have not to exceed _keepSizeAfter;
        protected UInt32 GetMatchLen(Int32 index, UInt32 distance, UInt32 limit)
        {
            if (_bufferBase is null) throw new Exception();
            if (_streamEndWasReached && (_pos + index) + limit > _streamPos)
            {
                limit = _streamPos - (UInt32)(_pos + index);
            }

            distance++;
            var pby = _bufferOffset + _pos + (UInt32)index;

            UInt32 i = 0;
            while (i < limit && _bufferBase[pby + i] == _bufferBase[pby + i - distance])
            {
                i++;
            }

            return i;
        }

        protected UInt32 GetNumAvailableBytes()
        {
            return _streamPos - _pos;
        }

        protected void ReduceOffsets(Int32 subValue)
        {
            _bufferOffset += (UInt32)subValue;
            _posLimit -= (UInt32)subValue;
            _pos -= (UInt32)subValue;
            _streamPos -= (UInt32)subValue;
        }
    }

    public class BinTree : InWindow
    {
        private UInt32 _cyclicBufferPos;
        private UInt32 _cyclicBufferSize;
        private UInt32 _matchMaxLen;

        private UInt32[]? _son;
        private UInt32[]? _hash;

        private UInt32 _cutValue = 0xFF;
        private UInt32 _hashMask;
        private UInt32 _hashSizeSum;

        private bool HASH_ARRAY = true;

        private const UInt32 kHash2Size = 1 << 10;
        private const UInt32 kHash3Size = 1 << 16;
        private const UInt32 kBT2HashSize = 1 << 16;
        private const UInt32 kStartMaxLen = 1;
        private const UInt32 kHash3Offset = kHash2Size;
        private const UInt32 kEmptyHashValue = 0;
        private const UInt32 kMaxValForNormalize = ((UInt32)1 << 31) - 1;

        private UInt32 kNumHashDirectBytes;
        private UInt32 kMinMatchCheck = 4;
        private UInt32 kFixHashSize = kHash2Size + kHash3Size;

        public void SetType(int numHashBytes)
        {
            HASH_ARRAY = (numHashBytes > 2);
            if (HASH_ARRAY)
            {
                kNumHashDirectBytes = 0;
                kMinMatchCheck = 4;
                kFixHashSize = kHash2Size + kHash3Size;
            }
            else
            {
                kNumHashDirectBytes = 2;
                kMinMatchCheck = 2 + 1;
                kFixHashSize = 0;
            }
        }

        public new void SetStream(Stream stream)
        {
            base.SetStream(stream);
        }

        public new void ReleaseStream()
        {
            base.ReleaseStream();
        }

        public new void Init()
        {
            base.Init();
            for (UInt32 i = 0; i < _hashSizeSum; i++) _hash![i] = kEmptyHashValue;
            _cyclicBufferPos = 0;
            ReduceOffsets(-1);
        }

        public new void MovePos()
        {
            if (++_cyclicBufferPos >= _cyclicBufferSize) _cyclicBufferPos = 0;
            base.MovePos();
            
            if (_pos == kMaxValForNormalize) Normalize();
        }

        public new Byte GetIndexByte(Int32 index)
        {
            return base.GetIndexByte(index);
        }

        public new UInt32 GetMatchLen(Int32 index, UInt32 distance, UInt32 limit)
        {
            return base.GetMatchLen(index, distance, limit);
        }

        public new UInt32 GetNumAvailableBytes()
        {
            return base.GetNumAvailableBytes();
        }

        public void Create(UInt32 historySize, UInt32 keepAddBufferBefore,
            UInt32 matchMaxLen, UInt32 keepAddBufferAfter)
        {
            if (historySize > kMaxValForNormalize - 256)
                throw new Exception();
            _cutValue = 16 + (matchMaxLen >> 1);

            var windowReserveSize = (historySize + keepAddBufferBefore +
                                    matchMaxLen + keepAddBufferAfter) / 2 + 256;

            base.Create(historySize + keepAddBufferBefore, matchMaxLen + keepAddBufferAfter, windowReserveSize);

            _matchMaxLen = matchMaxLen;

            var cyclicBufferSize = historySize + 1;
            if (_cyclicBufferSize != cyclicBufferSize)
                _son = new UInt32[(_cyclicBufferSize = cyclicBufferSize) * 2];

            var hs = kBT2HashSize;

            if (HASH_ARRAY)
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
                hs += kFixHashSize;
            }

            if (hs != _hashSizeSum)
                _hash = new UInt32[_hashSizeSum = hs];
        }

        public UInt32 GetMatches(UInt32[] distances)
        {
            UInt32 lenLimit;
            if (_pos + _matchMaxLen <= _streamPos)
                lenLimit = _matchMaxLen;
            else
            {
                lenLimit = _streamPos - _pos;
                if (lenLimit < kMinMatchCheck)
                {
                    MovePos();
                    return 0;
                }
            }

            UInt32 offset = 0;
            var matchMinPos = (_pos > _cyclicBufferSize) ? (_pos - _cyclicBufferSize) : 0;
            var cur = _bufferOffset + _pos;
            var maxLen = kStartMaxLen; // to avoid items for len < hashSize;
            UInt32 hashValue, hash2Value = 0, hash3Value = 0;
            
            if (_bufferBase is null || _hash is null) throw new Exception();

            if (HASH_ARRAY)
            {
                var temp = CRC.Table[_bufferBase[cur]] ^ _bufferBase[cur + 1];
                hash2Value = temp & (kHash2Size - 1);
                temp ^= ((UInt32)(_bufferBase[cur + 2]) << 8);
                hash3Value = temp & (kHash3Size - 1);
                hashValue = (temp ^ (CRC.Table[_bufferBase[cur + 3]] << 5)) & _hashMask;
            }
            else
                hashValue = _bufferBase[cur] ^ ((UInt32)(_bufferBase[cur + 1]) << 8);

            var curMatch = _hash[kFixHashSize + hashValue];
            if (HASH_ARRAY)
            {
                var curMatch2 = _hash[hash2Value];
                var curMatch3 = _hash[kHash3Offset + hash3Value];
                _hash[hash2Value] = _pos;
                _hash[kHash3Offset + hash3Value] = _pos;
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
                    maxLen = kStartMaxLen;
                }
            }

            _hash[kFixHashSize + hashValue] = _pos;

            var ptr0 = (_cyclicBufferPos << 1) + 1;
            var ptr1 = (_cyclicBufferPos << 1);

            UInt32 len1;
            var len0 = len1 = kNumHashDirectBytes;

            if (kNumHashDirectBytes != 0)
            {
                if (curMatch > matchMinPos)
                {
                    if (_bufferBase[_bufferOffset + curMatch + kNumHashDirectBytes] !=
                        _bufferBase[cur + kNumHashDirectBytes])
                    {
                        distances[offset++] = maxLen = kNumHashDirectBytes;
                        distances[offset++] = _pos - curMatch - 1;
                    }
                }
            }

            var count = _cutValue;

            while (true)
            {
                if (curMatch <= matchMinPos || count-- == 0)
                {
                    _son![ptr0] = _son[ptr1] = kEmptyHashValue;
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

        public void Skip(UInt32 num)
        {
            do
            {
                UInt32 lenLimit;
                if (_pos + _matchMaxLen <= _streamPos)
                    lenLimit = _matchMaxLen;
                else
                {
                    lenLimit = _streamPos - _pos;
                    if (lenLimit < kMinMatchCheck)
                    {
                        MovePos();
                        continue;
                    }
                }

                var matchMinPos = (_pos > _cyclicBufferSize) ? (_pos - _cyclicBufferSize) : 0;
                var cur = _bufferOffset + _pos;

                UInt32 hashValue;

                if (HASH_ARRAY)
                {
                    var temp = CRC.Table[_bufferBase![cur]] ^ _bufferBase[cur + 1];
                    var hash2Value = temp & (kHash2Size - 1);
                    _hash![hash2Value] = _pos;
                    temp ^= ((UInt32)(_bufferBase[cur + 2]) << 8);
                    var hash3Value = temp & (kHash3Size - 1);
                    _hash[kHash3Offset + hash3Value] = _pos;
                    hashValue = (temp ^ (CRC.Table[_bufferBase[cur + 3]] << 5)) & _hashMask;
                }
                else
                    hashValue = _bufferBase![cur] ^ ((UInt32)(_bufferBase[cur + 1]) << 8);

                var curMatch = _hash![kFixHashSize + hashValue];
                _hash[kFixHashSize + hashValue] = _pos;

                var ptr0 = (_cyclicBufferPos << 1) + 1;
                var ptr1 = (_cyclicBufferPos << 1);

                UInt32 len1;
                var len0 = len1 = kNumHashDirectBytes;

                var count = _cutValue;
                while (true)
                {
                    if (curMatch <= matchMinPos || count-- == 0)
                    {
                        _son![ptr0] = _son[ptr1] = kEmptyHashValue;
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

        private void NormalizeLinks(UInt32[] items, UInt32 numItems, UInt32 subValue)
        {
            for (UInt32 i = 0; i < numItems; i++)
            {
                var value = items[i];
                if (value <= subValue)
                    value = kEmptyHashValue;
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
            ReduceOffsets((Int32)subValue);
        }
    }

    public class OutWindow
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