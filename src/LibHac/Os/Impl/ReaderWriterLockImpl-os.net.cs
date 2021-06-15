using System;
using LibHac.Diag;

namespace LibHac.Os.Impl
{
    internal static partial class ReaderWriterLockImpl
    {
        public static void AcquireReadLockImpl(this OsStateImpl os, ref ReaderWriterLockType rwLock)
        {
            ref InternalCriticalSection cs = ref GetLockCount(ref rwLock).Cs;
            using ScopedLock<InternalCriticalSection> lk = ScopedLock.Lock(ref cs);

            // If we already own the lock, no additional action is needed
            if (rwLock.OwnerThread == Environment.CurrentManagedThreadId)
            {
                Assert.SdkEqual(GetWriteLocked(in GetLockCount(ref rwLock)), 1u);
            }
            // Otherwise we might need to block until we can acquire the read lock
            else
            {
                // Wait until there aren't any writers or waiting writers
                while (GetWriteLocked(in GetLockCount(ref rwLock)) == 1 ||
                       GetWriteLockWaiterCount(in GetLockCount(ref rwLock)) != 0)
                {
                    IncReadLockWaiterCount(ref GetLockCount(ref rwLock));
                    rwLock.CvReadLockWaiter.Wait(ref cs);
                    DecReadLockWaiterCount(ref GetLockCount(ref rwLock));
                }

                Assert.SdkEqual(GetWriteLockCount(in rwLock), 0u);
                Assert.SdkEqual(rwLock.OwnerThread, 0);
            }

            IncReadLockCount(ref GetLockCount(ref rwLock));
        }

        public static bool TryAcquireReadLockImpl(this OsStateImpl os, ref ReaderWriterLockType rwLock)
        {
            using ScopedLock<InternalCriticalSection> lk = ScopedLock.Lock(ref GetLockCount(ref rwLock).Cs);

            // Acquire the lock if we already have write access
            if (rwLock.OwnerThread == Environment.CurrentManagedThreadId)
            {
                Assert.SdkEqual(GetWriteLocked(in GetLockCount(ref rwLock)), 1u);

                IncReadLockCount(ref GetLockCount(ref rwLock));
                return true;
            }

            // Fail to acquire if there are any writers or waiting writers
            if (GetWriteLocked(in GetLockCount(ref rwLock)) == 1 ||
                GetWriteLockWaiterCount(in GetLockCount(ref rwLock)) != 0)
            {
                return false;
            }

            // Otherwise acquire the lock
            Assert.SdkEqual(GetWriteLockCount(in rwLock), 0u);
            Assert.SdkEqual(rwLock.OwnerThread, 0);

            IncReadLockCount(ref GetLockCount(ref rwLock));
            return true;
        }

        public static void ReleaseReadLockImpl(this OsStateImpl os, ref ReaderWriterLockType rwLock)
        {
            using ScopedLock<InternalCriticalSection> lk = ScopedLock.Lock(ref GetLockCount(ref rwLock).Cs);

            Assert.SdkLess(0u, GetReadLockCount(in GetLockCount(ref rwLock)));
            DecReadLockCount(ref GetLockCount(ref rwLock));

            // If we own the lock, check if we need to release ownership and signal any waiting threads
            if (rwLock.OwnerThread == Environment.CurrentManagedThreadId)
            {
                Assert.SdkEqual(GetWriteLocked(in GetLockCount(ref rwLock)), 1u);

                // Return if we still hold any locks
                if (GetWriteLockCount(in rwLock) != 0 || GetReadLockCount(in GetLockCount(ref rwLock)) != 0)
                {
                    return;
                }

                // We don't hold any more locks. Release our ownership of the lock
                rwLock.OwnerThread = 0;
                ClearWriteLocked(ref GetLockCount(ref rwLock));

                // Signal the next writer if any are waiting
                if (GetWriteLockWaiterCount(in GetLockCount(ref rwLock)) != 0)
                {
                    rwLock.CvWriteLockWaiter.Signal();
                }
                // Otherwise signal any waiting readers
                else if (GetReadLockWaiterCount(in GetLockCount(ref rwLock)) != 0)
                {
                    rwLock.CvReadLockWaiter.Broadcast();
                }
            }
            // Otherwise we need to signal the next writer if we were the only reader
            else
            {
                Assert.SdkEqual(GetWriteLockCount(in rwLock), 0u);
                Assert.SdkEqual(GetWriteLocked(in GetLockCount(ref rwLock)), 0u);
                Assert.SdkEqual(rwLock.OwnerThread, 0);

                // Signal the next writer if no readers are left
                if (GetReadLockCount(in GetLockCount(ref rwLock)) == 0 &&
                    GetWriteLockWaiterCount(in GetLockCount(ref rwLock)) != 0)
                {
                    rwLock.CvWriteLockWaiter.Signal();
                }
            }
        }

