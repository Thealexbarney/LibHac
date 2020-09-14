using System;
using System.Threading;

namespace LibHac.Fs.Impl
{
    /// <summary>
    /// A wrapper for handling write access to a reader-writer lock.
    /// </summary>
    public class UniqueLock : IDisposable
    {
        private ReaderWriterLockSlim _lock;
        private bool _hasLock;

        public UniqueLock(ReaderWriterLockSlim readerWriterLock)
        {
            _lock = readerWriterLock;
            readerWriterLock.EnterWriteLock();
            _hasLock = true;
        }

        public void Dispose()
        {
            if (_hasLock)
            {
                _lock.ExitWriteLock();
            }
        }
    }

    /// <summary>
    /// A wrapper for handling read access to a reader-writer lock.
    /// </summary>
    public class SharedLock : IDisposable
    {
        private ReaderWriterLockSlim _lock;
        private bool _hasLock;

        public SharedLock(ReaderWriterLockSlim readerWriterLock)
        {
            _lock = readerWriterLock;
            readerWriterLock.EnterReadLock();
            _hasLock = true;
        }

        public void Dispose()
        {
            if (_hasLock)
            {
                _lock.EnterReadLock();
            }
        }
    }
}
