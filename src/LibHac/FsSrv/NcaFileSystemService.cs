using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using LibHac.Lr;
using LibHac.Ncm;
using LibHac.Spl;
using LibHac.Util;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IStorage = LibHac.Fs.IStorage;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;
using Path = LibHac.Lr.Path;
using PathNormalizer = LibHac.FsSrv.Impl.PathNormalizer;

namespace LibHac.FsSrv
{
    internal class NcaFileSystemService : IRomFileSystemAccessFailureManager
    {
        private const int AocSemaphoreCount = 128;
        private const int RomSemaphoreCount = 10;

        private ReferenceCountedDisposable<NcaFileSystemService>.WeakReference SelfReference { get; set; }
        private NcaFileSystemServiceImpl ServiceImpl { get; }
        private ulong ProcessId { get; }
        private SemaphoreAdaptor AocMountCountSemaphore { get; }
        private SemaphoreAdaptor RomMountCountSemaphore { get; }

        private NcaFileSystemService(NcaFileSystemServiceImpl serviceImpl, ulong processId)
        {
            ServiceImpl = serviceImpl;
            ProcessId = processId;
            AocMountCountSemaphore = new SemaphoreAdaptor(AocSemaphoreCount, AocSemaphoreCount);
            RomMountCountSemaphore = new SemaphoreAdaptor(RomSemaphoreCount, RomSemaphoreCount);
        }

        public static ReferenceCountedDisposable<NcaFileSystemService> CreateShared(
            NcaFileSystemServiceImpl serviceImpl, ulong processId)
        {
            // Create the service
            var ncaService = new NcaFileSystemService(serviceImpl, processId);

            // Wrap the service in a ref-counter and give the service a weak self-reference
            var sharedService = new ReferenceCountedDisposable<NcaFileSystemService>(ncaService);
            ncaService.SelfReference =
                new ReferenceCountedDisposable<NcaFileSystemService>.WeakReference(sharedService);

            return sharedService;
        }

        public void Dispose()
        {
            AocMountCountSemaphore?.Dispose();
            RomMountCountSemaphore?.Dispose();
        }

        public Result OpenFileSystemWithPatch(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            ProgramId programId, FileSystemProxyType fsType)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            const StorageType storageFlag = StorageType.All;
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(storageFlag);

            // Get the program info for the caller and verify permissions
            Result rc = GetProgramInfo(out ProgramInfo callerProgramInfo);
            if (rc.IsFailure()) return rc;

            if (fsType != FileSystemProxyType.Manual)
            {
                if (fsType == FileSystemProxyType.Logo || fsType == FileSystemProxyType.Control)
                    return ResultFs.NotImplemented.Log();
                else
                    return ResultFs.InvalidArgument.Log();
            }

            Accessibility accessibility =
                callerProgramInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountContentManual);

            if (!accessibility.CanRead)
                return ResultFs.PermissionDenied.Log();

            // Get the program info for the owner of the file system being opened
            rc = GetProgramInfoByProgramId(out ProgramInfo ownerProgramInfo, programId.Value);
            if (rc.IsFailure()) return rc;

            // Try to find the path to the original version of the file system
            Result originalResult = ServiceImpl.ResolveApplicationHtmlDocumentPath(out Path originalPath,
                new Ncm.ApplicationId(programId.Value), ownerProgramInfo.StorageId);

            // The file system might have a patch version with no original version, so continue if not found
            if (originalResult.IsFailure() && !ResultLr.HtmlDocumentNotFound.Includes(originalResult))
                return originalResult;

            // Use a separate bool because ref structs can't be used as type parameters
            bool originalPathNormalizerHasValue = false;
            PathNormalizer originalPathNormalizer = default;

            // Normalize the original version path if found
            if (originalResult.IsSuccess())
            {
                originalPathNormalizer = new PathNormalizer(originalPath, GetPathNormalizerOptions(originalPath));
                if (originalPathNormalizer.Result.IsFailure()) return originalPathNormalizer.Result;

                originalPathNormalizerHasValue = true;
            }

