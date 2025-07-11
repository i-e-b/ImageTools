namespace ImageTools.Utilities
{
    /*
    public unsafe class UnsafeUIntArray
    {
        public int Length { get; private set; }

        private readonly uint[] _data;
        private readonly uint*  _dataPtr;

        public UnsafeUIntArray(int length)
        {
            Length = length;
            //_data = GC.AllocateArray<uint>(length, pinned: true);
            //ref uint dataRef = ref MemoryMarshal.GetArrayDataReference(_data);
            //_dataPtr = (uint*)Unsafe.AsPointer<uint>(ref dataRef);
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

    }*/
}