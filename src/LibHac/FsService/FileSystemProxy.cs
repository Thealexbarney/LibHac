using System;
using LibHac.Fs;

namespace LibHac.FsService
{
    public class FileSystemProxy
    {
        private FileSystemProxyCore FsProxyCore { get; }
        public long CurrentProcess { get; private set; }

        public long SaveDataSize { get; private set; }
        public long SaveDataJournalSize { get; private set; }
        public string SaveDataRootPath { get; private set; }
        public bool AutoCreateSaveData { get; private set; }

        public Result SetCurrentProcess(long processId)
        {
            CurrentProcess = processId;

            return Result.Success;
        }

        public Result DisableAutoSaveDataCreation()
        {
            AutoCreateSaveData = false;

            return Result.Success;
        }

        public Result SetSaveDataSize(long saveDataSize, long saveDataJournalSize)
        {
            if (saveDataSize < 0 || saveDataJournalSize < 0)
            {
                return ResultFs.InvalidSize;
            }

            SaveDataSize = saveDataSize;
            SaveDataJournalSize = saveDataJournalSize;

            return Result.Success;
        }

        public Result SetSaveDataRootPath(string path)
        {
            // Missing permission check

            if (path.Length > PathTools.MaxPathLength)
            {
                return ResultFs.TooLongPath;
            }

            SaveDataRootPath = path;

            return Result.Success;
        }

        public Result OpenBisFileSystem(out IFileSystem fileSystem, string rootPath, BisPartitionId partitionId)
        {
            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            return FsProxyCore.OpenBisFileSystem(out fileSystem, rootPath, partitionId);
        }

        public Result OpenSdCardFileSystem(out IFileSystem fileSystem)
        {
            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            return FsProxyCore.OpenSdCardFileSystem(out fileSystem);
        }

        public Result OpenContentStorageFileSystem(out IFileSystem fileSystem, ContentStorageId storageId)
        {
            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            return FsProxyCore.OpenContentStorageFileSystem(out fileSystem, storageId);
        }

        public Result SetSdCardEncryptionSeed(ReadOnlySpan<byte> seed)
        {
            // todo: use struct instead of byte span
            if (seed.Length != 0x16) return ResultFs.InvalidSize;

            // Missing permission check

            Result res = FsProxyCore.SetSdCardEncryptionSeed(seed);
            if (res.IsFailure()) return res;

            // todo: Reset save data indexer

            return Result.Success;
        }
    }
}
