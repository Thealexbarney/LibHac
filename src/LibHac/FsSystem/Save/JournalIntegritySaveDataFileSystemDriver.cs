// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Fs;
using LibHac.Os;
using LibHac.Util;

namespace LibHac.FsSystem.Save;

public class JournalIntegritySaveDataFileSystemDriver : ProxyFileSystemWithRetryingBufferAllocation, IInternalStorageFileSystem
{
    private ValueSubStorage _baseStorage;
    private SdkRecursiveMutex _mutex;
    private IBufferManager _bufferManager;
    private FileSystemBufferManagerSet _bufferManagerSet;
    private JournalIntegritySaveDataFileSystem _fileSystem;

    public JournalIntegritySaveDataFileSystemDriver()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public static Result QueryDataBlockCount(out uint outCountDataBlock, long blockSize, long availableSize,
        long journalSize, int countExpandMax, uint version)
    {
        throw new NotImplementedException();
    }

    public static Result QueryTotalSize(out long outSizeTotal, long blockSize, uint countAvailableBlock,
        uint countJournalBlock, int countExpandMax, uint version)
    {
        // Todo: Implement
        outSizeTotal = 0;
        return Result.Success;
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
        throw new NotImplementedException();
    }

    public static long QueryExpandLogSize(long blockSize, uint availableBlockCount, uint journalBlockCount)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }

    public static Result CommitExpand(
        in ValueSubStorage baseStorage,
        in ValueSubStorage logStorage,
        long blockSize,
        IBufferManager bufferManager)
    {
        throw new NotImplementedException();
    }

    public static Result ReadExtraData(out JournalIntegritySaveDataFileSystem.ExtraData outData,
        in ValueSubStorage saveImageStorage, IBufferManager bufferManager, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(in ValueSubStorage baseStorage, IBufferManager bufferManager, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    public void FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public Result WriteExtraData(in JournalIntegritySaveDataFileSystem.ExtraData extraData)
    {
        throw new NotImplementedException();
    }

    public Result ReadExtraData(out JournalIntegritySaveDataFileSystem.ExtraData outExtraData)
    {
        throw new NotImplementedException();
    }

    public Result RollbackOnlyModified()
    {
        throw new NotImplementedException();
    }

    public static Result UpdateMac(in ValueSubStorage saveDataStorage, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    public long GetCounterForBundledCommit()
    {
        throw new NotImplementedException();
    }

    public Result CommitFileSystem()
    {
        throw new NotImplementedException();
    }

    public void ExtractParameters(out JournalIntegritySaveDataParameters outParams)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }

    public Result UpdateMac(IMacGenerator macGenerator)
    {
        throw new NotImplementedException();
    }
}