// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Save;
using LibHac.Os;

namespace LibHac.FsSystem.Save;

public struct IntegritySaveDataParameters
{
    public uint BlockCount;
    public long BlockSize;
}

file static class Anonymous
{
    public static Result GetHashAlgorithmTypeImpl(out HashAlgorithmType outType, in IntegritySaveDataFileSystem.FileSystemLayoutHeader header)
    {
        throw new NotImplementedException();
    }

    public static HashAlgorithmType GetHashAlgorithmTypeImpl(uint version)
    {
        throw new NotImplementedException();
    }

    public static Result CheckVersion(uint version)
    {
        throw new NotImplementedException();
    }

    public static bool IsDataEncryptionEnabled(in IntegritySaveDataFileSystem.FileSystemLayoutHeader header)
    {
        throw new NotImplementedException();
    }

    public static bool IsDataEncryptionEnabled(uint version)
    {
        throw new NotImplementedException();
    }

    public static Result InitializeEncryptedStorage(ref ValueSubStorage outStorage,
        ref UniqueRef<IStorage> encryptionStorage, ref UniqueRef<IStorage> alignmentMatchingEncryptionStorage,
        in ValueSubStorage baseStorage, in IntegritySaveDataFileSystem.FileSystemLayoutHeader layoutHeader,
        ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2, long blockSize)
    {
        throw new NotImplementedException();
    }
}

public class IntegritySaveDataFileSystem : IFileSystem
{
    public struct ExtraData { }

    public struct FileSystemLayoutHeader { }

    public struct MasterHeader { }

    public static Result GetHashAlgorithmType(out HashAlgorithmType outType, in FileSystemLayoutHeader layoutHeader)
    {
        throw new NotImplementedException();
    }

    public static HashAlgorithmType GetHashAlgorithmType(uint version)
    {
        throw new NotImplementedException();
    }

    public class ControlAreaHolder : IDisposable
    {
        private byte[] _header;
        private UniqueRef<MemoryStorage> _storage;
        private ulong _bufferSize;

        public ControlAreaHolder()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Result Reset(in MasterHeader header)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public void Commit(ref MasterHeader header)
        {
            throw new NotImplementedException();
        }

        public ref ExtraData GetExtraData()
        {
            throw new NotImplementedException();
        }

        public ref readonly ValueSubStorage GetIntegrityControlAreaStorage()
        {
            throw new NotImplementedException();
        }

        public ref readonly ValueSubStorage GetSaveDataControlAreaStorage()
        {
            throw new NotImplementedException();
        }

        public ref readonly ValueSubStorage GetLayeredHashIntegrityMetaControlAreaStorage()
        {
            throw new NotImplementedException();
        }

        public ref readonly ValueSubStorage GetMasterHashStorage()
        {
            throw new NotImplementedException();
        }

        public ref readonly ValueSubStorage GetIntegrityMetaMasterHashStorage()
        {
            throw new NotImplementedException();
        }
    }

    private ControlAreaHolder _controlArea;
    private ValueSubStorage _masterHeaderStorage;
    private BufferedStorage _bufferedFileSystemStorage;
    private BufferedStorage _bufferedMetaStorage;
    private ValueSubStorage _fileSystemStorage;
    private IntegritySaveDataStorage _integrityStorage;
    private HierarchicalIntegrityVerificationStorage _integrityMetaStorage;
    private SaveDataFileSystemCore _saveFileSystemCore;
    private IBufferManager _bufferManager;
    private long _masterHeaderCacheHandle;
    private SdkRecursiveMutex _mutex;
    private bool _isInitialized;
    private bool _isExtraDataModified;
    private bool _isFirstControlAreaMounted;
    private IMacGenerator _macGenerator;
    private IHash256GeneratorFactorySelector _hashGeneratorFactorySelector;
    private ValueSubStorage _saveDataImageFileStorage;
    private UniqueRef<IStorage> _encryptedStorage;
    private UniqueRef<IStorage> _alignmentMatchingEncryptedStorage;

    public IntegritySaveDataFileSystem()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    private SdkRecursiveMutex GetLocker()
    {
        throw new NotImplementedException();
    }

    public static void SetGenerateRandomFunction(FileSystemClient fs, RandomDataGenerator randomFunction)
    {
        throw new NotImplementedException();
    }
    
    public static void SetVersionSupported(FileSystemClient fs, uint versionMin, uint versionMax)
    {
        throw new NotImplementedException();
    }

    private Result CommitFileSystemCore()
    {
        throw new NotImplementedException();
    }

