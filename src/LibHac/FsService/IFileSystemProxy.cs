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
        Result DeleteSaveDataFileSystem(long saveDataId);
        Result CreateSaveDataFileSystem(ref SaveDataAttribute2 attribute, ref SaveDataCreateInfo createInfo, ref SaveMetaCreateInfo metaCreateInfo);
        Result CreateSaveDataFileSystemBySystemSaveDataId(ref SaveDataAttribute2 attribute, ref SaveDataCreateInfo createInfo);
        Result RegisterSaveDataFileSystemAtomicDeletion(ReadOnlySpan<ulong> saveDataIds);

        Result OpenGameCardStorage(out IStorage storage, GameCardHandle handle, GameCardPartitionRaw partitionId);
        Result OpenSaveDataFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId, SaveDataAttribute attribute);
        Result OpenSaveDataFileSystemBySystemSaveDataId(out IFileSystem fileSystem, SaveDataSpaceId spaceId, SaveDataAttribute attribute);
        Result OpenContentStorageFileSystem(out IFileSystem fileSystem, ContentStorageId storageId);
        Result OpenCustomStorageFileSystem(out IFileSystem fileSystem, CustomStorageId storageId);
        Result SetSdCardEncryptionSeed(ReadOnlySpan<byte> seed);
        Result SetSaveDataSize(long saveDataSize, long saveDataJournalSize);
        Result SetSaveDataRootPath(U8Span path);
        Result DisableAutoSaveDataCreation();
    }
}