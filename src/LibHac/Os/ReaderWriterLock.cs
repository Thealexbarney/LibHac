using System;
using LibHac.Diag;
using LibHac.Os.Impl;

namespace LibHac.Os
{
    public static class ReaderWriterLockApi
    {
        public static void InitializeReaderWriterLock(this OsState os, ref ReaderWriterLockType rwLock)
        {
            // Create objects.
            ReaderWriterLockImpl.GetLockCount(ref rwLock).Cs.Initialize();
            rwLock.CvReadLockWaiter.Initialize();
            rwLock.CvWriteLockWaiter.Initialize();

            // Set member variables.
            ReaderWriterLockImpl.ClearReadLockCount(ref ReaderWriterLockImpl.GetLockCount(ref rwLock));
            ReaderWriterLockImpl.ClearWriteLocked(ref ReaderWriterLockImpl.GetLockCount(ref rwLock));
            ReaderWriterLockImpl.ClearReadLockWaiterCount(ref ReaderWriterLockImpl.GetLockCount(ref rwLock));
            ReaderWriterLockImpl.ClearWriteLockWaiterCount(ref ReaderWriterLockImpl.GetLockCount(ref rwLock));
            ReaderWriterLockImpl.ClearWriteLockCount(ref rwLock);
            rwLock.OwnerThread = 0;

            // Mark initialized.
            rwLock.LockState = ReaderWriterLockType.State.Initialized;
        }

        public static void FinalizeReaderWriterLock(this OsState os, ref ReaderWriterLockType rwLock)
        {
            Assert.SdkRequires(rwLock.LockState == ReaderWriterLockType.State.Initialized);

            // Don't allow finalizing a locked lock.
            Assert.SdkRequires(ReaderWriterLockImpl.GetReadLockCount(in ReaderWriterLockImpl.GetLockCount(ref rwLock)) == 0);
            Assert.SdkRequires(ReaderWriterLockImpl.GetWriteLocked(in ReaderWriterLockImpl.GetLockCount(ref rwLock)) == 0);

            // Mark not initialized.
            rwLock.LockState = ReaderWriterLockType.State.NotInitialized;

            // Destroy objects.
            ReaderWriterLockImpl.GetLockCount(ref rwLock).Cs.FinalizeObject();
        }

        public static void AcquireReadLock(this OsState os, ref ReaderWriterLockType rwLock)
        {
            Assert.SdkRequires(rwLock.LockState == ReaderWriterLockType.State.Initialized);
            os.Impl.AcquireReadLockImpl(ref rwLock);
        }

        public static bool TryAcquireReadLock(this OsState os, ref ReaderWriterLockType rwLock)
        {
            Assert.SdkRequires(rwLock.LockState == ReaderWriterLockType.State.Initialized);
            return os.Impl.TryAcquireReadLockImpl(ref rwLock);
        }

        public static void ReleaseReadLock(this OsState os, ref ReaderWriterLockType rwLock)
        {
            Assert.SdkRequires(rwLock.LockState == ReaderWriterLockType.State.Initialized);
            os.Impl.ReleaseReadLockImpl(ref rwLock);
        }

        public static void AcquireWriteLock(this OsState os, ref ReaderWriterLockType rwLock)
        {
            Assert.SdkRequires(rwLock.LockState == ReaderWriterLockType.State.Initialized);
            os.Impl.AcquireWriteLockImpl(ref rwLock);
        }

        public static bool TryAcquireWriteLock(this OsState os, ref ReaderWriterLockType rwLock)
        {
            Assert.SdkRequires(rwLock.LockState == ReaderWriterLockType.State.Initialized);
            return os.Impl.TryAcquireWriteLockImpl(ref rwLock);
        }

        public static void ReleaseWriteLock(this OsState os, ref ReaderWriterLockType rwLock)
        {
            Assert.SdkRequires(rwLock.LockState == ReaderWriterLockType.State.Initialized);
            os.Impl.ReleaseWriteLockImpl(ref rwLock);
        }

        public static bool IsReadLockHeld(this OsState os, in ReaderWriterLockType rwLock)
        {
            Assert.SdkRequires(rwLock.LockState == ReaderWriterLockType.State.Initialized);
            return ReaderWriterLockImpl.GetReadLockCount(in ReaderWriterLockImpl.GetLockCountRo(in rwLock)) != 0;

        }

        // Todo: Use Horizon thread APIs
        public static bool IsWriteLockHeldByCurrentThread(this OsState os, in ReaderWriterLockType rwLock)
        {
            Assert.SdkRequires(rwLock.LockState == ReaderWriterLockType.State.Initialized);
            return rwLock.OwnerThread == Environment.CurrentManagedThreadId &&
                   ReaderWriterLockImpl.GetWriteLockCount(in rwLock) != 0;
        }

        public static bool IsReaderWriterLockOwnerThread(this OsState os, in ReaderWriterLockType rwLock)
        {
            Assert.SdkRequires(rwLock.LockState == ReaderWriterLockType.State.Initialized);
            return rwLock.OwnerThread == Environment.CurrentManagedThreadId;
        }
    }

    public class ReaderWriterLock : ISharedMutex
    {
        public const int ReaderWriterLockCountMax = (1 << 15) - 1;
        public const int ReadWriteLockWaiterCountMax = (1 << 8) - 1;

        private readonly OsState _os;
        private ReaderWriterLockType _rwLock;

        public ReaderWriterLock(OsState os)
        {
            _os = os;
            _os.InitializeReaderWriterLock(ref _rwLock);
        }

        public void AcquireReadLock()
        {
            _os.AcquireReadLock(ref _rwLock);
        }

        public bool TryAcquireReadLock()
        {
            return _os.TryAcquireReadLock(ref _rwLock);
        }

        public void ReleaseReadLock()
        {
            _os.ReleaseReadLock(ref _rwLock);
        }

        public void AcquireWriteLock()
        {
            _os.AcquireWriteLock(ref _rwLock);
        }

        public bool TryAcquireWriteLock()
        {
            return _os.TryAcquireWriteLock(ref _rwLock);
        }

        public void ReleaseWriteLock()
        {
            _os.ReleaseWriteLock(ref _rwLock);
        }

        public bool IsReadLockHeld()
        {
            return _os.IsReadLockHeld(in _rwLock);
        }

        public bool IsWriteLockHeldByCurrentThread()
        {
            return _os.IsWriteLockHeldByCurrentThread(in _rwLock);
        }

        public bool IsLockOwner()
        {
            return _os.IsReaderWriterLockOwnerThread(in _rwLock);
        }

        public void LockShared()
        {
            AcquireReadLock();
        }

        public bool TryLockShared()
        {
            return TryAcquireReadLock();
        }

        public void UnlockShared()
        {
            ReleaseReadLock();
        }

        public void Lock()
        {
            AcquireWriteLock();
        }

        public bool TryLock()
        {
            return TryAcquireWriteLock();
        }

        public void Unlock()
        {
            ReleaseWriteLock();
        }

        public ref ReaderWriterLockType GetBase()
        {
            return ref _rwLock;
        }
    }
}
