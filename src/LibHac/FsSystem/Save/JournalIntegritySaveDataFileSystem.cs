// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Save;
using LibHac.Os;
using LibHac.Util;

namespace LibHac.FsSystem.Save;

public struct JournalIntegritySaveDataParameters
{
    public uint CountDataBlock;
    public uint CountJournalBlock;
    public long BlockSize;
    public int CountExpandMax;
    public uint Version;
    public bool IsMetaSetVerificationEnabled;
}

file static class Anonymous
{
    public static Result CheckVersion(uint version)
    {
        throw new NotImplementedException();
    }

    public static bool IsSaveDataMetaVerificationEnabled(in JournalIntegritySaveDataFileSystem.FileSystemLayoutHeader header)
    {
        throw new NotImplementedException();
    }

    public static bool IsDataEncryptionEnabled(in JournalIntegritySaveDataFileSystem.FileSystemLayoutHeader header)
    {
        throw new NotImplementedException();
    }

    public static bool IsDataEncryptionEnabled(uint version)
    {
        throw new NotImplementedException();
    }

    public static bool IsMetaSetVerificationEnabledImpl(in JournalIntegritySaveDataFileSystem.FileSystemLayoutHeader header)
    {
        throw new NotImplementedException();
    }

    public static bool IsMetaSetVerificationEnabledImpl(uint version)
    {
        throw new NotImplementedException();
    }

    public static bool IsHashSaltEnabledImpl(in JournalIntegritySaveDataFileSystem.FileSystemLayoutHeader header)
    {
        throw new NotImplementedException();
    }

    public static bool IsHashSaltEnabledImpl(uint version)
    {
        throw new NotImplementedException();
    }

    public static Result GetHashAlgorithmTypeImpl(out HashAlgorithmType outType, in JournalIntegritySaveDataFileSystem.FileSystemLayoutHeader header)
    {
        throw new NotImplementedException();
    }

    public static HashAlgorithmType GetHashAlgorithmTypeImpl(uint version)
    {
        throw new NotImplementedException();
    }

    public static int GetMetaRemapEntryCount(uint version, bool isMetaSetVerificationEnabled)
    {
        throw new NotImplementedException();
    }

    public static long GetRemappedFileSystemEntryTableSize(uint version, int countExpandMax)
    {
        throw new NotImplementedException();
    }

    public static long GetRemappedMetaEntryTableSize(uint version, int countExpandMax)
    {
        throw new NotImplementedException();
    }

    public static long GetRemappedSaveDataMetaAndJournalMetaEntryTableSize(uint version, int countExpandMax)
    {
        throw new NotImplementedException();
    }

    public static int GetExpandCountMaxFromRemappedFileSystem(uint version, int mapUpdateCount)
    {
        throw new NotImplementedException();
    }

    public static int GetExpandCountMaxFromRemappedMeta(uint version, bool isMetaSetVerificationEnabled, int mapUpdateCount)
    {
        throw new NotImplementedException();
    }

    public static int GetExpandCountMaxFromSaveDataMetaAndJournalMetaRemappedMeta(uint version, int mapUpdateCount)
    {
        throw new NotImplementedException();
    }

    public static Result InitializeEncryptedStorage(ref ValueSubStorage outStorage,
        ref UniqueRef<IStorage> encryptionStorage, ref UniqueRef<IStorage> alignmentMatchingEncryptionStorage,
        in ValueSubStorage baseStorage, in JournalIntegritySaveDataFileSystem.FileSystemLayoutHeader layoutHeader,
        ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2, long blockSize)
    {
        throw new NotImplementedException();
    }

    public static Result GenerateStorageHash256(Span<byte> outValue, in ValueSubStorage storage,
        IHash256GeneratorFactory hashGeneratorFactory)
    {
        throw new NotImplementedException();
    }
}

public class JournalIntegritySaveDataFileSystem : IFileSystem
{
    public struct ExtraData { }

