using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.FsSystem;
using LibHac.FsSrv.Creators;

namespace LibHac.FsSrv
{
    public class FileSystemProxyCoreImpl
    {
        internal FileSystemProxyConfiguration Config { get; }
        private FileSystemCreators FsCreators => Config.FsCreatorInterfaces;
        internal ProgramRegistryImpl ProgramRegistry { get; }

        private IDeviceOperator DeviceOperator { get; }

        private byte[] SdEncryptionSeed { get; } = new byte[0x10];

        private const string NintendoDirectoryName = "Nintendo";

        private GlobalAccessLogMode LogMode { get; set; }
        public bool IsSdCardAccessible { get; set; }

        internal ISaveDataIndexerManager SaveDataIndexerManager { get; private set; }

        public FileSystemProxyCoreImpl(FileSystemProxyConfiguration config, IDeviceOperator deviceOperator)
        {
            Config = config;
            ProgramRegistry = new ProgramRegistryImpl(Config.ProgramRegistryService);
            DeviceOperator = deviceOperator;
        }

        public Result OpenBisFileSystem(out IFileSystem fileSystem, string rootPath, BisPartitionId partitionId)
        {
            return FsCreators.BuiltInStorageFileSystemCreator.Create(out fileSystem, rootPath, partitionId);
        }

        public Result OpenSdCardFileSystem(out IFileSystem fileSystem)
        {
            return FsCreators.SdCardFileSystemCreator.Create(out fileSystem, false);
        }

        public Result OpenGameCardStorage(out IStorage storage, GameCardHandle handle, GameCardPartitionRaw partitionId)
        {
            switch (partitionId)
            {
                case GameCardPartitionRaw.NormalReadOnly:
                    return FsCreators.GameCardStorageCreator.CreateNormal(handle, out storage);
                case GameCardPartitionRaw.SecureReadOnly:
                    return FsCreators.GameCardStorageCreator.CreateSecure(handle, out storage);
                case GameCardPartitionRaw.RootWriteOnly:
                    return FsCreators.GameCardStorageCreator.CreateWritable(handle, out storage);
                default:
                    throw new ArgumentOutOfRangeException(nameof(partitionId), partitionId, null);
            }
        }

        public Result OpenDeviceOperator(out IDeviceOperator deviceOperator)
        {
            deviceOperator = DeviceOperator;
            return Result.Success;
        }

        public Result OpenCustomStorageFileSystem(out IFileSystem fileSystem, CustomStorageId storageId)
        {
            fileSystem = default;

            switch (storageId)
            {
                case CustomStorageId.SdCard:
                {
                    Result rc = FsCreators.SdCardFileSystemCreator.Create(out IFileSystem sdFs, false);
                    if (rc.IsFailure()) return rc;

                    string customStorageDir = CustomStorage.GetCustomStorageDirectoryName(CustomStorageId.SdCard);
                    string subDirName = $"/{NintendoDirectoryName}/{customStorageDir}";

                    rc = Util.CreateSubFileSystem(out IFileSystem subFs, sdFs, subDirName, true);
                    if (rc.IsFailure()) return rc;

                    rc = FsCreators.EncryptedFileSystemCreator.Create(out IFileSystem encryptedFs, subFs,
                        EncryptedFsKeyId.CustomStorage, SdEncryptionSeed);
                    if (rc.IsFailure()) return rc;

                    fileSystem = encryptedFs;
                    return Result.Success;
                }
                case CustomStorageId.System:
                {
                    Result rc = FsCreators.BuiltInStorageFileSystemCreator.Create(out IFileSystem userFs, string.Empty,
                        BisPartitionId.User);
                    if (rc.IsFailure()) return rc;

                    string customStorageDir = CustomStorage.GetCustomStorageDirectoryName(CustomStorageId.System);
                    string subDirName = $"/{customStorageDir}";

                    rc = Util.CreateSubFileSystem(out IFileSystem subFs, userFs, subDirName, true);
                    if (rc.IsFailure()) return rc;

                    fileSystem = subFs;
                    return Result.Success;
                }
                default:
                    return ResultFs.InvalidArgument.Log();
            }
        }

