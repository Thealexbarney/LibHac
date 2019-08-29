using System;
using LibHac.Fs;

namespace LibHac.FsService
{
    public interface IFileSystemProxy
    {
        Result SetCurrentProcess(long processId);
        Result OpenBisFileSystem(out IFileSystem fileSystem, string rootPath, BisPartitionId partitionId);
        Result OpenSdCardFileSystem(out IFileSystem fileSystem);
        Result OpenGameCardStorage(out IStorage storage, GameCardHandle handle, GameCardPartitionRaw partitionId);
        Result OpenSaveDataFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId, SaveDataAttribute attribute);
        Result OpenSaveDataFileSystemBySystemSaveDataId(out IFileSystem fileSystem, SaveDataSpaceId spaceId, SaveDataAttribute attribute);
        Result OpenContentStorageFileSystem(out IFileSystem fileSystem, ContentStorageId storageId);
        Result SetSdCardEncryptionSeed(ReadOnlySpan<byte> seed);
        Result SetSaveDataSize(long saveDataSize, long saveDataJournalSize);
        Result SetSaveDataRootPath(string path);
        Result DisableAutoSaveDataCreation();
    }
}