    public struct FileSystemLayoutHeader { }

    public struct MasterHeader
    {
        public struct CommitData
        {
            public long Counter;
        }

        public struct CommitData2 { }
    }

    public class ControlAreaHolder : IDisposable
    {
        private byte[] _header;
        private UniqueRef<MemoryStorage> _storage;
        private ulong _bufferSize;
        private ulong _bitmapSize;
        private int _version;
        private HashAlgorithmType _hashAlgorithmType;
        private bool _hashSaltEnabled;
        private bool _metaSetVerificationEnabled;

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

        public HashAlgorithmType GetHashAlgorithmType()
        {
            throw new NotImplementedException();
        }

        public int GetVersion()
        {
            throw new NotImplementedException();
        }

        public bool IsHashSaltEnabled()
        {
            throw new NotImplementedException();
        }

        public bool IsMetaSetVerificationEnabled()
        {
            throw new NotImplementedException();
        }

        public ref MasterHeader.CommitData GetCommitData()
        {
            throw new NotImplementedException();
        }

        public ref MasterHeader.CommitData2 GetCommitData2()
        {
            throw new NotImplementedException();
        }

        public ref ExtraData GetExtraData()
        {
            throw new NotImplementedException();
        }

        public ref readonly ValueSubStorage GetDuplexControlAreaStorage()
        {
            throw new NotImplementedException();
        }

        public ref readonly ValueSubStorage GetFileSystemRemapControlAreaStorage()
        {
            throw new NotImplementedException();
        }

        public ref readonly ValueSubStorage GetMetaRemapControlAreaStorage()
        {
            throw new NotImplementedException();
        }

        public ref readonly ValueSubStorage GetIntegrityControlAreaStorage()
        {
            throw new NotImplementedException();
        }

        public ref readonly ValueSubStorage GetJournalControlAreaStorage()
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

        public ref readonly ValueSubStorage GetSaveDataMetaAndJournalMetaRemapControlAreaStorage()
        {
            throw new NotImplementedException();
        }

        public ref readonly ValueSubStorage GetMasterHashStorage()
        {
            throw new NotImplementedException();
        }

        public ref readonly ValueSubStorage GetL1BitmapStorage()
        {
            throw new NotImplementedException();
        }

        public ref readonly ValueSubStorage GetIntegrityMetaMasterHashStorage()
        {
            throw new NotImplementedException();
        }

        public ref readonly ValueSubStorage GetFileSystemRemapMetaHashStorage()
        {
            throw new NotImplementedException();
        }

        public ref readonly ValueSubStorage GetMetaRemapMetaHashStorage()
        {
            throw new NotImplementedException();
        }

        public ref readonly ValueSubStorage GetSaveDataMetaAndJournalMetaRemapMetaHashStorage()
        {
            throw new NotImplementedException();
        }
    }

    private ControlAreaHolder _controlArea;
    private ValueSubStorage _masterHeaderStorage;
    private BufferedStorage _bufferedFileSystemStorage;
    private BufferedStorage _bufferedMetaStorage;
    private RemapStorage _remappedFileSystemStorage;
    private RemapStorage _remappedMetaStorage;
    private HierarchicalDuplexStorage _duplexStorage;
    private JournalIntegritySaveDataStorage _journalIntegrityStorage;
    private HierarchicalIntegrityVerificationStorage _metaIntegrityStorage;
    private SaveDataFileSystemCore _saveFileSystemCore;
    private IBufferManager _bufferManager;
    private long _masterHeaderCacheHandle;
    private SdkRecursiveMutex _mutex;
    private bool _isInitialized;
    private bool _isFirstControlAreaMounted;
    private bool _isProvisionallyCommitted;
    private bool _isExtraDataModified;
    private IMacGenerator _macGenerator;
    private IHash256GeneratorFactorySelector _hashGeneratorFactorySelector;
    private uint _version;
    private ValueSubStorage _saveDataImageFileStorage;
    private UniqueRef<IStorage> _encryptedStorage;
    private UniqueRef<IStorage> _alignmentMatchingEncryptedStorage;
    private UniqueRef<BufferedStorage> _bufferedMetaSetStorage;
    private UniqueRef<RemapStorage> _remappedSaveDataMetaAndJournalMetaStorage;

