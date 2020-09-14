using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Sf;
using LibHac.Spl;

namespace LibHac.FsSrv
{
    public interface IFileSystemProxy
    {
        Result SetCurrentProcess(ulong processId);
        Result OpenDataFileSystemByCurrentProcess(out ReferenceCountedDisposable<IFileSystemSf> fileSystem);
        Result OpenFileSystemWithPatch(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, ProgramId programId, FileSystemProxyType fsType);
        Result OpenFileSystemWithId(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, in FspPath path, ulong id, FileSystemProxyType fsType);
        Result OpenDataFileSystemByProgramId(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, ProgramId programId);
        Result OpenBisFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, in FspPath rootPath, BisPartitionId partitionId);
        Result OpenBisStorage(out ReferenceCountedDisposable<IStorageSf> storage, BisPartitionId partitionId);
        Result InvalidateBisCache();
        Result OpenHostFileSystemWithOption(out IFileSystem fileSystem, ref FsPath path, MountHostOption option);
        Result OpenHostFileSystem(out IFileSystem fileSystem, ref FsPath path);
        Result OpenSdCardFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem);
        Result FormatSdCardFileSystem();
        Result DeleteSaveDataFileSystem(ulong saveDataId);
        Result CreateSaveDataFileSystem(ref SaveDataAttribute attribute, ref SaveDataCreationInfo creationInfo, ref SaveMetaCreateInfo metaCreateInfo);
        Result CreateSaveDataFileSystemBySystemSaveDataId(ref SaveDataAttribute attribute, ref SaveDataCreationInfo creationInfo);
        Result RegisterSaveDataFileSystemAtomicDeletion(ReadOnlySpan<ulong> saveDataIds);
        Result DeleteSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId);
        Result FormatSdCardDryRun();
        Result IsExFatSupported(out bool isSupported);
        Result DeleteSaveDataFileSystemBySaveDataAttribute(SaveDataSpaceId spaceId, ref SaveDataAttribute attribute);
        Result OpenGameCardStorage(out IStorage storage, GameCardHandle handle, GameCardPartitionRaw partitionId);
        Result OpenGameCardFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, GameCardHandle handle, GameCardPartition partitionId);
        Result ExtendSaveDataFileSystem(SaveDataSpaceId spaceId, ulong saveDataId, long dataSize, long journalSize);
        Result DeleteCacheStorage(short index);
        Result GetCacheStorageSize(out long dataSize, out long journalSize, short index);
        Result CreateSaveDataFileSystemWithHashSalt(ref SaveDataAttribute attribute, ref SaveDataCreationInfo creationInfo, ref SaveMetaCreateInfo metaCreateInfo, ref HashSalt hashSalt);
        Result OpenSaveDataFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId, ref SaveDataAttribute attribute);
        Result OpenSaveDataFileSystemBySystemSaveDataId(out IFileSystem fileSystem, SaveDataSpaceId spaceId, ref SaveDataAttribute attribute);
        Result OpenReadOnlySaveDataFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId, ref SaveDataAttribute attribute);
        Result ReadSaveDataFileSystemExtraDataBySaveDataSpaceId(Span<byte> extraDataBuffer, SaveDataSpaceId spaceId, ulong saveDataId);
        Result ReadSaveDataFileSystemExtraData(Span<byte> extraDataBuffer, ulong saveDataId);
        Result WriteSaveDataFileSystemExtraData(ulong saveDataId, SaveDataSpaceId spaceId, ReadOnlySpan<byte> extraDataBuffer);
        Result OpenSaveDataInfoReader(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader);
        Result OpenSaveDataInfoReaderBySaveDataSpaceId(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader, SaveDataSpaceId spaceId);
        Result OpenSaveDataInfoReaderOnlyCacheStorage(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader);
        Result OpenSaveDataInternalStorageFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, SaveDataSpaceId spaceId, ulong saveDataId);
        Result UpdateSaveDataMacForDebug(SaveDataSpaceId spaceId, ulong saveDataId);
        Result WriteSaveDataFileSystemExtraDataWithMask(ulong saveDataId, SaveDataSpaceId spaceId, ReadOnlySpan<byte> extraDataBuffer, ReadOnlySpan<byte> maskBuffer);
        Result FindSaveDataWithFilter(out long count, Span<byte> saveDataInfoBuffer, SaveDataSpaceId spaceId, ref SaveDataFilter filter);
        Result OpenSaveDataInfoReaderWithFilter(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader, SaveDataSpaceId spaceId, ref SaveDataFilter filter);
        Result ReadSaveDataFileSystemExtraDataBySaveDataAttribute(Span<byte> extraDataBuffer, SaveDataSpaceId spaceId, ref SaveDataAttribute attribute);
        Result WriteSaveDataFileSystemExtraDataBySaveDataAttribute(ref SaveDataAttribute attribute, SaveDataSpaceId spaceId, ReadOnlySpan<byte> extraDataBuffer, ReadOnlySpan<byte> maskBuffer);
        Result OpenSaveDataMetaFile(out IFile file, SaveDataSpaceId spaceId, ref SaveDataAttribute attribute, SaveDataMetaType type);

        Result ListAccessibleSaveDataOwnerId(out int readCount, Span<Ncm.ApplicationId> idBuffer, ProgramId programId, int startIndex, int bufferIdCount);
        Result OpenImageDirectoryFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, ImageDirectoryId directoryId);
        Result OpenContentStorageFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, ContentStorageId storageId);
        Result OpenCloudBackupWorkStorageFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, CloudBackupWorkStorageId storageId);
        Result OpenCustomStorageFileSystem(out IFileSystem fileSystem, CustomStorageId storageId);
        Result OpenDataStorageByCurrentProcess(out ReferenceCountedDisposable<IStorageSf> storage);
        Result OpenDataStorageByProgramId(out ReferenceCountedDisposable<IStorageSf> storage, ProgramId programId);
        Result OpenDataStorageByDataId(out ReferenceCountedDisposable<IStorageSf> storage, DataId dataId, StorageId storageId);
        Result OpenPatchDataStorageByCurrentProcess(out ReferenceCountedDisposable<IStorageSf> storage);
        Result OpenDataFileSystemWithProgramIndex(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, byte programIndex);
        Result OpenDataStorageWithProgramIndex(out ReferenceCountedDisposable<IStorageSf> storage, byte programIndex);
        Result OpenDeviceOperator(out IDeviceOperator deviceOperator);

        Result OpenSystemDataUpdateEventNotifier(out ReferenceCountedDisposable<IEventNotifier> eventNotifier);
        Result NotifySystemDataUpdateEvent();

        Result QuerySaveDataTotalSize(out long totalSize, long dataSize, long journalSize);
        Result VerifySaveDataFileSystem(ulong saveDataId, Span<byte> readBuffer);
        Result CorruptSaveDataFileSystem(ulong saveDataId);
        Result CreatePaddingFile(long size);
        Result DeleteAllPaddingFiles();
        Result GetRightsId(out RightsId rightsId, ProgramId programId, StorageId storageId);
        Result RegisterExternalKey(in RightsId rightsId, in AccessKey externalKey);
        Result UnregisterAllExternalKey();
        Result GetRightsIdByPath(out RightsId rightsId, in FspPath path);
        Result GetRightsIdAndKeyGenerationByPath(out RightsId rightsId, out byte keyGeneration, in FspPath path);
        Result SetCurrentPosixTimeWithTimeDifference(long time, int difference);
        Result GetFreeSpaceSizeForSaveData(out long freeSpaceSize, SaveDataSpaceId spaceId);
        Result VerifySaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId, Span<byte> readBuffer);
        Result CorruptSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId);
        Result QuerySaveDataInternalStorageTotalSize(out long size, SaveDataSpaceId spaceId, ulong saveDataId);
        Result GetSaveDataCommitId(out long commitId, SaveDataSpaceId spaceId, ulong saveDataId);
        Result UnregisterExternalKey(in RightsId rightsId);
        Result SetSdCardEncryptionSeed(in EncryptionSeed seed);
        Result SetSdCardAccessibility(bool isAccessible);
        Result IsSdCardAccessible(out bool isAccessible);

        Result RegisterProgramIndexMapInfo(ReadOnlySpan<byte> programIndexMapInfoBuffer, int programCount);
        Result SetBisRootForHost(BisPartitionId partitionId, ref FsPath path);
        Result SetSaveDataSize(long saveDataSize, long saveDataJournalSize);
        Result SetSaveDataRootPath(ref FsPath path);
        Result DisableAutoSaveDataCreation();
        Result SetGlobalAccessLogMode(GlobalAccessLogMode mode);
        Result GetGlobalAccessLogMode(out GlobalAccessLogMode mode);
        Result OutputAccessLogToSdCard(U8Span logString);
        Result RegisterUpdatePartition();
        Result OpenRegisteredUpdatePartition(out ReferenceCountedDisposable<IFileSystemSf> fileSystem);

        Result GetProgramIndexForAccessLog(out int programIndex, out int programCount);
        Result OverrideSaveDataTransferTokenSignVerificationKey(ReadOnlySpan<byte> key);
        Result CorruptSaveDataFileSystemByOffset(SaveDataSpaceId spaceId, ulong saveDataId, long offset);
        Result OpenMultiCommitManager(out IMultiCommitManager commitManager);
        Result OpenBisWiper(out ReferenceCountedDisposable<IWiper> bisWiper, NativeHandle transferMemoryHandle, ulong transferMemorySize);
    }
}