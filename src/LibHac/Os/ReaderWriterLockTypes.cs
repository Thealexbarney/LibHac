using System.Runtime.CompilerServices;
using LibHac.Os.Impl;

namespace LibHac.Os
{
    public struct ReaderWriterLockType
    {
        internal LockCountType LockCount;
        internal State LockState;
        internal int OwnerThread;
        internal InternalConditionVariable CvReadLockWaiter;
        internal InternalConditionVariable CvWriteLockWaiter;

        public enum State
        {
            NotInitialized,
            Initialized
        }

        public struct LockCountType
        {
            public InternalCriticalSection Cs;
            public ReaderWriterLockCounter Counter;
            public uint WriteLockCount;
        }

        public struct ReaderWriterLockCounter
        {
            private uint _counter;

            public uint ReadLockCount
            {
                readonly get => GetBitsValue(_counter, 0, 15);
                set => _counter = SetBitsValue(value, 0, 15);
            }

            public uint WriteLocked
            {
                readonly get => GetBitsValue(_counter, 15, 1);
                set => _counter = SetBitsValue(value, 15, 1);
            }

            public uint ReadLockWaiterCount
            {
                readonly get => GetBitsValue(_counter, 16, 8);
                set => _counter = SetBitsValue(value, 16, 8);
            }

            public uint WriteLockWaiterCount
            {
                readonly get => GetBitsValue(_counter, 24, 8);
                set => _counter = SetBitsValue(value, 24, 8);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint GetBitsValue(uint value, int bitsOffset, int bitsCount) =>
                (value >> bitsOffset) & ~(~default(uint) << bitsCount);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint SetBitsValue(uint value, int bitsOffset, int bitsCount) =>
                (value & ~(~default(uint) << bitsCount)) << bitsOffset;
        }
    }
}
