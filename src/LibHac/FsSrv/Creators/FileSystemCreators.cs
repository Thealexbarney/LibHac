﻿namespace LibHac.FsSrv.Creators
{
    public class FileSystemCreators
    {
        public IRomFileSystemCreator RomFileSystemCreator { get; set; }
        public IPartitionFileSystemCreator PartitionFileSystemCreator { get; set; }
        public IStorageOnNcaCreator StorageOnNcaCreator { get; set; }
        public IFatFileSystemCreator FatFileSystemCreator { get; set; }
        public IHostFileSystemCreator HostFileSystemCreator { get; set; }
        public ITargetManagerFileSystemCreator TargetManagerFileSystemCreator { get; set; }
        public ISubDirectoryFileSystemCreator SubDirectoryFileSystemCreator { get; set; }
        public IBuiltInStorageCreator BuiltInStorageCreator { get; set; }
        public ISdStorageCreator SdStorageCreator { get; set; }
        public ISaveDataFileSystemCreator SaveDataFileSystemCreator { get; set; }
        public IGameCardStorageCreator GameCardStorageCreator { get; set; }
        public IGameCardFileSystemCreator GameCardFileSystemCreator { get; set; }
        public IEncryptedFileSystemCreator EncryptedFileSystemCreator { get; set; }
        public IMemoryStorageCreator MemoryStorageCreator { get; set; }
        public IBuiltInStorageFileSystemCreator BuiltInStorageFileSystemCreator { get; set; }
        public ISdFileSystemCreator SdFileSystemCreator { get; set; }
    }
}