    public JournalIntegritySaveDataFileSystem()
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

    public static void SetGenerateRandomFunction(RandomDataGenerator func)
    {
        throw new NotImplementedException();
    }

    private Result CommitFileSystemCore(IMacGenerator macGenerator, long counterForBundledCommit)
    {
        throw new NotImplementedException();
    }

    private Result FormatFileSystemLayoutHeader(
        out FileSystemLayoutHeader outFileSystemLayoutHeader,
        uint version,
        long remappedFileSystemTableSize,
        long remappedMetaTableSize,
        long remappedSaveDataMetaAndJournalMetaEntryTableSize,
        long blockSize,
        uint countAvailableBlock,
        uint countJournalBlock,
        bool isHashSaltEnabled,
        HashAlgorithmType hashAlgorithmType)
    {
        throw new NotImplementedException();
    }

    private void StoreMasterHeader()
    {
        throw new NotImplementedException();
    }

    private Result GetMasterHeader(ref MasterHeader masterHeader, uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    public static Result Format(
        in ValueSubStorage saveImageStorage,
        long sizeBlock,
        uint countDataBlock,
        uint countJournalBlock,
        int countExpandMax,
        FileSystemBufferManagerSet buffers,
        IBufferManager bufferManager,
        SdkRecursiveMutex locker,
        IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector,
        Optional<HashSalt> hashSalt,
        RandomDataGenerator encryptionKeyGenerator,
        uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    public static Result Expand(
        in ValueSubStorage saveImageStorage,
        uint countDataBlock,
        uint countJournalBlock,
        IBufferManager bufferManager,
        SdkRecursiveMutex locker,
        IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector,
        uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    public static Result VerifyMasterHeader(
        in ValueSubStorage saveImageStorage,
        IBufferManager bufferManager,
        IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector,
        uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    public static Result ReadExtraData(
        out ExtraData outData,
        in ValueSubStorage saveImageStorage,
        IBufferManager bufferManager,
        IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector,
        uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    public void ExtractParameters(
        out long outBlockSize,
        out uint outCountDataBlock,
        out uint outCountJournalBlock,
        out int outCountExpandMax,
        out uint outVersion,
        out bool outIsMetaSetVerificationEnabled)
    {
        throw new NotImplementedException();
    }

    private static Result ExpandMeta(
        out bool outNeedsCommit,
        in ValueSubStorage storageMetaRemapMeta,
        in ValueSubStorage storageRemappedFileSystem,
        in ValueSubStorage storageControlArea,
        in FileSystemLayoutHeader layoutHeaderOld,
        in FileSystemLayoutHeader layoutHeaderNew,
        long blockSize,
        IBufferManager bufferManager,
        IHash256GeneratorFactory hashGeneratorFactory,
        SdkRecursiveMutex mutex)
    {
        throw new NotImplementedException();
    }

    public static Result QuerySize(out long outSizeTotal, long sizeBlock, int countAvailableBlock,
        int countJournalBlock, int countExpandMax, uint minimumVersion)
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
        out bool outIsProvisionallyCommitted,
        out bool outIsFirstControlAreaMounted,
        long headerBufferSize,
        in ValueSubStorage baseStorage,
        IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector,
        uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    private static Result UpdateMasterControlAreaMac(in ValueSubStorage saveImageStorage, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    private Result MountSaveData(in FileSystemLayoutHeader layoutHeader, in SubStorage saveImageStorage, long blockSize,
        FileSystemBufferManagerSet integrityCacheBufferSet, IBufferManager bufferManager, SdkRecursiveMutex mutex)
    {
        throw new NotImplementedException();
    }

    private Result MountIntegritySaveData(in FileSystemLayoutHeader layoutHeader, long blockSize,
        FileSystemBufferManagerSet integrityCacheBufferSet, IBufferManager bufferManager, SdkRecursiveMutex mutex)
    {
        throw new NotImplementedException();
    }

    private Result RemountSaveData()
    {
        throw new NotImplementedException();
    }

    public Result Initialize(
        in SubStorage saveImageStorage,
        FileSystemBufferManagerSet integrityCacheBufferSet,
        IBufferManager duplicateCacheBuffer,
        SdkRecursiveMutex mutex,
        IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector,
        uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    public void FinalizeObject()
    {
        throw new NotImplementedException();
    }

    private Result RollbackFileSystemCore(bool rollbackProvisionalCommits)
    {
        throw new NotImplementedException();
    }

    public static Result UpdateMac(in SubStorage saveImageStorage, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    public static Result RecoverMasterHeader(in SubStorage saveImageStorage, IBufferManager bufferManager,
        IMacGenerator macGenerator, IHash256GeneratorFactorySelector hashGeneratorFactorySelector, uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    public static bool IsMetaSetVerificationEnabled(in FileSystemLayoutHeader header)
    {
        throw new NotImplementedException();
    }

    public static bool IsMetaSetVerificationEnabled(uint version)
    {
        throw new NotImplementedException();
    }

    public static bool IsHashSaltEnabled(in FileSystemLayoutHeader header)
    {
        throw new NotImplementedException();
    }

    public static bool IsHashSaltEnabled(uint version)
    {
        throw new NotImplementedException();
    }

    public static Result GetHashAlgorithmType(out HashAlgorithmType outType, in FileSystemLayoutHeader header)
    {
        throw new NotImplementedException();
    }

    public static HashAlgorithmType GetHashAlgorithmType(uint version)
    {
        throw new NotImplementedException();
    }

    public static void SetVersionSupported(FileSystemClient fs, uint versionMin, uint versionMax)
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

    private static Result UpdateRemapMetaHashStorage(in ValueSubStorage remapMetaStorage,
        in ValueSubStorage remapMetaHashStorage, IHash256GeneratorFactory hashGeneratorFactory)
    {
        throw new NotImplementedException();
    }

    private static Result VerifyRemapMetaHashStorage(in ValueSubStorage remapMetaStorage,
        in ValueSubStorage remapMetaHashStorage, IHash256GeneratorFactory hashGeneratorFactory)
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

    protected override Result DoCommit()
    {
        throw new NotImplementedException();
    }

    protected override Result DoCommitProvisionally(long counter)
    {
        throw new NotImplementedException();
    }

    protected override Result DoRollback()
    {
        throw new NotImplementedException();
    }

    public long GetCounterForBundledCommit()
    {
        throw new NotImplementedException();
    }

    public Result UpdateMacAndCommit(IMacGenerator macGenerator)
    {
        throw new NotImplementedException();
    }

    public Result RollbackOnlyModified()
    {
        throw new NotImplementedException();
    }

    public Result AcceptVisitor(IInternalStorageFileSystemVisitor visitor)
    {
        throw new NotImplementedException();
    }
}

public class IntegrityFilteredFile : IFile
{
    private UniqueRef<IFile> _file;
    private JournalIntegritySaveDataFileSystem _fileSystem;

    internal IntegrityFilteredFile(ref UniqueRef<IFile> file, JournalIntegritySaveDataFileSystem fileSystem)
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

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }
}

public class IntegrityFilteredDirectory : IDirectory
{
    private UniqueRef<IDirectory> _directory;
    private JournalIntegritySaveDataFileSystem _fileSystem;

    internal IntegrityFilteredDirectory(ref UniqueRef<IDirectory> directory, JournalIntegritySaveDataFileSystem fileSystem)
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