        public Result OpenHostFileSystem(out IFileSystem fileSystem, U8Span path, bool openCaseSensitive)
        {
            fileSystem = default;
            Result rc;

            if (!path.IsEmpty())
            {
                rc = Util.VerifyHostPath(path);
                if (rc.IsFailure()) return rc;
            }

            rc = FsCreators.TargetManagerFileSystemCreator.Create(out IFileSystem hostFs, openCaseSensitive);
            if (rc.IsFailure()) return rc;

            if (path.IsEmpty())
            {
                ReadOnlySpan<byte> rootHostPath = new[] { (byte)'C', (byte)':', (byte)'/' };
                rc = hostFs.GetEntryType(out _, new U8Span(rootHostPath));

                // Nintendo ignores all results other than this one
                if (ResultFs.TargetNotFound.Includes(rc))
                    return rc;

                fileSystem = hostFs;
                return Result.Success;
            }

            rc = FsCreators.SubDirectoryFileSystemCreator.Create(out IFileSystem subDirFs, hostFs, path, preserveUnc: true);
            if (rc.IsFailure()) return rc;

            fileSystem = subDirFs;
            return Result.Success;
        }

        public Result SetSdCardEncryptionSeed(in EncryptionSeed seed)
        {
            seed.Value.CopyTo(SdEncryptionSeed);
            // todo: FsCreators.SaveDataFileSystemCreator.SetSdCardEncryptionSeed(seed);

            SaveDataIndexerManager.InvalidateSdCardIndexer(SaveDataSpaceId.SdSystem);
            SaveDataIndexerManager.InvalidateSdCardIndexer(SaveDataSpaceId.SdCache);

            return Result.Success;
        }

        public bool AllowDirectorySaveData(SaveDataSpaceId spaceId, string saveDataRootPath)
        {
            return spaceId == SaveDataSpaceId.User && !string.IsNullOrWhiteSpace(saveDataRootPath);
        }

        public Result DoesSaveDataExist(out bool exists, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            exists = false;

            Result rc = OpenSaveDataDirectory(out IFileSystem fileSystem, spaceId, string.Empty, true);
            if (rc.IsFailure()) return rc;

            string saveDataPath = $"/{saveDataId:x16}";

            rc = fileSystem.GetEntryType(out _, saveDataPath.ToU8Span());

            if (rc.IsFailure())
            {
                if (ResultFs.PathNotFound.Includes(rc))
                {
                    return Result.Success;
                }

                return rc;
            }

            exists = true;
            return Result.Success;
        }

        public Result OpenSaveDataFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId, ulong saveDataId,
            string saveDataRootPath, bool openReadOnly, SaveDataType type, bool cacheExtraData)
        {
            fileSystem = default;

            Result rc = OpenSaveDataDirectory(out IFileSystem saveDirFs, spaceId, saveDataRootPath, true);
            if (rc.IsFailure()) return rc;

            // ReSharper disable once RedundantAssignment
            bool allowDirectorySaveData = AllowDirectorySaveData(spaceId, saveDataRootPath);
            bool useDeviceUniqueMac = Util.UseDeviceUniqueSaveMac(spaceId);

            // Always allow directory savedata because we don't support transaction with file savedata yet
            allowDirectorySaveData = true;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (allowDirectorySaveData)
            {
                rc = saveDirFs.EnsureDirectoryExists(GetSaveDataIdPath(saveDataId));
                if (rc.IsFailure()) return rc;
            }

            // Missing save FS cache lookup

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            rc = FsCreators.SaveDataFileSystemCreator.Create(out IFileSystem saveFs, out _, saveDirFs, saveDataId,
                allowDirectorySaveData, useDeviceUniqueMac, type, null);

            if (rc.IsFailure()) return rc;

            if (cacheExtraData)
            {
                // todo: Missing extra data caching
            }

            fileSystem = openReadOnly ? new ReadOnlyFileSystem(saveFs) : saveFs;

            return Result.Success;
        }

