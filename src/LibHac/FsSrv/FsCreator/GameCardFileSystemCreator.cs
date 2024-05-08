using System;
using System.Buffers;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.Storage;
using LibHac.FsSystem;
using LibHac.Gc;
using LibHac.Os;
using LibHac.Util;
using PartitionEntry = LibHac.FsSystem.Impl.Sha256PartitionFileSystemFormat.PartitionEntry;

namespace LibHac.FsSrv.FsCreator;

/// <summary>
/// Reads the root partition of a game card and handles opening the various partitions it contains.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
public class GameCardRootPartition : IDisposable
{
    private const int LogoPartitionSizeMax = 0x12000;
    private static ReadOnlySpan<byte> UpdatePartitionName => "update"u8;
    private static ReadOnlySpan<byte> NormalPartitionName => "normal"u8;
    private static ReadOnlySpan<byte> SecurePartitionName => "secure"u8;
    private static ReadOnlySpan<byte> LogoPartitionName => "logo"u8;
    private static ReadOnlySpan<byte> LogoPartitionPath => "/logo"u8;

    private UniqueRef<Sha256PartitionFileSystemMeta> _partitionFsMeta;
    private SharedRef<IStorage> _alignedRootStorage;
    private GameCardHandle _gcHandle;
    private long _metaDataSize;
    private IGameCardStorageCreator _gameCardStorageCreator;
    private byte[] _logoPartitionData;
    private SharedRef<IStorage> _logoPartitionStorage;
    private SdkMutexType _mutex;

    // LibHac addition so we can access fssrv::storage functions
    private readonly FileSystemServer _fsServer;

    public GameCardRootPartition(GameCardHandle handle, ref readonly SharedRef<IStorage> rootStorage,
        IGameCardStorageCreator storageCreator, ref UniqueRef<Sha256PartitionFileSystemMeta> partitionFsMeta,
        FileSystemServer fsServer)
    {
        _partitionFsMeta = new UniqueRef<Sha256PartitionFileSystemMeta>(ref partitionFsMeta);
        _alignedRootStorage = SharedRef<IStorage>.CreateCopy(in rootStorage);
        _gcHandle = handle;
        _gameCardStorageCreator = storageCreator;
        _logoPartitionStorage = new SharedRef<IStorage>();
        _mutex = new SdkMutexType();
        _metaDataSize = _partitionFsMeta.Get.GetMetaDataSize();

        _fsServer = fsServer;
    }

    public void Dispose()
    {
        _logoPartitionStorage.Destroy();
        _alignedRootStorage.Destroy();
        _partitionFsMeta.Destroy();

        if (_logoPartitionData is not null)
        {
            ArrayPool<byte>.Shared.Return(_logoPartitionData);
            _logoPartitionData = null;
        }
    }

    private static U8Span GetPartitionPath(GameCardPartition partitionType)
    {
        switch (partitionType)
        {
            case GameCardPartition.Update: return UpdatePartitionName;
            case GameCardPartition.Normal: return NormalPartitionName;
            case GameCardPartition.Secure: return SecurePartitionName;
            case GameCardPartition.Logo: return LogoPartitionName;
            default:
                Abort.UnexpectedDefault();
                return default;
        }
    }

    public bool IsValid()
    {
        Assert.SdkNotNull(in _alignedRootStorage);

        return _fsServer.Storage.IsGameCardActivationValid(_gcHandle);
    }

