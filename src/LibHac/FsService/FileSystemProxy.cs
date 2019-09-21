using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;

namespace LibHac.FsService
{
    public class FileSystemProxy
    {
        private FileSystemProxyCore FsProxyCore { get; }

        /// <summary>The client instance to be used for internal operations like save indexer access.</summary>
        private FileSystemClient FsClient { get; }

        public long CurrentProcess { get; private set; }

        public long SaveDataSize { get; private set; }
        public long SaveDataJournalSize { get; private set; }
        public string SaveDataRootPath { get; private set; }
        public bool AutoCreateSaveData { get; private set; }

        private const ulong SaveIndexerId = 0x8000000000000000;

        internal FileSystemProxy(FileSystemProxyCore fsProxyCore, FileSystemClient fsClient)
        {
            FsProxyCore = fsProxyCore;
            FsClient = fsClient;

            CurrentProcess = -1;
            SaveDataSize = 0x2000000;
            SaveDataJournalSize = 0x1000000;
            SaveDataRootPath = string.Empty;
            AutoCreateSaveData = true;
        }

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

        public Result OpenCustomStorageFileSystem(out IFileSystem fileSystem, CustomStorageId storageId)
        {
            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            return FsProxyCore.OpenCustomStorageFileSystem(out fileSystem, storageId);
        }

        public Result OpenSaveDataFileSystemBySystemSaveDataId(out IFileSystem fileSystem, SaveDataSpaceId spaceId,
            SaveDataAttribute attribute)
        {
            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter
            fileSystem = default;

            if (!IsSystemSaveDataId(attribute.SaveId)) return ResultFs.InvalidArgument.Log();

            Result rc = OpenSaveDataFileSystemImpl(out IFileSystem saveFs, out ulong saveDataId, spaceId,
                attribute, false, true);
            if (rc.IsFailure()) return rc;

            // Missing check if the current title owns the save data or can open it

            fileSystem = saveFs;

            return Result.Success;
        }

        public Result SetSdCardEncryptionSeed(ReadOnlySpan<byte> seed)
        {
            // todo: use struct instead of byte span
            if (seed.Length != 0x10) return ResultFs.InvalidSize;

            // Missing permission check

            Result rc = FsProxyCore.SetSdCardEncryptionSeed(seed);
            if (rc.IsFailure()) return rc;

            // todo: Reset save data indexer

            return Result.Success;
        }

        private Result OpenSaveDataFileSystemImpl(out IFileSystem fileSystem, out ulong saveDataId,
            SaveDataSpaceId spaceId, SaveDataAttribute attribute, bool openReadOnly, bool cacheExtraData)
        {
            bool hasFixedId = attribute.SaveId != 0 && attribute.UserId.Id == Id128.InvalidId;

            if (hasFixedId)
            {
                saveDataId = attribute.SaveId;
            }
            else
            {
                throw new NotImplementedException();
            }

            Result saveFsResult = FsProxyCore.OpenSaveDataFileSystem(out fileSystem, spaceId, saveDataId,
                SaveDataRootPath, openReadOnly, attribute.Type, cacheExtraData);

            if (saveFsResult.IsSuccess()) return Result.Success;

            if (saveFsResult == ResultFs.PathNotFound || saveFsResult == ResultFs.TargetNotFound) return saveFsResult;

            if (saveDataId != SaveIndexerId)
            {
                if (hasFixedId)
                {
                    // todo: remove save indexer entry
                }
            }

            return ResultFs.TargetNotFound;
        }

        private bool IsSystemSaveDataId(ulong id)
        {
            return (long)id < 0;
        }
    }
}
