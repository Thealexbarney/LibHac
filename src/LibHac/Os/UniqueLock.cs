using System.Runtime.CompilerServices;
using System.Threading;
using LibHac.Common;

namespace LibHac.Os
{
    public static class UniqueLock
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UniqueLock<TMutex> Lock<TMutex>(ref TMutex lockable) where TMutex : ILockable
        {
            return new UniqueLock<TMutex>(ref lockable);
        }
    }

    public ref struct UniqueLock<TMutex> where TMutex : ILockable
    {
        private Ref<TMutex> _mutex;
        private bool _ownsLock;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UniqueLock(ref TMutex mutex)
        {
            _mutex = new Ref<TMutex>(ref mutex);
            mutex.Lock();
            _ownsLock = true;
        }

        public UniqueLock(ref UniqueLock<TMutex> other)
        {
            this = other;
            other = default;
        }

        public void Set(ref UniqueLock<TMutex> other)
        {
            if (_ownsLock)
                _mutex.Value.Unlock();

            this = other;
            other = default;
        }

        public void Lock()
        {
            if (_mutex.IsNull)
                throw new SynchronizationLockException("UniqueLock.Lock: References null mutex");

            if (_ownsLock)
                throw new SynchronizationLockException("UniqueLock.Lock: Already locked");

            _mutex.Value.Lock();
            _ownsLock = true;
        }

        public bool TryLock()
        {
            if (_mutex.IsNull)
                throw new SynchronizationLockException("UniqueLock.TryLock: References null mutex");

            if (_ownsLock)
                throw new SynchronizationLockException("UniqueLock.TryLock: Already locked");

            _ownsLock = _mutex.Value.TryLock();
            return _ownsLock;
        }

        public void Unlock()
        {
            if (_ownsLock)
                throw new SynchronizationLockException("UniqueLock.Unlock: Not locked");

            _mutex.Value.Unlock();
            _ownsLock = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_ownsLock)
                _mutex.Value.Unlock();

            this = default;
        }
    }
}