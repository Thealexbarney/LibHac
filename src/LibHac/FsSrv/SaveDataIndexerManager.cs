using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSrv.Storage;
using LibHac.Util;

namespace LibHac.FsSrv
{
    /// <summary>
    /// Initializes and holds <see cref="ISaveDataIndexer"/>s for each save data space.
    /// Creates accessors for individual SaveDataIndexers.
    /// </summary>
    /// <remarks>Based on FS 10.0.0 (nnSdk 10.4.0)</remarks>
    internal class SaveDataIndexerManager : ISaveDataIndexerManager
    {
        private FileSystemClient FsClient { get; }
        private MemoryResource MemoryResource { get; }
        private ulong SaveDataId { get; }

        private IndexerHolder _bisIndexer = new IndexerHolder(new object());
        private IndexerHolder _tempIndexer = new IndexerHolder(new object());

        private IndexerHolder _sdCardIndexer = new IndexerHolder(new object());
        private StorageDeviceHandle _sdCardHandle;
        private IDeviceHandleManager _sdCardHandleManager;

        private IndexerHolder _safeIndexer = new IndexerHolder(new object());
        private IndexerHolder _properSystemIndexer = new IndexerHolder(new object());

        private bool IsBisUserRedirectionEnabled { get; }

        public SaveDataIndexerManager(FileSystemClient fsClient, ulong saveDataId, MemoryResource memoryResource,
            IDeviceHandleManager sdCardHandleManager, bool isBisUserRedirectionEnabled)
        {
            FsClient = fsClient;
            SaveDataId = saveDataId;
            MemoryResource = memoryResource;
            _sdCardHandleManager = sdCardHandleManager;
            IsBisUserRedirectionEnabled = isBisUserRedirectionEnabled;

            _tempIndexer.Indexer = new SaveDataIndexerLite();
        }

        /// <summary>
        /// Opens a <see cref="SaveDataIndexerAccessor"/> for the specified save data space.
        /// </summary>
        /// <remarks>
        /// The returned <see cref="SaveDataIndexerAccessor"/> will have exclusive access to the requested indexer.
        /// The accessor must be disposed after use.
        /// </remarks>
        /// <param name="accessor">If the method returns successfully, contains the created accessor.</param>
        /// <param name="neededInit">If the method returns successfully, contains <see langword="true"/>
        /// if the indexer needed to be initialized.</param>
        /// <param name="spaceId">The <see cref="SaveDataSpaceId"/> of the indexer to open.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public Result OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, out bool neededInit,
            SaveDataSpaceId spaceId)
        {
            UnsafeHelpers.SkipParamInit(out neededInit);

            if (IsBisUserRedirectionEnabled && spaceId == SaveDataSpaceId.User)
            {
                spaceId = SaveDataSpaceId.ProperSystem;
            }

            UniqueLock indexerLock = default;
            try
            {
                ISaveDataIndexer indexer;
                switch (spaceId)
                {
                    case SaveDataSpaceId.System:
                    case SaveDataSpaceId.User:
                        indexerLock = new UniqueLock(_bisIndexer.Locker);

                        if (!_bisIndexer.IsInitialized)
                        {
                            _bisIndexer.Indexer = new SaveDataIndexer(FsClient, new U8Span(SystemIndexerMountName),
                                SaveDataSpaceId.System, SaveDataId, MemoryResource);

                            neededInit = true;
                        }

                        indexer = _bisIndexer.Indexer;
                        break;
                    case SaveDataSpaceId.SdSystem:
                    case SaveDataSpaceId.SdCache:
                        // ReSharper doesn't realize that UniqueLock locks the indexer's lock object
                        // ReSharper disable InconsistentlySynchronizedField
                        indexerLock = new UniqueLock(_sdCardIndexer.Locker);

                        // We need to reinitialize the indexer if the SD card has changed
                        if (!_sdCardHandleManager.IsValid(in _sdCardHandle) && _sdCardIndexer.IsInitialized)
                        {
                            _sdCardIndexer.Indexer.Dispose();
                            _sdCardIndexer.Indexer = null;
                        }

                        if (!_sdCardIndexer.IsInitialized)
                        {
                            _sdCardIndexer.Indexer = new SaveDataIndexer(FsClient, new U8Span(SdCardIndexerMountName),
                                SaveDataSpaceId.SdSystem, SaveDataId, MemoryResource);

                            _sdCardHandleManager.GetHandle(out _sdCardHandle).IgnoreResult();

                            neededInit = true;
                        }

                        indexer = _sdCardIndexer.Indexer;
                        // ReSharper restore InconsistentlySynchronizedField

                        break;
                    case SaveDataSpaceId.Temporary:
                        indexerLock = new UniqueLock(_tempIndexer.Locker);

                        indexer = _tempIndexer.Indexer;
                        break;
                    case SaveDataSpaceId.ProperSystem:
                        indexerLock = new UniqueLock(_properSystemIndexer.Locker);

                        if (!_properSystemIndexer.IsInitialized)
                        {
                            _properSystemIndexer.Indexer = new SaveDataIndexer(FsClient,
                                new U8Span(ProperSystemIndexerMountName),
                                SaveDataSpaceId.ProperSystem, SaveDataId, MemoryResource);

                            neededInit = true;
                        }

                        indexer = _properSystemIndexer.Indexer;
                        break;
                    case SaveDataSpaceId.SafeMode:
                        indexerLock = new UniqueLock(_safeIndexer.Locker);

                        if (!_safeIndexer.IsInitialized)
                        {
                            _safeIndexer.Indexer = new SaveDataIndexer(FsClient, new U8Span(SafeModeIndexerMountName),
                                SaveDataSpaceId.SafeMode, SaveDataId, MemoryResource);

                            neededInit = true;
                        }

                        indexer = _safeIndexer.Indexer;
                        break;

                    default:
                        accessor = default;
                        return ResultFs.InvalidArgument.Log();
                }


                accessor = new SaveDataIndexerAccessor(indexer, ref indexerLock);
                return Result.Success;
            }
            finally
            {
                indexerLock.Dispose();
            }
        }

