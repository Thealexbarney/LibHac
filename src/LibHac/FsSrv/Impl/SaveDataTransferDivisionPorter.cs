// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Fs;
using LibHac.Fs.Impl;
using LibHac.Os;
using LibHac.Sf;
using LibHac.Util;
using ISaveDataChunkExporter = LibHac.FsSrv.Sf.ISaveDataChunkExporter;
using ISaveDataChunkImporter = LibHac.FsSrv.Sf.ISaveDataChunkImporter;
using ISaveDataChunkIterator = LibHac.FsSrv.Sf.ISaveDataChunkIterator;

namespace LibHac.FsSrv.Impl;

file static class Anonymous
{
    public static Result CalculateStorageHash(out InitialDataVersion2Detail.Hash outHash,
        out InitialDataVersion2Detail.ShortHash outHashShort1, out InitialDataVersion2Detail.ShortHash outHashShort2,
        IStorage storage, Span<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public static Result OpenSaveDataChunkIteratorDiff(ref SharedRef<ISaveDataChunkIterator> outIterator,
        in SaveDataChunkDiffInfo diffInfo, bool isExport, int count)
    {
        throw new NotImplementedException();
    }

    public static Result OpenSubStorage(ref SharedRef<IStorage> outSubStorage,
        ref readonly SharedRef<SaveDataInternalStorageAccessor> internalStorageAccessor,
        ref readonly SharedRef<ChunkSizeCalculator> chunkSizeCalculator, int chunkIndex)
    {
        throw new NotImplementedException();
    }

    public static void InheritPreviousInitialData(ref InitialDataVersion2Detail.Content initialData,
        in InitialDataVersion2Detail.Content previousInitialData)
    {
        throw new NotImplementedException();
    }

    public static Result CalculateChunkHash(out InitialDataVersion2Detail.Hash outHash,
        out InitialDataVersion2Detail.ShortHash outHashShort1, out InitialDataVersion2Detail.ShortHash outHashShort2,
        ref readonly SharedRef<SaveDataInternalStorageAccessor> internalStorageAccessor,
        ref readonly SharedRef<ChunkSizeCalculator> chunkSizeCalculator, int chunkId)
    {
        throw new NotImplementedException();
    }

    public static Result CalculateChunkHash(out InitialDataVersion2Detail.Hash outHash,
        ref readonly SharedRef<SaveDataInternalStorageAccessor> internalStorageAccessor,
        ref readonly SharedRef<ChunkSizeCalculator> chunkSizeCalculator, int chunkId)
    {
        throw new NotImplementedException();
    }

    public static Result EncryptPortContext(Span<byte> destination, ReadOnlySpan<byte> source,
        SaveDataTransferCryptoConfiguration cryptoConfig)
    {
        throw new NotImplementedException();
    }

    public static byte GetDiffChunkCount(Span<bool> isDifferent, int chunkCount, bool getHalfDivisionDiff)
    {
        throw new NotImplementedException();
    }

    public static byte GetCompressionRate(in InitialDataVersion2Detail.Content initialData)
    {
        throw new NotImplementedException();
    }

    public static bool IsTheirsHashSaltUsed(in InitialDataVersion2Detail.Content initialData,
        in InitialDataVersion2Detail.Content originalInitialData)
    {
        throw new NotImplementedException();
    }
}

public struct ExportContextDetail
{
    public AesGcmStreamHeader GcmStreamHeader;
    public Content DataContent;
    public AesGcmStreamTail GcmStreamTail;

    public struct Content
    {
        public uint Magic;
        public uint Unk;
        public bool IsDiffExport;
        public Array64<long> ChunkSizes;
        public Array64<AesMac> ChunkMacs;
        public Array64<bool> IsChunkComplete;
        public InitialDataVersion2Detail InitialData;
        public SaveDataSpaceId SpaceId;
        public ulong SaveDataId;
        public long CommitId;
        public int DivisionCount;
        public long DivisionAlignment;
        public InitialDataAad InitialDataAad;
        public bool IsInitialDataComplete;
        public Array64<AesIv> ChunkIvs;
        public AesIv OuterInitialDataIv;
        public KeySeed KeySeed;
        public InitialDataVersion2Detail.Hash ThumbnailHash;
        public Array5366<byte> Reserved;
    }
}

public struct ImportContextDetail
{
    public AesGcmStreamHeader GcmStreamHeader;
    public Content DataContent;
    public AesGcmStreamTail GcmStreamTail;

    public struct Content
    {
        public uint Magic;
        public uint Unk;
        public SaveDataDivisionImporter.Mode Mode;
        public InitialDataVersion2Detail InitialData;
        public SaveDataSpaceId SpaceId;
        public ulong SourceSaveDataId;
        public ulong DestinationSaveDataId;
        public long SourceZeroCommitId;
        public long DestinationZeroCommitId;
        public UserId UserId;
        public long TimeStamp;
        public ImportReportInfo ReportInfo;
        public InitialDataVersion2Detail.Hash ThumbnailHash;
        public Array8000<byte> Reserved;
    }
}

public class ChunkSizeCalculator : IDisposable
{
    private ulong _divisionAlignment;
    private long _chunkSize;
    private int _divisionCount;
    private int _longChunkCount;

    public ChunkSizeCalculator(long totalSize, ulong divisionAlignment, int divisionCount)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public int GetDivisionCount() => throw new NotImplementedException();

    public void GetOffsetAndSize(out long outOffset, out long outSize, int chunkId)
    {
        throw new NotImplementedException();
    }
}

public class SaveDataDivisionExporter : Prohibitee, Sf.ISaveDataDivisionExporter
{
    private SdkMutex _mutex;
    private SaveDataTransferCryptoConfiguration _cryptoConfig;
    private Optional<KeySeed> _keySeed;
    private SharedRef<IChunkEncryptorFactory> _encryptorFactory;
    private InitialDataVersion2Detail.Content _initialData;
    private InitialDataVersion2Detail _encryptedInitialData;
    private InitialDataAad _initialDataAad;
    private SaveDataSpaceId _saveDataSpaceId;
    private ulong _saveDataId;
    private InitialDataVersion2Detail.Hash _thumbnailHash;
    private ApplicationId _applicationId;
    private bool _isExportComplete;
    private bool _isReportAvailable;
    private bool _isDiffExport;
    private Array64<bool> _isChunkComplete;
    private bool _isInitialDataComplete;
    private SaveDataChunkDiffInfo _chunkDiffInfo;
    private Array64<AesIv> _chunkIvs;
    private AesIv _outerInitialDataIv;
    private SharedRef<SaveDataInternalStorageAccessor> _internalStorageAccessor;
    private SharedRef<ChunkSizeCalculator> _chunkSizeCalculator;
    private bool _hasOuterInitialDataMac;
    private SaveDataTransferCryptoConfiguration.KeyIndex _outerInitialDataMacKeyIndex;
    private InitialDataMac _initialDataMac;
    private int _outerInitialDataMacKeyGeneration;
    private bool _isCompressionEnabled;
    private SaveDataTransferCryptoConfiguration.Attributes _attribute;

    public SaveDataDivisionExporter(
        SaveDataTransferCryptoConfiguration cryptoConfig,
        in Optional<KeySeed> keySeed,
        ref readonly SharedRef<IChunkEncryptorFactory> encryptorFactory,
        SaveDataSpaceId spaceId,
        ulong saveDataId,
        SaveDataPorterManager porterManager,
        bool isDiffExport,
        bool isCompressionEnabled,
        SaveDataTransferCryptoConfiguration.Attributes attribute) : base(porterManager)
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    private Result InitializeCommon(ISaveDataTransferCoreInterface coreInterface,
        in Optional<InitialDataVersion2Detail.Content> initialData)
    {
        throw new NotImplementedException();
    }

    public Result InitializeForFullExport(ISaveDataTransferCoreInterface coreInterface)
    {
        throw new NotImplementedException();
    }

    public Result InitializeForFullExportWithOuterInitialDataMac(ISaveDataTransferCoreInterface coreInterface,
        SaveDataTransferCryptoConfiguration.KeyIndex keyIndex, int keyGeneration)
    {
        throw new NotImplementedException();
    }

    public Result InitializeForDiffExport(ISaveDataTransferCoreInterface coreInterface,
        Box<InitialDataVersion2Detail.Content> initialData, in InitialDataVersion2Detail initialDataEncrypted)
    {
        throw new NotImplementedException();
    }

    public Result ResumeExport(in ExportContextDetail.Content exportContext)
    {
        throw new NotImplementedException();
    }

    public Result SetDivisionCountCore(int divisionCount)
    {
        throw new NotImplementedException();
    }

    public Result SetDivisionCount(int divisionCount)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataDiffChunkIterator(ref SharedRef<ISaveDataChunkIterator> outIterator)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataChunkExporter(ref SharedRef<ISaveDataChunkExporter> outExporter, uint chunkId)
    {
        throw new NotImplementedException();
    }

    public Result ReportAesGcmInfo(ushort chunkId, in AesMac mac, long chunkSize)
    {
        throw new NotImplementedException();
    }

    public Result ReportInitialDataCompletion()
    {
        throw new NotImplementedException();
    }

    public Result ReportInitialDataMac(in AesMac mac)
    {
        throw new NotImplementedException();
    }

    private Result CheckPullCompletion(bool noCheckInitialDataCompletion)
    {
        throw new NotImplementedException();
    }

    public Result GetKeySeed(out KeySeed outKeySeed)
    {
        throw new NotImplementedException();
    }

    public Result GetInitialDataMac(out InitialDataMac outInitialDataMac)
    {
        throw new NotImplementedException();
    }

    public Result GetInitialDataMacKeyGeneration(out int outKeyGeneration)
    {
        throw new NotImplementedException();
    }

    public Result FinalizeExport()
    {
        throw new NotImplementedException();
    }

    public Result CancelExport()
    {
        throw new NotImplementedException();
    }

    public Result SuspendExport(OutBuffer outExportContext)
    {
        throw new NotImplementedException();
    }

    public Result GetImportInitialDataAad(out InitialDataAad outInitialDataAad)
    {
        throw new NotImplementedException();
    }

    public Result SetExportInitialDataAad(in InitialDataAad initialDataAad)
    {
        throw new NotImplementedException();
    }

    private InitialDataMac CalculateInitialDataMac(in InitialDataVersion2Detail.Content initialData)
    {
        throw new NotImplementedException();
    }

    public Result ReadSaveDataExtraData(OutBuffer outExtraData)
    {
        throw new NotImplementedException();
    }

    public Result GetReportInfo(out ExportReportInfo outReportInfo)
    {
        throw new NotImplementedException();
    }

    public override void Invalidate()
    {
        throw new NotImplementedException();
    }

    private void InvalidateCore()
    {
        throw new NotImplementedException();
    }

    public override ApplicationId GetApplicationId()
    {
        throw new NotImplementedException();
    }

    public UniqueLock<SdkMutex> GetScopedLock()
    {
        throw new NotImplementedException();
    }

    private ref readonly AesIv GetIv(ushort chunkId)
    {
        throw new NotImplementedException();
    }

    private bool IsPorterValid()
    {
        throw new NotImplementedException();
    }
}

public class SaveDataDivisionImporter : Prohibitee, Sf.ISaveDataDivisionImporter
{
    public enum Mode
    {
        None,
        Swap,
        Diff
    }

    public enum State
    {
        NotInitialized,
        Initializing,
        Initialized,
        Paused,
        Complete,
        Suspended
    }

    private SdkMutex _mutex;
    private SaveDataTransferCryptoConfiguration _cryptoConfig;
    private SharedRef<ISaveDataTransferCoreInterface> _transferInterface;
    private SharedRef<IChunkDecryptorFactory> _decryptorFactory;
    private InitialDataVersion2Detail.Content _initialData;
    private InitialDataVersion2Detail.Content _originalInitialData;
    private InitialDataVersion2Detail _originalInitialDataEncrypted;
    private SaveDataSpaceId _saveDataSpaceId;
    private SaveDataCreationInfo2 _creationInfo;
    private ulong _sourceSaveDataId;
    private ulong _destSaveDataId;
    private long _zeroCommitIdSource;
    private long _zeroCommitIdDest;
    private ulong _timestamp;
    private InitialDataVersion2Detail.Hash _thumbnailHash;
    private ApplicationId _applicationId;
    private State _state;
    private bool _isPorterInvalidated;
    private Mode _mode;
    private Array64<bool> _isChunkComplete;
    private SaveDataChunkDiffInfo _chunkDiffInfo;
    private SharedRef<SaveDataInternalStorageAccessor> _internalStorageAccessor;
    private SharedRef<ChunkSizeCalculator> _chunkSizeCalculator;
    private Optional<StorageDuplicator> _storageDuplicator;
    private ImportReportInfo _reportInfo;
    private bool _isCompressionEnabled;
    private bool _isLargeBufferEnabled;

    public SaveDataDivisionImporter(
        ref readonly SharedRef<ISaveDataTransferCoreInterface> transferCoreInterface,
        in InitialDataVersion2Detail.Content initialData,
        in InitialDataVersion2Detail initialDataEncrypted,
        SaveDataTransferCryptoConfiguration cryptoConfig,
        ref readonly SharedRef<IChunkDecryptorFactory> decryptorFactory,
        SaveDataSpaceId spaceId,
        in SaveDataCreationInfo2 creationInfo,
        SaveDataPorterManager porterManager,
        bool isCompressionEnabled,
        bool isLargeBufferEnabled) : base(porterManager)
    {
        throw new NotImplementedException();
    }

    public SaveDataDivisionImporter(
        ref readonly SharedRef<ISaveDataTransferCoreInterface> transferCoreInterface,
        in InitialDataVersion2Detail.Content initialData,
        in InitialDataVersion2Detail initialDataEncrypted,
        SaveDataTransferCryptoConfiguration cryptoConfig,
        ref readonly SharedRef<IChunkDecryptorFactory> decryptorFactory,
        SaveDataSpaceId spaceId,
        ulong sourceSaveDataId,
        SaveDataPorterManager porterManager,
        bool useSwap) : base(porterManager)
    {
        throw new NotImplementedException();
    }

    public SaveDataDivisionImporter(
        ref readonly SharedRef<ISaveDataTransferCoreInterface> transferCoreInterface,
        in InitialDataVersion2Detail.Content initialData,
        in InitialDataVersion2Detail initialDataEncrypted,
        SaveDataTransferCryptoConfiguration cryptoConfig,
        ref readonly SharedRef<IChunkDecryptorFactory> decryptorFactory,
        SaveDataSpaceId spaceId,
        ulong sourceSaveDataId,
        ulong destSaveDataId,
        SaveDataPorterManager porterManager,
        Mode mode) : base(porterManager)
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public new Result Initialize()
    {
        throw new NotImplementedException();
    }

    public Result ResumeImport(in ImportContextDetail.Content importContext)
    {
        throw new NotImplementedException();
    }

    public Result InitializeImport(out long outRemaining, long sizeToProcess)
    {
        throw new NotImplementedException();
    }

    public Result ReportCompletion(ushort chunkId, bool isComplete)
    {
        throw new NotImplementedException();
    }

    public Result FinalizeImport()
    {
        throw new NotImplementedException();
    }

    public Result FinalizeImportWithoutSwap()
    {
        throw new NotImplementedException();
    }

    private Result FinalizeImport(bool noSwap)
    {
        throw new NotImplementedException();
    }

    public Result CancelImport()
    {
        throw new NotImplementedException();
    }

    public Result GetImportContext(OutBuffer outImportContext)
    {
        throw new NotImplementedException();
    }

    public Result SuspendImport()
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataDiffChunkIterator(ref SharedRef<ISaveDataChunkIterator> outIterator)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataChunkImporter(ref SharedRef<ISaveDataChunkImporter> outImporter, uint chunkId)
    {
        throw new NotImplementedException();
    }

    public Result GetImportInitialDataAad(out InitialDataAad outInitialDataAad)
    {
        throw new NotImplementedException();
    }

    private Result CreateDstSaveData(bool isSecondarySave, in HashSalt hashSalt)
    {
        throw new NotImplementedException();
    }

    public Result ReadSaveDataExtraData(OutBuffer outExtraData)
    {
        throw new NotImplementedException();
    }

    public Result GetReportInfo(out ImportReportInfo outReportInfo)
    {
        throw new NotImplementedException();
    }

    public override void Invalidate()
    {
        throw new NotImplementedException();
    }

    private void InvalidateCore()
    {
        throw new NotImplementedException();
    }

    public override ApplicationId GetApplicationId()
    {
        throw new NotImplementedException();
    }

    public UniqueLock<SdkMutex> GetScopedLock()
    {
        throw new NotImplementedException();
    }

    private bool IsPorterValid()
    {
        throw new NotImplementedException();
    }

    private bool IsDiffMode()
    {
        throw new NotImplementedException();
    }

    private bool IsSwapMode()
    {
        throw new NotImplementedException();
    }
}