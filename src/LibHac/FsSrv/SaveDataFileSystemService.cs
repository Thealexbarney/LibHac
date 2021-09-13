﻿using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using LibHac.Kvdb;
using LibHac.Ncm;
using LibHac.Sf;
using LibHac.Util;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using IFile = LibHac.Fs.Fsa.IFile;
using IFileSf = LibHac.FsSrv.Sf.IFile;
using Path = LibHac.Fs.Path;
using SaveData = LibHac.Fs.SaveData;
using static LibHac.Fs.StringTraits;
using Utility = LibHac.FsSystem.Utility;

namespace LibHac.FsSrv
{
    /// <summary>
    /// Handles save-data-related calls for <see cref="FileSystemProxyImpl"/>.
    /// </summary>
    /// <remarks>FS will have one instance of this class for every connected process.
    /// The FS permissions of the calling process are checked on every function call.
    /// <br/>Based on FS 10.2.0 (nnSdk 10.6.0)</remarks>
    internal class SaveDataFileSystemService : ISaveDataTransferCoreInterface, ISaveDataMultiCommitCoreInterface
    {
        private const int OpenEntrySemaphoreCount = 256;
        private const int SaveMountSemaphoreCount = 10;

        private const int SaveDataBlockSize = 0x4000;

        private WeakRef<SaveDataFileSystemService> _selfReference;
        private SaveDataFileSystemServiceImpl _serviceImpl;
        private ulong _processId;
        private Path.Stored _saveDataRootPath;
        private SemaphoreAdapter _openEntryCountSemaphore;
        private SemaphoreAdapter _saveDataMountCountSemaphore;

        private HorizonClient Hos => _serviceImpl.Hos;

        private SharedRef<SaveDataFileSystemService> GetSharedFromThis() =>
            SharedRef<SaveDataFileSystemService>.Create(ref _selfReference);

        private SharedRef<ISaveDataMultiCommitCoreInterface> GetSharedMultiCommitInterfaceFromThis() =>
            SharedRef<ISaveDataMultiCommitCoreInterface>.Create(ref _selfReference);

        public SaveDataFileSystemService(SaveDataFileSystemServiceImpl serviceImpl, ulong processId)
        {
            _serviceImpl = serviceImpl;
            _processId = processId;
            _openEntryCountSemaphore = new SemaphoreAdapter(OpenEntrySemaphoreCount, OpenEntrySemaphoreCount);
            _saveDataMountCountSemaphore = new SemaphoreAdapter(SaveMountSemaphoreCount, SaveMountSemaphoreCount);
        }

        public static SharedRef<SaveDataFileSystemService> CreateShared(SaveDataFileSystemServiceImpl serviceImpl, ulong processId)
        {
            // Create the service
            var saveService = new SaveDataFileSystemService(serviceImpl, processId);

            // Wrap the service in a ref-counter and give the service a weak self-reference
            using var sharedService = new SharedRef<SaveDataFileSystemService>(saveService);
            saveService._selfReference = WeakRef<SaveDataFileSystemService>.Create(ref sharedService.Ref());

            return SharedRef<SaveDataFileSystemService>.CreateMove(ref sharedService.Ref());
        }

        private class SaveDataOpenCountAdapter : IEntryOpenCountSemaphoreManager
        {
            private SharedRef<SaveDataFileSystemService> _saveService;

            public SaveDataOpenCountAdapter(ref SharedRef<SaveDataFileSystemService> saveService)
            {
                _saveService = SharedRef<SaveDataFileSystemService>.CreateMove(ref saveService);
            }

            public Result TryAcquireEntryOpenCountSemaphore(ref UniqueRef<IUniqueLock> outSemaphore)
            {
                return _saveService.Get.TryAcquireSaveDataEntryOpenCountSemaphore(ref outSemaphore);
            }

            public void Dispose()
            {
                _saveService.Destroy();
            }
        }

        private Result CheckOpenSaveDataInfoReaderAccessControl(ProgramInfo programInfo, SaveDataSpaceId spaceId)
        {
            switch (spaceId)
            {
                case SaveDataSpaceId.System:
                case SaveDataSpaceId.SdSystem:
                case SaveDataSpaceId.ProperSystem:
                case SaveDataSpaceId.SafeMode:
                    if (!programInfo.AccessControl.CanCall(OperationType.OpenSaveDataInfoReaderForSystem))
                        return ResultFs.PermissionDenied.Log();
                    break;
                case SaveDataSpaceId.User:
                case SaveDataSpaceId.Temporary:
                case SaveDataSpaceId.SdCache:
                    if (!programInfo.AccessControl.CanCall(OperationType.OpenSaveDataInfoReader))
                        return ResultFs.PermissionDenied.Log();
                    break;
                default:
                    return ResultFs.InvalidSaveDataSpaceId.Log();
            }

            return Result.Success;
        }

        private static class SaveDataAccessibilityChecker
        {
            public delegate Result ExtraDataGetter(out SaveDataExtraData extraData);

            public static Result CheckCreate(in SaveDataAttribute attribute, in SaveDataCreationInfo creationInfo,
                ProgramInfo programInfo, ProgramId programId)
            {
                AccessControl accessControl = programInfo.AccessControl;

                if (SaveDataProperties.IsSystemSaveData(attribute.Type))
                {
                    if (creationInfo.OwnerId == programInfo.ProgramIdValue)
                    {
                        bool canAccess = accessControl.CanCall(OperationType.CreateSystemSaveData);

                        if (!canAccess)
                            return ResultFs.PermissionDenied.Log();
                    }
                    else
                    {
                        // If the program doesn't own the created save data it needs either the permission to create
                        // any system save data or it needs explicit access to the owner's save data.
                        Accessibility accessibility =
                            accessControl.GetAccessibilitySaveDataOwnedBy(creationInfo.OwnerId);

                        bool canAccess =
                            accessControl.CanCall(OperationType.CreateSystemSaveData) &&
                            accessControl.CanCall(OperationType.CreateOthersSystemSaveData) || accessibility.CanWrite;

                        if (!canAccess)
                            return ResultFs.PermissionDenied.Log();
                    }
                }
                else if (attribute.Type == SaveDataType.Account && attribute.UserId == UserId.InvalidId)
                {
                    bool canAccess =
                        accessControl.CanCall(OperationType.CreateSaveData) ||
                        accessControl.CanCall(OperationType.DebugSaveData);

                    if (!canAccess)
                        return ResultFs.PermissionDenied.Log();
                }
                else
                {
                    Result rc = GetAccessibilityForSaveData(out Accessibility accessibility, programInfo,
                        creationInfo.OwnerId);
                    if (rc.IsFailure()) return rc;

                    // If none of the above conditions apply, the program needs write access to the owner's save data.
                    // The program also needs either permission to create any save data, or it must be creating its own
                    // save data and have the permission to do so.
                    bool canAccess = accessControl.CanCall(OperationType.CreateSaveData);

                    if (accessibility.CanWrite &&
                        attribute.ProgramId == programId || attribute.ProgramId.Value == creationInfo.OwnerId)
                    {
                        canAccess |= accessControl.CanCall(OperationType.CreateOwnSaveData);
                    }

                    if (!canAccess)
                        return ResultFs.PermissionDenied.Log();
                }

                return Result.Success;
            }

            public static Result CheckOpenPre(in SaveDataAttribute attribute, ProgramInfo programInfo)
            {
                AccessControl accessControl = programInfo.AccessControl;

                if (attribute.Type == SaveDataType.Device)
                {
                    Accessibility accessibility =
                        accessControl.GetAccessibilityFor(AccessibilityType.MountDeviceSaveData);

                    bool canAccess = accessibility.CanRead && accessibility.CanWrite;

                    if (!canAccess)
                        return ResultFs.PermissionDenied.Log();
                }
                else if (attribute.Type == SaveDataType.Account)
                {
                    bool canAccess = attribute.UserId != UserId.InvalidId ||
                                     accessControl.CanCall(OperationType.DebugSaveData);

                    if (!canAccess)
                        return ResultFs.PermissionDenied.Log();
                }

                return Result.Success;
            }

            public static Result CheckOpen(in SaveDataAttribute attribute, ProgramInfo programInfo,
                ExtraDataGetter extraDataGetter)
            {
                AccessControl accessControl = programInfo.AccessControl;

                Result rc = GetAccessibilityForSaveData(out Accessibility accessibility, programInfo, extraDataGetter);
                if (rc.IsFailure()) return rc;

                // Note: This is correct. Even if a program only has read accessibility to another program's save data,
                // Nintendo gives it full read/write accessibility as of FS 12.0.0
                if (accessibility.CanRead || accessibility.CanWrite)
                    return Result.Success;

                // The program doesn't have permissions for this specific save data. Check if it has overall
                // permissions for other programs' save data.
                if (SaveDataProperties.IsSystemSaveData(attribute.Type))
                {
                    accessibility = accessControl.GetAccessibilityFor(AccessibilityType.MountOthersSystemSaveData);
                }
                else
                {
                    accessibility = accessControl.GetAccessibilityFor(AccessibilityType.MountOthersSaveData);
                }

                if (accessibility.CanRead && accessibility.CanWrite)
                    return Result.Success;

                return ResultFs.PermissionDenied.Log();
            }

            public static Result CheckDelete(in SaveDataAttribute attribute, ProgramInfo programInfo,
                ExtraDataGetter extraDataGetter)
            {
                AccessControl accessControl = programInfo.AccessControl;

                // DeleteSystemSaveData permission is needed to delete system save data
                if (SaveDataProperties.IsSystemSaveData(attribute.Type) &&
                    !accessControl.CanCall(OperationType.DeleteSystemSaveData))
                {
                    return ResultFs.PermissionDenied.Log();
                }

                // The DeleteSaveData permission allows deleting any non-system save data
                if (accessControl.CanCall(OperationType.DeleteSaveData))
                {
                    return Result.Success;
                }

                // Otherwise the program needs the DeleteOwnSaveData permission and write access to the save
                Result rc = GetAccessibilityForSaveData(out Accessibility accessibility, programInfo, extraDataGetter);
                if (rc.IsFailure()) return rc;

                if (accessControl.CanCall(OperationType.DeleteOwnSaveData) && accessibility.CanWrite)
                {
                    return Result.Success;
                }

                return ResultFs.PermissionDenied.Log();
            }

