using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSrv.Storage;
using LibHac.Os;
using LibHac.Util;

namespace LibHac.FsSrv;

/// <summary>
/// Initializes and holds <see cref="ISaveDataIndexer"/>s for each save data space.
/// Creates accessors for individual SaveDataIndexers.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
internal class SaveDataIndexerManager : ISaveDataIndexerManager, IDisposable
{
    private MemoryResource _memoryResource;
    private readonly ulong _indexerSaveDataId;
    private SdkMutex _bisIndexerMutex;
    private Optional<SaveDataIndexer> _bisIndexer;
    private SdkMutex _tempIndexerMutex;
    private SaveDataIndexerLite _tempIndexer;
    private SdkMutex _sdIndexerMutex;
    private Optional<SaveDataIndexer> _sdIndexer;
    private StorageDeviceHandle _sdCardHandle;
    private IDeviceHandleManager _sdHandleManager;
    private SdkMutex _properBisIndexerMutex;
    private Optional<SaveDataIndexer> _properBisIndexer;
    private SdkMutex _safeIndexerMutex;
    private Optional<SaveDataIndexer> _safeIndexer;
    private readonly bool _isBisUserRedirectionEnabled;

    // LibHac addition
    private FileSystemClient _fsClient;

    public SaveDataIndexerManager(FileSystemClient fsClient, ulong saveDataId, MemoryResource memoryResource,
        IDeviceHandleManager sdCardHandleManager, bool isBisUserRedirectionEnabled)
    {
        _memoryResource = memoryResource;
        _indexerSaveDataId = saveDataId;

        _bisIndexerMutex = new SdkMutex();
        _tempIndexerMutex = new SdkMutex();
        _tempIndexer = new SaveDataIndexerLite();
        _sdIndexerMutex = new SdkMutex();
        _sdHandleManager = sdCardHandleManager;
        _properBisIndexerMutex = new SdkMutex();
        _safeIndexerMutex = new SdkMutex();

        _isBisUserRedirectionEnabled = isBisUserRedirectionEnabled;

        _fsClient = fsClient;
    }

    public void Dispose()
    {
        InvalidateIndexerImpl(ref _bisIndexer);
        _tempIndexer.Dispose();
        InvalidateIndexerImpl(ref _sdIndexer);
        InvalidateIndexerImpl(ref _properBisIndexer);
        InvalidateIndexerImpl(ref _safeIndexer);
    }

    public void InvalidateAllIndexers()
    {
        InvalidateIndexerImpl(ref _bisIndexer);
        InvalidateIndexerImpl(ref _sdIndexer);
        InvalidateIndexerImpl(ref _properBisIndexer);
        InvalidateIndexerImpl(ref _safeIndexer);
    }