    private static Result FormatFileSystemLayoutHeader(out FileSystemLayoutHeader outFileSystemLayoutHeader,
        long blockSize, uint blockCount, HashAlgorithmType hashAlgorithmType, uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    private Result GetMasterHeader(ref MasterHeader masterHeader)
    {
        throw new NotImplementedException();
    }

    private void StoreMasterHeader()
    {
        throw new NotImplementedException();
    }

    public static Result Format(in ValueSubStorage saveFileStorage, long blockSize, uint blockCount,
        FileSystemBufferManagerSet buffers, IBufferManager bufferManager, SdkRecursiveMutex mutex,
        IMacGenerator macGenerator, IHash256GeneratorFactorySelector hashGeneratorFactorySelector,
        RandomDataGenerator encryptionKeyGenerator, uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    public static Result VerifyMasterHeader(
        in ValueSubStorage saveImageStorage,
        IBufferManager bufferManager,
        IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector)
    {
        throw new NotImplementedException();
    }

    public static Result ReadExtraData(
        out ExtraData outData,
        in ValueSubStorage saveImageStorage,
        IBufferManager bufferManager,
        IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector)
    {
        throw new NotImplementedException();
    }

    public static Result QuerySize(out long outSize, long blockSize, int blockCount, uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    private static Result Sign(Span<byte> outBuffer, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, ReadOnlySpan<byte> data)
    {
        throw new NotImplementedException();
    }

    private static Result Verify(IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector,
        ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        throw new NotImplementedException();
    }

    private static bool VerifyMasterHeaderContent(ref MasterHeader header,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector)
    {
        throw new NotImplementedException();
    }

    private static Result ReadMasterControlArea(
        out MasterHeader outHeader,
        out bool outIsFirstControlAreaMounted,
        long headerBufferSize,
        in ValueSubStorage baseStorage,
        IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector)
    {
        throw new NotImplementedException();
    }

    private Result MountSaveData(in FileSystemLayoutHeader layoutHeader, in SubStorage saveImageStorage, long blockSize,
        FileSystemBufferManagerSet integrityCacheBufferSet, IBufferManager cacheBuffer, SdkRecursiveMutex mutex)
    {
        throw new NotImplementedException();
    }

    private Result MountIntegritySaveData(in FileSystemLayoutHeader layoutHeader,
        FileSystemBufferManagerSet integrityCacheBufferSet, IBufferManager cacheBuffer, SdkRecursiveMutex mutex)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(
        in SubStorage saveImageStorage,
        FileSystemBufferManagerSet integrityCacheBufferSet,
        IBufferManager cacheBuffer,
        SdkRecursiveMutex mutex,
        IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector)
    {
        throw new NotImplementedException();
    }

    public void FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public Result WriteExtraData(in ExtraData extraData)
    {
        throw new NotImplementedException();
    }

    public Result ReadExtraData(out ExtraData outExtraData)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCreateFile(ref readonly Path path, long size, CreateFileOptions option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCreateDirectory(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, ref readonly Path path, OpenMode mode)
    {
        throw new NotImplementedException();
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, ref readonly Path path, OpenDirectoryMode mode)
    {
        throw new NotImplementedException();
    }

    protected override Result DoDeleteFile(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoDeleteDirectory(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoDeleteDirectoryRecursively(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCleanDirectoryRecursively(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoRenameFile(ref readonly Path currentPath, ref readonly Path newPath)
    {
        throw new NotImplementedException();
    }

    protected override Result DoRenameDirectory(ref readonly Path currentPath, ref readonly Path newPath)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCommit()
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetFileSystemAttribute(out FileSystemAttribute outAttribute)
    {
        throw new NotImplementedException();
    }
}

public class IntegritySaveDataFile : IFile
{
    private UniqueRef<IFile> _file;
    private IntegritySaveDataFileSystem _fileSystem;

    internal IntegritySaveDataFile(ref UniqueRef<IFile> file, IntegritySaveDataFileSystem fileSystem)
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetSize(out long size)
    {
        throw new NotImplementedException();
    }

    protected override Result DoSetSize(long size)
    {
        throw new NotImplementedException();
    }

    protected override Result DoFlush()
    {
        throw new NotImplementedException();
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }
}

public class IntegritySaveDataDirectory : IDirectory
{
    private IntegritySaveDataFileSystem _fileSystem;
    private UniqueRef<IDirectory> _directory;

    internal IntegritySaveDataDirectory(ref UniqueRef<IDirectory> directory, IntegritySaveDataFileSystem fileSystem)
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetEntryCount(out long entryCount)
    {
        throw new NotImplementedException();
    }
}