            public static Result CheckReadExtraData(in SaveDataAttribute attribute, in SaveDataExtraData mask,
                ProgramInfo programInfo, ExtraDataGetter extraDataGetter)
            {
                AccessControl accessControl = programInfo.AccessControl;

                bool canAccess = accessControl.CanCall(OperationType.ReadSaveDataFileSystemExtraData);

                Result rc = GetAccessibilityForSaveData(out Accessibility accessibility, programInfo,
                    extraDataGetter);
                if (rc.IsFailure()) return rc;

                SaveDataExtraData emptyMask = default;
                SaveDataExtraData maskWithoutRestoreFlag = mask;
                maskWithoutRestoreFlag.Flags &= ~SaveDataFlags.Restore;

                // Only read access to the save is needed to read the restore flag
                if (SpanHelpers.AsReadOnlyByteSpan(in emptyMask)
                    .SequenceEqual(SpanHelpers.AsReadOnlyByteSpan(in maskWithoutRestoreFlag)))
                {
                    canAccess |= accessibility.CanRead;
                }

                if (SaveDataProperties.IsSystemSaveData(attribute.Type))
                {
                    canAccess |= accessibility.CanRead;
                }
                else if (attribute.ProgramId == programInfo.ProgramId)
                {
                    canAccess |= accessControl.CanCall(OperationType.ReadOwnSaveDataFileSystemExtraData);
                }

                if (!canAccess)
                    return ResultFs.PermissionDenied.Log();

                return Result.Success;
            }

            public static Result CheckWriteExtraData(in SaveDataAttribute attribute, in SaveDataExtraData mask,
                ProgramInfo programInfo, ExtraDataGetter extraDataGetter)
            {
                AccessControl accessControl = programInfo.AccessControl;

                if (mask.Flags != SaveDataFlags.None)
                {
                    bool canAccess = accessControl.CanCall(OperationType.WriteSaveDataFileSystemExtraDataAll) ||
                                     accessControl.CanCall(OperationType.WriteSaveDataFileSystemExtraDataFlags);

                    if (SaveDataProperties.IsSystemSaveData(attribute.Type))
                    {
                        Result rc = GetAccessibilityForSaveData(out Accessibility accessibility, programInfo,
                            extraDataGetter);
                        if (rc.IsFailure()) return rc;

                        canAccess |= accessibility.CanWrite;
                    }

                    if ((mask.Flags & ~SaveDataFlags.Restore) == 0)
                    {
                        Result rc = GetAccessibilityForSaveData(out Accessibility accessibility, programInfo,
                            extraDataGetter);
                        if (rc.IsFailure()) return rc;

                        canAccess |= accessibility.CanWrite;
                    }

                    if (!canAccess)
                        return ResultFs.PermissionDenied.Log();
                }

                if (mask.TimeStamp != 0)
                {
                    bool canAccess = accessControl.CanCall(OperationType.WriteSaveDataFileSystemExtraDataAll) ||
                                     accessControl.CanCall(OperationType.WriteSaveDataFileSystemExtraDataTimeStamp);

                    if (!canAccess)
                        return ResultFs.PermissionDenied.Log();
                }

                if (mask.CommitId != 0)
                {
                    bool canAccess = accessControl.CanCall(OperationType.WriteSaveDataFileSystemExtraDataAll) ||
                                     accessControl.CanCall(OperationType.WriteSaveDataFileSystemExtraDataCommitId);

                    if (!canAccess)
                        return ResultFs.PermissionDenied.Log();
                }

                SaveDataExtraData emptyMask = default;
                SaveDataExtraData maskWithoutFlags = mask;
                maskWithoutFlags.Flags = SaveDataFlags.None;
                maskWithoutFlags.TimeStamp = 0;
                maskWithoutFlags.CommitId = 0;

                // Full write access is needed for writing anything other than flags, timestamp or commit ID
                if (SpanHelpers.AsReadOnlyByteSpan(in emptyMask)
                    .SequenceEqual(SpanHelpers.AsReadOnlyByteSpan(in maskWithoutFlags)))
                {
                    bool canAccess = accessControl.CanCall(OperationType.WriteSaveDataFileSystemExtraDataAll);

                    if (!canAccess)
                        return ResultFs.PermissionDenied.Log();
                }

                return Result.Success;
            }

            public static Result CheckFind(in SaveDataFilter filter, ProgramInfo programInfo)
            {
                bool canAccess;

                if (programInfo.ProgramId == filter.Attribute.ProgramId)
                {
                    canAccess = programInfo.AccessControl.CanCall(OperationType.FindOwnSaveDataWithFilter);
                }
                else
                {
                    canAccess = true;
                }

                if (!canAccess)
                    return ResultFs.PermissionDenied.Log();

                return Result.Success;
            }


            private static Result GetAccessibilityForSaveData(out Accessibility accessibility, ProgramInfo programInfo,
                ExtraDataGetter extraDataGetter)
            {
                UnsafeHelpers.SkipParamInit(out accessibility);

                Result rc = extraDataGetter(out SaveDataExtraData extraData);
                if (rc.IsFailure())
                {
                    if (ResultFs.TargetNotFound.Includes(rc))
                    {
                        accessibility = new Accessibility(false, false);
                        return Result.Success;
                    }

                    return rc;
                }

                // Allow access when opening a directory save FS on a dev console
                if (extraData.OwnerId == 0 && extraData.DataSize == 0 && extraData.JournalSize == 0 &&
                    programInfo.AccessControl.CanCall(OperationType.DebugSaveData))
                {
                    accessibility = new Accessibility(true, true);
                    return Result.Success;
                }

                return GetAccessibilityForSaveData(out accessibility, programInfo, extraData.OwnerId);
            }

            private static Result GetAccessibilityForSaveData(out Accessibility accessibility, ProgramInfo programInfo,
                ulong ownerId)
            {
                if (ownerId == programInfo.ProgramIdValue)
                {
                    // A program always has full access to its own save data
                    accessibility = new Accessibility(true, true);
                }
                else
                {
                    accessibility = programInfo.AccessControl.GetAccessibilitySaveDataOwnedBy(ownerId);
                }

                return Result.Success;
            }
        }

        public Result GetFreeSpaceSizeForSaveData(out long freeSpaceSize, SaveDataSpaceId spaceId)
        {
            throw new NotImplementedException();
        }

        public Result RegisterSaveDataFileSystemAtomicDeletion(InBuffer saveDataIds)
        {
            throw new NotImplementedException();
        }

        public Result DeleteSaveDataFileSystem(ulong saveDataId)
        {
            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.Bis);

            return DeleteSaveDataFileSystemCommon(SaveDataSpaceId.System, saveDataId);
        }

        private Result DeleteSaveDataFileSystemCore(SaveDataSpaceId spaceId, ulong saveDataId, bool wipeSaveFile)
        {
            // Delete the save data's meta files
            Result rc = _serviceImpl.DeleteAllSaveDataMetas(saveDataId, spaceId);
            if (rc.IsFailure() && !ResultFs.PathNotFound.Includes(rc))
                return rc;

            // Delete the actual save data.
            Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
            rc = _serviceImpl.DeleteSaveDataFileSystem(spaceId, saveDataId, wipeSaveFile, in saveDataRootPath);
            if (rc.IsFailure() && !ResultFs.PathNotFound.Includes(rc))
                return rc;

            return Result.Success;
        }

