using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using LibHac.Kvdb;
using LibHac.Ncm;
using LibHac.Spl;

namespace LibHac.FsSrv
{
    public class FileSystemProxy : IFileSystemProxy, IFileSystemProxyForLoader
    {
        private FileSystemProxyCore FsProxyCore { get; }
        internal HorizonClient Hos { get; }

        public ulong CurrentProcess { get; private set; }

        public long SaveDataSize { get; private set; }
        public long SaveDataJournalSize { get; private set; }
        public FsPath SaveDataRootPath { get; }
        public bool AutoCreateSaveData { get; private set; }

        internal FileSystemProxy(HorizonClient horizonClient, FileSystemProxyCore fsProxyCore)
        {
            FsProxyCore = fsProxyCore;
            Hos = horizonClient;

            CurrentProcess = ulong.MaxValue;
            SaveDataSize = 0x2000000;
            SaveDataJournalSize = 0x1000000;
            AutoCreateSaveData = true;
        }

        private ProgramRegistryService GetProgramRegistryService()
        {
            return new ProgramRegistryService(FsProxyCore.Config.ProgramRegistryServiceImpl, CurrentProcess);
        }

        public Result OpenFileSystemWithId(out IFileSystem fileSystem, ref FsPath path, ulong id, FileSystemProxyType type)
        {
            fileSystem = default;

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            AccessControl ac = programInfo.AccessControl;

            switch (type)
            {
                case FileSystemProxyType.Logo:
                    if (!ac.GetAccessibilityFor(AccessibilityType.MountLogo).CanRead)
                        return ResultFs.PermissionDenied.Log();
                    break;
                case FileSystemProxyType.Control:
                    if (!ac.GetAccessibilityFor(AccessibilityType.MountContentControl).CanRead)
                        return ResultFs.PermissionDenied.Log();
                    break;
                case FileSystemProxyType.Manual:
                    if (!ac.GetAccessibilityFor(AccessibilityType.MountContentManual).CanRead)
                        return ResultFs.PermissionDenied.Log();
                    break;
                case FileSystemProxyType.Meta:
                    if (!ac.GetAccessibilityFor(AccessibilityType.MountContentMeta).CanRead)
                        return ResultFs.PermissionDenied.Log();
                    break;
                case FileSystemProxyType.Data:
                    if (!ac.GetAccessibilityFor(AccessibilityType.MountContentData).CanRead)
                        return ResultFs.PermissionDenied.Log();
                    break;
                case FileSystemProxyType.Package:
                    if (!ac.GetAccessibilityFor(AccessibilityType.MountApplicationPackage).CanRead)
                        return ResultFs.PermissionDenied.Log();
                    break;
                default:
                    return ResultFs.InvalidArgument.Log();
            }

            if (type == FileSystemProxyType.Meta)
            {
                id = ulong.MaxValue;
            }
            else if (id == ulong.MaxValue)
            {
                return ResultFs.InvalidArgument.Log();
            }

            bool canMountSystemDataPrivate = ac.GetAccessibilityFor(AccessibilityType.MountSystemDataPrivate).CanRead;

            var normalizer = new PathNormalizer(path, GetPathNormalizerOptions(path));
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            return FsProxyCore.OpenFileSystem(out fileSystem, normalizer.Path, type, canMountSystemDataPrivate, id);

            // Missing speed emulation storage type wrapper, async wrapper, and FileSystemInterfaceAdapter
        }

        private PathNormalizer.Option GetPathNormalizerOptions(U8Span path)
        {
            int hostMountLength = StringUtils.GetLength(CommonMountNames.HostRootFileSystemMountName,
                PathTools.MountNameLengthMax);

            bool isHostPath = StringUtils.Compare(path, CommonMountNames.HostRootFileSystemMountName, hostMountLength) == 0;

            PathNormalizer.Option hostOption = isHostPath ? PathNormalizer.Option.PreserveUnc : PathNormalizer.Option.None;
            return PathNormalizer.Option.HasMountName | PathNormalizer.Option.PreserveTailSeparator | hostOption;
        }

        public Result OpenFileSystemWithPatch(out IFileSystem fileSystem, ProgramId programId, FileSystemProxyType type)
        {
            throw new NotImplementedException();
        }

        public Result OpenCodeFileSystem(out IFileSystem fileSystem, out CodeVerificationData verificationData, in FspPath path,
            ProgramId programId)
        {
            throw new NotImplementedException();
        }

        public Result IsArchivedProgram(out bool isArchived, ulong processId)
        {
            throw new NotImplementedException();
        }

        public Result SetCurrentProcess(ulong processId)
        {
            CurrentProcess = processId;

            return Result.Success;
        }

        public Result GetFreeSpaceSizeForSaveData(out long freeSpaceSize, SaveDataSpaceId spaceId)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataFileSystemByCurrentProcess(out IFileSystem fileSystem)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataFileSystemByProgramId(out IFileSystem fileSystem, ProgramId programId)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataStorageByCurrentProcess(out IStorage storage)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataStorageByProgramId(out IStorage storage, ProgramId programId)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataStorageByDataId(out IStorage storage, DataId dataId, StorageId storageId)
        {
            throw new NotImplementedException();
        }