        public void ResetIndexer(SaveDataSpaceId spaceId)
        {
            if (spaceId != SaveDataSpaceId.Temporary)
            {
                Abort.UnexpectedDefault();
            }

            // ReSharper disable once RedundantAssignment
            Result rc = _tempIndexer.Indexer.Reset();
            Assert.SdkAssert(rc.IsSuccess());
        }

        public void InvalidateIndexer(SaveDataSpaceId spaceId)
        {
            // Note: Nintendo doesn't lock when doing this operation
            lock (_sdCardIndexer.Locker)
            {
                if (spaceId != SaveDataSpaceId.SdCache && spaceId != SaveDataSpaceId.SdSystem)
                {
                    Abort.UnexpectedDefault();
                }

                if (_sdCardIndexer.IsInitialized)
                {
                    _sdCardIndexer.Indexer.Dispose();
                    _sdCardIndexer.Indexer = null;
                }
            }
        }

        private struct IndexerHolder
        {
            public object Locker { get; }
            public ISaveDataIndexer Indexer { get; set; }

            public IndexerHolder(object locker)
            {
                Locker = locker;
                Indexer = null;
            }

            public bool IsInitialized => Indexer != null;
        }

        private static ReadOnlySpan<byte> SystemIndexerMountName => // saveDataIxrDb
            new[]
            {
                (byte) 's', (byte) 'a', (byte) 'v', (byte) 'e', (byte) 'D', (byte) 'a', (byte) 't', (byte) 'a',
                (byte) 'I', (byte) 'x', (byte) 'r', (byte) 'D', (byte) 'b'
            };

        private static ReadOnlySpan<byte> SdCardIndexerMountName => // saveDataIxrDbSd
            new[]
            {
                (byte) 's', (byte) 'a', (byte) 'v', (byte) 'e', (byte) 'D', (byte) 'a', (byte) 't', (byte) 'a',
                (byte) 'I', (byte) 'x', (byte) 'r', (byte) 'D', (byte) 'b', (byte) 'S', (byte) 'd'
            };

        private static ReadOnlySpan<byte> ProperSystemIndexerMountName => // saveDataIxrDbPr
            new[]
            {
                (byte) 's', (byte) 'a', (byte) 'v', (byte) 'e', (byte) 'D', (byte) 'a', (byte) 't', (byte) 'a',
                (byte) 'I', (byte) 'x', (byte) 'r', (byte) 'D', (byte) 'b', (byte) 'P', (byte) 'r'
            };

        private static ReadOnlySpan<byte> SafeModeIndexerMountName => // saveDataIxrDbSf
            new[]
            {
                (byte) 's', (byte) 'a', (byte) 'v', (byte) 'e', (byte) 'D', (byte) 'a', (byte) 't', (byte) 'a',
                (byte) 'I', (byte) 'x', (byte) 'r', (byte) 'D', (byte) 'b', (byte) 'S', (byte) 'f'
            };
    }

    /// <summary>
    /// Gives exclusive access to an <see cref="ISaveDataIndexer"/>.
    /// Releases the lock to the <see cref="ISaveDataIndexer"/> upon disposal.
    /// </summary>
    /// <remarks>Based on FS 10.0.0 (nnSdk 10.4.0)</remarks>
    public class SaveDataIndexerAccessor : IDisposable
    {
        public ISaveDataIndexer Indexer { get; }
        private UniqueLock _locker;

        public SaveDataIndexerAccessor(ISaveDataIndexer indexer, ref UniqueLock locker)
        {
            Indexer = indexer;
            _locker = new UniqueLock(ref locker);
        }

        public void Dispose()
        {
            _locker.Dispose();
        }
    }
}
