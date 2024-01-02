// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Fs;
using LibHac.Os;

namespace LibHac.FsSystem.Save;

public class IntegritySaveDataFileSystemDriver : ProxyFileSystemWithRetryingBufferAllocation
{
    private ValueSubStorage _baseStorage;
    private SdkRecursiveMutex _mutex;
    private IBufferManager _bufferManager;
    private FileSystemBufferManagerSet _bufferManagerSet;
    private IntegritySaveDataFileSystem _fileSystem;

    public IntegritySaveDataFileSystemDriver()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }


    public static Result QueryTotalSize(out long outSizeTotal, long sizeBlock, uint blockCount, uint version)
    {
        throw new NotImplementedException();
    }

    public static IntegritySaveDataParameters SetUpSaveDataParameters(long blockSize, long dataSize)
    {
        throw new NotImplementedException();
    }

    public static Result Format(in ValueSubStorage saveFileStorage, long blockSize, uint blockCount,
        IBufferManager bufferManager, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, RandomDataGenerator encryptionKeyGenerator,
        uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    public static Result ReadExtraData(out IntegritySaveDataFileSystem.ExtraData outExtraData,
        in ValueSubStorage saveFileStorage, IBufferManager bufferManager, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(in ValueSubStorage saveFileStorage, IBufferManager bufferManager,
        IMacGenerator macGenerator, IHash256GeneratorFactorySelector hashGeneratorFactorySelector)
    {
        throw new NotImplementedException();
    }

    public void FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public void ReadExtraData(out IntegritySaveDataFileSystem.ExtraData outExtraData)
    {
        throw new NotImplementedException();
    }

    public void WriteExtraData(in IntegritySaveDataFileSystem.ExtraData extraData)
    {
        throw new NotImplementedException();
    }
}