        public Result OpenPatchDataStorageByCurrentProcess(out IStorage storage)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataFileSystemWithProgramIndex(out IFileSystem fileSystem, byte programIndex)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataStorageWithProgramIndex(out IStorage storage, byte programIndex)
        {
            throw new NotImplementedException();
        }

        public Result RegisterSaveDataFileSystemAtomicDeletion(ReadOnlySpan<ulong> saveDataIds)
        {
            throw new NotImplementedException();
        }

        public Result DeleteSaveDataFileSystem(ulong saveDataId)
        {
            return DeleteSaveDataFileSystemImpl(SaveDataSpaceId.System, saveDataId);
        }

        private Result DeleteSaveDataFileSystemImpl(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            SaveDataIndexerAccessor accessor = null;

            try
            {
                SaveDataType saveType;

                if (saveDataId == FileSystemServer.SaveIndexerId)
                {
                    if (!IsCurrentProcess(CurrentProcess))
                        return ResultFs.PermissionDenied.Log();

                    saveType = SaveDataType.System;
                }
                else
                {
                    rc = OpenSaveDataIndexerAccessor(out accessor, spaceId);
                    if (rc.IsFailure()) return rc;

                    if (spaceId != SaveDataSpaceId.ProperSystem && spaceId != SaveDataSpaceId.SafeMode)
                    {
                        rc = accessor.Indexer.GetValue(out SaveDataIndexerValue value, saveDataId);
                        if (rc.IsFailure()) return rc;

                        spaceId = value.SpaceId;
                    }

                    rc = accessor.Indexer.GetKey(out SaveDataAttribute key, saveDataId);
                    if (rc.IsFailure()) return rc;

                    if (key.Type == SaveDataType.System || key.Type == SaveDataType.SystemBcat)
                    {
                        if (!programInfo.AccessControl.CanCall(OperationType.DeleteSystemSaveData))
                            return ResultFs.PermissionDenied.Log();
                    }
                    else
                    {
                        if (!programInfo.AccessControl.CanCall(OperationType.DeleteSaveData))
                            return ResultFs.PermissionDenied.Log();
                    }

                    saveType = key.Type;

                    rc = accessor.Indexer.SetState(saveDataId, SaveDataState.Creating);
                    if (rc.IsFailure()) return rc;

                    rc = accessor.Indexer.Commit();
                    if (rc.IsFailure()) return rc;
                }

                rc = DeleteSaveDataFileSystemImpl2(spaceId, saveDataId, saveType, false);
                if (rc.IsFailure()) return rc;

                if (saveDataId != FileSystemServer.SaveIndexerId)
                {
                    // ReSharper disable once PossibleNullReferenceException
                    rc = accessor.Indexer.Delete(saveDataId);
                    if (rc.IsFailure()) return rc;

                    rc = accessor.Indexer.Commit();
                    if (rc.IsFailure()) return rc;
                }

                return Result.Success;
            }
            finally { accessor?.Dispose(); }
        }

        // ReSharper disable once UnusedParameter.Local
        private Result DeleteSaveDataFileSystemImpl2(SaveDataSpaceId spaceId, ulong saveDataId, SaveDataType type, bool shouldWipe)
        {
            // missing: Check extra data flags for this value. Bit 3
            bool doSecureDelete = shouldWipe;

            Result rc = FsProxyCore.DeleteSaveDataMetaFiles(saveDataId, spaceId);
            if (rc.IsFailure() && !ResultFs.PathNotFound.Includes(rc))
                return rc;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            rc = FsProxyCore.DeleteSaveDataFileSystem(spaceId, saveDataId, doSecureDelete);
            if (rc.IsFailure() && !ResultFs.PathNotFound.Includes(rc))
                return rc;

            return Result.Success;
        }

        public Result DeleteSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            return DeleteSaveDataFileSystemBySaveDataSpaceIdImpl(spaceId, saveDataId);
        }

        private Result DeleteSaveDataFileSystemBySaveDataSpaceIdImpl(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            if (saveDataId != FileSystemServer.SaveIndexerId)
            {
                Result rc = OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, spaceId);
                if (rc.IsFailure()) return rc;

                using (accessor)
                {
                    rc = accessor.Indexer.GetValue(out SaveDataIndexerValue value, saveDataId);
                    if (rc.IsFailure()) return rc;

                    if (value.SpaceId != GetSpaceIdForIndexer(spaceId))
                        return ResultFs.TargetNotFound.Log();
                }
            }