        public Result OpenSaveDataDirectory(out IFileSystem fileSystem, SaveDataSpaceId spaceId, string saveDataRootPath, bool openOnHostFs)
        {
            if (openOnHostFs && AllowDirectorySaveData(spaceId, saveDataRootPath))
            {
                Result rc = FsCreators.TargetManagerFileSystemCreator.Create(out IFileSystem hostFs, false);

                if (rc.IsFailure())
                {
                    fileSystem = default;
                    return rc;
                }

                return Util.CreateSubFileSystem(out fileSystem, hostFs, saveDataRootPath, true);
            }

            string dirName = spaceId == SaveDataSpaceId.Temporary ? "/temp" : "/save";

            return OpenSaveDataDirectoryImpl(out fileSystem, spaceId, dirName, true);
        }

        public Result OpenSaveDataDirectoryImpl(out IFileSystem fileSystem, SaveDataSpaceId spaceId, string saveDirName, bool createIfMissing)
        {
            fileSystem = default;
            Result rc;

            switch (spaceId)
            {
                case SaveDataSpaceId.System:
                    rc = OpenBisFileSystem(out IFileSystem sysFs, string.Empty, BisPartitionId.System);
                    if (rc.IsFailure()) return rc;

                    return Util.CreateSubFileSystem(out fileSystem, sysFs, saveDirName, createIfMissing);

                case SaveDataSpaceId.User:
                case SaveDataSpaceId.Temporary:
                    rc = OpenBisFileSystem(out IFileSystem userFs, string.Empty, BisPartitionId.User);
                    if (rc.IsFailure()) return rc;

                    return Util.CreateSubFileSystem(out fileSystem, userFs, saveDirName, createIfMissing);

                case SaveDataSpaceId.SdSystem:
                case SaveDataSpaceId.SdCache:
                    rc = OpenSdCardFileSystem(out IFileSystem sdFs);
                    if (rc.IsFailure()) return rc;

                    string sdSaveDirPath = $"/{NintendoDirectoryName}{saveDirName}";

                    rc = Util.CreateSubFileSystem(out IFileSystem sdSubFs, sdFs, sdSaveDirPath, createIfMissing);
                    if (rc.IsFailure()) return rc;

                    return FsCreators.EncryptedFileSystemCreator.Create(out fileSystem, sdSubFs,
                        EncryptedFsKeyId.Save, SdEncryptionSeed);

                case SaveDataSpaceId.ProperSystem:
                    rc = OpenBisFileSystem(out IFileSystem sysProperFs, string.Empty, BisPartitionId.SystemProperPartition);
                    if (rc.IsFailure()) return rc;

                    return Util.CreateSubFileSystem(out fileSystem, sysProperFs, saveDirName, createIfMissing);

                case SaveDataSpaceId.SafeMode:
                    rc = OpenBisFileSystem(out IFileSystem safeFs, string.Empty, BisPartitionId.SafeMode);
                    if (rc.IsFailure()) return rc;

                    return Util.CreateSubFileSystem(out fileSystem, safeFs, saveDirName, createIfMissing);

                default:
                    return ResultFs.InvalidArgument.Log();
            }
        }

        public Result OpenSaveDataMetaFile(out IFile file, ulong saveDataId, SaveDataSpaceId spaceId, SaveDataMetaType type)
        {
            file = default;

            string metaDirPath = $"/saveMeta/{saveDataId:x16}";

            Result rc = OpenSaveDataDirectoryImpl(out IFileSystem tmpMetaDirFs, spaceId, metaDirPath, true);
            using IFileSystem metaDirFs = tmpMetaDirFs;
            if (rc.IsFailure()) return rc;

            string metaFilePath = $"/{(int)type:x8}.meta";

            return metaDirFs.OpenFile(out file, metaFilePath.ToU8Span(), OpenMode.ReadWrite);
        }

