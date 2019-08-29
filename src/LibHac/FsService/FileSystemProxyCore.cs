using System;
using LibHac.Fs;
using LibHac.Fs.Save;
using LibHac.FsService.Creators;

namespace LibHac.FsService
{
    public class FileSystemProxyCore
    {
        private FileSystemCreators FsCreators { get; }
        private byte[] SdEncryptionSeed { get; } = new byte[0x10];

        private const string NintendoDirectoryName = "Nintendo";
        private const string ContentDirectoryName = "Contents";

        public FileSystemProxyCore(FileSystemCreators fsCreators)
        {
            FsCreators = fsCreators;
        }

        public Result OpenBisFileSystem(out IFileSystem fileSystem, string rootPath, BisPartitionId partitionId)
        {
            return FsCreators.BuiltInStorageFileSystemCreator.Create(out fileSystem, rootPath, partitionId);
        }

        public Result OpenSdCardFileSystem(out IFileSystem fileSystem)
        {
            return FsCreators.SdFileSystemCreator.Create(out fileSystem);
        }

        public Result OpenContentStorageFileSystem(out IFileSystem fileSystem, ContentStorageId storageId)
        {
            fileSystem = default;

            string contentDirPath = default;
            IFileSystem baseFileSystem = default;
            bool isEncrypted = false;
            Result baseFsResult;

            switch (storageId)
            {
                case ContentStorageId.System:
                    baseFsResult = OpenBisFileSystem(out baseFileSystem, string.Empty, BisPartitionId.System);
                    contentDirPath = $"/{ContentDirectoryName}";
                    break;
                case ContentStorageId.User:
                    baseFsResult = OpenBisFileSystem(out baseFileSystem, string.Empty, BisPartitionId.User);
                    contentDirPath = $"/{ContentDirectoryName}";
                    break;
                case ContentStorageId.SdCard:
                    baseFsResult = OpenSdCardFileSystem(out baseFileSystem);
                    contentDirPath = $"/{NintendoDirectoryName}/{ContentDirectoryName}";
                    isEncrypted = true;
                    break;
                default:
                    baseFsResult = ResultFs.InvalidArgument;
                    break;
            }

            if (baseFsResult.IsFailure()) return baseFsResult;

            baseFileSystem.EnsureDirectoryExists(contentDirPath);

            Result subFsResult = FsCreators.SubDirectoryFileSystemCreator.Create(out IFileSystem subDirFileSystem,
                baseFileSystem, contentDirPath);
            if (subFsResult.IsFailure()) return subFsResult;

            if (!isEncrypted)
            {
                fileSystem = subDirFileSystem;
                return Result.Success;
            }

            return FsCreators.EncryptedFileSystemCreator.Create(out fileSystem, subDirFileSystem,
                EncryptedFsKeyId.Content, SdEncryptionSeed);
        }

        public Result SetSdCardEncryptionSeed(ReadOnlySpan<byte> seed)
        {
            seed.CopyTo(SdEncryptionSeed);
            //FsCreators.SaveDataFileSystemCreator.SetSdCardEncryptionSeed(seed);

            return Result.Success;
        }

        public bool AllowDirectorySaveData(SaveDataSpaceId spaceId, string saveDataRootPath)
        {
            return spaceId == SaveDataSpaceId.User && !string.IsNullOrWhiteSpace(saveDataRootPath);
        }

        public Result OpenSaveDataFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId, ulong saveDataId,
            string saveDataRootPath, bool openReadOnly, SaveDataType type, bool cacheExtraData)
        {
            fileSystem = default;

            Result openSaveDirResult = OpenSaveDataDirectory(out IFileSystem saveDirFs, spaceId, saveDataRootPath, true);
            if (openSaveDirResult.IsFailure()) return openSaveDirResult.Log();

            bool allowDirectorySaveData = AllowDirectorySaveData(spaceId, saveDataRootPath);
            bool useDeviceUniqueMac = Util.UseDeviceUniqueSaveMac(spaceId);

            if (allowDirectorySaveData)
            {
                try
                {
                    saveDirFs.EnsureDirectoryExists(GetSaveDataIdPath(saveDataId));
                }
                catch (HorizonResultException ex)
                {
                    return ex.ResultValue;
                }
            }

            // Missing save FS cache lookup

            Result saveFsResult = FsCreators.SaveDataFileSystemCreator.Create(out IFileSystem saveFs,
                out ISaveDataExtraDataAccessor extraDataAccessor, saveDirFs, saveDataId, allowDirectorySaveData,
                useDeviceUniqueMac, type, null);

            if (saveFsResult.IsFailure()) return saveFsResult.Log();

            if (cacheExtraData)
            {
                // Missing extra data caching
            }

            fileSystem = openReadOnly ? new ReadOnlyFileSystem(saveFs) : saveFs;

            return Result.Success;
        }