            return DeleteSaveDataFileSystemImpl(spaceId, saveDataId);
        }

        private Result GetSaveDataInfo(out SaveDataInfo info, SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
        {
            Unsafe.SkipInit(out info);

            Result rc = OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, spaceId);
            if (rc.IsFailure()) return rc;

            using (accessor)
            {
                rc = accessor.Indexer.Get(out SaveDataIndexerValue value, in attribute);
                if (rc.IsFailure()) return rc;

                SaveDataIndexer.GenerateSaveDataInfo(out info, in attribute, in value);
                return Result.Success;
            }
        }

        public Result DeleteSaveDataFileSystemBySaveDataAttribute(SaveDataSpaceId spaceId, ref SaveDataAttribute attribute)
        {
            Result rs = GetSaveDataInfo(out SaveDataInfo info, spaceId, in attribute);
            if (rs.IsFailure()) return rs;

            return DeleteSaveDataFileSystemBySaveDataSpaceIdImpl(spaceId, info.SaveDataId);
        }

        public Result UpdateSaveDataMacForDebug(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        private Result CreateSaveDataFileSystemImpl(ref SaveDataAttribute attribute, ref SaveDataCreationInfo creationInfo,
            ref SaveMetaCreateInfo metaCreateInfo, ref OptionalHashSalt hashSalt, bool something)
        {
            ulong saveDataId = 0;
            bool isDeleteNeeded = false;
            Result rc;

            SaveDataIndexerAccessor accessor = null;

            // Missing permission checks

            try
            {
                if (attribute.StaticSaveDataId == FileSystemServer.SaveIndexerId)
                {
                    saveDataId = FileSystemServer.SaveIndexerId;
                    rc = FsProxyCore.DoesSaveDataExist(out bool saveExists, creationInfo.SpaceId, saveDataId);

                    if (rc.IsSuccess() && saveExists)
                    {
                        return ResultFs.PathAlreadyExists.Log();
                    }

                    isDeleteNeeded = true;
                }
                else
                {
                    rc = OpenSaveDataIndexerAccessor(out accessor, creationInfo.SpaceId);
                    if (rc.IsFailure()) return rc;

                    SaveDataAttribute indexerKey = attribute;

                    if (attribute.StaticSaveDataId != 0 && attribute.UserId == UserId.Zero)
                    {
                        saveDataId = attribute.StaticSaveDataId;

                        rc = accessor.Indexer.PutStaticSaveDataIdIndex(in indexerKey);
                    }
                    else
                    {
                        if (attribute.Type != SaveDataType.System &&
                            attribute.Type != SaveDataType.SystemBcat)
                        {
                            if (accessor.Indexer.IsRemainedReservedOnly())
                            {
                                return ResultKvdb.OutOfKeyResource.Log();
                            }
                        }

                        rc = accessor.Indexer.Publish(out saveDataId, in indexerKey);
                    }

                    if (ResultFs.SaveDataPathAlreadyExists.Includes(rc))
                    {
                        return ResultFs.PathAlreadyExists.LogConverted(rc);
                    }

                    isDeleteNeeded = true;

                    rc = accessor.Indexer.SetState(saveDataId, SaveDataState.Creating);
                    if (rc.IsFailure()) return rc;

                    SaveDataSpaceId indexerSpaceId = GetSpaceIdForIndexer(creationInfo.SpaceId);

                    rc = accessor.Indexer.SetSpaceId(saveDataId, indexerSpaceId);
                    if (rc.IsFailure()) return rc;

                    // todo: calculate size
                    long size = 0;

                    rc = accessor.Indexer.SetSize(saveDataId, size);
                    if (rc.IsFailure()) return rc;

                    rc = accessor.Indexer.Commit();
                    if (rc.IsFailure()) return rc;
                }

                rc = FsProxyCore.CreateSaveDataFileSystem(saveDataId, ref attribute, ref creationInfo, SaveDataRootPath,
                    hashSalt, false);

                if (rc.IsFailure())
                {
                    if (!ResultFs.PathAlreadyExists.Includes(rc)) return rc;

                    rc = DeleteSaveDataFileSystemImpl2(creationInfo.SpaceId, saveDataId, attribute.Type, false);
                    if (rc.IsFailure()) return rc;

                    rc = FsProxyCore.CreateSaveDataFileSystem(saveDataId, ref attribute, ref creationInfo, SaveDataRootPath,
                        hashSalt, false);
                    if (rc.IsFailure()) return rc;
                }

                if (metaCreateInfo.Type != SaveDataMetaType.None)
                {
                    rc = FsProxyCore.CreateSaveDataMetaFile(saveDataId, creationInfo.SpaceId, metaCreateInfo.Type,
                        metaCreateInfo.Size);
                    if (rc.IsFailure()) return rc;

                    if (metaCreateInfo.Type == SaveDataMetaType.Thumbnail)
                    {
                        rc = FsProxyCore.OpenSaveDataMetaFile(out IFile metaFile, saveDataId, creationInfo.SpaceId,
                            metaCreateInfo.Type);

                        using (metaFile)
                        {
                            if (rc.IsFailure()) return rc;

                            ReadOnlySpan<byte> metaFileData = stackalloc byte[0x20];

                            rc = metaFile.Write(0, metaFileData, WriteOption.Flush);
                            if (rc.IsFailure()) return rc;
                        }
                    }
                }

                if (attribute.StaticSaveDataId == FileSystemServer.SaveIndexerId || something)
                {
                    isDeleteNeeded = false;

                    return Result.Success;
                }

                // accessor shouldn't ever be null, but checking makes the analyzers happy
                Abort.DoAbortUnless(accessor != null);

                rc = accessor.Indexer.SetState(saveDataId, SaveDataState.Normal);
                if (rc.IsFailure()) return rc;

                rc = accessor.Indexer.Commit();
                if (rc.IsFailure()) return rc;

                isDeleteNeeded = false;

                return Result.Success;
            }
            finally
            {
                // Revert changes if an error happened in the middle of creation
                if (isDeleteNeeded)
                {
                    DeleteSaveDataFileSystemImpl2(creationInfo.SpaceId, saveDataId, attribute.Type, false).IgnoreResult();

                    if (accessor != null && saveDataId != FileSystemServer.SaveIndexerId)
                    {
                        rc = accessor.Indexer.GetValue(out SaveDataIndexerValue value, saveDataId);

                        if (rc.IsSuccess() && value.SpaceId == creationInfo.SpaceId)
                        {
                            accessor.Indexer.Delete(saveDataId).IgnoreResult();
                            accessor.Indexer.Commit().IgnoreResult();
                        }
                    }
                }

                accessor?.Dispose();
            }
        }

        public Result CreateSaveDataFileSystem(ref SaveDataAttribute attribute, ref SaveDataCreationInfo creationInfo,
            ref SaveMetaCreateInfo metaCreateInfo)
        {
            OptionalHashSalt hashSalt = default;

            return CreateUserSaveDataFileSystem(ref attribute, ref creationInfo, ref metaCreateInfo, ref hashSalt);
        }

        public Result CreateSaveDataFileSystemWithHashSalt(ref SaveDataAttribute attribute, ref SaveDataCreationInfo creationInfo,
            ref SaveMetaCreateInfo metaCreateInfo, ref HashSalt hashSalt)
        {
            var hashSaltCopy = new OptionalHashSalt
            {
                IsSet = true,
                HashSalt = hashSalt
            };

            return CreateUserSaveDataFileSystem(ref attribute, ref creationInfo, ref metaCreateInfo, ref hashSaltCopy);
        }

        private Result CreateUserSaveDataFileSystem(ref SaveDataAttribute attribute, ref SaveDataCreationInfo creationInfo,
            ref SaveMetaCreateInfo metaCreateInfo, ref OptionalHashSalt hashSalt)
        {
            return CreateSaveDataFileSystemImpl(ref attribute, ref creationInfo, ref metaCreateInfo, ref hashSalt, false);
        }

        public Result CreateSaveDataFileSystemBySystemSaveDataId(ref SaveDataAttribute attribute, ref SaveDataCreationInfo creationInfo)
        {
            if (!IsSystemSaveDataId(attribute.StaticSaveDataId))
                return ResultFs.InvalidArgument.Log();

            SaveDataCreationInfo newCreationInfo = creationInfo;

            if (creationInfo.OwnerId == 0)
            {
                // todo: Assign the current program's ID
                // throw new NotImplementedException();
            }

            // Missing permission checks

            if (attribute.Type == SaveDataType.SystemBcat)
            {
                newCreationInfo.OwnerId = SystemProgramId.Bcat.Value;
            }

            SaveMetaCreateInfo metaCreateInfo = default;
            OptionalHashSalt optionalHashSalt = default;

            return CreateSaveDataFileSystemImpl(ref attribute, ref newCreationInfo, ref metaCreateInfo,
                ref optionalHashSalt, false);
        }

        public Result ExtendSaveDataFileSystem(SaveDataSpaceId spaceId, ulong saveDataId, long dataSize, long journalSize)
        {
            throw new NotImplementedException();
        }

        private Result OpenSaveDataFileSystemImpl(out IFileSystem fileSystem, out ulong saveDataId,
            SaveDataSpaceId spaceId, ref SaveDataAttribute attribute, bool openReadOnly, bool cacheExtraData)
        {
            fileSystem = default;
            saveDataId = default;

            bool hasFixedId = attribute.StaticSaveDataId != 0 && attribute.UserId == UserId.Zero;

            if (hasFixedId)
            {
                saveDataId = attribute.StaticSaveDataId;
            }
            else
            {
                SaveDataAttribute indexerKey = attribute;

                Result rc = OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor tempAccessor, spaceId);
                using SaveDataIndexerAccessor accessor = tempAccessor;
                if (rc.IsFailure()) return rc;

                rc = accessor.Indexer.Get(out SaveDataIndexerValue indexerValue, in indexerKey);
                if (rc.IsFailure()) return rc;

                SaveDataSpaceId indexerSpaceId = GetSpaceIdForIndexer(spaceId);

                if (indexerValue.SpaceId != indexerSpaceId)
                    return ResultFs.TargetNotFound.Log();

                if (indexerValue.State == SaveDataState.Extending)
                    return ResultFs.SaveDataIsExtending.Log();

                saveDataId = indexerValue.SaveDataId;
            }

            Result saveFsResult = FsProxyCore.OpenSaveDataFileSystem(out fileSystem, spaceId, saveDataId,
                SaveDataRootPath.ToString(), openReadOnly, attribute.Type, cacheExtraData);

            if (saveFsResult.IsSuccess()) return Result.Success;

            if (!ResultFs.PathNotFound.Includes(saveFsResult) && !ResultFs.TargetNotFound.Includes(saveFsResult)) return saveFsResult;

            if (saveDataId != FileSystemServer.SaveIndexerId)
            {
                if (hasFixedId)
                {
                    // todo: remove save indexer entry
                }
            }

            if (ResultFs.PathNotFound.Includes(saveFsResult))
            {
                return ResultFs.TargetNotFound.LogConverted(saveFsResult);
            }

            return saveFsResult;
        }

        private Result OpenSaveDataFileSystem3(out IFileSystem fileSystem, SaveDataSpaceId spaceId,
            ref SaveDataAttribute attribute, bool openReadOnly)
        {
            // Missing check if the open limit has been hit

            Result rc = OpenSaveDataFileSystemImpl(out fileSystem, out _, spaceId, ref attribute, openReadOnly, true);

            // Missing permission check based on the save's owner ID,
            // speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            return rc;
        }

        private Result OpenSaveDataFileSystem2(out IFileSystem fileSystem, SaveDataSpaceId spaceId,
            ref SaveDataAttribute attribute, bool openReadOnly)
        {
            fileSystem = default;

            // Missing permission checks

            SaveDataAttribute attributeCopy;

            if (attribute.ProgramId == ProgramId.InvalidId)
            {
                throw new NotImplementedException();
            }
            else
            {
                attributeCopy = attribute;
            }

            SaveDataSpaceId actualSpaceId;

            if (attributeCopy.Type == SaveDataType.Cache)
            {
                // Check whether the save is on the SD card or the BIS
                Result rc = GetSpaceIdForCacheStorage(out actualSpaceId, attributeCopy.ProgramId);
                if (rc.IsFailure()) return rc;
            }
            else
            {
                actualSpaceId = spaceId;
            }

            return OpenSaveDataFileSystem3(out fileSystem, actualSpaceId, ref attributeCopy, openReadOnly);
        }

        public Result OpenSaveDataFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId, ref SaveDataAttribute attribute)
        {
            return OpenSaveDataFileSystem2(out fileSystem, spaceId, ref attribute, false);
        }

        public Result OpenReadOnlySaveDataFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId,
            ref SaveDataAttribute attribute)
        {
            return OpenSaveDataFileSystem2(out fileSystem, spaceId, ref attribute, true);
        }

        public Result OpenSaveDataFileSystemBySystemSaveDataId(out IFileSystem fileSystem, SaveDataSpaceId spaceId,
            ref SaveDataAttribute attribute)
        {
            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter
            fileSystem = default;

            if (!IsSystemSaveDataId(attribute.StaticSaveDataId)) return ResultFs.InvalidArgument.Log();

            Result rc = OpenSaveDataFileSystemImpl(out IFileSystem saveFs, out _, spaceId,
                ref attribute, false, true);
            if (rc.IsFailure()) return rc;

            // Missing check if the current title owns the save data or can open it

            fileSystem = saveFs;

            return Result.Success;
        }

        public Result ReadSaveDataFileSystemExtraData(Span<byte> extraDataBuffer, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result ReadSaveDataFileSystemExtraDataBySaveDataSpaceId(Span<byte> extraDataBuffer, SaveDataSpaceId spaceId,
            ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result ReadSaveDataFileSystemExtraDataBySaveDataAttribute(Span<byte> extraDataBuffer, SaveDataSpaceId spaceId,
            ref SaveDataAttribute attribute)
        {
            throw new NotImplementedException();
        }

        public Result WriteSaveDataFileSystemExtraData(ulong saveDataId, SaveDataSpaceId spaceId, ReadOnlySpan<byte> extraDataBuffer)
        {
            throw new NotImplementedException();
        }

        public Result WriteSaveDataFileSystemExtraDataBySaveDataAttribute(ref SaveDataAttribute attribute, SaveDataSpaceId spaceId,
            ReadOnlySpan<byte> extraDataBuffer, ReadOnlySpan<byte> maskBuffer)
        {
            throw new NotImplementedException();
        }

        public Result WriteSaveDataFileSystemExtraDataWithMask(ulong saveDataId, SaveDataSpaceId spaceId, ReadOnlySpan<byte> extraDataBuffer,
            ReadOnlySpan<byte> maskBuffer)
        {
            throw new NotImplementedException();
        }

        public Result OpenImageDirectoryFileSystem(out IFileSystem fileSystem, ImageDirectoryId dirId)
        {
            throw new NotImplementedException();
        }

        public Result RegisterProgramIndexMapInfo(ReadOnlySpan<byte> programIndexMapInfoBuffer, int programCount)
        {
            return GetProgramRegistryService().RegisterProgramIndexMapInfo(programIndexMapInfoBuffer, programCount);
        }

        public Result SetBisRootForHost(BisPartitionId partitionId, ref FsPath path)
        {
            throw new NotImplementedException();
        }

        public Result OpenBisFileSystem(out IFileSystem fileSystem, ref FsPath rootPath, BisPartitionId partitionId)
        {
            fileSystem = default;

            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            Result rc = PathTools.Normalize(out U8Span normalizedPath, rootPath);
            if (rc.IsFailure()) return rc;

            return FsProxyCore.OpenBisFileSystem(out fileSystem, normalizedPath.ToString(), partitionId);
        }

        public Result OpenBisStorage(out IStorage storage, BisPartitionId partitionId)
        {
            throw new NotImplementedException();
        }

        public Result InvalidateBisCache()
        {
            throw new NotImplementedException();
        }

        public Result OpenHostFileSystemWithOption(out IFileSystem fileSystem, ref FsPath path, MountHostOption option)
        {
            // Missing permission check

            return FsProxyCore.OpenHostFileSystem(out fileSystem, new U8Span(path.Str), option.HasFlag(MountHostOption.PseudoCaseSensitive));
        }

        public Result OpenHostFileSystem(out IFileSystem fileSystem, ref FsPath path)
        {
            return OpenHostFileSystemWithOption(out fileSystem, ref path, MountHostOption.None);
        }

        public Result OpenSdCardFileSystem(out IFileSystem fileSystem)
        {
            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            return FsProxyCore.OpenSdCardFileSystem(out fileSystem);
        }

        public Result FormatSdCardFileSystem()
        {
            throw new NotImplementedException();
        }

        public Result FormatSdCardDryRun()
        {
            throw new NotImplementedException();
        }

        public Result IsExFatSupported(out bool isSupported)
        {
            throw new NotImplementedException();
        }

        public Result OpenGameCardStorage(out IStorage storage, GameCardHandle handle, GameCardPartitionRaw partitionId)
        {
            // Missing permission check and StorageInterfaceAdapter

            return FsProxyCore.OpenGameCardStorage(out storage, handle, partitionId);
        }

        public Result OpenDeviceOperator(out IDeviceOperator deviceOperator)
        {
            // Missing permission check

            return FsProxyCore.OpenDeviceOperator(out deviceOperator);
        }

        public Result OpenSaveDataInfoReader(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader)
        {
            infoReader = default;

            // Missing permission check

            Result rc = OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, SaveDataSpaceId.System);
            if (rc.IsFailure()) return rc;

            using (accessor)
            {
                return accessor.Indexer.OpenSaveDataInfoReader(out infoReader);
            }
        }

        public Result OpenSaveDataInfoReaderBySaveDataSpaceId(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader, SaveDataSpaceId spaceId)
        {
            infoReader = default;

            // Missing permission check

            Result rc = OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, spaceId);
            if (rc.IsFailure()) return rc;

            using (accessor)
            {
                rc = accessor.Indexer.OpenSaveDataInfoReader(
                    out ReferenceCountedDisposable<ISaveDataInfoReader> baseInfoReader);
                if (rc.IsFailure()) return rc;

                var filter = new SaveDataFilterInternal
                {
                    FilterBySaveDataSpaceId = true,
                    SpaceId = GetSpaceIdForIndexer(spaceId)
                };

                var filterReader = new SaveDataInfoFilterReader(baseInfoReader, ref filter);
                infoReader = new ReferenceCountedDisposable<ISaveDataInfoReader>(filterReader);

                return Result.Success;
            }
        }

        public Result OpenSaveDataInfoReaderWithFilter(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader, SaveDataSpaceId spaceId,
            ref SaveDataFilter filter)
        {
            infoReader = default;

            // Missing permission check

            Result rc = OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, spaceId);
            if (rc.IsFailure()) return rc;

            using (accessor)
            {
                rc = accessor.Indexer.OpenSaveDataInfoReader(
                    out ReferenceCountedDisposable<ISaveDataInfoReader> baseInfoReader);
                if (rc.IsFailure()) return rc;

                var filterInternal = new SaveDataFilterInternal(ref filter, spaceId);

                var filterReader = new SaveDataInfoFilterReader(baseInfoReader, ref filterInternal);
                infoReader = new ReferenceCountedDisposable<ISaveDataInfoReader>(filterReader);

                return Result.Success;
            }
        }

        public Result FindSaveDataWithFilter(out long count, Span<byte> saveDataInfoBuffer, SaveDataSpaceId spaceId,
            ref SaveDataFilter filter)
        {
            count = default;

            if (saveDataInfoBuffer.Length != Unsafe.SizeOf<SaveDataInfo>())
            {
                return ResultFs.InvalidArgument.Log();
            }

            // Missing permission check

            var internalFilter = new SaveDataFilterInternal(ref filter, GetSpaceIdForIndexer(spaceId));

            ref SaveDataInfo saveDataInfo = ref Unsafe.As<byte, SaveDataInfo>(ref saveDataInfoBuffer[0]);

            return FindSaveDataWithFilterImpl(out count, out saveDataInfo, spaceId, ref internalFilter);
        }

        private Result FindSaveDataWithFilterImpl(out long count, out SaveDataInfo info, SaveDataSpaceId spaceId,
            ref SaveDataFilterInternal filter)
        {
            count = default;
            info = default;


            Result rc = OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, spaceId);
            if (rc.IsFailure()) return rc;

            using (accessor)
            {
                rc = accessor.Indexer.OpenSaveDataInfoReader(
                    out ReferenceCountedDisposable<ISaveDataInfoReader> baseInfoReader);
                if (rc.IsFailure()) return rc;

                using (var infoReader = new SaveDataInfoFilterReader(baseInfoReader, ref filter))
                {
                    return infoReader.Read(out count, SpanHelpers.AsByteSpan(ref info));
                }
            }
        }

        public Result OpenSaveDataInternalStorageFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result QuerySaveDataInternalStorageTotalSize(out long size, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result GetSaveDataCommitId(out long commitId, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataInfoReaderOnlyCacheStorage(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataMetaFile(out IFile file, SaveDataSpaceId spaceId, ref SaveDataAttribute attribute,
            SaveDataMetaType type)
        {
            throw new NotImplementedException();
        }

        private Result GetSpaceIdForCacheStorage(out SaveDataSpaceId spaceId, ProgramId programId)
        {
            spaceId = default;

            if (FsProxyCore.IsSdCardAccessible)
            {
                var filter = new SaveDataFilterInternal();

                filter.SetSaveDataSpaceId(SaveDataSpaceId.SdCache);
                filter.SetProgramId(programId);
                filter.SetSaveDataType(SaveDataType.Cache);

                Result rc = FindSaveDataWithFilterImpl(out long count, out _, SaveDataSpaceId.SdCache, ref filter);
                if (rc.IsFailure()) return rc;

                if (count > 0)
                {
                    spaceId = SaveDataSpaceId.SdCache;
                    return Result.Success;
                }
            }

            {
                var filter = new SaveDataFilterInternal();

                filter.SetSaveDataSpaceId(SaveDataSpaceId.User);
                filter.SetProgramId(programId);
                filter.SetSaveDataType(SaveDataType.Cache);

                Result rc = FindSaveDataWithFilterImpl(out long count, out _, SaveDataSpaceId.User, ref filter);
                if (rc.IsFailure()) return rc;

                if (count > 0)
                {
                    spaceId = SaveDataSpaceId.User;
                    return Result.Success;
                }
            }

            return ResultFs.TargetNotFound.Log();
        }

        public Result DeleteCacheStorage(short index)
        {
            throw new NotImplementedException();
        }

        public Result GetCacheStorageSize(out long dataSize, out long journalSize, short index)
        {
            throw new NotImplementedException();
        }

        public Result ListAccessibleSaveDataOwnerId(out int readCount, Span<Ncm.ApplicationId> idBuffer, ProgramId programId, int startIndex,
            int bufferIdCount)
        {
            throw new NotImplementedException();
        }

        public Result SetSaveDataSize(long saveDataSize, long saveDataJournalSize)
        {
            if (saveDataSize < 0 || saveDataJournalSize < 0)
            {
                return ResultFs.InvalidSize.Log();
            }

            SaveDataSize = saveDataSize;
            SaveDataJournalSize = saveDataJournalSize;

            return Result.Success;
        }
        public Result SetSaveDataRootPath(ref FsPath path)
        {
            // Missing permission check

            if (StringUtils.GetLength(path.Str, FsPath.MaxLength + 1) > FsPath.MaxLength)
            {
                return ResultFs.TooLongPath.Log();
            }

            StringUtils.Copy(SaveDataRootPath.Str, path.Str);

            return Result.Success;
        }

        public Result OpenContentStorageFileSystem(out IFileSystem fileSystem, ContentStorageId storageId)
        {
            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            return FsProxyCore.OpenContentStorageFileSystem(out fileSystem, storageId);
        }

        public Result OpenCloudBackupWorkStorageFileSystem(out IFileSystem fileSystem, CloudBackupWorkStorageId storageId)
        {
            throw new NotImplementedException();
        }

        public Result OpenCustomStorageFileSystem(out IFileSystem fileSystem, CustomStorageId storageId)
        {
            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            return FsProxyCore.OpenCustomStorageFileSystem(out fileSystem, storageId);
        }

        public Result OpenGameCardFileSystem(out IFileSystem fileSystem, GameCardHandle handle,
            GameCardPartition partitionId)
        {
            // Missing permission check and FileSystemInterfaceAdapter

            return FsProxyCore.OpenGameCardFileSystem(out fileSystem, handle, partitionId);
        }

        public Result QuerySaveDataTotalSize(out long totalSize, long dataSize, long journalSize)
        {
            // todo: Implement properly

            totalSize = 0;

            return Result.Success;
        }

        public Result SetCurrentPosixTimeWithTimeDifference(long time, int difference)
        {
            throw new NotImplementedException();
        }

        public Result GetRightsId(out RightsId rightsId, ProgramId programId, StorageId storageId)
        {
            throw new NotImplementedException();
        }

        public Result GetRightsIdByPath(out RightsId rightsId, ref FsPath path)
        {
            throw new NotImplementedException();
        }

        public Result GetRightsIdAndKeyGenerationByPath(out RightsId rightsId, out byte keyGeneration, ref FsPath path)
        {
            throw new NotImplementedException();
        }

        public Result RegisterExternalKey(ref RightsId rightsId, ref AccessKey externalKey)
        {
            // Missing permission check

            return FsProxyCore.RegisterExternalKey(ref rightsId, ref externalKey);
        }

        public Result UnregisterExternalKey(ref RightsId rightsId)
        {
            // Missing permission check

            return FsProxyCore.UnregisterExternalKey(ref rightsId);
        }

        public Result UnregisterAllExternalKey()
        {
            // Missing permission check

            return FsProxyCore.UnregisterAllExternalKey();
        }

        public Result SetSdCardEncryptionSeed(ref EncryptionSeed seed)
        {
            // Missing permission check

            Result rc = FsProxyCore.SetSdCardEncryptionSeed(ref seed);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public Result VerifySaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId, Span<byte> readBuffer)
        {
            throw new NotImplementedException();
        }

        public Result VerifySaveDataFileSystem(ulong saveDataId, Span<byte> readBuffer)
        {
            throw new NotImplementedException();
        }

        public Result CorruptSaveDataFileSystemByOffset(SaveDataSpaceId spaceId, ulong saveDataId, long offset)
        {
            throw new NotImplementedException();
        }

        public Result CorruptSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result CorruptSaveDataFileSystem(ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result CreatePaddingFile(long size)
        {
            throw new NotImplementedException();
        }

        public Result DeleteAllPaddingFiles()
        {
            throw new NotImplementedException();
        }

        public Result DisableAutoSaveDataCreation()
        {
            AutoCreateSaveData = false;

            return Result.Success;
        }

        public Result SetGlobalAccessLogMode(GlobalAccessLogMode mode)
        {
            // Missing permission check

            return FsProxyCore.SetGlobalAccessLogMode(mode);
        }

        public Result GetGlobalAccessLogMode(out GlobalAccessLogMode mode)
        {
            return FsProxyCore.GetGlobalAccessLogMode(out mode);
        }

        public Result GetProgramIndexForAccessLog(out int programIndex, out int programCount)
        {
            return GetProgramRegistryService().GetProgramIndexForAccessLog(out programIndex, out programCount);
        }

        public Result OutputAccessLogToSdCard(U8Span logString)
        {
            throw new NotImplementedException();
        }

        public Result RegisterUpdatePartition()
        {
            throw new NotImplementedException();
        }

        public Result OpenRegisteredUpdatePartition(out IFileSystem fileSystem)
        {
            throw new NotImplementedException();
        }

        public Result OverrideSaveDataTransferTokenSignVerificationKey(ReadOnlySpan<byte> key)
        {
            throw new NotImplementedException();
        }

        public Result CleanUpTemporaryStorage()
        {
            Result rc = FsProxyCore.OpenSaveDataDirectory(out IFileSystem saveDirFs, SaveDataSpaceId.Temporary,
                string.Empty, false);
            if (rc.IsFailure()) return rc;

            rc = saveDirFs.CleanDirectoryRecursively("/".ToU8Span());
            if (rc.IsFailure()) return rc;

            FsProxyCore.SaveDataIndexerManager.ResetTemporaryStorageIndexer(SaveDataSpaceId.Temporary);

            return Result.Success;
        }

        public Result SetSdCardAccessibility(bool isAccessible)
        {
            // Missing permission check

            FsProxyCore.IsSdCardAccessible = isAccessible;
            return Result.Success;
        }

        public Result IsSdCardAccessible(out bool isAccessible)
        {
            isAccessible = FsProxyCore.IsSdCardAccessible;
            return Result.Success;
        }

        public Result OpenMultiCommitManager(out IMultiCommitManager commitManager)
        {
            commitManager = new MultiCommitManager(this);
            return Result.Success;
        }

        internal Result OpenMultiCommitContextSaveData(out IFileSystem fileSystem)
        {
            fileSystem = default;

            var attribute = new SaveDataAttribute(new ProgramId(MultiCommitManager.ProgramId), SaveDataType.System,
                UserId.Zero, MultiCommitManager.SaveDataId);

            Result rc = OpenSaveDataFileSystemImpl(out IFileSystem saveFs, out _, SaveDataSpaceId.System, ref attribute,
                false, true);
            if (rc.IsFailure()) return rc;

            fileSystem = saveFs;
            return Result.Success;
        }

        // todo: split the FileSystemProxy classes
        // nn::fssrv::SaveDataFileSystemService::GetSaveDataIndexerAccessor
        private Result OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, SaveDataSpaceId spaceId)
        {
            accessor = default;

            Result rc = FsProxyCore.OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessorTemp,
                out bool neededInit, spaceId);
            if (rc.IsFailure()) return rc;

            try
            {
                if (neededInit)
                {
                    // todo: nn::fssrv::SaveDataFileSystemService::CleanUpSaveDataCore
                    // nn::fssrv::SaveDataFileSystemService::CompleteSaveDataExtensionCore
                }

                accessor = accessorTemp;
                accessorTemp = null;

                return Result.Success;
            }
            finally
            {
                accessorTemp?.Dispose();
            }
        }

        private Result GetProgramInfo(out ProgramInfo programInfo)
        {
            return FsProxyCore.ProgramRegistry.GetProgramInfo(out programInfo, CurrentProcess);
        }

        internal bool IsCurrentProcess(ulong processId)
        {
            ulong currentId = Hos.Os.GetCurrentProcessId().Value;

            return processId == currentId;
        }

        private static bool IsSystemSaveDataId(ulong id)
        {
            return (long)id < 0;
        }

        private static SaveDataSpaceId GetSpaceIdForIndexer(SaveDataSpaceId spaceId)
        {
            return spaceId == SaveDataSpaceId.ProperSystem || spaceId == SaveDataSpaceId.SafeMode
                ? SaveDataSpaceId.System
                : spaceId;
        }
    }
}