        public Result DeleteSaveDataMetaFiles(ulong saveDataId, SaveDataSpaceId spaceId)
        {
            Result rc = OpenSaveDataDirectoryImpl(out IFileSystem metaDirFs, spaceId, "/saveMeta", false);

            using (metaDirFs)
            {
                if (rc.IsFailure()) return rc;

                rc = metaDirFs.DeleteDirectoryRecursively($"/{saveDataId:x16}".ToU8Span());

                if (rc.IsFailure() && !ResultFs.PathNotFound.Includes(rc))
                    return rc;

                return Result.Success;
            }
        }

        public Result CreateSaveDataMetaFile(ulong saveDataId, SaveDataSpaceId spaceId, SaveDataMetaType type, long size)
        {
            string metaDirPath = $"/saveMeta/{saveDataId:x16}";

            Result rc = OpenSaveDataDirectoryImpl(out IFileSystem tmpMetaDirFs, spaceId, metaDirPath, true);
            using IFileSystem metaDirFs = tmpMetaDirFs;
            if (rc.IsFailure()) return rc;

            string metaFilePath = $"/{(int)type:x8}.meta";

            if (size < 0) return ResultFs.OutOfRange.Log();

            return metaDirFs.CreateFile(metaFilePath.ToU8Span(), size, CreateFileOptions.None);
        }

        public Result CreateSaveDataFileSystem(ulong saveDataId, ref SaveDataAttribute attribute,
            ref SaveDataCreationInfo creationInfo, U8Span rootPath, OptionalHashSalt hashSalt, bool something)
        {
            // Use directory save data for now

            Result rc = OpenSaveDataDirectory(out IFileSystem fileSystem, creationInfo.SpaceId, string.Empty, false);
            if (rc.IsFailure()) return rc;

            return fileSystem.EnsureDirectoryExists(GetSaveDataIdPath(saveDataId));
        }

        public Result DeleteSaveDataFileSystem(SaveDataSpaceId spaceId, ulong saveDataId, bool doSecureDelete)
        {
            Result rc = OpenSaveDataDirectory(out IFileSystem fileSystem, spaceId, string.Empty, false);

            using (fileSystem)
            {
                if (rc.IsFailure()) return rc;

                var saveDataPath = GetSaveDataIdPath(saveDataId).ToU8Span();

                rc = fileSystem.GetEntryType(out DirectoryEntryType entryType, saveDataPath);
                if (rc.IsFailure()) return rc;

                if (entryType == DirectoryEntryType.Directory)
                {
                    rc = fileSystem.DeleteDirectoryRecursively(saveDataPath);
                }
                else
                {
                    if (doSecureDelete)
                    {
                        // Overwrite file with garbage before deleting
                        throw new NotImplementedException();
                    }

                    rc = fileSystem.DeleteFile(saveDataPath);
                }

                return rc;
            }
        }

        public Result SetGlobalAccessLogMode(GlobalAccessLogMode mode)
        {
            LogMode = mode;
            return Result.Success;
        }

        public Result GetGlobalAccessLogMode(out GlobalAccessLogMode mode)
        {
            mode = LogMode;
            return Result.Success;
        }

        internal void SetSaveDataIndexerManager(ISaveDataIndexerManager manager)
        {
            SaveDataIndexerManager = manager;
        }

        internal Result OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, out bool neededInit, SaveDataSpaceId spaceId)
        {
            return SaveDataIndexerManager.OpenAccessor(out accessor, out neededInit, spaceId);
        }

        private string GetSaveDataIdPath(ulong saveDataId)
        {
            return $"/{saveDataId:x16}";
        }
    }
}