    public Result OpenPartition(ref SharedRef<IStorage> outStorage, out ReadOnlyRef<PartitionEntry> outEntry,
        GameCardHandle handle, GameCardPartition partitionType)
    {
        outEntry = default;

        switch (partitionType)
        {
            case GameCardPartition.Update:
            case GameCardPartition.Normal:
            case GameCardPartition.Secure:
            case GameCardPartition.Logo:
                break;
            default:
                return ResultFs.InvalidArgument.Log();
        }

        int entryIndex = _partitionFsMeta.Get.GetEntryIndex(GetPartitionPath(partitionType));
        if (entryIndex < 0)
            return ResultFs.PartitionNotFound.Log();

        ref readonly PartitionEntry entry = ref _partitionFsMeta.Get.GetEntry(entryIndex);
        outEntry = new ReadOnlyRef<PartitionEntry>(in entry);

        switch (partitionType)
        {
            case GameCardPartition.Update:
            case GameCardPartition.Normal:
            {
                // The root partition contains the entire non-secure section of the game card, so we just need to make
                // a SubStorage of the appropriate range.
                outStorage.Reset(new SubStorage(in _alignedRootStorage, _metaDataSize + entry.Offset, entry.Size));

                if (!outStorage.HasValue)
                    return ResultFs.AllocationMemoryFailedInGameCardFileSystemCreatorA.Log();

                return Result.Success;
            }
            case GameCardPartition.Secure:
            {
                Result res = EnsureLogoDataCached();
                if (res.IsFailure() && !ResultFs.PartitionNotFound.Includes(res))
                    return res.Miss();

                using var secureStorage = new SharedRef<IStorage>();
                res = _gameCardStorageCreator.CreateSecureReadOnly(handle, ref secureStorage.Ref);
                if (res.IsFailure()) return res.Miss();

                const int dataAlignment = 512;

                using var alignedStorage = new SharedRef<IStorage>(
                    new AlignmentMatchingStorageInBulkRead<AlignmentMatchingStorageSize1>(in secureStorage,
                        dataAlignment));

                if (!alignedStorage.HasValue)
                    return ResultFs.AllocationMemoryFailedInGameCardFileSystemCreatorB.Log();

                outStorage.SetByMove(ref alignedStorage.Ref);
                return Result.Success;
            }
            case GameCardPartition.Logo:
            {
                Result res = EnsureLogoDataCached();
                if (res.IsFailure() && !ResultFs.PartitionNotFound.Includes(res))
                    return res.Miss();

                outStorage.SetByCopy(in _logoPartitionStorage);
                return Result.Success;
            }
        }

        return ResultFs.PartitionNotFound.Log();
    }

    public Result EnsureLogoDataCached()
    {
        int entryIndex = _partitionFsMeta.Get.GetEntryIndex(GetPartitionPath(GameCardPartition.Logo));
        if (entryIndex < 0)
            return ResultFs.PartitionNotFound.Log();

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_logoPartitionStorage.HasValue)
            return Result.Success;

        ref readonly PartitionEntry logoEntry = ref _partitionFsMeta.Get.GetEntry(entryIndex);
        if (logoEntry.Size > LogoPartitionSizeMax)
            return ResultFs.GameCardLogoDataTooLarge.Log();

        using var rootPartitionFs = new Sha256PartitionFileSystem();
        Result res = rootPartitionFs.Initialize(_partitionFsMeta.Get, in _alignedRootStorage);
        if (res.IsFailure()) return res.Miss();

        using var file = new UniqueRef<IFile>();

        using var pathLogo = new Path();
        res = PathFunctions.SetUpFixedPath(ref pathLogo.Ref(), LogoPartitionPath);
        if (res.IsFailure()) return res.Miss();

        res = rootPartitionFs.OpenFile(ref file.Ref, in pathLogo, OpenMode.Read);
        if (res.IsFailure()) return res.Miss();

        _logoPartitionData = ArrayPool<byte>.Shared.Rent(LogoPartitionSizeMax);

        res = file.Get.Read(out long readSize, offset: 0, _logoPartitionData.AsSpan(0, LogoPartitionSizeMax),
            ReadOption.None);
        if (ResultFs.DataCorrupted.Includes(res))
            return ResultFs.GameCardLogoDataCorrupted.LogConverted(res);

        if (res.IsFailure()) return res.Miss();

        if (readSize != logoEntry.Size)
            return ResultFs.GameCardLogoDataSizeInvalid.Log();

        _logoPartitionStorage.Reset(new MemoryStorage(_logoPartitionData, (int)logoEntry.Size));

        return Result.Success;
    }
}

/// <summary>
/// Creates <see cref="IFileSystem"/>s of the various partitions contained by the currently mounted game card.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
public class GameCardFileSystemCreator : IGameCardFileSystemCreator
{
    private MemoryResource _allocator;
    private GameCardStorageCreator _gameCardStorageCreator;
    private UniqueRef<GameCardRootPartition> _rootPartition;
    private SdkMutexType _mutex;

    // LibHac addition so we can access fssrv::storage functions
    private readonly FileSystemServer _fsServer;

    public GameCardFileSystemCreator(MemoryResource allocator, GameCardStorageCreator gameCardStorageCreator,
        FileSystemServer fsServer)
    {
        _allocator = allocator;
        _gameCardStorageCreator = gameCardStorageCreator;
        _rootPartition = new UniqueRef<GameCardRootPartition>();
        _mutex = new SdkMutexType();

        _fsServer = fsServer;
    }

    public void Dispose()
    {
        _rootPartition.Destroy();
    }

