// ReSharper disable UnusedMember.Local
using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Impl;
using LibHac.Os;

namespace LibHac.Fs.Impl
{
    /// <summary>
    /// Handles getting scoped read access to the global file data cache.
    /// </summary>
    /// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
    internal struct GlobalFileDataCacheAccessorReadableScopedPointer : IDisposable
    {
        private FileDataCacheAccessor _accessor;
        private ReaderWriterLock _lock;

        public void Dispose()
        {
            _lock?.ReleaseReadLock();
        }

        public readonly FileDataCacheAccessor Get() => _accessor;

        public void Set(FileDataCacheAccessor accessor, ReaderWriterLock rwLock)
        {
            if (_lock is not null && _lock != rwLock)
                _lock.ReleaseReadLock();

            _accessor = accessor;
            _lock = rwLock;
        }
    }
}

namespace LibHac.Fs.Shim
{
    public static class FileDataCacheShim
    {
        internal struct Globals : IDisposable
        {
            public nint FileSystemProxyServiceObjectInitGuard;
            public GlobalFileDataCacheAccessorHolder GlobalFileDataCacheAccessorHolder;

            public void Dispose()
            {
                GlobalFileDataCacheAccessorHolder.Dispose();
            }
        }

        internal class GlobalFileDataCacheAccessorHolder
        {
            private FileDataCacheAccessor _accessor;
            private ReaderWriterLock _accessorLock;
            private long _cacheSize;
            private bool _isDefault;

            public GlobalFileDataCacheAccessorHolder(HorizonClient hos)
            {
                _accessorLock = new ReaderWriterLock(hos.Os);
            }

            public void Dispose()
            {
                _accessorLock?.Dispose();
            }

            public ReaderWriterLock GetLock() => _accessorLock;

            public bool HasAccessor()
            {
                Assert.SdkAssert(_accessorLock.IsReadLockHeld() || _accessorLock.IsWriteLockHeldByCurrentThread());

                return _accessor is not null;
            }

            public void SetAccessor()
            {
                Assert.SdkAssert(_accessorLock.IsWriteLockHeldByCurrentThread());

                _accessor = null;
            }

            public void SetAccessor(FileDataCacheAccessor accessor, long cacheSize, bool isDefault)
            {
                Assert.SdkAssert(_accessorLock.IsWriteLockHeldByCurrentThread());

                _accessor = accessor;
                _cacheSize = cacheSize;
                _isDefault = isDefault;
            }

            public FileDataCacheAccessor GetAccessor()
            {
                Assert.SdkAssert(_accessorLock.IsReadLockHeld() || _accessorLock.IsWriteLockHeldByCurrentThread());

                return _accessor;
            }

            public long GetCacheSize()
            {
                Assert.SdkAssert(_accessorLock.IsReadLockHeld() || _accessorLock.IsWriteLockHeldByCurrentThread());

                return _cacheSize;
            }

            public bool IsDefaultGlobalFileDataCache()
            {
                Assert.SdkAssert(_accessorLock.IsReadLockHeld() || _accessorLock.IsWriteLockHeldByCurrentThread());
                Assert.SdkNotNull(_accessor);

                return _isDefault;
            }
        }

        private static GlobalFileDataCacheAccessorHolder GetGlobalFileDataCacheAccessorHolder(FileSystemClient fs)
        {
            ref Globals g = ref fs.Globals.FileDataCache;
            using var guard = new InitializationGuard(ref g.FileSystemProxyServiceObjectInitGuard,
                fs.Globals.InitMutex);

            if (!guard.IsInitialized)
            {
                g.GlobalFileDataCacheAccessorHolder = new GlobalFileDataCacheAccessorHolder(fs.Hos);
            }

            return g.GlobalFileDataCacheAccessorHolder;
        }

        private static Result EnableGlobalFileDataCacheImpl(FileSystemClient fs, Memory<byte> buffer, bool isDefault)
        {
            throw new NotImplementedException();
        }

        private static Result DisableGlobalFileDataCacheImpl(FileSystemClient fs)
        {
            throw new NotImplementedException();
        }

        private static int PrintDefaultGlobalFileDataCacheAccessLog(Span<byte> textBuffer)
        {
            throw new NotImplementedException();
        }

        internal static bool IsGlobalFileDataCacheEnabled(this FileSystemClientImpl fs)
        {
            return false;
        }

        internal static bool TryGetGlobalFileDataCacheAccessor(this FileSystemClientImpl fs,
            ref GlobalFileDataCacheAccessorReadableScopedPointer scopedPointer)
        {
            throw new NotImplementedException();
        }

        internal static void SetGlobalFileDataCacheAccessorForDebug(this FileSystemClientImpl fs,
            FileDataCacheAccessor accessor)
        {
            throw new NotImplementedException();
        }

        public static void EnableGlobalFileDataCache(this FileSystemClient fs, Memory<byte> buffer)
        {
            throw new NotImplementedException();
        }

        public static void DisableGlobalFileDataCache(this FileSystemClient fs)
        {
            throw new NotImplementedException();
        }

        public static void EnableDefaultGlobalFileDataCache(this FileSystemClient fs, Memory<byte> buffer)
        {
            throw new NotImplementedException();
        }

        public static bool IsDefaultGlobalFileDataCacheEnabled(this FileSystemClient fs)
        {
            throw new NotImplementedException();
        }
    }
}