        public Result OpenSaveDataDirectory(out IFileSystem fileSystem, SaveDataSpaceId spaceId, string saveDataRootPath, bool openOnHostFs)
        {
            if (openOnHostFs && AllowDirectorySaveData(spaceId, saveDataRootPath))
            {
                Result hostFsResult = FsCreators.TargetManagerFileSystemCreator.Create(out IFileSystem hostFs, false);

                if (hostFsResult.IsFailure())
                {
                    fileSystem = default;
                    return hostFsResult.Log();
                }

                return Util.CreateSubFileSystem(out fileSystem, hostFs, saveDataRootPath, true);
            }

            string dirName = spaceId == SaveDataSpaceId.TemporaryStorage ? "/temp" : "/save";

            return OpenSaveDataDirectoryImpl(out fileSystem, spaceId, dirName, true);
        }

        public Result OpenSaveDataDirectoryImpl(out IFileSystem fileSystem, SaveDataSpaceId spaceId, string saveDirName, bool createIfMissing)
        {
            fileSystem = default;

            switch (spaceId)
            {
                case SaveDataSpaceId.System:
                    Result sysFsResult = OpenBisFileSystem(out IFileSystem sysFs, string.Empty, BisPartitionId.System);
                    if (sysFsResult.IsFailure()) return sysFsResult.Log();

                    return Util.CreateSubFileSystem(out fileSystem, sysFs, saveDirName, createIfMissing);

                case SaveDataSpaceId.User:
                case SaveDataSpaceId.TemporaryStorage:
                    Result userFsResult = OpenBisFileSystem(out IFileSystem userFs, string.Empty, BisPartitionId.System);
                    if (userFsResult.IsFailure()) return userFsResult.Log();

                    return Util.CreateSubFileSystem(out fileSystem, userFs, saveDirName, createIfMissing);

                case SaveDataSpaceId.SdSystem:
                case SaveDataSpaceId.SdCache:
                    Result sdFsResult = OpenSdCardFileSystem(out IFileSystem sdFs);
                    if (sdFsResult.IsFailure()) return sdFsResult.Log();

                    string sdSaveDirPath = $"/{NintendoDirectoryName}{saveDirName}";

                    Result sdSubResult = Util.CreateSubFileSystem(out IFileSystem sdSubFs, sdFs, sdSaveDirPath, createIfMissing);
                    if (sdSubResult.IsFailure()) return sdSubResult.Log();

                    return FsCreators.EncryptedFileSystemCreator.Create(out fileSystem, sdSubFs,
                        EncryptedFsKeyId.Save, SdEncryptionSeed);

                case SaveDataSpaceId.ProperSystem:
                    Result sysProperFsResult = OpenBisFileSystem(out IFileSystem sysProperFs, string.Empty, BisPartitionId.SystemProperPartition);
                    if (sysProperFsResult.IsFailure()) return sysProperFsResult.Log();

                    return Util.CreateSubFileSystem(out fileSystem, sysProperFs, saveDirName, createIfMissing);

                case SaveDataSpaceId.Safe:
                    Result safeFsResult = OpenBisFileSystem(out IFileSystem safeFs, string.Empty, BisPartitionId.SafeMode);
                    if (safeFsResult.IsFailure()) return safeFsResult.Log();

                    return Util.CreateSubFileSystem(out fileSystem, safeFs, saveDirName, createIfMissing);

                default:
                    return ResultFs.InvalidArgument.Log();
            }
        }

        private string GetSaveDataIdPath(ulong saveDataId)
        {
            return $"/{saveDataId:x16}";
        }
    }
}
