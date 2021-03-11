using LibHac.Diag;

namespace LibHac.Os.Impl
{
    internal static partial class ReaderWriterLockImpl
    {
        public static void ClearReadLockCount(ref ReaderWriterLockType.LockCountType lc)
        {
            lc.Counter.ReadLockCount = 0;
        }

        public static void ClearWriteLocked(ref ReaderWriterLockType.LockCountType lc)
        {
            lc.Counter.WriteLocked = 0;
        }

        public static void ClearReadLockWaiterCount(ref ReaderWriterLockType.LockCountType lc)
        {
            lc.Counter.ReadLockWaiterCount = 0;
        }

        public static void ClearWriteLockWaiterCount(ref ReaderWriterLockType.LockCountType lc)
        {
            lc.Counter.WriteLockWaiterCount = 0;
        }

        public static void ClearWriteLockCount(ref ReaderWriterLockType rwLock)
        {
            rwLock.LockCount.WriteLockCount = 0;
        }

        public static ref ReaderWriterLockType.LockCountType GetLockCount(ref ReaderWriterLockType rwLock)
        {
            return ref rwLock.LockCount;
        }

        public static ref readonly ReaderWriterLockType.LockCountType GetLockCountRo(in ReaderWriterLockType rwLock)
        {
            return ref rwLock.LockCount;
        }

        public static uint GetReadLockCount(in ReaderWriterLockType.LockCountType lc)
        {
            return lc.Counter.ReadLockCount;
        }

        public static uint GetWriteLocked(in ReaderWriterLockType.LockCountType lc)
        {
            return lc.Counter.WriteLocked;
        }

        public static uint GetReadLockWaiterCount(in ReaderWriterLockType.LockCountType lc)
        {
            return lc.Counter.ReadLockWaiterCount;
        }

        public static uint GetWriteLockWaiterCount(in ReaderWriterLockType.LockCountType lc)
        {
            return lc.Counter.WriteLockWaiterCount;
        }

        public static uint GetWriteLockCount(in ReaderWriterLockType rwLock)
        {
            return rwLock.LockCount.WriteLockCount;
        }

        public static void IncReadLockCount(ref ReaderWriterLockType.LockCountType lc)
        {
            uint readLockCount = lc.Counter.ReadLockCount;
            Assert.True(readLockCount < ReaderWriterLock.ReaderWriterLockCountMax);
            lc.Counter.ReadLockCount = readLockCount + 1;
        }

        public static void DecReadLockCount(ref ReaderWriterLockType.LockCountType lc)
        {
            uint readLockCount = lc.Counter.ReadLockCount;
            Assert.True(readLockCount > 0);
            lc.Counter.ReadLockCount = readLockCount - 1;
        }

        public static void IncReadLockWaiterCount(ref ReaderWriterLockType.LockCountType lc)
        {
            uint readLockWaiterCount = lc.Counter.ReadLockWaiterCount;
            Assert.True(readLockWaiterCount < ReaderWriterLock.ReadWriteLockWaiterCountMax);
            lc.Counter.ReadLockWaiterCount = readLockWaiterCount + 1;
        }

        public static void DecReadLockWaiterCount(ref ReaderWriterLockType.LockCountType lc)
        {
            uint readLockWaiterCount = lc.Counter.ReadLockWaiterCount;
            Assert.True(readLockWaiterCount > 0);
            lc.Counter.ReadLockWaiterCount = readLockWaiterCount - 1;
        }

        public static void IncWriteLockWaiterCount(ref ReaderWriterLockType.LockCountType lc)
        {
            uint writeLockWaiterCount = lc.Counter.WriteLockWaiterCount;
            Assert.True(writeLockWaiterCount < ReaderWriterLock.ReadWriteLockWaiterCountMax);
            lc.Counter.WriteLockWaiterCount = writeLockWaiterCount + 1;
        }

        public static void DecWriteLockWaiterCount(ref ReaderWriterLockType.LockCountType lc)
        {
            uint writeLockWaiterCount = lc.Counter.WriteLockWaiterCount;
            Assert.True(writeLockWaiterCount > 0);
            lc.Counter.WriteLockWaiterCount = writeLockWaiterCount - 1;
        }

        public static void IncWriteLockCount(ref ReaderWriterLockType rwLock)
        {
            uint writeLockCount = rwLock.LockCount.WriteLockCount;
            Assert.True(writeLockCount < ReaderWriterLock.ReaderWriterLockCountMax);
            rwLock.LockCount.WriteLockCount = writeLockCount + 1;
        }

        public static void DecWriteLockCount(ref ReaderWriterLockType rwLock)
        {
            uint writeLockCount = rwLock.LockCount.WriteLockCount;
            Assert.True(writeLockCount > 0);
            rwLock.LockCount.WriteLockCount = writeLockCount - 1;
        }

        public static void SetWriteLocked(ref ReaderWriterLockType.LockCountType lc)
        {
            lc.Counter.WriteLocked = 1;
        }
    }
}
