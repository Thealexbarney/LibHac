using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSystem.Buffers;
using LibHac.Os;
using LibHac.Util;

namespace LibHac.FsSystem.Save;

/// <summary>
/// Provides access to a <see cref="JournalIntegritySaveDataFileSystem"/>. Abstracts some operations such as expansion,
/// and retries operations if buffer allocation fails.
/// </summary>
/// <remarks>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
public class JournalIntegritySaveDataFileSystemDriver : ProxyFileSystemWithRetryingBufferAllocation, IInternalStorageFileSystem
{
    private ValueSubStorage _baseStorage;
    private SdkRecursiveMutex _mutex;
    private IBufferManager _bufferManager;
    private FileSystemBufferManagerSet _bufferManagerSet;
    private JournalIntegritySaveDataFileSystem _fileSystem;

    public JournalIntegritySaveDataFileSystemDriver()
    {
        _baseStorage = new ValueSubStorage();
        _mutex = new SdkRecursiveMutex();
        _fileSystem = new JournalIntegritySaveDataFileSystem();
    }

    public override void Dispose()
    {
        FinalizeObject();
        _fileSystem.Dispose();
        _baseStorage.Dispose();
        base.Dispose();
    }

    public static Result QueryDataBlockCount(out uint outCountDataBlock, long blockSize, long totalSize,
        long journalSize, int countExpandMax, uint version)
    {
        outCountDataBlock = 0;

        uint lowerBound = 0;
        uint upperBound = (uint)(totalSize / blockSize);
        uint journalBlockCount = (uint)(journalSize / blockSize);

        int iterationsRemaining = 32;

        // Do a binary search to find the maximum number of data blocks you can have without going over the total
        // save image size limit.
        uint midpoint;
        do
        {
            if (upperBound == lowerBound)
                break;
            if (iterationsRemaining-- == 0)
                break;

            midpoint = (upperBound + lowerBound) / 2;
            Result res = QueryTotalSize(out long sizeQuery, blockSize, midpoint, journalBlockCount, countExpandMax, version);
            if (res.IsFailure()) return res.Miss();

            if (sizeQuery > totalSize)
                upperBound = midpoint;
            else
                lowerBound = midpoint;
        } while (upperBound - lowerBound > 1 || midpoint != lowerBound);

        {
            Result res = QueryTotalSize(out long sizeQuery, blockSize, lowerBound, journalBlockCount, countExpandMax, version);
            if (res.IsFailure()) return res.Miss();

            if (sizeQuery > totalSize)
            {
                return ResultFs.UsableSpaceNotEnough.Log();
            }

            outCountDataBlock = lowerBound;
        }

        return Result.Success;
    }

    public static Result QueryTotalSize(out long outSizeTotal, long blockSize, uint countAvailableBlock,
        uint countJournalBlock, int countExpandMax, uint version)
    {
        return JournalIntegritySaveDataFileSystem.QuerySize(out outSizeTotal, blockSize, countAvailableBlock,
            countJournalBlock, countExpandMax, version).Ret();
    }

    public static Result Format(
        in ValueSubStorage saveFileStorage,
        long blockSize,
        uint blockCount,
        uint journalBlockCount,
        int countExpandMax,
        IBufferManager bufferManager,
        IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector,
        Optional<HashSalt> hashSalt,
        RandomDataGenerator encryptionKeyGenerator,
        uint version)
    {
        // Create a local copy of the SubStorage so it can be captured for use in a local function or lambda
        using var tempSaveFileStorage = new ValueSubStorage(in saveFileStorage);

        var locker = new SdkRecursiveMutex();

        var bufferManagerSet = new FileSystemBufferManagerSet();
        for (int i = 0; i < bufferManagerSet.Buffers.Length; i++)
        {
            bufferManagerSet.Buffers[i] = bufferManager;
        }

        return BufferManagerUtility.DoContinuouslyUntilBufferIsAllocated(FormatImpl).Ret();

        Result FormatImpl()
        {
            using var scopedRegistration = new ScopedBufferManagerContextRegistration();

            return JournalIntegritySaveDataFileSystem.Format(in tempSaveFileStorage, blockSize, blockCount,
                journalBlockCount, countExpandMax, bufferManagerSet, bufferManager, locker, macGenerator,
                hashGeneratorFactorySelector, hashSalt, encryptionKeyGenerator, version).Ret();
        }
    }

    public static long QueryExpandLogSize(long blockSize, uint availableBlockCount, uint journalBlockCount)
    {
        long dataSize = ((long)journalBlockCount + availableBlockCount) * blockSize;
        return BitUtil.DivideUp(dataSize, 0x2000000) * 0x10000 + 0x100000;
    }