            // Try to find the path to the patch file system
            Result patchResult = ServiceImpl.ResolveRegisteredHtmlDocumentPath(out Path patchPath, programId.Value);

            ReferenceCountedDisposable<IFileSystem> tempFileSystem = null;
            ReferenceCountedDisposable<IRomFileSystemAccessFailureManager> accessFailureManager = null;
            try
            {
                if (ResultLr.HtmlDocumentNotFound.Includes(patchResult))
                {
                    // There must either be an original version or patch version of the file system being opened
                    if (originalResult.IsFailure())
                        return originalResult;

                    Assert.SdkAssert(originalPathNormalizerHasValue);

                    // There is an original version and no patch version. Open the original directly
                    rc = ServiceImpl.OpenFileSystem(out tempFileSystem, originalPathNormalizer.Path, fsType, programId.Value);
                    if (rc.IsFailure()) return rc;
                }
                else
                {
                    // Get the normalized path to the original file system
                    U8Span normalizedOriginalPath;
                    if (originalPathNormalizerHasValue)
                    {
                        normalizedOriginalPath = originalPathNormalizer.Path;
                    }
                    else
                    {
                        normalizedOriginalPath = U8Span.Empty;
                    }

                    // Normalize the path to the patch file system
                    using var patchPathNormalizer = new PathNormalizer(patchPath, GetPathNormalizerOptions(patchPath));
                    if (patchPathNormalizer.Result.IsFailure()) return patchPathNormalizer.Result;

                    if (patchResult.IsFailure())
                        return patchResult;

                    U8Span normalizedPatchPath = patchPathNormalizer.Path;

                    // Open the file system using both the original and patch versions
                    rc = ServiceImpl.OpenFileSystemWithPatch(out tempFileSystem, normalizedOriginalPath,
                        normalizedPatchPath, fsType, programId.Value);
                    if (rc.IsFailure()) return rc;
                }

                // Add all the file system wrappers
                tempFileSystem = StorageLayoutTypeSetFileSystem.CreateShared(ref tempFileSystem, storageFlag);
                tempFileSystem = AsynchronousAccessFileSystem.CreateShared(ref tempFileSystem);

                accessFailureManager = SelfReference.AddReference<IRomFileSystemAccessFailureManager>();
                tempFileSystem = DeepRetryFileSystem.CreateShared(ref tempFileSystem, ref accessFailureManager);

                fileSystem = FileSystemInterfaceAdapter.CreateShared(ref tempFileSystem);
                return Result.Success;
            }
            finally
            {
                tempFileSystem?.Dispose();
                accessFailureManager?.Dispose();
            }
        }

        public Result OpenCodeFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            out CodeVerificationData verificationData, in FspPath path, ProgramId programId)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataFileSystemByCurrentProcess(out ReferenceCountedDisposable<IFileSystemSf> fileSystem)
        {
            throw new NotImplementedException();
        }

        private Result OpenDataStorageCore(out ReferenceCountedDisposable<IStorage> storage, out Hash ncaHeaderDigest,
                ulong id, StorageId storageId)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataStorageByCurrentProcess(out ReferenceCountedDisposable<IStorageSf> storage)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataStorageByProgramId(out ReferenceCountedDisposable<IStorageSf> storage, ProgramId programId)
        {
            throw new NotImplementedException();
        }

        public Result OpenFileSystemWithId(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, in FspPath path,
            ulong id, FileSystemProxyType fsType)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            AccessControl ac = programInfo.AccessControl;

            switch (fsType)
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

            if (fsType == FileSystemProxyType.Meta)
            {
                id = ulong.MaxValue;
            }
            else if (id == ulong.MaxValue)
            {
                return ResultFs.InvalidArgument.Log();
            }

            bool canMountSystemDataPrivate = ac.GetAccessibilityFor(AccessibilityType.MountSystemDataPrivate).CanRead;

            using var normalizer = new PathNormalizer(path, GetPathNormalizerOptions(path));
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            ReferenceCountedDisposable<IFileSystem> fs = null;

            try
            {
                rc = ServiceImpl.OpenFileSystem(out fs, out _, path, fsType,
                    canMountSystemDataPrivate, id);
                if (rc.IsFailure()) return rc;

                // Create an SF adapter for the file system
                fileSystem = FileSystemInterfaceAdapter.CreateShared(ref fs);

                return Result.Success;
            }
            finally
            {
                fs?.Dispose();
            }
        }

        public Result OpenDataFileSystemByProgramId(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            ProgramId programId)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataStorageByDataId(out ReferenceCountedDisposable<IStorageSf> storage, DataId dataId,
            StorageId storageId)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataFileSystemWithProgramIndex(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            byte programIndex)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            // Get the program ID of the program with the specified index if in a multi-program application
            rc = ServiceImpl.ResolveRomReferenceProgramId(out ProgramId targetProgramId, programInfo.ProgramId,
                programIndex);
            if (rc.IsFailure()) return rc;

            ReferenceCountedDisposable<IFileSystem> tempFileSystem = null;
            try
            {
                rc = OpenDataFileSystemCore(out tempFileSystem, out _, targetProgramId.Value,
                    programInfo.StorageId);
                if (rc.IsFailure()) return rc;

                // Verify the caller has access to the file system
                if (targetProgramId != programInfo.ProgramId &&
                    !programInfo.AccessControl.HasContentOwnerId(targetProgramId.Value))
                {
                    return ResultFs.PermissionDenied.Log();
                }

                tempFileSystem = AsynchronousAccessFileSystem.CreateShared(ref tempFileSystem);
                if (tempFileSystem is null)
                    return ResultFs.AllocationMemoryFailedAllocateShared.Log();

                fileSystem = FileSystemInterfaceAdapter.CreateShared(ref tempFileSystem);
                if (fileSystem is null)
                    return ResultFs.AllocationMemoryFailedCreateShared.Log();

                return Result.Success;
            }
            finally
            {
                tempFileSystem?.Dispose();
            }
        }

        public Result OpenDataStorageWithProgramIndex(out ReferenceCountedDisposable<IStorageSf> storage,
            byte programIndex)
        {
            throw new NotImplementedException();
        }

        public Result GetRightsId(out RightsId rightsId, ProgramId programId, StorageId storageId)
        {
            UnsafeHelpers.SkipParamInit(out rightsId);

            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageType.All);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.GetRightsId))
                return ResultFs.PermissionDenied.Log();

            rc = ServiceImpl.ResolveProgramPath(out Path programPath, programId, storageId);
            if (rc.IsFailure()) return rc;

            using var normalizer = new PathNormalizer(programPath, GetPathNormalizerOptions(programPath));
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            rc = ServiceImpl.GetRightsId(out RightsId tempRightsId, out _, normalizer.Path, programId);
            if (rc.IsFailure()) return rc;

            rightsId = tempRightsId;
            return Result.Success;
        }

        public Result GetRightsIdAndKeyGenerationByPath(out RightsId rightsId, out byte keyGeneration, in FspPath path)
        {
            UnsafeHelpers.SkipParamInit(out rightsId, out keyGeneration);

            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageType.All);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.GetRightsId))
                return ResultFs.PermissionDenied.Log();

            using var normalizer = new PathNormalizer(path, GetPathNormalizerOptions(path));
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            rc = ServiceImpl.GetRightsId(out RightsId tempRightsId, out byte tempKeyGeneration, normalizer.Path,
                new ProgramId(ulong.MaxValue));
            if (rc.IsFailure()) return rc;

            rightsId = tempRightsId;
            keyGeneration = tempKeyGeneration;
            return Result.Success;
        }

        private Result OpenDataFileSystemCore(out ReferenceCountedDisposable<IFileSystem> fileSystem, out bool isHostFs,
            ulong programId, StorageId storageId)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem, out isHostFs);

            if (Unsafe.IsNullRef(ref isHostFs))
                return ResultFs.NullptrArgument.Log();

            StorageType storageFlag = ServiceImpl.GetStorageFlag(programId);
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(storageFlag);

            Result rc = ServiceImpl.ResolveRomPath(out Path romPath, programId, storageId);
            if (rc.IsFailure()) return rc;

            using var normalizer = new PathNormalizer(romPath, GetPathNormalizerOptions(romPath));
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            isHostFs = IsHostFs(romPath);

            ReferenceCountedDisposable<IFileSystem> tempFileSystem = null;
            try
            {
                rc = ServiceImpl.OpenDataFileSystem(out tempFileSystem, normalizer.Path, FileSystemProxyType.Rom,
                    programId);
                if (rc.IsFailure()) return rc;

                tempFileSystem = StorageLayoutTypeSetFileSystem.CreateShared(ref tempFileSystem, storageFlag);

                Shared.Move(out fileSystem, ref tempFileSystem);
                return Result.Success;
            }
            finally
            {
                tempFileSystem?.Dispose();
            }
        }

        public Result OpenContentStorageFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            ContentStorageId contentStorageId)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            StorageType storageFlag = contentStorageId == ContentStorageId.System ? StorageType.Bis : StorageType.All;
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(storageFlag);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            Accessibility accessibility =
                programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountContentStorage);

            if (!accessibility.CanRead || !accessibility.CanWrite)
                return ResultFs.PermissionDenied.Log();

            ReferenceCountedDisposable<IFileSystem> tempFileSystem = null;

            try
            {
                rc = ServiceImpl.OpenContentStorageFileSystem(out tempFileSystem, contentStorageId);
                if (rc.IsFailure()) return rc;

                tempFileSystem = StorageLayoutTypeSetFileSystem.CreateShared(ref tempFileSystem, storageFlag);
                tempFileSystem = AsynchronousAccessFileSystem.CreateShared(ref tempFileSystem);

                // Create an SF adapter for the file system
                fileSystem = FileSystemInterfaceAdapter.CreateShared(ref tempFileSystem);

                return Result.Success;
            }
            finally
            {
                tempFileSystem?.Dispose();
            }
        }

        public Result RegisterExternalKey(in RightsId rightsId, in AccessKey accessKey)
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.RegisterExternalKey))
                return ResultFs.PermissionDenied.Log();

            return ServiceImpl.RegisterExternalKey(in rightsId, in accessKey);
        }

        public Result UnregisterExternalKey(in RightsId rightsId)
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.RegisterExternalKey))
                return ResultFs.PermissionDenied.Log();

            return ServiceImpl.UnregisterExternalKey(in rightsId);
        }

        public Result UnregisterAllExternalKey()
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.RegisterExternalKey))
                return ResultFs.PermissionDenied.Log();

            return ServiceImpl.UnregisterAllExternalKey();
        }

        public Result RegisterUpdatePartition()
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.RegisterUpdatePartition))
                return ResultFs.PermissionDenied.Log();

            rc = ServiceImpl.ResolveRomPath(out Path romPath, programInfo.ProgramIdValue, programInfo.StorageId);
            if (rc.IsFailure()) return rc;

            using var normalizer = new PathNormalizer(romPath, GetPathNormalizerOptions(romPath));
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            return ServiceImpl.RegisterUpdatePartition(programInfo.ProgramIdValue, normalizer.Path);
        }

        public Result OpenRegisteredUpdatePartition(out ReferenceCountedDisposable<IFileSystemSf> fileSystem)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            var storageFlag = StorageType.All;
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(storageFlag);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            Accessibility accessibility =
                programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountRegisteredUpdatePartition);
            if (!accessibility.CanRead)
                return ResultFs.PermissionDenied.Log();

            ReferenceCountedDisposable<IFileSystem> tempFileSystem = null;
            try
            {
                rc = ServiceImpl.OpenRegisteredUpdatePartition(out tempFileSystem);
                if (rc.IsFailure()) return rc;

                tempFileSystem = StorageLayoutTypeSetFileSystem.CreateShared(ref tempFileSystem, storageFlag);
                if (tempFileSystem is null)
                    return ResultFs.AllocationMemoryFailedAllocateShared.Log();

                tempFileSystem = AsynchronousAccessFileSystem.CreateShared(ref tempFileSystem);
                if (tempFileSystem is null)
                    return ResultFs.AllocationMemoryFailedAllocateShared.Log();

                fileSystem = FileSystemInterfaceAdapter.CreateShared(ref tempFileSystem);
                if (fileSystem is null)
                    return ResultFs.AllocationMemoryFailedCreateShared.Log();

                return Result.Success;
            }
            finally
            {
                tempFileSystem?.Dispose();
            }
        }

        public Result IsArchivedProgram(out bool isArchived, ulong processId)
        {
            throw new NotImplementedException();
        }

        public Result SetSdCardEncryptionSeed(in EncryptionSeed encryptionSeed)
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.SetEncryptionSeed))
                return ResultFs.PermissionDenied.Log();

            return ServiceImpl.SetSdCardEncryptionSeed(in encryptionSeed);
        }

        public Result OpenSystemDataUpdateEventNotifier(out ReferenceCountedDisposable<IEventNotifier> eventNotifier)
        {
            throw new NotImplementedException();
        }

        public Result NotifySystemDataUpdateEvent()
        {
            throw new NotImplementedException();
        }

        public Result HandleResolubleAccessFailure(out bool wasDeferred, Result resultForNoFailureDetected)
        {
            return ServiceImpl.HandleResolubleAccessFailure(out wasDeferred, resultForNoFailureDetected, ProcessId);
        }

        public void IncrementRomFsRemountForDataCorruptionCount()
        {
            ServiceImpl.IncrementRomFsRemountForDataCorruptionCount();
        }

        public void IncrementRomFsUnrecoverableDataCorruptionByRemountCount()
        {
            ServiceImpl.IncrementRomFsUnrecoverableDataCorruptionByRemountCount();
        }

        public void IncrementRomFsRecoveredByInvalidateCacheCount()
        {
            ServiceImpl.IncrementRomFsRecoveredByInvalidateCacheCount();
        }

        private Result TryAcquireAddOnContentOpenCountSemaphore(out IUniqueLock semaphoreLock)
        {
            throw new NotImplementedException();
        }

        private Result TryAcquireRomMountCountSemaphore(out IUniqueLock semaphoreLock)
        {
            throw new NotImplementedException();
        }

        private Result GetProgramInfo(out ProgramInfo programInfo)
        {
            return ServiceImpl.GetProgramInfoByProcessId(out programInfo, ProcessId);
        }

        private Result GetProgramInfoByProcessId(out ProgramInfo programInfo, ulong processId)
        {
            return ServiceImpl.GetProgramInfoByProcessId(out programInfo, processId);
        }

        private Result GetProgramInfoByProgramId(out ProgramInfo programInfo, ulong programId)
        {
            return ServiceImpl.GetProgramInfoByProgramId(out programInfo, programId);
        }

        private PathNormalizer.Option GetPathNormalizerOptions(U8Span path)
        {
            // Set the PreserveUnc flag if the path is on the host file system
            PathNormalizer.Option hostOption = IsHostFs(path) ? PathNormalizer.Option.PreserveUnc : PathNormalizer.Option.None;
            return PathNormalizer.Option.HasMountName | PathNormalizer.Option.PreserveTrailingSeparator | hostOption;
        }

        private bool IsHostFs(U8Span path)
        {
            int hostMountLength = StringUtils.GetLength(CommonPaths.HostRootFileSystemMountName,
                PathTools.MountNameLengthMax);

            return StringUtils.Compare(path, CommonPaths.HostRootFileSystemMountName, hostMountLength) == 0;
        }

        Result IRomFileSystemAccessFailureManager.OpenDataStorageCore(out ReferenceCountedDisposable<IStorage> storage,
            out Hash ncaHeaderDigest, ulong id, StorageId storageId)
        {
            return OpenDataStorageCore(out storage, out ncaHeaderDigest, id, storageId);
        }
    }
}
