using System;
using System.Threading;
using LibHac.Common;
using LibHac.Diag;

namespace LibHac.FsSystem
{
    public interface IUniqueLock : IDisposable
    {
    }

    /// <summary>
    /// Represents a lock that may be passed between functions or objects.
    /// </summary>
    /// <remarks>This struct must never be copied. It must always be passed by
    /// reference or moved via the move constructor.</remarks>
    public struct UniqueLockSemaphore : IDisposable
    {
        private SemaphoreAdaptor _semaphore;
        private bool _isLocked;

        public UniqueLockSemaphore(SemaphoreAdaptor semaphore)
        {
            _semaphore = semaphore;
            _isLocked = false;
        }

        public UniqueLockSemaphore(ref UniqueLockSemaphore other)
        {
            _semaphore = other._semaphore;
            _isLocked = other._isLocked;

            other._isLocked = false;
            other._semaphore = null;
        }

        public bool IsLocked => _isLocked;

        public bool TryLock()
        {
            if (_isLocked)
            {
                throw new SynchronizationLockException("Attempted to lock a UniqueLock that was already locked.");
            }

            _isLocked = _semaphore.TryLock();
            return _isLocked;
        }

        public void Unlock()
        {
            if (!_isLocked)
            {
                throw new SynchronizationLockException("Attempted to unlock a UniqueLock that was not locked.");
            }

            _semaphore.Unlock();
            _isLocked = false;
        }

        public void Dispose()
        {
            if (_isLocked)
            {
                _semaphore.Unlock();

                _isLocked = false;
                _semaphore = null;
            }
        }
    }

    public class UniqueLockWithPin<T> : IUniqueLock where T : class, IDisposable
    {
        private UniqueLockSemaphore _semaphore;
        private ReferenceCountedDisposable<T> _pinnedObject;

        public UniqueLockWithPin(ref UniqueLockSemaphore semaphore, ref ReferenceCountedDisposable<T> pinnedObject)
        {
            Shared.Move(out _semaphore, ref semaphore);
            Shared.Move(out _pinnedObject, ref pinnedObject);

            Assert.SdkAssert(_semaphore.IsLocked);
        }

        public void Dispose()
        {
            if (_pinnedObject != null)
            {
                _semaphore.Dispose();
                _pinnedObject.Dispose();

                _pinnedObject = null;
            }
        }
    }
}