    public static Result OperateExpand(
        in ValueSubStorage baseStorage,
        in ValueSubStorage logStorage,
        long blockSize,
        uint availableBlockCount,
        uint journalBlockCount,
        IBufferManager bufferManager,
        IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector,
        uint minimumVersion)
    {
        using var tempBaseStorage = new ValueSubStorage(in baseStorage);
        using var tempLogStorage = new ValueSubStorage(in logStorage);

        var locker = new SdkRecursiveMutex();

        return BufferManagerUtility.DoContinuouslyUntilBufferIsAllocated(OperateImpl).Ret();

        Result OperateImpl()
        {
            using var scopedRegistration = new ScopedBufferManagerContextRegistration();
            using var unionStorage = new UnionStorage();

            Result res = tempLogStorage.GetSize(out long logStorageSize);
            if (res.IsFailure()) return res.Miss();

            using var storageBufferingLog = new BufferedStorage();
            res = storageBufferingLog.Initialize(in tempLogStorage, bufferManager, (int)blockSize, bufferCount: 2);
            if (res.IsFailure()) return res.Miss();

            using var bufferedLogStorage = new ValueSubStorage(storageBufferingLog, 0, logStorageSize);

            res = UnionStorage.Format(in bufferedLogStorage, blockSize);
            if (res.IsFailure()) return res.Miss();

            res = unionStorage.Initialize(in tempBaseStorage, in bufferedLogStorage, blockSize);
            if (res.IsFailure()) return res.Miss();

            res = unionStorage.GetSize(out long sizeUnionStorage);
            if (res.IsFailure()) return res.Miss();

            using var tempUnionStorage = new ValueSubStorage(unionStorage, 0, sizeUnionStorage);
            res = JournalIntegritySaveDataFileSystem.Expand(in tempUnionStorage, availableBlockCount, journalBlockCount,
                bufferManager, locker, macGenerator, hashGeneratorFactorySelector, minimumVersion);
            if (res.IsFailure()) return res.Miss();

            res = unionStorage.Freeze();
            if (res.IsFailure()) return res.Miss();

            res = unionStorage.Flush();
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
    }

    public static Result CommitExpand(
        in ValueSubStorage baseStorage,
        in ValueSubStorage logStorage,
        long blockSize,
        IBufferManager bufferManager)
    {
        using var tempBaseStorage = new ValueSubStorage(in baseStorage);
        using var tempLogStorage = new ValueSubStorage(in logStorage);

        return BufferManagerUtility.DoContinuouslyUntilBufferIsAllocated(CommitImpl).Ret();

        Result CommitImpl()
        {
            using var scopedRegistration = new ScopedBufferManagerContextRegistration();
            using var unionStorage = new UnionStorage();

            Result res = tempBaseStorage.GetSize(out long baseStorageSize);
            if (res.IsFailure()) return res.Miss();

            using var storageBufferingBase = new BufferedStorage();
            res = storageBufferingBase.Initialize(in tempBaseStorage, bufferManager, (int)blockSize, bufferCount: 16);
            if (res.IsFailure()) return res.Miss();

            res = tempLogStorage.GetSize(out long logStorageSize);
            if (res.IsFailure()) return res.Miss();

            using var storageBufferingLog = new BufferedStorage();
            res = storageBufferingBase.Initialize(in tempLogStorage, bufferManager, (int)blockSize, bufferCount: 2);
            if (res.IsFailure()) return res.Miss();

            using var bufferedBaseStorage = new ValueSubStorage(storageBufferingBase, 0, baseStorageSize);
            using var bufferedLogStorage = new ValueSubStorage(storageBufferingLog, 0, logStorageSize);

            res = unionStorage.Initialize(in bufferedBaseStorage, in bufferedLogStorage, blockSize);
            if (res.IsFailure()) return res.Miss();

            res = unionStorage.Commit();
            if (res.IsFailure()) return res.Miss();

            res = unionStorage.Flush();
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
    }

    public static Result ReadExtraData(out JournalIntegritySaveDataFileSystem.ExtraData outData,
        in ValueSubStorage saveImageStorage, IBufferManager bufferManager, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, uint minimumVersion)
    {
        UnsafeHelpers.SkipParamInit(out outData);

        using var tempSaveImageStorage = new ValueSubStorage(in saveImageStorage);
        JournalIntegritySaveDataFileSystem.ExtraData extraData = default;

        Result res = BufferManagerUtility.DoContinuouslyUntilBufferIsAllocated(ReadExtraDataImpl).Ret();
        if (res.IsFailure()) return res.Miss();

        outData = extraData;
        return Result.Success;

        Result ReadExtraDataImpl()
        {
            using var scopedRegistration = new ScopedBufferManagerContextRegistration();

            return JournalIntegritySaveDataFileSystem.ReadExtraData(out extraData, in tempSaveImageStorage,
                bufferManager, macGenerator, hashGeneratorFactorySelector, minimumVersion).Ret();
        }
    }

    public Result Initialize(in ValueSubStorage baseStorage, IBufferManager bufferManager, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, uint minimumVersion)
    {
        Assert.SdkRequiresNotNull(bufferManager);

        _baseStorage.Set(in baseStorage);
        _bufferManager = bufferManager;
        _bufferManagerSet = new FileSystemBufferManagerSet();

        for (int i = 0; i < _bufferManagerSet.Buffers.Length; i++)
        {
            _bufferManagerSet.Buffers[i] = bufferManager;
        }

        using var scopedRegistration = new ScopedBufferManagerContextRegistration();
        BufferManagerUtility.EnableBlockingBufferManagerAllocation();

        Result res = _fileSystem.Initialize(in _baseStorage, _bufferManagerSet, _bufferManager, _mutex, macGenerator,
            hashGeneratorFactorySelector, minimumVersion);
        if (res.IsFailure()) return res.Miss();

        base.Initialize(_fileSystem);
        return Result.Success;
    }

    public new void FinalizeObject()
    {
        using var scopedRegistration = new ScopedBufferManagerContextRegistration();
        BufferManagerUtility.EnableBlockingBufferManagerAllocation();

        _fileSystem.FinalizeObject();
        base.FinalizeObject();
    }

    public Result WriteExtraData(in JournalIntegritySaveDataFileSystem.ExtraData extraData)
    {
        return _fileSystem.WriteExtraData(in extraData).Ret();
    }

    public void ReadExtraData(out JournalIntegritySaveDataFileSystem.ExtraData outExtraData)
    {
        _fileSystem.ReadExtraData(out outExtraData);
    }

    public Result RollbackOnlyModified()
    {
        return BufferManagerUtility.DoContinuouslyUntilBufferIsAllocated(RollbackOnlyModifiedImpl).Ret();

        Result RollbackOnlyModifiedImpl()
        {
            using var scopedRegistration = new ScopedBufferManagerContextRegistration();

            return _fileSystem.RollbackOnlyModified().Ret();
        }
    }

    public static Result UpdateMac(in ValueSubStorage saveDataStorage, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, uint minimumVersion)
    {
        return JournalIntegritySaveDataFileSystem
            .UpdateMac(in saveDataStorage, macGenerator, hashGeneratorFactorySelector, minimumVersion).Ret();
    }

    public long GetCounterForBundledCommit()
    {
        return _fileSystem.GetCounterForBundledCommit();
    }

    public Result CommitFileSystem()
    {
        return Commit().Ret();
    }

    public void ExtractParameters(out JournalIntegritySaveDataParameters outParams)
    {
        outParams = default;

        _fileSystem.ExtractParameters(out outParams.BlockSize, out outParams.CountDataBlock,
            out outParams.CountJournalBlock, out outParams.CountExpandMax, out outParams.Version,
            out outParams.IsMetaSetVerificationEnabled);
    }

    public static JournalIntegritySaveDataParameters SetUpSaveDataParameters(long blockSize, long dataSize, long journalSize)
    {
        return new JournalIntegritySaveDataParameters()
        {
            // Align the data sizes up to a multiple of the block size
            CountDataBlock = (uint)((dataSize + blockSize - 1) / blockSize),
            CountJournalBlock = (uint)((journalSize + blockSize - 1) / blockSize),
            BlockSize = blockSize,
            CountExpandMax = 1
        };
    }

    public Result AcceptVisitor(IInternalStorageFileSystemVisitor visitor)
    {
        Assert.SdkRequiresNotNull(visitor);

        return BufferManagerUtility.DoContinuouslyUntilBufferIsAllocated(AcceptVisitorImpl).Ret();

        Result AcceptVisitorImpl()
        {
            using var scopedRegistration = new ScopedBufferManagerContextRegistration();
            BufferManagerUtility.EnableBlockingBufferManagerAllocation();

            return _fileSystem.AcceptVisitor(visitor).Ret();
        }
    }

    public Result UpdateMac(IMacGenerator macGenerator)
    {
        Assert.SdkRequiresNotNull(macGenerator);

        return _fileSystem.UpdateMacAndCommit(macGenerator).Ret();
    }
}