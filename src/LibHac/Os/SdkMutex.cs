using LibHac.Diag;
using LibHac.Os.Impl;

namespace LibHac.Os
{
    public class SdkMutex : ILockable
    {
        private SdkMutexType _mutex;

        public void Initialize()
        {
            _mutex.Initialize();
        }

        public void Lock()
        {
            _mutex.Lock();
        }

        public bool TryLock()
        {
            return _mutex.TryLock();
        }

        public void Unlock()
        {
            _mutex.Unlock();
        }

        public bool IsLockedByCurrentThread()
        {
            return _mutex.IsLockedByCurrentThread();
        }
    }

    public struct SdkMutexType : ILockable
    {
        private InternalCriticalSection _cs;

        public void Initialize()
        {
            _cs.Initialize();
        }

        public void Lock()
        {
            Abort.DoAbortUnless(!IsLockedByCurrentThread());
            _cs.Enter();
        }

        public bool TryLock()
        {
            Abort.DoAbortUnless(!IsLockedByCurrentThread());
            return _cs.TryEnter();
        }

        public void Unlock()
        {
            Abort.DoAbortUnless(IsLockedByCurrentThread());
            _cs.Leave();
        }

        public bool IsLockedByCurrentThread()
        {
            return _cs.IsLockedByCurrentThread();
        }
    }

    public class SdkRecursiveMutex : IBasicLockable
    {
        private SdkRecursiveMutexType _impl;

        public SdkRecursiveMutex()
        {
            _impl.Initialize();
        }

        public void Lock()
        {
            _impl.Lock();
        }

        public void Unlock()
        {
            _impl.Unlock();
        }

        public bool IsLockedByCurrentThread()
        {
            return _impl.IsLockedByCurrentThread();
        }
    }

    public struct SdkRecursiveMutexType : ILockable
    {
        private InternalCriticalSection _cs;
        private int _recursiveCount;

        public void Initialize()
        {
            _cs.Initialize();
            _recursiveCount = 0;
        }

        public void Lock()
        {
            if (!IsLockedByCurrentThread())
            {
                _cs.Enter();
            }

            _recursiveCount++;
            Abort.DoAbortUnless(_recursiveCount != 0);
        }

        public bool TryLock()
        {
            if (!IsLockedByCurrentThread())
            {
                if (!_cs.TryEnter())
                {
                    return false;
                }
            }

            _recursiveCount++;
            Abort.DoAbortUnless(_recursiveCount != 0);

            return true;
        }

        public void Unlock()
        {
            Abort.DoAbortUnless(IsLockedByCurrentThread());

            _recursiveCount--;
            if (_recursiveCount == 0)
            {
                _cs.Leave();
            }
        }

        public bool IsLockedByCurrentThread()
        {
            return _cs.IsLockedByCurrentThread();
        }
    }
}