        public Result DeleteSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.NonGameCard);

            return DeleteSaveDataFileSystemBySaveDataSpaceIdCore(spaceId, saveDataId);
        }

        private Result DeleteSaveDataFileSystemBySaveDataSpaceIdCore(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            if (saveDataId != SaveData.SaveIndexerId)
            {
                using var accessor = new UniqueRef<SaveDataIndexerAccessor>();
                Result rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), spaceId);
                if (rc.IsFailure()) return rc;

                rc = accessor.Get.Indexer.GetValue(out SaveDataIndexerValue value, saveDataId);
                if (rc.IsFailure()) return rc;

                if (value.SpaceId != ConvertToRealSpaceId(spaceId))
                    return ResultFs.TargetNotFound.Log();
            }

            return DeleteSaveDataFileSystemCommon(spaceId, saveDataId);
        }

        public Result DeleteSaveDataFileSystemBySaveDataAttribute(SaveDataSpaceId spaceId,
            in SaveDataAttribute attribute)
        {
            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.NonGameCard);

            Result rs = GetSaveDataInfo(out SaveDataInfo info, spaceId, in attribute);
            if (rs.IsFailure()) return rs;

            return DeleteSaveDataFileSystemBySaveDataSpaceIdCore(spaceId, info.SaveDataId);
        }

        private Result DeleteSaveDataFileSystemCommon(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

            SaveDataSpaceId actualSpaceId;

            // Only the FS process may delete the save indexer's save data.
            if (saveDataId == SaveData.SaveIndexerId)
            {
                if (!IsCurrentProcess(_processId))
                    return ResultFs.PermissionDenied.Log();

                actualSpaceId = spaceId;
            }
            else
            {
                rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), spaceId);
                if (rc.IsFailure()) return rc;

                // Get the actual space ID of this save.
                if (spaceId == SaveDataSpaceId.ProperSystem && spaceId == SaveDataSpaceId.SafeMode)
                {
                    actualSpaceId = spaceId;
                }
                else
                {
                    rc = accessor.Get.Indexer.GetValue(out SaveDataIndexerValue value, saveDataId);
                    if (rc.IsFailure()) return rc;

                    actualSpaceId = value.SpaceId;
                }

                // Check if the caller has permission to delete this save.
                rc = accessor.Get.Indexer.GetKey(out SaveDataAttribute key, saveDataId);
                if (rc.IsFailure()) return rc;

                Result GetExtraData(out SaveDataExtraData data) =>
                    _serviceImpl.ReadSaveDataFileSystemExtraData(out data, actualSpaceId, saveDataId, key.Type,
                        _saveDataRootPath.DangerousGetPath());

                rc = SaveDataAccessibilityChecker.CheckDelete(in key, programInfo, GetExtraData);
                if (rc.IsFailure()) return rc;

                // Pre-delete checks successful. Put the save in the Processing state until deletion is finished.
                rc = accessor.Get.Indexer.SetState(saveDataId, SaveDataState.Processing);
                if (rc.IsFailure()) return rc;

                rc = accessor.Get.Indexer.Commit();
                if (rc.IsFailure()) return rc;
            }

            // Do the actual deletion.
            rc = DeleteSaveDataFileSystemCore(actualSpaceId, saveDataId, false);
            if (rc.IsFailure()) return rc;

            // Remove the save data from the indexer.
            // The indexer doesn't track itself, so skip if deleting its save data.
            if (saveDataId != SaveData.SaveIndexerId)
            {
                rc = accessor.Get.Indexer.Delete(saveDataId);
                if (rc.IsFailure()) return rc;

                rc = accessor.Get.Indexer.Commit();
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        public Result SwapSaveDataKeyAndState(SaveDataSpaceId spaceId, ulong saveDataId1, ulong saveDataId2)
        {
            throw new NotImplementedException();
        }

        public Result SetSaveDataState(SaveDataSpaceId spaceId, ulong saveDataId, SaveDataState state)
        {
            throw new NotImplementedException();
        }

        public Result SetSaveDataRank(SaveDataSpaceId spaceId, ulong saveDataId, SaveDataRank rank)
        {
            throw new NotImplementedException();
        }

        public Result FinalizeSaveDataCreation(ulong saveDataId, SaveDataSpaceId spaceId)
        {
            throw new NotImplementedException();
        }

        public Result CancelSaveDataCreation(ulong saveDataId, SaveDataSpaceId spaceId)
        {
            throw new NotImplementedException();
        }

        public Result SetSaveDataRootPath(in FspPath path)
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.DebugSaveData))
                return ResultFs.PermissionDenied.Log();

            using var saveDataRootPath = new Path();

            if (path.Str[0] == NullTerminator)
            {
                rc = saveDataRootPath.Initialize(new[] { (byte)'/' });
                if (rc.IsFailure()) return rc;
            }
            else
            {
                rc = saveDataRootPath.InitializeWithReplaceUnc(path.Str);
                if (rc.IsFailure()) return rc;
            }

            var pathFlags = new PathFlags();
            pathFlags.AllowWindowsPath();
            pathFlags.AllowRelativePath();
            pathFlags.AllowEmptyPath();

            rc = saveDataRootPath.Normalize(pathFlags);
            if (rc.IsFailure()) return rc;

            _saveDataRootPath.Initialize(in saveDataRootPath);

            return Result.Success;
        }

        public Result UnsetSaveDataRootPath()
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.DebugSaveData))
                return ResultFs.PermissionDenied.Log();

            using var saveDataRootPath = new Path();
            rc = saveDataRootPath.InitializeAsEmpty();
            if (rc.IsFailure()) return rc;

            _saveDataRootPath.Initialize(in saveDataRootPath);

            return Result.Success;
        }

        // ReSharper disable once UnusedParameter.Global
        public Result UpdateSaveDataMacForDebug(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            if (saveDataId == SaveData.SaveIndexerId)
                return ResultFs.InvalidArgument.Log();

            return ResultFs.NotImplemented.Log();
        }

        public Result OpenSaveDataFile(ref SharedRef<IFileSf> file, SaveDataSpaceId spaceId,
            in SaveDataAttribute attribute, SaveDataMetaType metaType)
        {
            throw new NotImplementedException();
        }

        public Result CheckSaveDataFile(long saveDataId, SaveDataSpaceId spaceId)
        {
            throw new NotImplementedException();
        }

        private Result CreateSaveDataFileSystemCore(in SaveDataAttribute attribute,
            in SaveDataCreationInfo creationInfo,
            in SaveDataMetaInfo metaInfo, in Optional<HashSalt> hashSalt)
        {
            return CreateSaveDataFileSystemCore(in attribute, in creationInfo, in metaInfo, in hashSalt, false);
        }

        private Result CreateSaveDataFileSystemCore(in SaveDataAttribute attribute,
            in SaveDataCreationInfo creationInfo,
            in SaveDataMetaInfo metaInfo, in Optional<HashSalt> hashSalt, bool leaveUnfinalized)
        {
            ulong saveDataId = 0;
            bool creating = false;
            bool accessorInitialized = false;
            Result rc;

            StorageType storageFlag = DecidePossibleStorageFlag(attribute.Type, creationInfo.SpaceId);
            using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

            using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

            try
            {
                // Add the new save data to the save indexer
                if (attribute.StaticSaveDataId == SaveData.SaveIndexerId)
                {
                    // The save indexer doesn't index itself
                    saveDataId = SaveData.SaveIndexerId;
                    rc = _serviceImpl.DoesSaveDataEntityExist(out bool saveExists, creationInfo.SpaceId, saveDataId);

                    if (rc.IsSuccess() && saveExists)
                    {
                        return ResultFs.PathAlreadyExists.Log();
                    }

                    creating = true;
                }
                else
                {
                    rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), creationInfo.SpaceId);
                    if (rc.IsFailure()) return rc;

                    accessorInitialized = true;

                    SaveDataAttribute indexerKey = attribute;

                    // Add the new value to the indexer
                    if (attribute.StaticSaveDataId != 0 && attribute.UserId == UserId.InvalidId)
                    {
                        // If a static save data ID is specified that ID is always used
                        saveDataId = attribute.StaticSaveDataId;

                        rc = accessor.Get.Indexer.PutStaticSaveDataIdIndex(in indexerKey);
                    }
                    else
                    {
                        // The save indexer has an upper limit on the number of entries it can hold.
                        // A few of those entries are reserved for system saves so the system doesn't
                        // end up in a situation where it can't create a required system save.
                        if (!SaveDataProperties.CanUseIndexerReservedArea(attribute.Type))
                        {
                            if (accessor.Get.Indexer.IsRemainedReservedOnly())
                            {
                                return ResultKvdb.OutOfKeyResource.Log();
                            }
                        }

                        // If a static save data ID is no specified we're assigned a new save ID
                        rc = accessor.Get.Indexer.Publish(out saveDataId, in indexerKey);
                    }

                    if (rc.IsFailure())
                    {
                        if (ResultFs.AlreadyExists.Includes(rc))
                        {
                            return ResultFs.PathAlreadyExists.LogConverted(rc);
                        }

                        return rc;
                    }

                    creating = true;

                    // Set the state, space ID and size on the new save indexer entry.
                    rc = accessor.Get.Indexer.SetState(saveDataId, SaveDataState.Processing);
                    if (rc.IsFailure()) return rc;

                    rc = accessor.Get.Indexer.SetSpaceId(saveDataId, ConvertToRealSpaceId(creationInfo.SpaceId));
                    if (rc.IsFailure()) return rc;

                    rc = QuerySaveDataTotalSize(out long saveDataSize, creationInfo.Size, creationInfo.JournalSize);
                    if (rc.IsFailure()) return rc;

                    rc = accessor.Get.Indexer.SetSize(saveDataId, saveDataSize);
                    if (rc.IsFailure()) return rc;

                    rc = accessor.Get.Indexer.Commit();
                    if (rc.IsFailure()) return rc;
                }

                // After the new save was added to the save indexer, create the save data file or directory.
                Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
                rc = _serviceImpl.CreateSaveDataFileSystem(saveDataId, in attribute, in creationInfo,
                    in saveDataRootPath, in hashSalt, false);

                if (rc.IsFailure())
                {
                    if (!ResultFs.PathAlreadyExists.Includes(rc)) return rc;

                    // Handle the situation where a save exists on disk but not in the save indexer.
                    // Delete the save data and try creating it again.
                    rc = DeleteSaveDataFileSystemCore(creationInfo.SpaceId, saveDataId, false);
                    if (rc.IsFailure()) return rc;

                    rc = _serviceImpl.CreateSaveDataFileSystem(saveDataId, in attribute, in creationInfo,
                        in saveDataRootPath, in hashSalt, false);
                    if (rc.IsFailure()) return rc;
                }

                if (metaInfo.Type != SaveDataMetaType.None)
                {
                    // Create the requested save data meta file.
                    rc = _serviceImpl.CreateSaveDataMeta(saveDataId, creationInfo.SpaceId, metaInfo.Type,
                        metaInfo.Size);
                    if (rc.IsFailure()) return rc;

                    if (metaInfo.Type == SaveDataMetaType.Thumbnail)
                    {
                        using var metaFile = new UniqueRef<IFile>();
                        rc = _serviceImpl.OpenSaveDataMeta(ref metaFile.Ref(), saveDataId, creationInfo.SpaceId,
                            metaInfo.Type);

                        if (rc.IsFailure()) return rc;

                        // The first 0x20 bytes of thumbnail meta files is an SHA-256 hash.
                        // Zero the hash to indicate that it's currently unused.
                        ReadOnlySpan<byte> metaFileHash = stackalloc byte[0x20];

                        rc = metaFile.Get.Write(0, metaFileHash, WriteOption.Flush);
                        if (rc.IsFailure()) return rc;
                    }
                }

                if (leaveUnfinalized)
                {
                    creating = false;
                    return Result.Success;
                }

                // The indexer's save data isn't tracked, so we don't need to update its state.
                if (attribute.StaticSaveDataId != SaveData.SaveIndexerId)
                {
                    // Mark the save data as being successfully created
                    rc = accessor.Get.Indexer.SetState(saveDataId, SaveDataState.Normal);
                    if (rc.IsFailure()) return rc;

                    rc = accessor.Get.Indexer.Commit();
                    if (rc.IsFailure()) return rc;
                }

                creating = false;
                return Result.Success;
            }
            finally
            {
                // Revert changes if an error happened in the middle of creation
                if (creating)
                {
                    DeleteSaveDataFileSystemCore(creationInfo.SpaceId, saveDataId, false).IgnoreResult();

                    if (accessorInitialized && saveDataId != SaveData.SaveIndexerId)
                    {
                        rc = accessor.Get.Indexer.GetValue(out SaveDataIndexerValue value, saveDataId);

                        if (rc.IsSuccess() && value.SpaceId == creationInfo.SpaceId)
                        {
                            accessor.Get.Indexer.Delete(saveDataId).IgnoreResult();
                            accessor.Get.Indexer.Commit().IgnoreResult();
                        }
                    }
                }
            }
        }

        public Result GetSaveDataInfo(out SaveDataInfo info, SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
        {
            UnsafeHelpers.SkipParamInit(out info);

            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.NonGameCard);

            using var accessor = new UniqueRef<SaveDataIndexerAccessor>();
            Result rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), spaceId);
            if (rc.IsFailure()) return rc;

            rc = accessor.Get.Indexer.Get(out SaveDataIndexerValue value, in attribute);
            if (rc.IsFailure()) return rc;

            SaveDataIndexer.GenerateSaveDataInfo(out info, in attribute, in value);
            return Result.Success;
        }

        public Result QuerySaveDataTotalSize(out long totalSize, long dataSize, long journalSize)
        {
            UnsafeHelpers.SkipParamInit(out totalSize);

            if (dataSize < 0 || journalSize < 0)
                return ResultFs.InvalidSize.Log();

            return _serviceImpl.QuerySaveDataTotalSize(out totalSize, SaveDataBlockSize, dataSize, journalSize);
        }

        public Result CreateSaveDataFileSystem(in SaveDataAttribute attribute, in SaveDataCreationInfo creationInfo,
            in SaveDataMetaInfo metaInfo)
        {
            var hashSalt = new Optional<HashSalt>();

            return CreateSaveDataFileSystemWithHashSaltImpl(in attribute, in creationInfo, in metaInfo, in hashSalt);
        }

        public Result CreateSaveDataFileSystemWithHashSalt(in SaveDataAttribute attribute,
            in SaveDataCreationInfo creationInfo, in SaveDataMetaInfo metaInfo, in HashSalt hashSalt)
        {
            var optionalHashSalt = new Optional<HashSalt>(in hashSalt);

            return CreateSaveDataFileSystemWithHashSaltImpl(in attribute, in creationInfo, in metaInfo,
                in optionalHashSalt);
        }

        private Result CreateSaveDataFileSystemWithHashSaltImpl(in SaveDataAttribute attribute,
            in SaveDataCreationInfo creationInfo, in SaveDataMetaInfo metaInfo, in Optional<HashSalt> hashSalt)
        {
            StorageType storageFlag = DecidePossibleStorageFlag(attribute.Type, creationInfo.SpaceId);
            using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            SaveDataAttribute tempAttribute = attribute;
            SaveDataCreationInfo tempCreationInfo = creationInfo;

            if (hashSalt.HasValue && !programInfo.AccessControl.CanCall(OperationType.CreateSaveDataWithHashSalt))
            {
                return ResultFs.PermissionDenied.Log();
            }

            ProgramId programId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId);
            rc = SaveDataAccessibilityChecker.CheckCreate(in attribute, in creationInfo, programInfo, programId);
            if (rc.IsFailure()) return rc;

            if (tempAttribute.Type == SaveDataType.Account && tempAttribute.UserId == UserId.InvalidId)
            {
                if (tempAttribute.ProgramId == ProgramId.InvalidId)
                {
                    tempAttribute.ProgramId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId);
                }

                if (tempCreationInfo.OwnerId == 0)
                {
                    tempCreationInfo.OwnerId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId).Value;
                }
            }

            return CreateSaveDataFileSystemCore(in tempAttribute, in tempCreationInfo, in metaInfo, in hashSalt);
        }

        public Result CreateSaveDataFileSystemBySystemSaveDataId(in SaveDataAttribute attribute,
            in SaveDataCreationInfo creationInfo)
        {
            StorageType storageFlag = DecidePossibleStorageFlag(attribute.Type, creationInfo.SpaceId);
            using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!IsStaticSaveDataIdValueRange(attribute.StaticSaveDataId))
                return ResultFs.InvalidArgument.Log();

            SaveDataCreationInfo tempCreationInfo = creationInfo;

            if (tempCreationInfo.OwnerId == 0)
            {
                tempCreationInfo.OwnerId = programInfo.ProgramIdValue;
            }

            rc = SaveDataAccessibilityChecker.CheckCreate(in attribute, in tempCreationInfo, programInfo,
                programInfo.ProgramId);
            if (rc.IsFailure()) return rc;

            // Static system saves don't usually have meta files
            SaveDataMetaInfo metaInfo = default;
            Optional<HashSalt> hashSalt = default;

            return CreateSaveDataFileSystemCore(in attribute, in tempCreationInfo, in metaInfo, in hashSalt);
        }

        public Result ExtendSaveDataFileSystem(SaveDataSpaceId spaceId, ulong saveDataId, long dataSize,
            long journalSize)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataFileSystem(ref SharedRef<IFileSystemSf> fileSystem,
            SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
        {
            return OpenUserSaveDataFileSystem(ref fileSystem, spaceId, in attribute, false);
        }

        public Result OpenReadOnlySaveDataFileSystem(ref SharedRef<IFileSystemSf> fileSystem,
            SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
        {
            return OpenUserSaveDataFileSystem(ref fileSystem, spaceId, in attribute, true);
        }

        private Result OpenSaveDataFileSystemCore(ref SharedRef<IFileSystem> outFileSystem,
            out ulong saveDataId, SaveDataSpaceId spaceId, in SaveDataAttribute attribute, bool openReadOnly,
            bool cacheExtraData)
        {
            UnsafeHelpers.SkipParamInit(out saveDataId);

            using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

            ulong tempSaveDataId;
            bool isStaticSaveDataId = attribute.StaticSaveDataId != 0 && attribute.UserId == UserId.InvalidId;

            // Get the ID of the save data
            if (isStaticSaveDataId)
            {
                tempSaveDataId = attribute.StaticSaveDataId;
            }
            else
            {
                Result rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), spaceId);
                if (rc.IsFailure()) return rc;

                rc = accessor.Get.Indexer.Get(out SaveDataIndexerValue indexerValue, in attribute);
                if (rc.IsFailure()) return rc;

                if (indexerValue.SpaceId != ConvertToRealSpaceId(spaceId))
                    return ResultFs.TargetNotFound.Log();

                if (indexerValue.State == SaveDataState.Extending)
                    return ResultFs.SaveDataExtending.Log();

                tempSaveDataId = indexerValue.SaveDataId;
            }

            // Open the save data using its ID
            Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
            Result saveFsResult = _serviceImpl.OpenSaveDataFileSystem(ref outFileSystem, spaceId, tempSaveDataId,
                in saveDataRootPath, openReadOnly, attribute.Type, cacheExtraData);

            if (saveFsResult.IsSuccess())
            {
                saveDataId = tempSaveDataId;
                return Result.Success;
            }

            // Copy the key so we can use it in a local function
            SaveDataAttribute key = attribute;

            // Remove the save from the indexer if the save is missing from the disk.
            if (ResultFs.PathNotFound.Includes(saveFsResult))
            {
                Result rc = RemoveSaveIndexerEntry();
                if (rc.IsFailure()) return rc;

                return ResultFs.TargetNotFound.LogConverted(saveFsResult);
            }

            if (ResultFs.TargetNotFound.Includes(saveFsResult))
            {
                Result rc = RemoveSaveIndexerEntry();
                if (rc.IsFailure()) return rc;
            }

            return saveFsResult;

            Result RemoveSaveIndexerEntry()
            {
                if (tempSaveDataId == SaveData.SaveIndexerId)
                    return Result.Success;

                if (isStaticSaveDataId)
                {
                    // The accessor won't be open yet if the save has a static ID
                    Result rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), spaceId);
                    if (rc.IsFailure()) return rc;

                    // Check the space ID of the save data
                    rc = accessor.Get.Indexer.Get(out SaveDataIndexerValue value, in key);
                    if (rc.IsFailure()) return rc;

                    if (value.SpaceId != ConvertToRealSpaceId(spaceId))
                        return ResultFs.TargetNotFound.Log();
                }

                // Remove the indexer entry. Nintendo ignores these results
                accessor.Get.Indexer.Delete(tempSaveDataId).IgnoreResult();
                accessor.Get.Indexer.Commit().IgnoreResult();

                return Result.Success;
            }
        }

        private Result OpenUserSaveDataFileSystemCore(ref SharedRef<IFileSystemSf> outFileSystem,
            SaveDataSpaceId spaceId, in SaveDataAttribute attribute, ProgramInfo programInfo, bool openReadOnly)
        {
            StorageType storageFlag = DecidePossibleStorageFlag(attribute.Type, spaceId);
            using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

            // Try grabbing the mount count semaphore
            using var mountCountSemaphore = new UniqueRef<IUniqueLock>();
            Result rc = TryAcquireSaveDataMountCountSemaphore(ref mountCountSemaphore.Ref());
            if (rc.IsFailure()) return rc;

            Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
            bool useAsyncFileSystem = !_serviceImpl.IsAllowedDirectorySaveData(spaceId, in saveDataRootPath);

            using var fileSystem = new SharedRef<IFileSystem>();

            // Open the file system
            rc = OpenSaveDataFileSystemCore(ref fileSystem.Ref(), out ulong saveDataId, spaceId, in attribute,
                openReadOnly, true);
            if (rc.IsFailure()) return rc;

            // Can't use attribute in a closure, so copy the needed field
            SaveDataType type = attribute.Type;

            Result ReadExtraData(out SaveDataExtraData data)
            {
                Path savePath = _saveDataRootPath.DangerousGetPath();
                return _serviceImpl.ReadSaveDataFileSystemExtraData(out data, spaceId, saveDataId, type,
                    in savePath);
            }

            // Check if we have permissions to open this save data
            rc = SaveDataAccessibilityChecker.CheckOpen(in attribute, programInfo, ReadExtraData);
            if (rc.IsFailure()) return rc;

            // Add all the wrappers for the file system
            using var typeSetFileSystem =
                new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(ref fileSystem.Ref(), storageFlag));

            using var asyncFileSystem = new SharedRef<IFileSystem>();

            if (useAsyncFileSystem)
            {
                asyncFileSystem.Reset(new AsynchronousAccessFileSystem(ref typeSetFileSystem.Ref()));
            }
            else
            {
                asyncFileSystem.SetByMove(ref typeSetFileSystem.Ref());
            }

            using SharedRef<SaveDataFileSystemService> saveService = GetSharedFromThis();
            using var openEntryCountAdapter =
                new SharedRef<IEntryOpenCountSemaphoreManager>(new SaveDataOpenCountAdapter(ref saveService.Ref()));

            using var openCountFileSystem = new SharedRef<IFileSystem>(
                new OpenCountFileSystem(ref asyncFileSystem.Ref(), ref openEntryCountAdapter.Ref(),
                    ref mountCountSemaphore.Ref()));

            var pathFlags = new PathFlags();
            pathFlags.AllowBackslash();

            using SharedRef<IFileSystemSf> fileSystemAdapter =
                FileSystemInterfaceAdapter.CreateShared(ref openCountFileSystem.Ref(), pathFlags, false);

            outFileSystem.SetByMove(ref fileSystemAdapter.Ref());

            return Result.Success;
        }

        private Result OpenUserSaveDataFileSystem(ref SharedRef<IFileSystemSf> outFileSystem,
            SaveDataSpaceId spaceId, in SaveDataAttribute attribute, bool openReadOnly)
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            rc = SaveDataAccessibilityChecker.CheckOpenPre(in attribute, programInfo);
            if (rc.IsFailure()) return rc;

            SaveDataAttribute tempAttribute;

            if (attribute.ProgramId.Value == 0)
            {
                ProgramId programId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId);

                rc = SaveDataAttribute.Make(out tempAttribute, programId, attribute.Type, attribute.UserId,
                    attribute.StaticSaveDataId, attribute.Index);
                if (rc.IsFailure()) return rc;
            }
            else
            {
                tempAttribute = attribute;
            }

            SaveDataSpaceId actualSpaceId;

            if (tempAttribute.Type == SaveDataType.Cache)
            {
                // Check whether the save is on the SD card or the BIS
                rc = GetCacheStorageSpaceId(out actualSpaceId, tempAttribute.ProgramId.Value);
                if (rc.IsFailure()) return rc;
            }
            else
            {
                actualSpaceId = spaceId;
            }

            return OpenUserSaveDataFileSystemCore(ref outFileSystem, actualSpaceId, in tempAttribute, programInfo,
                openReadOnly);
        }

        public Result OpenSaveDataFileSystemBySystemSaveDataId(ref SharedRef<IFileSystemSf> outFileSystem,
            SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
        {
            if (!IsStaticSaveDataIdValueRange(attribute.StaticSaveDataId))
                return ResultFs.InvalidArgument.Log();

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            StorageType storageFlag = DecidePossibleStorageFlag(attribute.Type, spaceId);
            using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

            Accessibility accessibility =
                programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountSystemSaveData);

            if (!accessibility.CanRead || !accessibility.CanWrite)
                return ResultFs.PermissionDenied.Log();

            Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
            bool useAsyncFileSystem = !_serviceImpl.IsAllowedDirectorySaveData(spaceId, in saveDataRootPath);

            using var fileSystem = new SharedRef<IFileSystem>();

            // Open the file system
            rc = OpenSaveDataFileSystemCore(ref fileSystem.Ref(), out ulong saveDataId, spaceId, in attribute,
                false, true);
            if (rc.IsFailure()) return rc;

            // Can't use attribute in a closure, so copy the needed field
            SaveDataType type = attribute.Type;

            Result ReadExtraData(out SaveDataExtraData data)
            {
                Path savePath = _saveDataRootPath.DangerousGetPath();
                return _serviceImpl.ReadSaveDataFileSystemExtraData(out data, spaceId, saveDataId, type,
                    in savePath);
            }

            // Check if we have permissions to open this save data
            rc = SaveDataAccessibilityChecker.CheckOpen(in attribute, programInfo, ReadExtraData);
            if (rc.IsFailure()) return rc;

            // Add all the wrappers for the file system
            using var typeSetFileSystem =
                new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(ref fileSystem.Ref(), storageFlag));

            using var asyncFileSystem = new SharedRef<IFileSystem>();

            if (useAsyncFileSystem)
            {
                asyncFileSystem.Reset(new AsynchronousAccessFileSystem(ref typeSetFileSystem.Ref()));
            }
            else
            {
                asyncFileSystem.SetByMove(ref typeSetFileSystem.Ref());
            }

            using SharedRef<SaveDataFileSystemService> saveService = GetSharedFromThis();
            using var openEntryCountAdapter =
                new SharedRef<IEntryOpenCountSemaphoreManager>(new SaveDataOpenCountAdapter(ref saveService.Ref()));

            using var openCountFileSystem = new SharedRef<IFileSystem>(
                new OpenCountFileSystem(ref asyncFileSystem.Ref(), ref openEntryCountAdapter.Ref()));

            var pathFlags = new PathFlags();
            pathFlags.AllowBackslash();

            using SharedRef<IFileSystemSf> fileSystemAdapter =
                FileSystemInterfaceAdapter.CreateShared(ref openCountFileSystem.Ref(), pathFlags, false);

            outFileSystem.SetByMove(ref fileSystemAdapter.Ref());

            return Result.Success;
        }

        // ReSharper disable once UnusedParameter.Local
        // Nintendo used isTemporarySaveData in older FS versions, but never removed the parameter.
        private Result ReadSaveDataFileSystemExtraDataCore(out SaveDataExtraData extraData, SaveDataSpaceId spaceId,
            ulong saveDataId, bool isTemporarySaveData)
        {
            UnsafeHelpers.SkipParamInit(out extraData);

            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.NonGameCard);
            using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

            Result rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), spaceId);
            if (rc.IsFailure()) return rc;

            rc = accessor.Get.Indexer.GetKey(out SaveDataAttribute key, saveDataId);
            if (rc.IsFailure()) return rc;

            Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
            return _serviceImpl.ReadSaveDataFileSystemExtraData(out extraData, spaceId, saveDataId, key.Type,
                in saveDataRootPath);
        }

        private Result ReadSaveDataFileSystemExtraDataCore(out SaveDataExtraData extraData, SaveDataSpaceId spaceId,
            ulong saveDataId, in SaveDataExtraData extraDataMask)
        {
            UnsafeHelpers.SkipParamInit(out extraData);

            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.NonGameCard);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            SaveDataSpaceId resolvedSpaceId;
            SaveDataAttribute key;

            if (spaceId == SaveDataSpaceId.BisAuto)
            {
                using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

                if (IsStaticSaveDataIdValueRange(saveDataId))
                {
                    rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), SaveDataSpaceId.System);
                    if (rc.IsFailure()) return rc;
                }
                else
                {
                    rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), SaveDataSpaceId.User);
                    if (rc.IsFailure()) return rc;
                }

                rc = accessor.Get.Indexer.GetValue(out SaveDataIndexerValue value, saveDataId);
                if (rc.IsFailure()) return rc;

                resolvedSpaceId = value.SpaceId;

                rc = accessor.Get.Indexer.GetKey(out key, saveDataId);
                if (rc.IsFailure()) return rc;
            }
            else
            {
                using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

                rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), spaceId);
                if (rc.IsFailure()) return rc;

                rc = accessor.Get.Indexer.GetValue(out SaveDataIndexerValue value, saveDataId);
                if (rc.IsFailure()) return rc;

                resolvedSpaceId = value.SpaceId;

                rc = accessor.Get.Indexer.GetKey(out key, saveDataId);
                if (rc.IsFailure()) return rc;
            }

            Result ReadExtraData(out SaveDataExtraData data) => _serviceImpl.ReadSaveDataFileSystemExtraData(out data,
                resolvedSpaceId, saveDataId, key.Type, _saveDataRootPath.DangerousGetPath());

            rc = SaveDataAccessibilityChecker.CheckReadExtraData(in key, in extraDataMask, programInfo,
                ReadExtraData);
            if (rc.IsFailure()) return rc;

            Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
            rc = _serviceImpl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData tempExtraData, resolvedSpaceId,
                saveDataId, key.Type, in saveDataRootPath);
            if (rc.IsFailure()) return rc;

            MaskExtraData(ref tempExtraData, in extraDataMask);
            extraData = tempExtraData;

            return Result.Success;
        }

        public Result ReadSaveDataFileSystemExtraData(OutBuffer extraData, ulong saveDataId)
        {
            if (extraData.Size != Unsafe.SizeOf<SaveDataExtraData>())
                return ResultFs.InvalidArgument.Log();

            // Make a mask for reading the entire extra data
            Unsafe.SkipInit(out SaveDataExtraData extraDataMask);
            SpanHelpers.AsByteSpan(ref extraDataMask).Fill(0xFF);

            return ReadSaveDataFileSystemExtraDataCore(out SpanHelpers.AsStruct<SaveDataExtraData>(extraData.Buffer),
                SaveDataSpaceId.BisAuto, saveDataId, in extraDataMask);
        }

        public Result ReadSaveDataFileSystemExtraDataBySaveDataAttribute(OutBuffer extraData,
            SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
        {
            if (extraData.Size != Unsafe.SizeOf<SaveDataExtraData>())
                return ResultFs.InvalidArgument.Log();

            ref SaveDataExtraData extraDataRef = ref SpanHelpers.AsStruct<SaveDataExtraData>(extraData.Buffer);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            SaveDataAttribute tempAttribute = attribute;

            if (tempAttribute.ProgramId == SaveData.AutoResolveCallerProgramId)
            {
                tempAttribute.ProgramId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId);
            }

            rc = GetSaveDataInfo(out SaveDataInfo info, spaceId, in tempAttribute);
            if (rc.IsFailure()) return rc;

            // Make a mask for reading the entire extra data
            Unsafe.SkipInit(out SaveDataExtraData extraDataMask);
            SpanHelpers.AsByteSpan(ref extraDataMask).Fill(0xFF);

            return ReadSaveDataFileSystemExtraDataCore(out extraDataRef, spaceId, info.SaveDataId, in extraDataMask);
        }

        public Result ReadSaveDataFileSystemExtraDataBySaveDataSpaceId(OutBuffer extraData,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            if (extraData.Size != Unsafe.SizeOf<SaveDataExtraData>())
                return ResultFs.InvalidArgument.Log();

            ref SaveDataExtraData extraDataRef = ref SpanHelpers.AsStruct<SaveDataExtraData>(extraData.Buffer);

            // Make a mask for reading the entire extra data
            Unsafe.SkipInit(out SaveDataExtraData extraDataMask);
            SpanHelpers.AsByteSpan(ref extraDataMask).Fill(0xFF);

            return ReadSaveDataFileSystemExtraDataCore(out extraDataRef, spaceId, saveDataId, in extraDataMask);
        }

        public Result ReadSaveDataFileSystemExtraDataWithMaskBySaveDataAttribute(OutBuffer extraData,
            SaveDataSpaceId spaceId, in SaveDataAttribute attribute, InBuffer extraDataMask)
        {
            if (extraDataMask.Size != Unsafe.SizeOf<SaveDataExtraData>())
                return ResultFs.InvalidArgument.Log();

            if (extraData.Size != Unsafe.SizeOf<SaveDataExtraData>())
                return ResultFs.InvalidArgument.Log();

            ref readonly SaveDataExtraData maskRef =
                ref SpanHelpers.AsReadOnlyStruct<SaveDataExtraData>(extraDataMask.Buffer);

            ref SaveDataExtraData extraDataRef = ref SpanHelpers.AsStruct<SaveDataExtraData>(extraData.Buffer);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            SaveDataAttribute tempAttribute = attribute;

            if (tempAttribute.ProgramId == SaveData.AutoResolveCallerProgramId)
            {
                tempAttribute.ProgramId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId);
            }

            rc = GetSaveDataInfo(out SaveDataInfo info, spaceId, in tempAttribute);
            if (rc.IsFailure()) return rc;

            return ReadSaveDataFileSystemExtraDataCore(out extraDataRef, spaceId, info.SaveDataId, in maskRef);
        }

        private Result WriteSaveDataFileSystemExtraDataCore(SaveDataSpaceId spaceId, ulong saveDataId,
            in SaveDataExtraData extraData, SaveDataType saveType, bool updateTimeStamp)
        {
            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.NonGameCard);

            Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
            return _serviceImpl.WriteSaveDataFileSystemExtraData(spaceId, saveDataId, in extraData, in saveDataRootPath,
                saveType, updateTimeStamp);
        }

        private Result WriteSaveDataFileSystemExtraDataWithMaskCore(ulong saveDataId, SaveDataSpaceId spaceId,
            in SaveDataExtraData extraData, in SaveDataExtraData extraDataMask)
        {
            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.NonGameCard);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

            rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), spaceId);
            if (rc.IsFailure()) return rc;

            rc = accessor.Get.Indexer.GetKey(out SaveDataAttribute key, saveDataId);
            if (rc.IsFailure()) return rc;

            Result ReadExtraData(out SaveDataExtraData data) => _serviceImpl.ReadSaveDataFileSystemExtraData(out data,
                spaceId, saveDataId, key.Type, _saveDataRootPath.DangerousGetPath());

            rc = SaveDataAccessibilityChecker.CheckWriteExtraData(in key, in extraDataMask, programInfo,
                ReadExtraData);
            if (rc.IsFailure()) return rc;

            Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
            rc = _serviceImpl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraDataModify, spaceId,
                saveDataId, key.Type, in saveDataRootPath);
            if (rc.IsFailure()) return rc;

            ModifySaveDataExtraData(ref extraDataModify, in extraData, in extraDataMask);

            return _serviceImpl.WriteSaveDataFileSystemExtraData(spaceId, saveDataId, in extraDataModify,
                in saveDataRootPath, key.Type, false);
        }

        public Result WriteSaveDataFileSystemExtraData(ulong saveDataId, SaveDataSpaceId spaceId, InBuffer extraData)
        {
            if (extraData.Size != Unsafe.SizeOf<SaveDataExtraData>())
                return ResultFs.InvalidArgument.Log();

            ref readonly SaveDataExtraData extraDataRef =
                ref SpanHelpers.AsReadOnlyStruct<SaveDataExtraData>(extraData.Buffer);

            var extraDataMask = new SaveDataExtraData();
            extraDataMask.Flags = unchecked((SaveDataFlags)0xFFFFFFFF);

            return WriteSaveDataFileSystemExtraDataWithMaskCore(saveDataId, spaceId, in extraDataRef, in extraDataMask);
        }

        public Result WriteSaveDataFileSystemExtraDataWithMaskBySaveDataAttribute(in SaveDataAttribute attribute,
            SaveDataSpaceId spaceId, InBuffer extraData, InBuffer extraDataMask)
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            SaveDataAttribute tempAttribute = attribute;

            if (tempAttribute.ProgramId == SaveData.AutoResolveCallerProgramId)
            {
                tempAttribute.ProgramId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId);
            }

            rc = GetSaveDataInfo(out SaveDataInfo info, spaceId, in tempAttribute);
            if (rc.IsFailure()) return rc;

            return WriteSaveDataFileSystemExtraDataWithMask(info.SaveDataId, spaceId, extraData, extraDataMask);
        }

        public Result WriteSaveDataFileSystemExtraDataWithMask(ulong saveDataId, SaveDataSpaceId spaceId,
            InBuffer extraData, InBuffer extraDataMask)
        {
            if (extraDataMask.Size != Unsafe.SizeOf<SaveDataExtraData>())
                return ResultFs.InvalidArgument.Log();

            if (extraData.Size != Unsafe.SizeOf<SaveDataExtraData>())
                return ResultFs.InvalidArgument.Log();

            ref readonly SaveDataExtraData maskRef =
                ref SpanHelpers.AsReadOnlyStruct<SaveDataExtraData>(extraDataMask.Buffer);

            ref readonly SaveDataExtraData extraDataRef =
                ref SpanHelpers.AsReadOnlyStruct<SaveDataExtraData>(extraData.Buffer);

            return WriteSaveDataFileSystemExtraDataWithMaskCore(saveDataId, spaceId, in extraDataRef, in maskRef);
        }

        public Result OpenSaveDataInfoReader(ref SharedRef<ISaveDataInfoReader> outInfoReader)
        {
            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.Bis);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.OpenSaveDataInfoReader) ||
                !programInfo.AccessControl.CanCall(OperationType.OpenSaveDataInfoReaderForSystem))
            {
                return ResultFs.PermissionDenied.Log();
            }

            using var reader = new SharedRef<SaveDataInfoReaderImpl>();

            using (var accessor = new UniqueRef<SaveDataIndexerAccessor>())
            {
                rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), SaveDataSpaceId.System);
                if (rc.IsFailure()) return rc;

                rc = accessor.Get.Indexer.OpenSaveDataInfoReader(ref reader.Ref());
                if (rc.IsFailure()) return rc;
            }

            outInfoReader.SetByMove(ref reader.Ref());

            return Result.Success;
        }

        public Result OpenSaveDataInfoReaderBySaveDataSpaceId(
            ref SharedRef<ISaveDataInfoReader> outInfoReader, SaveDataSpaceId spaceId)
        {
            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.NonGameCard);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            rc = CheckOpenSaveDataInfoReaderAccessControl(programInfo, spaceId);
            if (rc.IsFailure()) return rc;

            using var filterReader = new UniqueRef<SaveDataInfoFilterReader>();

            using (var accessor = new UniqueRef<SaveDataIndexerAccessor>())
            {
                rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), spaceId);
                if (rc.IsFailure()) return rc;

                using var reader = new SharedRef<SaveDataInfoReaderImpl>();

                rc = accessor.Get.Indexer.OpenSaveDataInfoReader(ref reader.Ref());
                if (rc.IsFailure()) return rc;

                var filter = new SaveDataInfoFilter(ConvertToRealSpaceId(spaceId), programId: default,
                    saveDataType: default, userId: default, saveDataId: default, index: default, rank: 0);

                filterReader.Reset(new SaveDataInfoFilterReader(ref reader.Ref(), in filter));
            }

            outInfoReader.Set(ref filterReader.Ref());

            return Result.Success;
        }

        public Result OpenSaveDataInfoReaderWithFilter(ref SharedRef<ISaveDataInfoReader> outInfoReader,
            SaveDataSpaceId spaceId, in SaveDataFilter filter)
        {
            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.NonGameCard);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.OpenSaveDataInfoReaderForInternal))
                return ResultFs.PermissionDenied.Log();

            rc = CheckOpenSaveDataInfoReaderAccessControl(programInfo, spaceId);
            if (rc.IsFailure()) return rc;

            using var filterReader = new UniqueRef<SaveDataInfoFilterReader>();

            using (var accessor = new UniqueRef<SaveDataIndexerAccessor>())
            {
                rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), spaceId);
                if (rc.IsFailure()) return rc;

                using var reader = new SharedRef<SaveDataInfoReaderImpl>();

                rc = accessor.Get.Indexer.OpenSaveDataInfoReader(ref reader.Ref());
                if (rc.IsFailure()) return rc;

                var infoFilter = new SaveDataInfoFilter(ConvertToRealSpaceId(spaceId), in filter);

                filterReader.Reset(new SaveDataInfoFilterReader(ref reader.Ref(), in infoFilter));
            }

            outInfoReader.Set(ref filterReader.Ref());

            return Result.Success;
        }

        private Result FindSaveDataWithFilterImpl(out long count, out SaveDataInfo info, SaveDataSpaceId spaceId,
            in SaveDataInfoFilter infoFilter)
        {
            UnsafeHelpers.SkipParamInit(out count, out info);

            using var reader = new SharedRef<SaveDataInfoReaderImpl>();
            using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

            Result rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), spaceId);
            if (rc.IsFailure()) return rc;

            rc = accessor.Get.Indexer.OpenSaveDataInfoReader(ref reader.Ref());
            if (rc.IsFailure()) return rc;

            using var filterReader =
                new UniqueRef<SaveDataInfoFilterReader>(new SaveDataInfoFilterReader(ref reader.Ref(), in infoFilter));

            return filterReader.Get.Read(out count, new OutBuffer(SpanHelpers.AsByteSpan(ref info)));
        }

        public Result FindSaveDataWithFilter(out long count, OutBuffer saveDataInfoBuffer, SaveDataSpaceId spaceId,
            in SaveDataFilter filter)
        {
            UnsafeHelpers.SkipParamInit(out count);

            if (saveDataInfoBuffer.Size != Unsafe.SizeOf<SaveDataInfo>())
                return ResultFs.InvalidArgument.Log();

            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.NonGameCard);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            rc = CheckOpenSaveDataInfoReaderAccessControl(programInfo, spaceId);

            if (rc.IsFailure())
            {
                if (!ResultFs.PermissionDenied.Includes(rc))
                    return rc;

                // Don't have full info reader permissions. Check if we have find permissions.
                rc = SaveDataAccessibilityChecker.CheckFind(in filter, programInfo);
                if (rc.IsFailure()) return rc;
            }

            var infoFilter = new SaveDataInfoFilter(ConvertToRealSpaceId(spaceId), in filter);

            return FindSaveDataWithFilterImpl(out count,
                out SpanHelpers.AsStruct<SaveDataInfo>(saveDataInfoBuffer.Buffer), spaceId, in infoFilter);
        }

        private Result CreateEmptyThumbnailFile(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        private Result OpenSaveDataInternalStorageFileSystemCore(ref SharedRef<IFileSystem> fileSystem,
            SaveDataSpaceId spaceId, ulong saveDataId, bool useSecondMacKey)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataInternalStorageFileSystem(ref SharedRef<IFileSystemSf> fileSystem,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result QuerySaveDataInternalStorageTotalSize(out long size, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result GetSaveDataCommitId(out long commitId, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out commitId);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.GetSaveDataCommitId))
                return ResultFs.PermissionDenied.Log();

            Unsafe.SkipInit(out SaveDataExtraData extraData);
            rc = ReadSaveDataFileSystemExtraDataBySaveDataSpaceId(OutBuffer.FromStruct(ref extraData), spaceId,
                saveDataId);
            if (rc.IsFailure()) return rc;

            commitId = Impl.Utility.ConvertZeroCommitId(in extraData);
            return Result.Success;
        }

        public Result OpenSaveDataInfoReaderOnlyCacheStorage(ref SharedRef<ISaveDataInfoReader> outInfoReader)
        {
            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.NonGameCard);

            // Find where the current program's cache storage is located
            Result rc = GetCacheStorageSpaceId(out SaveDataSpaceId spaceId);

            if (rc.IsFailure())
            {
                spaceId = SaveDataSpaceId.User;

                if (!ResultFs.TargetNotFound.Includes(rc))
                    return rc;
            }

            return OpenSaveDataInfoReaderOnlyCacheStorage(ref outInfoReader, spaceId);
        }

        private Result OpenSaveDataInfoReaderOnlyCacheStorage(ref SharedRef<ISaveDataInfoReader> outInfoReader,
            SaveDataSpaceId spaceId)
        {
            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.NonGameCard);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (spaceId != SaveDataSpaceId.SdCache && spaceId != SaveDataSpaceId.User)
                return ResultFs.InvalidSaveDataSpaceId.Log();

            using var filterReader = new UniqueRef<SaveDataInfoFilterReader>();

            using (var reader = new SharedRef<SaveDataInfoReaderImpl>())
            using (var accessor = new UniqueRef<SaveDataIndexerAccessor>())
            {
                rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), spaceId);
                if (rc.IsFailure()) return rc;

                rc = accessor.Get.Indexer.OpenSaveDataInfoReader(ref reader.Ref());
                if (rc.IsFailure()) return rc;

                ProgramId resolvedProgramId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId);

                var filter = new SaveDataInfoFilter(ConvertToRealSpaceId(spaceId), resolvedProgramId,
                    SaveDataType.Cache, userId: default, saveDataId: default, index: default,
                    (int)SaveDataRank.Primary);

                filterReader.Reset(new SaveDataInfoFilterReader(ref reader.Ref(), in filter));
            }

            outInfoReader.Set(ref filterReader.Ref());

            return Result.Success;
        }

        private Result OpenSaveDataMetaFileRaw(ref SharedRef<IFile> file, SaveDataSpaceId spaceId,
            ulong saveDataId, SaveDataMetaType metaType, OpenMode mode)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataMetaFile(ref SharedRef<IFileSf> file, SaveDataSpaceId spaceId,
            in SaveDataAttribute attribute, SaveDataMetaType metaType)
        {
            throw new NotImplementedException();
        }

        private Result GetCacheStorageSpaceId(out SaveDataSpaceId spaceId)
        {
            UnsafeHelpers.SkipParamInit(out spaceId);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            ulong programId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId).Value;
            return GetCacheStorageSpaceId(out spaceId, programId);
        }

        private Result GetCacheStorageSpaceId(out SaveDataSpaceId spaceId, ulong programId)
        {
            UnsafeHelpers.SkipParamInit(out spaceId);
            Result rc;

            // Cache storage on the SD card will always take priority over case storage in NAND
            if (_serviceImpl.IsSdCardAccessible())
            {
                rc = SaveExists(out bool existsOnSdCard, SaveDataSpaceId.SdCache);
                if (rc.IsFailure()) return rc;

                if (existsOnSdCard)
                {
                    spaceId = SaveDataSpaceId.SdCache;
                    return Result.Success;
                }
            }

            rc = SaveExists(out bool existsOnNand, SaveDataSpaceId.User);
            if (rc.IsFailure()) return rc;

            if (existsOnNand)
            {
                spaceId = SaveDataSpaceId.User;
                return Result.Success;
            }

            return ResultFs.TargetNotFound.Log();

            Result SaveExists(out bool exists, SaveDataSpaceId saveSpaceId)
            {
                UnsafeHelpers.SkipParamInit(out exists);

                var infoFilter = new SaveDataInfoFilter(saveSpaceId, new ProgramId(programId), SaveDataType.Cache,
                    default, default, default, 0);

                Result result = FindSaveDataWithFilterImpl(out long count, out _, saveSpaceId, in infoFilter);
                if (result.IsFailure()) return result;

                exists = count != 0;
                return Result.Success;
            }
        }

        private Result FindCacheStorage(out SaveDataInfo saveInfo, out SaveDataSpaceId spaceId, ushort index)
        {
            UnsafeHelpers.SkipParamInit(out saveInfo, out spaceId);

            Result rc = GetCacheStorageSpaceId(out spaceId);
            if (rc.IsFailure()) return rc;

            rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            ProgramId resolvedProgramId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId);

            var filter = new SaveDataInfoFilter(ConvertToRealSpaceId(spaceId), resolvedProgramId, SaveDataType.Cache,
                userId: default, saveDataId: default, index, (int)SaveDataRank.Primary);

            rc = FindSaveDataWithFilterImpl(out long count, out SaveDataInfo info, spaceId, in filter);
            if (rc.IsFailure()) return rc;

            if (count == 0)
                return ResultFs.TargetNotFound.Log();

            saveInfo = info;
            return Result.Success;
        }

        public Result DeleteCacheStorage(ushort index)
        {
            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.NonGameCard);

            Result rc = FindCacheStorage(out SaveDataInfo saveInfo, out SaveDataSpaceId spaceId, index);
            if (rc.IsFailure()) return rc;

            rc = Hos.Fs.DeleteSaveData(spaceId, saveInfo.SaveDataId);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public Result GetCacheStorageSize(out long usableDataSize, out long journalSize, ushort index)
        {
            UnsafeHelpers.SkipParamInit(out usableDataSize, out journalSize);

            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.NonGameCard);

            Result rc = FindCacheStorage(out SaveDataInfo saveInfo, out SaveDataSpaceId spaceId, index);
            if (rc.IsFailure()) return rc;

            Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
            rc = _serviceImpl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, spaceId,
                saveInfo.SaveDataId, saveInfo.Type, in saveDataRootPath);
            if (rc.IsFailure()) return rc;

            usableDataSize = extraData.DataSize;
            journalSize = extraData.JournalSize;

            return Result.Success;
        }

        public Result OpenSaveDataTransferManager(ref SharedRef<ISaveDataTransferManager> manager)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataTransferManagerVersion2(
            ref SharedRef<ISaveDataTransferManagerWithDivision> manager)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataTransferManagerForSaveDataRepair(
            ref SharedRef<ISaveDataTransferManagerForSaveDataRepair> manager)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataTransferManagerForRepair(
            ref SharedRef<ISaveDataTransferManagerForRepair> manager)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataTransferProhibiter(
            ref SharedRef<ISaveDataTransferProhibiter> prohibiter, Ncm.ApplicationId applicationId)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataMover(ref SharedRef<ISaveDataMover> saveMover,
            SaveDataSpaceId sourceSpaceId, SaveDataSpaceId destinationSpaceId, NativeHandle workBufferHandle,
            ulong workBufferSize)
        {
            throw new NotImplementedException();
        }

        public Result SetSdCardEncryptionSeed(in EncryptionSeed seed)
        {
            return _serviceImpl.SetSdCardEncryptionSeed(in seed);
        }

        public Result ListAccessibleSaveDataOwnerId(out int readCount, OutBuffer idBuffer, ProgramId programId,
            int startIndex, int bufferIdCount)
        {
            throw new NotImplementedException();
        }

        private ProgramId ResolveDefaultSaveDataReferenceProgramId(ProgramId programId)
        {
            return _serviceImpl.ResolveDefaultSaveDataReferenceProgramId(programId);
        }

        public Result VerifySaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId,
            OutBuffer workBuffer)
        {
            throw new NotImplementedException();
        }

        public Result CorruptSaveDataFileSystemByOffset(SaveDataSpaceId spaceId, ulong saveDataId, long offset)
        {
            throw new NotImplementedException();
        }

        public Result CleanUpSaveData()
        {
            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.Bis);
            using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

            Result rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), SaveDataSpaceId.System);
            if (rc.IsFailure()) return rc;

            return CleanUpSaveData(accessor.Get);
        }

        private Result CleanUpSaveData(SaveDataIndexerAccessor accessor)
        {
            // Todo: Implement
            return Result.Success;
        }

        public Result CompleteSaveDataExtension()
        {
            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.Bis);
            using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

            Result rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), SaveDataSpaceId.System);
            if (rc.IsFailure()) return rc;

            return CompleteSaveDataExtension(accessor.Get);
        }

        private Result CompleteSaveDataExtension(SaveDataIndexerAccessor accessor)
        {
            // Todo: Implement
            return Result.Success;
        }

        public Result CleanUpTemporaryStorage()
        {
            using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.Bis);
            using var fileSystem = new SharedRef<IFileSystem>();

            Result rc = _serviceImpl.OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref(), SaveDataSpaceId.Temporary);
            if (rc.IsFailure()) return rc;

            using var pathRoot = new Path();
            rc = PathFunctions.SetUpFixedPath(ref pathRoot.Ref(), new[] { (byte)'/' });
            if (rc.IsFailure()) return rc;

            rc = fileSystem.Get.CleanDirectoryRecursively(in pathRoot);
            if (rc.IsFailure()) return rc;

            _serviceImpl.ResetTemporaryStorageIndexer();
            return Result.Success;
        }

        public Result FixSaveData()
        {
            // Todo: Implement
            return Result.Success;
        }

        public Result OpenMultiCommitManager(ref SharedRef<IMultiCommitManager> outCommitManager)
        {
            using SharedRef<ISaveDataMultiCommitCoreInterface>
                commitInterface = GetSharedMultiCommitInterfaceFromThis();

            outCommitManager.Reset(new MultiCommitManager(_serviceImpl.FsServer, ref commitInterface.Ref()));

            return Result.Success;
        }

        public Result OpenMultiCommitContext(ref SharedRef<IFileSystem> contextFileSystem)
        {
            var attribute = new SaveDataAttribute
            {
                Index = 0,
                Type = SaveDataType.System,
                UserId = UserId.InvalidId,
                StaticSaveDataId = MultiCommitManager.SaveDataId,
                ProgramId = new ProgramId(MultiCommitManager.ProgramId)
            };

            return OpenSaveDataFileSystemCore(ref contextFileSystem, out _, SaveDataSpaceId.System, in attribute, false,
                true);
        }

        public Result RecoverMultiCommit()
        {
            return MultiCommitManager.Recover(_serviceImpl.FsServer, this, _serviceImpl);
        }

        public Result IsProvisionallyCommittedSaveData(out bool isProvisionallyCommitted, in SaveDataInfo saveInfo)
        {
            return _serviceImpl.IsProvisionallyCommittedSaveData(out isProvisionallyCommitted, in saveInfo);
        }

        public Result RecoverProvisionallyCommittedSaveData(in SaveDataInfo saveInfo, bool doRollback)
        {
            var attribute = new SaveDataAttribute
            {
                Index = saveInfo.Index,
                Type = saveInfo.Type,
                UserId = UserId.InvalidId,
                StaticSaveDataId = saveInfo.StaticSaveDataId,
                ProgramId = saveInfo.ProgramId
            };

            using var fileSystem = new SharedRef<IFileSystem>();

            Result rc = OpenSaveDataFileSystemCore(ref fileSystem.Ref(), out _, saveInfo.SpaceId, in attribute, false,
                false);
            if (rc.IsFailure()) return rc;

            if (doRollback)
            {
                rc = fileSystem.Get.Rollback();
            }
            else
            {
                rc = fileSystem.Get.Commit();
            }

            return rc;
        }

        private Result TryAcquireSaveDataEntryOpenCountSemaphore(ref UniqueRef<IUniqueLock> outSemaphoreLock)
        {
            using SharedRef<SaveDataFileSystemService> saveService = GetSharedFromThis();

            Result rc = Utility.MakeUniqueLockWithPin(ref outSemaphoreLock, _openEntryCountSemaphore,
                ref saveService.Ref());
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        private Result TryAcquireSaveDataMountCountSemaphore(ref UniqueRef<IUniqueLock> outSemaphoreLock)
        {
            using SharedRef<SaveDataFileSystemService> saveService = GetSharedFromThis();

            Result rc = Utility.MakeUniqueLockWithPin(ref outSemaphoreLock, _saveDataMountCountSemaphore,
                ref saveService.Ref());
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public Result OverrideSaveDataTransferTokenSignVerificationKey(InBuffer key)
        {
            throw new NotImplementedException();
        }

        public Result SetSdCardAccessibility(bool isAccessible)
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.SetSdCardAccessibility))
                return ResultFs.PermissionDenied.Log();

            _serviceImpl.SetSdCardAccessibility(isAccessible);
            return Result.Success;
        }

        public Result IsSdCardAccessible(out bool isAccessible)
        {
            isAccessible = _serviceImpl.IsSdCardAccessible();
            return Result.Success;
        }

        private Result OpenSaveDataIndexerAccessor(ref UniqueRef<SaveDataIndexerAccessor> outAccessor,
            SaveDataSpaceId spaceId)
        {
            using var accessor = new UniqueRef<SaveDataIndexerAccessor>();
            Result rc = _serviceImpl.OpenSaveDataIndexerAccessor(ref accessor.Ref(), out bool neededInit, spaceId);
            if (rc.IsFailure()) return rc;

            if (neededInit)
            {
                // todo: nn::fssrv::SaveDataFileSystemService::CleanUpSaveDataCore
                // nn::fssrv::SaveDataFileSystemService::CompleteSaveDataExtensionCore
            }

            outAccessor.Set(ref accessor.Ref());
            return Result.Success;
        }

        private Result GetProgramInfo(out ProgramInfo programInfo)
        {
            return _serviceImpl.GetProgramInfo(out programInfo, _processId);
        }

        private bool IsCurrentProcess(ulong processId)
        {
            ulong currentId = Hos.Os.GetCurrentProcessId().Value;

            return processId == currentId;
        }

        private SaveDataSpaceId ConvertToRealSpaceId(SaveDataSpaceId spaceId)
        {
            return spaceId == SaveDataSpaceId.ProperSystem || spaceId == SaveDataSpaceId.SafeMode
                ? SaveDataSpaceId.System
                : spaceId;
        }

        private bool IsStaticSaveDataIdValueRange(ulong id)
        {
            return (long)id < 0;
        }

        private void ModifySaveDataExtraData(ref SaveDataExtraData currentExtraData, in SaveDataExtraData extraData,
            in SaveDataExtraData extraDataMask)
        {
            Span<byte> currentExtraDataBytes = SpanHelpers.AsByteSpan(ref currentExtraData);
            ReadOnlySpan<byte> extraDataBytes = SpanHelpers.AsReadOnlyByteSpan(in extraData);
            ReadOnlySpan<byte> extraDataMaskBytes = SpanHelpers.AsReadOnlyByteSpan(in extraDataMask);

            for (int i = 0; i < Unsafe.SizeOf<SaveDataExtraData>(); i++)
            {
                currentExtraDataBytes[i] = (byte)(extraDataBytes[i] & extraDataMaskBytes[i] |
                                              currentExtraDataBytes[i] & ~extraDataMaskBytes[i]);
            }
        }

        private void MaskExtraData(ref SaveDataExtraData extraData, in SaveDataExtraData extraDataMask)
        {
            Span<byte> extraDataBytes = SpanHelpers.AsByteSpan(ref extraData);
            ReadOnlySpan<byte> extraDataMaskBytes = SpanHelpers.AsReadOnlyByteSpan(in extraDataMask);

            for (int i = 0; i < Unsafe.SizeOf<SaveDataExtraData>(); i++)
            {
                extraDataBytes[i] &= extraDataMaskBytes[i];
            }
        }

        private StorageType DecidePossibleStorageFlag(SaveDataType type, SaveDataSpaceId spaceId)
        {
            if (type == SaveDataType.Cache || type == SaveDataType.Bcat)
                return StorageType.Bis | StorageType.SdCard | StorageType.Usb;

            if (type == SaveDataType.System ||
                spaceId != SaveDataSpaceId.SdSystem && spaceId != SaveDataSpaceId.SdCache)
                return StorageType.Bis;

            return StorageType.SdCard | StorageType.Usb;
        }

        Result ISaveDataTransferCoreInterface.CreateSaveDataFileSystemCore(in SaveDataAttribute attribute,
            in SaveDataCreationInfo creationInfo, in SaveDataMetaInfo metaInfo, in Optional<HashSalt> hashSalt,
            bool leaveUnfinalized)
        {
            return CreateSaveDataFileSystemCore(in attribute, in creationInfo, in metaInfo, in hashSalt,
                leaveUnfinalized);
        }

        Result ISaveDataTransferCoreInterface.ReadSaveDataFileSystemExtraDataCore(out SaveDataExtraData extraData,
            SaveDataSpaceId spaceId, ulong saveDataId, bool isTemporarySaveData)
        {
            return ReadSaveDataFileSystemExtraDataCore(out extraData, spaceId, saveDataId, isTemporarySaveData);
        }

        Result ISaveDataTransferCoreInterface.WriteSaveDataFileSystemExtraDataCore(SaveDataSpaceId spaceId,
            ulong saveDataId, in SaveDataExtraData extraData, SaveDataType type, bool updateTimeStamp)
        {
            return WriteSaveDataFileSystemExtraDataCore(spaceId, saveDataId, in extraData, type, updateTimeStamp);
        }

        Result ISaveDataTransferCoreInterface.OpenSaveDataMetaFileRaw(ref SharedRef<IFile> file,
            SaveDataSpaceId spaceId, ulong saveDataId, SaveDataMetaType metaType, OpenMode mode)
        {
            return OpenSaveDataMetaFileRaw(ref file, spaceId, saveDataId, metaType, mode);
        }

        Result ISaveDataTransferCoreInterface.OpenSaveDataInternalStorageFileSystemCore(
            ref SharedRef<IFileSystem> fileSystem, SaveDataSpaceId spaceId, ulong saveDataId,
            bool useSecondMacKey)
        {
            return OpenSaveDataInternalStorageFileSystemCore(ref fileSystem, spaceId, saveDataId, useSecondMacKey);
        }

        Result ISaveDataTransferCoreInterface.OpenSaveDataIndexerAccessor(
            ref UniqueRef<SaveDataIndexerAccessor> outAccessor, SaveDataSpaceId spaceId)
        {
            return OpenSaveDataIndexerAccessor(ref outAccessor, spaceId);
        }

        public void Dispose()
        {
            _openEntryCountSemaphore.Dispose();
            _saveDataMountCountSemaphore.Dispose();
        }
    }
}