    public void InvalidateIndexer(SaveDataSpaceId spaceId)
    {
        switch (spaceId)
        {
            case SaveDataSpaceId.SdSystem:
            case SaveDataSpaceId.SdUser:
            {
                // Note: Nintendo doesn't lock when doing this operation
                using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _sdIndexerMutex);
                InvalidateIndexerImpl(ref _sdIndexer);
                break;
            }

            default:
                Abort.UnexpectedDefault();
                break;
        }
    }

    // Todo: Figure out how to add generic disposal to Optional<T>
    private static void InvalidateIndexerImpl(ref Optional<SaveDataIndexer> indexer)
    {
        if (indexer.HasValue)
            indexer.Value.Dispose();

        indexer.Clear();
    }

    public void ResetIndexer(SaveDataSpaceId spaceId)
    {
        switch (spaceId)
        {
            case SaveDataSpaceId.Temporary:
                // ReSharper disable once RedundantAssignment
                Result res = _tempIndexer.Reset();
                Assert.SdkAssert(res.IsSuccess());
                break;

            default:
                Abort.UnexpectedDefault();
                break;
        }
    }

    /// <summary>
    /// Opens a <see cref="SaveDataIndexerAccessor"/> for the specified save data space.
    /// </summary>
    /// <remarks>
    /// The returned <see cref="SaveDataIndexerAccessor"/> will have exclusive access to the requested indexer.
    /// The accessor must be disposed after use.
    /// </remarks>
    /// <param name="outAccessor">If the method returns successfully, contains the created accessor.</param>
    /// <param name="isInitialOpen">If the method returns successfully, contains <see langword="true"/>
    /// if the indexer needed to be initialized because this was the first time it was opened.</param>
    /// <param name="spaceId">The <see cref="SaveDataSpaceId"/> of the indexer to open.</param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    public Result OpenSaveDataIndexerAccessor(ref UniqueRef<SaveDataIndexerAccessor> outAccessor,
        out bool isInitialOpen, SaveDataSpaceId spaceId)
    {
        UnsafeHelpers.SkipParamInit(out isInitialOpen);

        if (_isBisUserRedirectionEnabled && spaceId == SaveDataSpaceId.User)
        {
            spaceId = SaveDataSpaceId.ProperSystem;
        }

        ISaveDataIndexer indexer;
        using var indexerLock = new UniqueLock<SdkMutex>();
        bool wasIndexerInitialized = false;

        switch (spaceId)
        {
            case SaveDataSpaceId.System:
            case SaveDataSpaceId.User:
            {
                indexerLock.Reset(_bisIndexerMutex);

                if (!_bisIndexer.HasValue)
                {
                    _bisIndexer.Set(new SaveDataIndexer(_fsClient, new U8Span(BisIndexerMountName),
                        SaveDataSpaceId.System, _indexerSaveDataId, _memoryResource));
                    wasIndexerInitialized = true;
                }

                indexer = _bisIndexer.Value;
                break;
            }
            case SaveDataSpaceId.Temporary:
            {
                indexerLock.Reset(_tempIndexerMutex);
                indexer = _tempIndexer;
                break;
            }
            case SaveDataSpaceId.SdSystem:
            case SaveDataSpaceId.SdUser:
            {
                if (_sdHandleManager is null)
                    return ResultFs.InvalidArgument.Log();

                indexerLock.Reset(_sdIndexerMutex);

                // We need to reinitialize the indexer if the SD card has changed
                if (!_sdHandleManager.IsValid(in _sdCardHandle) && _sdIndexer.HasValue)
                {
                    _sdIndexer.Value.Dispose();
                    _sdIndexer.Clear();
                }

                if (!_sdIndexer.HasValue)
                {
                    _sdIndexer.Set(new SaveDataIndexer(_fsClient, new U8Span(SdCardIndexerMountName),
                        SaveDataSpaceId.SdSystem, _indexerSaveDataId, _memoryResource));

                    _sdHandleManager.GetHandle(out _sdCardHandle).IgnoreResult();
                    wasIndexerInitialized = true;
                }

                indexer = _sdIndexer.Value;
                break;
            }
            case SaveDataSpaceId.ProperSystem:
            {
                indexerLock.Reset(_properBisIndexerMutex);

                if (!_properBisIndexer.HasValue)
                {
                    _properBisIndexer.Set(new SaveDataIndexer(_fsClient, new U8Span(ProperBisIndexerMountName),
                        SaveDataSpaceId.ProperSystem, _indexerSaveDataId, _memoryResource));

                    wasIndexerInitialized = true;
                }

                indexer = _properBisIndexer.Value;
                break;
            }
            case SaveDataSpaceId.SafeMode:
            {
                indexerLock.Reset(_safeIndexerMutex);

                if (!_safeIndexer.HasValue)
                {
                    _safeIndexer.Set(new SaveDataIndexer(_fsClient, new U8Span(SafeModeIndexerMountName),
                        SaveDataSpaceId.SafeMode, _indexerSaveDataId, _memoryResource));

                    wasIndexerInitialized = true;
                }

                indexer = _safeIndexer.Value;
                break;
            }

            default:
                return ResultFs.InvalidArgument.Log();
        }

        outAccessor.Reset(new SaveDataIndexerAccessor(indexer, ref indexerLock.Ref()));
        isInitialOpen = wasIndexerInitialized;
        return Result.Success;
    }

    /// <summary>"<c>saveDataIxrDb</c>"</summary>
    private static ReadOnlySpan<byte> BisIndexerMountName =>
        new[]
        {
            (byte)'s', (byte)'a', (byte)'v', (byte)'e', (byte)'D', (byte)'a', (byte)'t', (byte)'a',
            (byte)'I', (byte)'x', (byte)'r', (byte)'D', (byte)'b'
        };

    /// <summary>"<c>saveDataIxrDbSd</c>"</summary>
    private static ReadOnlySpan<byte> SdCardIndexerMountName =>
        new[]
        {
            (byte)'s', (byte)'a', (byte)'v', (byte)'e', (byte)'D', (byte)'a', (byte)'t', (byte)'a',
            (byte)'I', (byte)'x', (byte)'r', (byte)'D', (byte)'b', (byte)'S', (byte)'d'
        };

    /// <summary>"<c>saveDataIxrDbPr</c>"</summary>
    private static ReadOnlySpan<byte> ProperBisIndexerMountName =>
        new[]
        {
            (byte)'s', (byte)'a', (byte)'v', (byte)'e', (byte)'D', (byte)'a', (byte)'t', (byte)'a',
            (byte)'I', (byte)'x', (byte)'r', (byte)'D', (byte)'b', (byte)'P', (byte)'r'
        };

    /// <summary>"<c>saveDataIxrDbSf</c>"</summary>
    private static ReadOnlySpan<byte> SafeModeIndexerMountName =>
        new[]
        {
            (byte)'s', (byte)'a', (byte)'v', (byte)'e', (byte)'D', (byte)'a', (byte)'t', (byte)'a',
            (byte)'I', (byte)'x', (byte)'r', (byte)'D', (byte)'b', (byte)'S', (byte)'f'
        };
}

/// <summary>
/// Gives exclusive access to an <see cref="ISaveDataIndexer"/>.
/// Releases the lock to the <see cref="ISaveDataIndexer"/> upon disposal.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class SaveDataIndexerAccessor : IDisposable
{
    private readonly ISaveDataIndexer _indexer;
    private UniqueLock<SdkMutex> _lock;

    public SaveDataIndexerAccessor(ISaveDataIndexer indexer, ref UniqueLock<SdkMutex> indexerLock)
    {
        _indexer = indexer;
        _lock = new UniqueLock<SdkMutex>(ref indexerLock);
    }

    public void Dispose()
    {
        _lock.Dispose();
    }

    public ISaveDataIndexer GetInterface()
    {
        return _indexer;
    }
}