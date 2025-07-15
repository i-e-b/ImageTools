using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ImageTools.Utilities
{
    public unsafe class UnsafeUIntArray
    {
        public int Length { get; init; }

        private readonly uint[] _data;
        private readonly uint*  _dataPtr;

        public UnsafeUIntArray(int length)
        {
            Length = length;
            _data = GC.AllocateArray<uint>(length, pinned: true);
            ref uint dataRef = ref MemoryMarshal.GetArrayDataReference(_data);
            _dataPtr = (uint*)Unsafe.AsPointer<uint>(ref dataRef);
        }

        public uint this[int key]
        {
            get => Get(key);
            set => Set(key, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe uint Get(int index)
        {
            var ptr = (_dataPtr + (uint)index);
            return *ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Set(int index, uint value)
        {
            var ptr = (_dataPtr + (uint)index);
            *ptr = value;
        }

    }

    public unsafe class UnsafeDoubleArray
    {
        public int Length { get; init; }
        public double* Buffer => _dataPtr;

        private readonly double[] _data;
        private readonly double*  _dataPtr;

        public UnsafeDoubleArray(int length)
        {
            Length = length;
            _data = GC.AllocateArray<double>(length, pinned: true);
            ref double dataRef = ref MemoryMarshal.GetArrayDataReference(_data);
            _dataPtr = (double*)Unsafe.AsPointer(ref dataRef);
        }

        public double this[int key]
        {
            get => Get(key);
            set => Set(key, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Get(int index)
        {
            var ptr = (_dataPtr + (uint)index);
            return *ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, double value)
        {
            var ptr = (_dataPtr + (uint)index);
            *ptr = value;
        }

    }
}