    public Result Create(ref SharedRef<IFileSystem> outFileSystem, GameCardHandle handle, GameCardPartition partitionType)
    {
        Result res;

        using (ScopedLock.Lock(ref _mutex))
        {
            // Initialize the root partition if not already initialized.
            if (!_rootPartition.HasValue || !_rootPartition.Get.IsValid())
            {
                // Open an IStorage of the game card's normal area.
                using var rootStorage = new SharedRef<IStorage>();
                res = _gameCardStorageCreator.CreateReadOnly(handle, ref rootStorage.Ref);
                if (res.IsFailure()) return res.Miss();

                // Make sure reads to the game card are aligned to the game card's sector size.
                const int dataAlignment = 512;
                using var alignedRootStorage = new SharedRef<IStorage>(
                    new AlignmentMatchingStorageInBulkRead<AlignmentMatchingStorageSize1>(in rootStorage, dataAlignment));

                if (!alignedRootStorage.HasValue)
                    return ResultFs.AllocationMemoryFailedInGameCardFileSystemCreatorC.Log();

                res = _fsServer.Storage.GetGameCardStatus(out GameCardStatus status, handle);
                if (res.IsFailure()) return res.Miss();

                // Get an IStorage of the start of the root partition to the start of the secure area.
                long updateAndNormalPartitionSize = status.NormalAreaSize - status.PartitionFsHeaderAddress;
                using var rootFsStorage = new SharedRef<IStorage>(new SubStorage(in alignedRootStorage,
                    status.PartitionFsHeaderAddress, updateAndNormalPartitionSize));

                if (!rootFsStorage.HasValue)
                    return ResultFs.AllocationMemoryFailedInGameCardFileSystemCreatorD.Log();

                // Initialize a reader for the root partition.
                using var rootPartitionFsMeta = new UniqueRef<Sha256PartitionFileSystemMeta>(new Sha256PartitionFileSystemMeta());
                if (!rootPartitionFsMeta.HasValue)
                    return ResultFs.AllocationMemoryFailedInGameCardFileSystemCreatorG.Log();

                res = GetSaltFromCompatibilityType(out Optional<byte> salt, status.CompatibilityType);
                if (res.IsFailure()) return res.Miss();

                res = rootPartitionFsMeta.Get.Initialize(rootFsStorage.Get, _allocator, status.PartitionFsHeaderHash, salt);
                if (res.IsFailure()) return res.Miss();

                _rootPartition.Reset(new GameCardRootPartition(handle, in rootFsStorage, _gameCardStorageCreator,
                    ref rootPartitionFsMeta.Ref, _fsServer));

                if (!_rootPartition.HasValue)
                    return ResultFs.AllocationMemoryFailedInGameCardFileSystemCreatorE.Log();
            }
        }

        // Open the raw storage of the requested partition.
        using var partitionStorage = new SharedRef<IStorage>();
        res = _rootPartition.Get.OpenPartition(ref partitionStorage.Ref, out ReadOnlyRef<PartitionEntry> refEntry,
            handle, partitionType);
        if (res.IsFailure()) return res.Miss();

        // Initialize a Sha256PartitionFileSystem for reading the partition's file system.
        using var partitionFsMeta = new UniqueRef<Sha256PartitionFileSystemMeta>(new Sha256PartitionFileSystemMeta());
        if (!partitionFsMeta.HasValue)
            return ResultFs.AllocationMemoryFailedInGameCardFileSystemCreatorH.Log();

        if (partitionType == GameCardPartition.Logo)
        {
            res = partitionFsMeta.Get.Initialize(partitionStorage.Get, _allocator);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            res = partitionFsMeta.Get.Initialize(partitionStorage.Get, _allocator, refEntry.Value.Hash);
            if (res.IsFailure()) return res.Miss();
        }

        res = Sha256PartitionFileSystemMeta.QueryMetaDataSize(out _, partitionStorage.Get);
        if (res.IsFailure()) return res.Miss();

        using var fs = new SharedRef<Sha256PartitionFileSystem>(new Sha256PartitionFileSystem());
        if (!fs.HasValue)
            return ResultFs.AllocationMemoryFailedInGameCardFileSystemCreatorF.Log();

        res = fs.Get.Initialize(ref partitionFsMeta.Ref, in partitionStorage);
        if (res.IsFailure()) return res.Miss();

        outFileSystem.SetByMove(ref fs.Ref);
        return Result.Success;
    }

    private Result GetSaltFromCompatibilityType(out Optional<byte> outSalt, byte compatibilityType)
    {
        switch ((GameCardCompatibilityType)compatibilityType)
        {
            case GameCardCompatibilityType.Normal:
                outSalt = default;
                break;
            case GameCardCompatibilityType.Terra:
                outSalt = new Optional<byte>(compatibilityType);
                break;
            default:
                outSalt = default;
                return ResultFs.GameCardFsInvalidCompatibilityType.Log();
        }

        return Result.Success;
    }
}