        public static void AcquireWriteLockImpl(this OsStateImpl os, ref ReaderWriterLockType rwLock)
        {
            ref InternalCriticalSection cs = ref GetLockCount(ref rwLock).Cs;
            using ScopedLock<InternalCriticalSection> lk = ScopedLock.Lock(ref cs);

            int currentThread = Environment.CurrentManagedThreadId;

            // Increase the write lock count if we already own the lock
            if (rwLock.OwnerThread == currentThread)
            {
                Assert.SdkEqual(GetWriteLocked(in GetLockCount(ref rwLock)), 1u);

                IncWriteLockCount(ref rwLock);
                return;
            }

            // Otherwise wait until there aren't any readers or writers
            while (GetReadLockCount(in GetLockCount(ref rwLock)) != 0 ||
                   GetWriteLocked(in GetLockCount(ref rwLock)) == 1)
            {
                IncWriteLockWaiterCount(ref GetLockCount(ref rwLock));
                rwLock.CvWriteLockWaiter.Wait(ref cs);
                DecWriteLockWaiterCount(ref GetLockCount(ref rwLock));
            }

            Assert.SdkEqual(GetWriteLockCount(in rwLock), 0u);
            Assert.SdkEqual(rwLock.OwnerThread, 0);

            // Acquire the lock
            IncWriteLockCount(ref rwLock);
            SetWriteLocked(ref GetLockCount(ref rwLock));
            rwLock.OwnerThread = currentThread;
        }

        public static bool TryAcquireWriteLockImpl(this OsStateImpl os, ref ReaderWriterLockType rwLock)
        {
            using ScopedLock<InternalCriticalSection> lk = ScopedLock.Lock(ref GetLockCount(ref rwLock).Cs);

            int currentThread = Environment.CurrentManagedThreadId;

            // Acquire the lock if we already have write access
            if (rwLock.OwnerThread == currentThread)
            {
                Assert.SdkEqual(GetWriteLocked(in GetLockCount(ref rwLock)), 1u);

                IncWriteLockCount(ref rwLock);
                return true;
            }

            // Fail to acquire if there are any readers or writers
            if (GetReadLockCount(in GetLockCount(ref rwLock)) != 0 ||
                GetWriteLocked(in GetLockCount(ref rwLock)) == 1)
            {
                return false;
            }

            // Otherwise acquire the lock
            Assert.SdkEqual(GetWriteLockCount(in rwLock), 0u);
            Assert.SdkEqual(rwLock.OwnerThread, 0);

            IncWriteLockCount(ref rwLock);
            SetWriteLocked(ref GetLockCount(ref rwLock));
            rwLock.OwnerThread = currentThread;
            return true;
        }

        public static void ReleaseWriteLockImpl(this OsStateImpl os, ref ReaderWriterLockType rwLock)
        {
            using ScopedLock<InternalCriticalSection> lk = ScopedLock.Lock(ref GetLockCount(ref rwLock).Cs);

            Assert.SdkRequiresGreater(GetWriteLockCount(in rwLock), 0u);
            Assert.SdkNotEqual(GetWriteLocked(in GetLockCount(ref rwLock)), 0u);
            Assert.SdkEqual(rwLock.OwnerThread, Environment.CurrentManagedThreadId);

            DecWriteLockCount(ref rwLock);

            // Return if we still hold any locks
            if (GetWriteLockCount(in rwLock) != 0 || GetReadLockCount(in GetLockCountRo(in rwLock)) != 0)
            {
                return;
            }

            // We don't hold any more locks. Release our ownership of the lock
            rwLock.OwnerThread = 0;
            ClearWriteLocked(ref GetLockCount(ref rwLock));

            // Signal the next writer if any are waiting
            if (GetWriteLockWaiterCount(in GetLockCount(ref rwLock)) != 0)
            {
                rwLock.CvWriteLockWaiter.Signal();
            }
            // Otherwise signal any waiting readers
            else if (GetReadLockWaiterCount(in GetLockCount(ref rwLock)) != 0)
            {
                rwLock.CvReadLockWaiter.Broadcast();
            }
        }
    }
}
