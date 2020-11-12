using System;
using System.Threading;

namespace LibHac.Util
{
    /// <summary>
    /// Represents a lock that may be passed between functions or objects.
    /// </summary>
    /// <remarks>This struct must never be copied. It must always be passed by
    /// reference or moved via the move constructor.</remarks>
    public struct UniqueLock : IDisposable
    {
        private object _lockObject;
        private bool _isLocked;

        /// <summary>
        /// Creates a new <see cref="UniqueLock"/> from the provided object and acquires the lock.
        /// </summary>
        /// <param name="lockObject">The object to lock.</param>
        public UniqueLock(object lockObject)
        {
            _lockObject = lockObject;
            _isLocked = false;

            Lock();
        }

        public UniqueLock(ref UniqueLock other)
        {
            _lockObject = other._lockObject;
            _isLocked = other._isLocked;

            other._isLocked = false;
            other._lockObject = null;
        }

        public bool IsLocked => _isLocked;

        public void Lock()
        {
            if (_isLocked)
            {
                throw new SynchronizationLockException("Attempted to lock a UniqueLock that was already locked.");
            }

            Monitor.Enter(_lockObject, ref _isLocked);
        }

        public void Unlock()
        {
            if (!_isLocked)
            {
                throw new SynchronizationLockException("Attempted to unlock a UniqueLock that was not locked.");
            }

            Monitor.Exit(_lockObject);
            _isLocked = false;
        }

        public void Dispose()
        {
            if (_isLocked)
            {
                Monitor.Exit(_lockObject);

                _isLocked = false;
                _lockObject = null;
            }
        }
    }
}
