using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSrv;
using LibHac.Os;
using LibHac.Util;
using static LibHac.FsSystem.Constants;
using static LibHac.FsSystem.HierarchicalIntegrityVerificationStorage;
using static LibHac.Util.BitUtil;

namespace LibHac.FsSystem;

internal static class Constants
{
    public const int IntegrityMinLayerCount = 2;
    public const int IntegrityMaxLayerCount = 7;
}

internal struct HierarchicalIntegrityVerificationStorageGlobals
{
    public RandomDataGenerator GenerateRandom;
    public Semaphore GlobalWriteSemaphore;
    public Semaphore GlobalReadSemaphore;

    public void Initialize(FileSystemServer fsServer)
    {
        GlobalWriteSemaphore = new Semaphore(fsServer.Hos.Os, AccessCountMax, AccessCountMax);
        GlobalReadSemaphore = new Semaphore(fsServer.Hos.Os, AccessCountMax, AccessCountMax);
    }

    public void Dispose()
    {
        GlobalReadSemaphore.Dispose();
    }
}

public class FileSystemBufferManagerSet
{
    public IBufferManager[] Buffers;

    public FileSystemBufferManagerSet()
    {
        Buffers = new IBufferManager[IntegrityMaxLayerCount];
    }
}

public struct HierarchicalIntegrityVerificationLevelInformation
{
    public Fs.Int64 Offset;
    public Fs.Int64 Size;
    public int BlockOrder;
    public uint Reserved;
}

public struct HierarchicalIntegrityVerificationInformation
{
    public uint MaxLayers;
    public Array6<HierarchicalIntegrityVerificationLevelInformation> Layers;
    public HashSalt HashSalt;
}

public struct HierarchicalIntegrityVerificationMetaInformation
{
    public uint Magic;
    public uint Version;
    public uint MasterHashSize;
    public HierarchicalIntegrityVerificationInformation LevelHashInfo;

    public void Format()
    {
        var hashSalt = new Optional<HashSalt>();
        Format(in hashSalt);
    }

    public void Format(in Optional<HashSalt> hashSalt)
    {
        Magic = IntegrityVerificationStorageMagic;
        Version = IntegrityVerificationStorageVersion;
        MasterHashSize = 0;
        LevelHashInfo = default;

        if (hashSalt.HasValue)
        {
            LevelHashInfo.HashSalt = hashSalt.ValueRo;
        }
    }
}

public struct HierarchicalIntegrityVerificationSizeSet
{
    public long ControlSize;
    public long MasterHashSize;
    public Array5<long> LayeredHashSizes;
}

public class HierarchicalIntegrityVerificationStorageControlArea : IDisposable
{
    public struct InputParam
    {
        public Array6<int> LevelBlockSizes;
    }

    public const int HashSize = Sha256.DigestSize;

    private ValueSubStorage _storage;
    private HierarchicalIntegrityVerificationMetaInformation _meta;

    public HierarchicalIntegrityVerificationStorageControlArea()
    {
        _storage = new ValueSubStorage();
    }

    public void Dispose()
    {
        _storage.Dispose();
    }

    public static Result QuerySize(out HierarchicalIntegrityVerificationSizeSet outSizeSet, in InputParam inputParam,
        int layerCount, long dataSize)
    {
        UnsafeHelpers.SkipParamInit(out outSizeSet);

        Assert.SdkRequires(layerCount >= IntegrityMinLayerCount && layerCount <= IntegrityMaxLayerCount);

        for (int level = 0; level < layerCount - 1; level++)
        {
            Assert.SdkRequires(inputParam.LevelBlockSizes[level] > 0 && IsPowerOfTwo(inputParam.LevelBlockSizes[level]));
        }

        {
            outSizeSet.ControlSize = Unsafe.SizeOf<HierarchicalIntegrityVerificationMetaInformation>();

            Span<long> levelSize = stackalloc long[IntegrityMaxLayerCount];
            int level = layerCount - 1;

            levelSize[level] = Alignment.AlignUp(dataSize, (uint)inputParam.LevelBlockSizes[level - 1]);
            level--;

            for (; level > 0; level--)
            {
                // Calculate how much space is needed to store the hashes of the above level, rounding up to the next block size.
                levelSize[level] =
                    Alignment.AlignUp(levelSize[level + 1] / inputParam.LevelBlockSizes[level] * HashSize,
                        (uint)inputParam.LevelBlockSizes[level - 1]);
            }

            // The size of the master hash does not get rounded up to the next block size.
            levelSize[0] = levelSize[1] / inputParam.LevelBlockSizes[0] * HashSize;
            outSizeSet.MasterHashSize = levelSize[0];

            // Write the sizes of each level to the output struct.
            for (level = 1; level < layerCount - 1; level++)
            {
                outSizeSet.LayeredHashSizes[level - 1] = levelSize[level];
            }

            return Result.Success;
        }
    }

