using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Sf;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using PathNormalizer = LibHac.FsSrv.Impl.PathNormalizer;

namespace LibHac.FsSrv
{
    public readonly struct BaseFileSystemService
    {
        private readonly BaseFileSystemServiceImpl _serviceImpl;
        private readonly ulong _processId;

        public BaseFileSystemService(BaseFileSystemServiceImpl serviceImpl, ulong processId)
        {
            _serviceImpl = serviceImpl;
            _processId = processId;
        }

        private Result GetProgramInfo(out ProgramInfo programInfo)
        {
            return GetProgramInfo(out programInfo, _processId);
        }

        private Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
        {
            return _serviceImpl.GetProgramInfo(out programInfo, processId);
        }

        private Result CheckCapabilityById(BaseFileSystemId id, ulong processId)
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo, processId);
            if (rc.IsFailure()) return rc;

            if (id == BaseFileSystemId.TemporaryDirectory)
            {
                Accessibility accessibility =
                    programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountTemporaryDirectory);

                if (!accessibility.CanRead || !accessibility.CanWrite)
                    return ResultFs.PermissionDenied.Log();
            }
            else
            {
                Accessibility accessibility =
                    programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountAllBaseFileSystem);

                if (!accessibility.CanRead || !accessibility.CanWrite)
                    return ResultFs.PermissionDenied.Log();
            }

            return Result.Success;
        }

        public Result OpenBaseFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            BaseFileSystemId fileSystemId)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            Result rc = CheckCapabilityById(fileSystemId, _processId);
            if (rc.IsFailure()) return rc;

            ReferenceCountedDisposable<IFileSystem> fs = null;

            try
            {
                // Open the file system
                rc = _serviceImpl.OpenBaseFileSystem(out fs, fileSystemId);
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

        public Result OpenBisFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, in FspPath rootPath,
            BisPartitionId partitionId)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            // Get the permissions the caller needs
            AccessibilityType requiredAccess = partitionId switch
            {
                BisPartitionId.CalibrationFile => AccessibilityType.MountBisCalibrationFile,
                BisPartitionId.SafeMode => AccessibilityType.MountBisSafeMode,
                BisPartitionId.User => AccessibilityType.MountBisUser,
                BisPartitionId.System => AccessibilityType.MountBisSystem,
                BisPartitionId.SystemProperPartition => AccessibilityType.MountBisSystemProperPartition,
                _ => AccessibilityType.NotMount
            };

            // Reject opening invalid partitions
            if (requiredAccess == AccessibilityType.NotMount)
                return ResultFs.InvalidArgument.Log();

            // Verify the caller has the required permissions
            Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(requiredAccess);

            if (!accessibility.CanRead || !accessibility.CanWrite)
                return ResultFs.PermissionDenied.Log();

            // Normalize the path
            using var normalizer = new PathNormalizer(rootPath, PathNormalizer.Option.AcceptEmpty);
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            ReferenceCountedDisposable<IFileSystem> fs = null;

            try
            {
                // Open the file system
                rc = _serviceImpl.OpenBisFileSystem(out fs, normalizer.Path,
                    partitionId);
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

        public Result SetBisRootForHost(BisPartitionId partitionId, in FspPath path)
        {
            throw new NotImplementedException();
        }

        public Result CreatePaddingFile(long size)
        {
            // File size must be non-negative
            if (size < 0)
                return ResultFs.InvalidSize.Log();

            // Caller must have the FillBis permission
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.FillBis))
                return ResultFs.PermissionDenied.Log();

            return _serviceImpl.CreatePaddingFile(size);
        }

        public Result DeleteAllPaddingFiles()
        {
            // Caller must have the FillBis permission
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.FillBis))
                return ResultFs.PermissionDenied.Log();

            return _serviceImpl.DeleteAllPaddingFiles();
        }

        public Result OpenGameCardFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, GameCardHandle handle,
            GameCardPartition partitionId)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountGameCard).CanRead)
                return ResultFs.PermissionDenied.Log();

            ReferenceCountedDisposable<IFileSystem> fs = null;

            try
            {
                rc = _serviceImpl.OpenGameCardFileSystem(out fs, handle, partitionId);
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

        public Result OpenSdCardFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountSdCard);

            if (!accessibility.CanRead || !accessibility.CanWrite)
                return ResultFs.PermissionDenied.Log();

            ReferenceCountedDisposable<IFileSystem> fs = null;

            try
            {
                rc = _serviceImpl.OpenSdCardProxyFileSystem(out fs);
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

        public Result FormatSdCardFileSystem()
        {
            // Caller must have the FormatSdCard permission
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.FormatSdCard))
                return ResultFs.PermissionDenied.Log();

            return _serviceImpl.FormatSdCardProxyFileSystem();
        }

        public Result FormatSdCardDryRun()
        {
            // No permissions are needed to call this method

            return _serviceImpl.FormatSdCardProxyFileSystem();
        }

        public Result IsExFatSupported(out bool isSupported)
        {
            // No permissions are needed to call this method

            isSupported = _serviceImpl.IsExFatSupported();
            return Result.Success;
        }

        public Result OpenImageDirectoryFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            ImageDirectoryId directoryId)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            // Caller must have the MountImageAndVideoStorage permission
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            Accessibility accessibility =
                programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountImageAndVideoStorage);

            if (!accessibility.CanRead || !accessibility.CanWrite)
                return ResultFs.PermissionDenied.Log();

            // Get the base file system ID
            BaseFileSystemId fileSystemId;
            switch (directoryId)
            {
                case ImageDirectoryId.Nand:
                    fileSystemId = BaseFileSystemId.ImageDirectoryNand;
                    break;
                case ImageDirectoryId.SdCard:
                    fileSystemId = BaseFileSystemId.ImageDirectorySdCard;
                    break;
                default:
                    return ResultFs.InvalidArgument.Log();
            }
            ReferenceCountedDisposable<IFileSystem> fs = null;

            try
            {
                rc = _serviceImpl.OpenBaseFileSystem(out fs, fileSystemId);
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

        public Result OpenBisWiper(out ReferenceCountedDisposable<IWiper> bisWiper, NativeHandle transferMemoryHandle, ulong transferMemorySize)
        {
            UnsafeHelpers.SkipParamInit(out bisWiper);

            // Caller must have the OpenBisWiper permission
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.OpenBisWiper))
                return ResultFs.PermissionDenied.Log();

            rc = _serviceImpl.OpenBisWiper(out IWiper wiper, transferMemoryHandle, transferMemorySize);
            if (rc.IsFailure()) return rc;

            bisWiper = new ReferenceCountedDisposable<IWiper>(wiper);
            return Result.Success;
        }
    }
}
