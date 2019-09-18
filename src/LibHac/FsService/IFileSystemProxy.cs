using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Ncm;

namespace LibHac.FsService
{
    public interface IFileSystemProxy
    {
        Result SetCurrentProcess(long processId);
        Result OpenDataFileSystemByCurrentProcess(out IFileSystem fileSystem);
        Result OpenFileSystemWithPatch(out IFileSystem fileSystem, TitleId titleId, FileSystemType type);
        Result OpenFileSystemWithId(out IFileSystem fileSystem, U8Span path, TitleId titleId, FileSystemType type);
        Result OpenDataFileSystemByProgramId(out IFileSystem fileSystem, TitleId titleId);
        Result OpenBisFileSystem(out IFileSystem fileSystem, U8Span rootPath, BisPartitionId partitionId);
        Result OpenBisStorage(out IStorage storage, BisPartitionId partitionId);
        Result InvalidateBisCache();
        Result OpenHostFileSystem(out IFileSystem fileSystem, U8Span subPath);
        Result OpenSdCardFileSystem(out IFileSystem fileSystem);
        Result FormatSdCardFileSystem();
        Result DeleteSaveDataFileSystem(ulong saveDataId);
        Result CreateSaveDataFileSystem(ref SaveDataAttribute2 attribute, ref SaveDataCreateInfo createInfo, ref SaveMetaCreateInfo metaCreateInfo);
        Result CreateSaveDataFileSystemBySystemSaveDataId(ref SaveDataAttribute2 attribute, ref SaveDataCreateInfo createInfo);
        Result RegisterSaveDataFileSystemAtomicDeletion(ReadOnlySpan<ulong> saveDataIds);
        Result DeleteSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId);
        Result FormatSdCardDryRun();
        Result IsExFatSupported(out bool isSupported);
        Result DeleteSaveDataFileSystemBySaveDataAttribute(SaveDataSpaceId spaceId, ref SaveDataAttribute2 attribute);
        Result OpenGameCardStorage(out IStorage storage, GameCardHandle handle, GameCardPartitionRaw partitionId);
        Result OpenGameCardFileSystem(out IFileSystem fileSystem, GameCardHandle handle, GameCardPartition partitionId);
        Result ExtendSaveDataFileSystem(SaveDataSpaceId spaceId, ulong saveDataId, long dataSize, long journalSize);
        Result DeleteCacheStorage(short index);
        Result GetCacheStorageSize(out long dataSize, out long journalSize, short index);
        Result CreateSaveDataFileSystemWithHashSalt(ref SaveDataAttribute2 attribute, ref SaveDataCreateInfo createInfo, ref SaveMetaCreateInfo metaCreateInfo, ref HashSalt hashSalt);
        Result OpenSaveDataFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId, ref SaveDataAttribute attribute);
        Result OpenSaveDataFileSystemBySystemSaveDataId(out IFileSystem fileSystem, SaveDataSpaceId spaceId, ref SaveDataAttribute attribute);
        Result OpenReadOnlySaveDataFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId, ref SaveDataAttribute attribute);
        Result ReadSaveDataFileSystemExtraDataBySaveDataSpaceId(Span<byte> extraDataBuffer, SaveDataSpaceId spaceId, ulong saveDataId);
        Result ReadSaveDataFileSystemExtraData(Span<byte> extraDataBuffer, ulong saveDataId);
        Result WriteSaveDataFileSystemExtraData(ulong saveDataId, SaveDataSpaceId spaceId, ReadOnlySpan<byte> extraDataBuffer);
        Result OpenSaveDataInfoReader(out ISaveDataInfoReader infoReader);
        Result OpenSaveDataInfoReaderBySaveDataSpaceId(out ISaveDataInfoReader infoReader, SaveDataSpaceId spaceId);
        Result OpenSaveDataInfoReaderOnlyCacheStorage(out ISaveDataInfoReader infoReader);
        Result OpenSaveDataInternalStorageFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId, ulong saveDataId);
        Result UpdateSaveDataMacForDebug(SaveDataSpaceId spaceId, ulong saveDataId);
        Result WriteSaveDataFileSystemExtraDataWithMask(ulong saveDataId, SaveDataSpaceId spaceId, ReadOnlySpan<byte> extraDataBuffer, ReadOnlySpan<byte> maskBuffer);
        Result FindSaveDataWithFilter(out long count, Span<byte> saveDataInfoBuffer, SaveDataSpaceId spaceId, ref SaveDataFilter filter);
        Result OpenSaveDataInfoReaderWithFilter(out ISaveDataInfoReader infoReader, SaveDataSpaceId spaceId, ref SaveDataFilter filter);
        Result ReadSaveDataFileSystemExtraDataBySaveDataAttribute(Span<byte> extraDataBuffer, SaveDataSpaceId spaceId, ref SaveDataAttribute2 attribute);
        Result WriteSaveDataFileSystemExtraDataBySaveDataAttribute(ref SaveDataAttribute2 attribute, SaveDataSpaceId spaceId, ReadOnlySpan<byte> extraDataBuffer, ReadOnlySpan<byte> maskBuffer);
        Result OpenSaveDataMetaFile(out IFile file, SaveDataSpaceId spaceId, ref SaveDataAttribute2 attribute, SaveMetaType type);

        Result OpenContentStorageFileSystem(out IFileSystem fileSystem, ContentStorageId storageId);
        Result OpenCustomStorageFileSystem(out IFileSystem fileSystem, CustomStorageId storageId);
        Result OpenDataStorageByCurrentProcess(out IStorage storage);
        Result OpenDataStorageByProgramId(out IStorage storage, TitleId programId);
        Result OpenDataStorageByDataId(out IStorage storage, TitleId dataId, StorageId storageId);

        Result SetSdCardEncryptionSeed(ReadOnlySpan<byte> seed);
        Result SetSaveDataSize(long saveDataSize, long saveDataJournalSize);
        Result SetSaveDataRootPath(U8Span path);
        Result DisableAutoSaveDataCreation();
        Result SetGlobalAccessLogMode(int mode);
        Result GetGlobalAccessLogMode(out int mode);
        Result OutputAccessLogToSdCard(U8Span logString);
        Result RegisterUpdatePartition();
        Result OpenRegisteredUpdatePartition(out IFileSystem fileSystem);
    }
}