    public static Result Format(ref readonly ValueSubStorage metaStorage,
        in HierarchicalIntegrityVerificationMetaInformation metaInfo)
    {
        // Ensure the storage is large enough to hold the meta info.
        Result res = metaStorage.GetSize(out long metaSize);
        if (res.IsFailure()) return res.Miss();

        if (metaSize < Unsafe.SizeOf<HierarchicalIntegrityVerificationMetaInformation>())
            return ResultFs.InvalidSize.Log();

        // Validate the meta magic and version.
        if (metaInfo.Magic != IntegrityVerificationStorageMagic)
            return ResultFs.IncorrectIntegrityVerificationMagicCode.Log();

        if ((metaInfo.Version & IntegrityVerificationStorageVersionMask) != IntegrityVerificationStorageVersion)
            return ResultFs.UnsupportedVersion.Log();

        // Write the meta info to the storage.
        res = metaStorage.Write(0, SpanHelpers.AsReadOnlyByteSpan(in metaInfo));
        if (res.IsFailure()) return res.Miss();

        res = metaStorage.Flush();
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result Expand(ref readonly ValueSubStorage metaStorage,
        in HierarchicalIntegrityVerificationMetaInformation newMeta)
    {
        // Ensure the storage is large enough to hold the meta info.
        Result res = metaStorage.GetSize(out long metaSize);
        if (res.IsFailure()) return res.Miss();

        if (metaSize < Unsafe.SizeOf<HierarchicalIntegrityVerificationMetaInformation>())
            return ResultFs.InvalidSize.Log();

        // Validate both the previous and new metas.
        HierarchicalIntegrityVerificationMetaInformation previousMeta = default;
        res = metaStorage.Read(0, SpanHelpers.AsByteSpan(ref previousMeta));
        if (res.IsFailure()) return res.Miss();

        if (newMeta.Magic != IntegrityVerificationStorageMagic || newMeta.Magic != previousMeta.Magic)
            return ResultFs.IncorrectIntegrityVerificationMagicCode.Log();

        if (newMeta.Version != IntegrityVerificationStorageVersion || newMeta.Version != previousMeta.Version)
            return ResultFs.UnsupportedVersion.Log();

        // Write the new meta.
        res = metaStorage.Write(0, SpanHelpers.AsReadOnlyByteSpan(in newMeta));
        if (res.IsFailure()) return res.Miss();

        res = metaStorage.Flush();
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public uint GetMasterHashSize()
    {
        return _meta.MasterHashSize;
    }

    public void GetLevelHashInfo(out HierarchicalIntegrityVerificationInformation outInfo)
    {
        outInfo = _meta.LevelHashInfo;
    }

    public Result Initialize(ref readonly ValueSubStorage metaStorage)
    {
        // Ensure the storage is large enough to hold the meta info.
        Result res = metaStorage.GetSize(out long metaSize);
        if (res.IsFailure()) return res.Miss();

        if (metaSize < Unsafe.SizeOf<HierarchicalIntegrityVerificationMetaInformation>())
            return ResultFs.InvalidSize.Log();

        // Set the storage and read the meta.
        _storage.Set(in metaStorage);
        res = _storage.Read(0, SpanHelpers.AsByteSpan(ref _meta));
        if (res.IsFailure()) return res.Miss();

        // Validate the meta magic and version.
        if (_meta.Magic != IntegrityVerificationStorageMagic)
            return ResultFs.IncorrectIntegrityVerificationMagicCode.Log();

        if ((_meta.Version & IntegrityVerificationStorageVersionMask) != IntegrityVerificationStorageVersion)
            return ResultFs.UnsupportedVersion.Log();

        return Result.Success;
    }

    public void FinalizeObject()
    {
        using var emptySubStorage = new ValueSubStorage();
        _storage.Set(in emptySubStorage);
    }
}

public class HierarchicalIntegrityVerificationStorage : IStorage
{
    [NonCopyableDisposable]
    public struct HierarchicalStorageInformation : IDisposable
    {
        public enum Storage
        {
            MasterStorage = 0,
            Layer1Storage = 1,
            Layer2Storage = 2,
            Layer3Storage = 3,
            Layer4Storage = 4,
            Layer5Storage = 5,
            DataStorage = 6
        }

        private ValueSubStorage[] _storages;

        public HierarchicalStorageInformation(ref readonly HierarchicalStorageInformation other)
        {
            _storages = new ValueSubStorage[(int)Storage.DataStorage + 1];

            for (int i = 0; i < _storages.Length; i++)
            {
                _storages[i] = new ValueSubStorage(in other._storages[i]);
            }
        }

        public HierarchicalStorageInformation()
        {
            _storages = new ValueSubStorage[(int)Storage.DataStorage + 1];
        }

        public void Dispose()
        {
            for (int i = 0; i < _storages.Length; i++)
            {
                _storages[i].Dispose();
            }
        }

        public ref ValueSubStorage this[int index]
        {
            get
            {
                Assert.SdkRequiresInRange(index, (int)Storage.MasterStorage, (int)Storage.DataStorage + 1);
                return ref _storages[index];
            }
        }

        public void SetMasterHashStorage(ref readonly ValueSubStorage storage) => _storages[(int)Storage.MasterStorage].Set(in storage);
        public void SetLayer1HashStorage(ref readonly ValueSubStorage storage) => _storages[(int)Storage.Layer1Storage].Set(in storage);
        public void SetLayer2HashStorage(ref readonly ValueSubStorage storage) => _storages[(int)Storage.Layer2Storage].Set(in storage);
        public void SetLayer3HashStorage(ref readonly ValueSubStorage storage) => _storages[(int)Storage.Layer3Storage].Set(in storage);
        public void SetLayer4HashStorage(ref readonly ValueSubStorage storage) => _storages[(int)Storage.Layer4Storage].Set(in storage);
        public void SetLayer5HashStorage(ref readonly ValueSubStorage storage) => _storages[(int)Storage.Layer5Storage].Set(in storage);
        public void SetDataStorage(ref readonly ValueSubStorage storage) => _storages[(int)Storage.DataStorage].Set(in storage);
    }

    internal const uint IntegrityVerificationStorageMagic = 0x43465649; // IVFC
    internal const uint IntegrityVerificationStorageVersion = 0x00020000;
    internal const uint IntegrityVerificationStorageVersionMask = 0xFFFF0000;

    internal const int HashSize = Sha256.DigestSize;

    internal const int AccessCountMax = 5;
    internal TimeSpan AccessTimeout => TimeSpan.FromMilliSeconds(10);

    private const sbyte BaseBufferLevel = 0x10;

    private FileSystemBufferManagerSet _bufferManagers;
    private SdkRecursiveMutex _mutex;
    private IntegrityVerificationStorage[] _integrityStorages;
    private BlockCacheBufferedStorage[] _bufferedStorages;
    private Semaphore _readSemaphore;
    private Semaphore _writeSemaphore;
    private long _dataSize;
    private int _layerCount;

    // LibHac addition
    private FileSystemServer _fsServer;

    private static readonly byte[][] KeyArray =
        [MasterKey.ToArray(), L1Key.ToArray(), L2Key.ToArray(), L3Key.ToArray(), L4Key.ToArray(), L5Key.ToArray()];

    public HierarchicalIntegrityVerificationStorage(FileSystemServer fsServer)
    {
        _integrityStorages = new IntegrityVerificationStorage[IntegrityMaxLayerCount - 1];
        _bufferedStorages = new BlockCacheBufferedStorage[IntegrityMaxLayerCount - 1];

        _dataSize = -1;
        _fsServer = fsServer;
    }

    public override void Dispose()
    {
        FinalizeObject();

        if (_integrityStorages is not null)
        {
            foreach (IntegrityVerificationStorage storage in _integrityStorages)
            {
                storage.Dispose();
            }
        }

        if (_bufferedStorages is not null)
        {
            foreach (BlockCacheBufferedStorage storage in _bufferedStorages)
            {
                storage.Dispose();
            }
        }

        base.Dispose();
    }

    public FileSystemBufferManagerSet GetBuffers()
    {
        return _bufferManagers;
    }

    public void GetParameters(out HierarchicalIntegrityVerificationStorageControlArea.InputParam outParam)
    {
        outParam = default;

        for (int i = 0; i < _layerCount - 2; i++)
        {
            outParam.LevelBlockSizes[i] = _integrityStorages[i].GetBlockSize();
        }
    }

    public bool IsInitialized()
    {
        return _dataSize >= 0;
    }

    public ValueSubStorage GetL1HashStorage()
    {
        return new ValueSubStorage(_bufferedStorages[_layerCount - 3], 0,
            DivideUp(_dataSize, GetL1HashVerificationBlockSize()));
    }

    public long GetL1HashVerificationBlockSize()
    {
        return _integrityStorages[_layerCount - 2].GetBlockSize();
    }

    public Result Initialize(in HierarchicalIntegrityVerificationInformation info,
        ref HierarchicalStorageInformation storageInfo, FileSystemBufferManagerSet buffers,
        IHash256GeneratorFactory hashGeneratorFactory, bool isHashSaltEnabled, SdkRecursiveMutex mutex,
        int maxDataCacheEntries, int maxHashCacheEntries, sbyte bufferLevel, bool isWritable, bool allowClearedBlocks)
    {
        return Initialize(in info, ref storageInfo, buffers, hashGeneratorFactory, isHashSaltEnabled, mutex, null, null,
            maxDataCacheEntries, maxHashCacheEntries, bufferLevel, isWritable, allowClearedBlocks);
    }

    public Result Initialize(in HierarchicalIntegrityVerificationInformation info,
        ref HierarchicalStorageInformation storageInfo, FileSystemBufferManagerSet buffers,
        IHash256GeneratorFactory hashGeneratorFactory, bool isHashSaltEnabled, SdkRecursiveMutex mutex,
        Semaphore readSemaphore, Semaphore writeSemaphore, int maxDataCacheEntries, int maxHashCacheEntries,
        sbyte bufferLevel, bool isWritable, bool allowClearedBlocks)
    {
        // Validate preconditions.
        Assert.SdkNotNull(buffers);
        Assert.SdkAssert(info.MaxLayers >= IntegrityMinLayerCount && info.MaxLayers <= IntegrityMaxLayerCount);

        // Set member variables.
        _layerCount = (int)info.MaxLayers;
        _bufferManagers = buffers;
        _mutex = mutex;
        _readSemaphore = readSemaphore;
        _writeSemaphore = writeSemaphore;

        {
            // If hash salt is enabled, generate it.
            var mac = new Optional<HashSalt>();
            if (isHashSaltEnabled)
            {
                mac.Set();
                HmacSha256.GenerateHmacSha256(mac.Value.Hash, info.HashSalt.HashRo, KeyArray[0]);
            }

            // Initialize the top level verification storage.
            _integrityStorages[0] = new IntegrityVerificationStorage();
            _integrityStorages[0].Initialize(in storageInfo[(int)HierarchicalStorageInformation.Storage.MasterStorage],
                in storageInfo[(int)HierarchicalStorageInformation.Storage.Layer1Storage],
                1 << info.Layers[0].BlockOrder, HashSize, _bufferManagers.Buffers[_layerCount - 2],
                hashGeneratorFactory, in mac, false, isWritable, allowClearedBlocks);
        }

        // Initialize the top level buffer storage.
        _bufferedStorages[0] = new BlockCacheBufferedStorage();
        Result res = _bufferedStorages[0].Initialize(_bufferManagers.Buffers[0], _mutex, _integrityStorages[0],
            info.Layers[0].Size, 1 << info.Layers[0].BlockOrder, maxHashCacheEntries, false, BaseBufferLevel, false,
            isWritable);

        if (!res.IsFailure())
        {
            int level;

            // Initialize the level storages.
            for (level = 0; level < _layerCount - 3; level++)
            {
                // If hash salt is enabled, generate it.
                var mac = new Optional<HashSalt>();
                if (isHashSaltEnabled)
                {
                    mac.Set();
                    HmacSha256.GenerateHmacSha256(mac.Value.Hash, info.HashSalt.HashRo, KeyArray[level + 1]);
                }

                // Initialize the verification storage.
                using (var hashStorage = new ValueSubStorage(_bufferedStorages[level], 0, info.Layers[level].Size))
                {
                    _integrityStorages[level + 1] = new IntegrityVerificationStorage();
                    _integrityStorages[level + 1].Initialize(in hashStorage, in storageInfo[level + 2],
                        1 << info.Layers[level + 1].BlockOrder, 1 << info.Layers[level].BlockOrder,
                        _bufferManagers.Buffers[_layerCount - 2], hashGeneratorFactory, in mac, false, isWritable,
                        allowClearedBlocks);
                }

                // Initialize the buffer storage.
                _bufferedStorages[level + 1] = new BlockCacheBufferedStorage();
                res = _bufferedStorages[level + 1].Initialize(_bufferManagers.Buffers[level + 1], _mutex,
                    _integrityStorages[level + 1], info.Layers[level + 1].Size, 1 << info.Layers[level + 1].BlockOrder,
                    maxHashCacheEntries, false, (sbyte)(BaseBufferLevel + (level + 1)), false, isWritable);

                if (res.IsFailure())
                {
                    // Cleanup initialized storages if we failed.
                    _integrityStorages[level + 1].FinalizeObject();

                    for (; level > 0; level--)
                    {
                        _bufferedStorages[level].FinalizeObject();
                        _integrityStorages[level].FinalizeObject();
                    }

                    break;
                }
            }

            if (!res.IsFailure())
            {
                // Initialize the final level storage.
                // If hash salt is enabled, generate it.
                var mac = new Optional<HashSalt>();
                if (isHashSaltEnabled)
                {
                    mac.Set();
                    HmacSha256.GenerateHmacSha256(mac.Value.Hash, info.HashSalt.HashRo, KeyArray[level + 1]);
                }

                // Initialize the verification storage.
                using (var hashStorage = new ValueSubStorage(_bufferedStorages[level], 0, info.Layers[level].Size))
                {
                    _integrityStorages[level + 1] = new IntegrityVerificationStorage();
                    _integrityStorages[level + 1].Initialize(in hashStorage,
                        in storageInfo[(int)HierarchicalStorageInformation.Storage.DataStorage],
                        1 << info.Layers[level + 1].BlockOrder, 1 << info.Layers[level].BlockOrder,
                        _bufferManagers.Buffers[_layerCount - 2], hashGeneratorFactory, in mac, true, isWritable,
                        allowClearedBlocks);
                }

                // Initialize the buffer storage.
                _bufferedStorages[level + 1] = new BlockCacheBufferedStorage();
                res = _bufferedStorages[level + 1].Initialize(_bufferManagers.Buffers[level + 1], _mutex,
                    _integrityStorages[level + 1], info.Layers[level + 1].Size, 1 << info.Layers[level + 1].BlockOrder,
                    maxDataCacheEntries, true, bufferLevel, true, isWritable);

                if (!res.IsFailure())
                {
                    _dataSize = info.Layers[level + 1].Size;
                    return Result.Success;
                }

                // Cleanup initialized storages if we failed.
                _integrityStorages[level + 1].FinalizeObject();

                for (; level > 0; level--)
                {
                    _bufferedStorages[level].FinalizeObject();
                    _integrityStorages[level].FinalizeObject();
                }
            }

            _bufferedStorages[0].FinalizeObject();
        }

        _integrityStorages[0].FinalizeObject();

        // Ensure we're uninitialized if we failed.
        _dataSize = -1;
        _bufferManagers = null;
        _mutex = null;

        return res;
    }

    public void FinalizeObject()
    {
        if (_dataSize >= 0)
        {
            _dataSize = 0;
            _bufferManagers = null;
            _mutex = null;

            for (int level = _layerCount - 2; level >= 0; level--)
            {
                _bufferedStorages[level].FinalizeObject();
                _integrityStorages[level].FinalizeObject();
            }

            _dataSize = -1;
        }
    }

    public override Result GetSize(out long size)
    {
        Assert.SdkRequires(_dataSize >= 0);

        size = _dataSize;
        return Result.Success;
    }

    public override Result SetSize(long size)
    {
        return ResultFs.UnsupportedSetSizeForHierarchicalIntegrityVerificationStorage.Log();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        Assert.SdkRequires(_dataSize >= 0);

        if (destination.Length == 0)
            return Result.Success;

        ref HierarchicalIntegrityVerificationStorageGlobals g =
            ref _fsServer.Globals.HierarchicalIntegrityVerificationStorage;

        _readSemaphore?.Acquire();

        try
        {
            if (!g.GlobalReadSemaphore.TimedAcquire(AccessTimeout))
            {
                for (int level = _layerCount - 2; level >= 0; level--)
                {
                    Result res = _bufferedStorages[level].Flush();
                    if (res.IsFailure()) return res.Miss();
                }

                g.GlobalReadSemaphore.Acquire();
            }

            try
            {
                Result res = _bufferedStorages[_layerCount - 2].Read(offset, destination);
                if (res.IsFailure()) return res.Miss();

                return Result.Success;
            }
            finally
            {
                g.GlobalReadSemaphore.Release();
            }
        }
        finally
        {
            _readSemaphore?.Release();
        }
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        Assert.SdkRequires(_dataSize >= 0);

        if (source.Length == 0)
            return Result.Success;

        ref HierarchicalIntegrityVerificationStorageGlobals g =
            ref _fsServer.Globals.HierarchicalIntegrityVerificationStorage;

        _writeSemaphore?.Acquire();

        try
        {
            if (!g.GlobalWriteSemaphore.TimedAcquire(AccessTimeout))
            {
                for (int level = _layerCount - 2; level >= 0; level--)
                {
                    Result res = _bufferedStorages[level].Flush();
                    if (res.IsFailure()) return res.Miss();
                }

                g.GlobalWriteSemaphore.Acquire();
            }

            try
            {
                Result res = _bufferedStorages[_layerCount - 2].Write(offset, source);
                if (res.IsFailure()) return res.Miss();

                return Result.Success;
            }
            finally
            {
                g.GlobalWriteSemaphore.Release();
            }
        }
        finally
        {
            _writeSemaphore?.Release();
        }
    }

    public override Result Flush()
    {
        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        switch (operationId)
        {
            case OperationId.FillZero:
            case OperationId.DestroySignature:
            {
                Result res = _bufferedStorages[_layerCount - 2]
                    .OperateRange(outBuffer, operationId, offset, size, inBuffer);
                if (res.IsFailure()) return res.Miss();

                return Result.Success;
            }
            case OperationId.InvalidateCache:
            case OperationId.QueryRange:
            {
                Result res = _bufferedStorages[_layerCount - 2]
                    .OperateRange(outBuffer, operationId, offset, size, inBuffer);
                if (res.IsFailure()) return res.Miss();

                return Result.Success;
            }
            default:
                return ResultFs.UnsupportedOperateRangeForHierarchicalIntegrityVerificationStorage.Log();
        }
    }

    public Result Commit()
    {
        for (int level = _layerCount - 2; level >= 0; level--)
        {
            Result res = _bufferedStorages[level].Commit();
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    public Result OnRollback()
    {
        for (int level = _layerCount - 2; level >= 0; level--)
        {
            Result res = _bufferedStorages[level].OnRollback();
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    public static void SetGenerateRandomFunction(FileSystemServer fsServer, RandomDataGenerator function)
    {
        fsServer.Globals.HierarchicalIntegrityVerificationStorage.GenerateRandom = function;
    }

    public static sbyte GetDefaultDataCacheBufferLevel(int maxLayers)
    {
        return (sbyte)(BaseBufferLevel + maxLayers - 2);
    }

    /// <summary>"<c>HierarchicalIntegrityVerificationStorage::Master</c>"</summary>
    public static ReadOnlySpan<byte> MasterKey => "HierarchicalIntegrityVerificationStorage::Master"u8;

    /// <summary>"<c>HierarchicalIntegrityVerificationStorage::L1</c>"</summary>
    public static ReadOnlySpan<byte> L1Key => "HierarchicalIntegrityVerificationStorage::L1"u8;

    /// <summary>"<c>HierarchicalIntegrityVerificationStorage::L2</c>"</summary>
    public static ReadOnlySpan<byte> L2Key => "HierarchicalIntegrityVerificationStorage::L2"u8;

    /// <summary>"<c>HierarchicalIntegrityVerificationStorage::L3</c>"</summary>
    public static ReadOnlySpan<byte> L3Key => "HierarchicalIntegrityVerificationStorage::L3"u8;

    /// <summary>"<c>HierarchicalIntegrityVerificationStorage::L4</c>"</summary>
    public static ReadOnlySpan<byte> L4Key => "HierarchicalIntegrityVerificationStorage::L4"u8;

    /// <summary>"<c>HierarchicalIntegrityVerificationStorage::L5</c>"</summary>
    public static ReadOnlySpan<byte> L5Key => "HierarchicalIntegrityVerificationStorage::L5"u8;
}