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
        Result CreateSaveDataFileSystem(in SaveDataAttribute attribute, in SaveDataCreationInfo creationInfo, in SaveDataMetaInfo metaInfo);
        Result CreateSaveDataFileSystemBySystemSaveDataId(ref SaveDataAttribute attribute, ref SaveDataCreationInfo creationInfo);
        Result RegisterSaveDataFileSystemAtomicDeletion(InBuffer saveDataIds);
        Result DeleteSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId);
        Result FormatSdCardDryRun();
        Result IsExFatSupported(out bool isSupported);
        Result DeleteSaveDataFileSystemBySaveDataAttribute(SaveDataSpaceId spaceId, in SaveDataAttribute attribute);
        Result OpenGameCardStorage(out IStorage storage, GameCardHandle handle, GameCardPartitionRaw partitionId);
        Result OpenGameCardFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, GameCardHandle handle, GameCardPartition partitionId);
        Result ExtendSaveDataFileSystem(SaveDataSpaceId spaceId, ulong saveDataId, long dataSize, long journalSize);
        Result DeleteCacheStorage(ushort index);
        Result GetCacheStorageSize(out long dataSize, out long journalSize, ushort index);
        Result CreateSaveDataFileSystemWithHashSalt(in SaveDataAttribute attribute, in SaveDataCreationInfo creationInfo, in SaveDataMetaInfo metaInfo, in HashSalt hashSalt);
        Result OpenSaveDataFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, SaveDataSpaceId spaceId, in SaveDataAttribute attribute);
        Result OpenSaveDataFileSystemBySystemSaveDataId(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, SaveDataSpaceId spaceId, in SaveDataAttribute attribute);
        Result OpenReadOnlySaveDataFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, SaveDataSpaceId spaceId, in SaveDataAttribute attribute);
        Result ReadSaveDataFileSystemExtraDataBySaveDataSpaceId(OutBuffer extraDataBuffer, SaveDataSpaceId spaceId, ulong saveDataId);
        Result ReadSaveDataFileSystemExtraData(OutBuffer extraDataBuffer, ulong saveDataId);
        Result WriteSaveDataFileSystemExtraData(ulong saveDataId, SaveDataSpaceId spaceId, InBuffer extraDataBuffer);
        Result OpenSaveDataInfoReader(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader);
        Result OpenSaveDataInfoReaderBySaveDataSpaceId(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader, SaveDataSpaceId spaceId);
        Result OpenSaveDataInfoReaderOnlyCacheStorage(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader);
        Result OpenSaveDataInternalStorageFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, SaveDataSpaceId spaceId, ulong saveDataId);
        Result UpdateSaveDataMacForDebug(SaveDataSpaceId spaceId, ulong saveDataId);
        Result WriteSaveDataFileSystemExtraDataWithMask(ulong saveDataId, SaveDataSpaceId spaceId, InBuffer extraDataBuffer, InBuffer maskBuffer);
        Result FindSaveDataWithFilter(out long count, OutBuffer saveDataInfoBuffer, SaveDataSpaceId spaceId, in SaveDataFilter filter);
        Result OpenSaveDataInfoReaderWithFilter(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader, SaveDataSpaceId spaceId, in SaveDataFilter filter);
        Result ReadSaveDataFileSystemExtraDataBySaveDataAttribute(OutBuffer extraDataBuffer, SaveDataSpaceId spaceId, in SaveDataAttribute attribute);
        Result WriteSaveDataFileSystemExtraDataWithMaskBySaveDataAttribute(in SaveDataAttribute attribute, SaveDataSpaceId spaceId, InBuffer extraDataBuffer, InBuffer maskBuffer);
        Result ReadSaveDataFileSystemExtraDataWithMaskBySaveDataAttribute(OutBuffer extraDataBuffer, SaveDataSpaceId spaceId, in SaveDataAttribute attribute, InBuffer maskBuffer);
        Result OpenSaveDataMetaFile(out ReferenceCountedDisposable<IFileSf> file, SaveDataSpaceId spaceId, in SaveDataAttribute attribute, SaveDataMetaType type);

        Result ListAccessibleSaveDataOwnerId(out int readCount, OutBuffer idBuffer, ProgramId programId, int startIndex, int bufferIdCount);
        Result OpenSaveDataMover(out ReferenceCountedDisposable<ISaveDataMover> saveMover, SaveDataSpaceId sourceSpaceId, SaveDataSpaceId destinationSpaceId, NativeHandle workBufferHandle, ulong workBufferSize);
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
        Result VerifySaveDataFileSystem(ulong saveDataId, OutBuffer readBuffer);
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
        Result VerifySaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId, OutBuffer readBuffer);
        Result CorruptSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId);
        Result QuerySaveDataInternalStorageTotalSize(out long size, SaveDataSpaceId spaceId, ulong saveDataId);
        Result GetSaveDataCommitId(out long commitId, SaveDataSpaceId spaceId, ulong saveDataId);
        Result UnregisterExternalKey(in RightsId rightsId);
        Result SetSdCardEncryptionSeed(in EncryptionSeed seed);
        Result SetSdCardAccessibility(bool isAccessible);
        Result IsSdCardAccessible(out bool isAccessible);

        Result RegisterProgramIndexMapInfo(InBuffer programIndexMapInfoBuffer, int programCount);
        Result SetBisRootForHost(BisPartitionId partitionId, ref FsPath path);
        Result SetSaveDataSize(long saveDataSize, long saveDataJournalSize);
        Result SetSaveDataRootPath(in FspPath path);
        Result DisableAutoSaveDataCreation();
        Result SetGlobalAccessLogMode(GlobalAccessLogMode mode);
        Result GetGlobalAccessLogMode(out GlobalAccessLogMode mode);
        Result OutputAccessLogToSdCard(U8Span logString);
        Result RegisterUpdatePartition();
        Result OpenRegisteredUpdatePartition(out ReferenceCountedDisposable<IFileSystemSf> fileSystem);

        Result GetProgramIndexForAccessLog(out int programIndex, out int programCount);
        Result UnsetSaveDataRootPath();
        Result OverrideSaveDataTransferTokenSignVerificationKey(ReadOnlySpan<byte> key);
        Result CorruptSaveDataFileSystemByOffset(SaveDataSpaceId spaceId, ulong saveDataId, long offset);
        Result OpenMultiCommitManager(out ReferenceCountedDisposable<IMultiCommitManager> commitManager);
        Result OpenBisWiper(out ReferenceCountedDisposable<IWiper> bisWiper, NativeHandle transferMemoryHandle, ulong transferMemorySize);
    }
}