using System.Diagnostics;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.Impl;

namespace LibHac.FsSrv.FsCreator
{
    /// <summary>
    /// Provides <see cref="IFileSystem"/> objects for the built-in storage (BIS) partitions.
    /// </summary>
    /// <remarks>
    /// An <see cref="EmulatedBisFileSystemCreator"/> provides <see cref="IFileSystem"/>s of the
    /// four BIS partitions: <c>CalibrationFile</c>, <c>SafeMode</c>, <c>User</c> and <c>System</c>.
    /// The source of each partition is determined by the (optionally) provided
    /// <see cref="EmulatedBisFileSystemCreatorConfig"/>.<br/>
    /// There are multiple ways the source of a partition can be specified in the configuration. In order of precedence:
    /// <list type="number">
    /// <item><description>Set an <see cref="IFileSystem"/> object with <see cref="EmulatedBisFileSystemCreatorConfig.SetFileSystem"/>.<br/>
    /// The IFileSystem will directly be used for the specified partition.</description></item>
    /// <item><description>Set a path with <see cref="EmulatedBisFileSystemCreatorConfig.SetPath"/>.<br/>
    /// The source for the partition will be the provided path in the root file system. e.g. at <c>/my/path</c> in the root FS.
    /// The root file system must be set in the configuration when using this option.</description></item>
    /// <item><description>Only set the root file system in the configuration.<br/>
    /// The source of the partition will be at its default path in the root file system.</description></item></list>
    /// Default paths for each partition:<br/>
    /// <see cref="BisPartitionId.CalibrationFile"/>: <c>/bis/cal</c><br/>
    /// <see cref="BisPartitionId.SafeMode"/>: <c>/bis/safe</c><br/>
    /// <see cref="BisPartitionId.User"/>: <c>/bis/user</c><br/>
    /// <see cref="BisPartitionId.System"/>: <c>/bis/system</c><br/>
    /// </remarks>
    public class EmulatedBisFileSystemCreator : IBuiltInStorageFileSystemCreator
    {
        private EmulatedBisFileSystemCreatorConfig Config { get; }

        /// <summary>
        /// Initializes an <see cref="EmulatedBisFileSystemCreator"/> with the default
        /// <see cref="EmulatedBisFileSystemCreatorConfig"/> using the provided <see cref="IFileSystem"/>.
        /// Each partition will be located at their default paths in this IFileSystem.
        /// </summary>
        /// <param name="rootFileSystem">The <see cref="IFileSystem"/> to use as the root file system.</param>
        public EmulatedBisFileSystemCreator(IFileSystem rootFileSystem)
        {
            Config = new EmulatedBisFileSystemCreatorConfig();
            Config.RootFileSystem = rootFileSystem;
        }

        /// <summary>
        /// Initializes an <see cref="EmulatedBisFileSystemCreator"/> with the provided configuration.
        /// </summary>
        /// <param name="config">The configuration to use.</param>
        public EmulatedBisFileSystemCreator(EmulatedBisFileSystemCreatorConfig config)
        {
            Config = config;
        }

        public Result Create(out IFileSystem fileSystem, string rootPath, BisPartitionId partitionId)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            if (!IsValidPartitionId(partitionId)) return ResultFs.InvalidArgument.Log();
            if (rootPath == null) return ResultFs.NullptrArgument.Log();

            if (Config.TryGetFileSystem(out fileSystem, partitionId))
            {
                return Result.Success;
            }

            if (Config.RootFileSystem == null)
            {
                return ResultFs.PreconditionViolation.Log();
            }

            string partitionPath = GetPartitionPath(partitionId);

            Result rc =
                Util.CreateSubFileSystem(out IFileSystem subFileSystem, Config.RootFileSystem, partitionPath, true);

            if (rc.IsFailure()) return rc;

            if (rootPath == string.Empty)
            {
                fileSystem = subFileSystem;
                return Result.Success;
            }

            return Util.CreateSubFileSystemImpl(out fileSystem, subFileSystem, rootPath);
        }

        // Todo: Make case sensitive
        public Result Create(out ReferenceCountedDisposable<IFileSystem> fileSystem, U8Span rootPath,
            BisPartitionId partitionId, bool caseSensitive)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            if (!IsValidPartitionId(partitionId)) return ResultFs.InvalidArgument.Log();
            if (rootPath.IsNull()) return ResultFs.NullptrArgument.Log();

            if (Config.TryGetFileSystem(out IFileSystem fs, partitionId))
            {
                fileSystem = new ReferenceCountedDisposable<IFileSystem>(fs);
                return Result.Success;
            }

            if (Config.RootFileSystem == null)
            {
                return ResultFs.PreconditionViolation.Log();
            }

            var partitionPath = GetPartitionPath(partitionId).ToU8String();

            ReferenceCountedDisposable<IFileSystem> partitionFileSystem = null;
            ReferenceCountedDisposable<IFileSystem> sharedRootFs = null;
            try
            {
                sharedRootFs = new ReferenceCountedDisposable<IFileSystem>(Config.RootFileSystem);

                Result rc = Utility.WrapSubDirectory(out partitionFileSystem, ref sharedRootFs, partitionPath, true);

                if (rc.IsFailure()) return rc;

                if (rootPath.IsEmpty())
                {
                    Shared.Move(out fileSystem, ref partitionFileSystem);
                    return Result.Success;
                }

                return Utility.CreateSubDirectoryFileSystem(out fileSystem, ref partitionFileSystem, rootPath);
            }
            finally
            {
                partitionFileSystem?.Dispose();
                sharedRootFs?.Dispose();
            }
        }

        public Result CreateFatFileSystem(out IFileSystem fileSystem, BisPartitionId partitionId)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);
            return ResultFs.NotImplemented.Log();
        }

        public Result SetBisRootForHost(BisPartitionId partitionId, string rootPath)
        {
            return Config.SetPath(rootPath, partitionId);
        }

        private bool IsValidPartitionId(BisPartitionId id)
        {
            return id >= BisPartitionId.CalibrationFile && id <= BisPartitionId.System;
        }

        private string GetPartitionPath(BisPartitionId id)
        {
            if (Config.TryGetPartitionPath(out string path, id))
            {
                return path;
            }

            return GetDefaultPartitionPath(id);
        }

        /// <summary>
        /// Gets the default path for the specified partition.
        /// </summary>
        /// <param name="id">The partition ID of the path to get.</param>
        /// <returns>The default path for the specified partition.</returns>
        /// <remarks>These paths are the same paths that Nintendo uses on Windows.</remarks>
        private string GetDefaultPartitionPath(BisPartitionId id)
        {
            Debug.Assert(IsValidPartitionId(id));

            switch (id)
            {
                case BisPartitionId.CalibrationFile:
                    return "/bis/cal";
                case BisPartitionId.SafeMode:
                    return "/bis/safe";
                case BisPartitionId.User:
                    return "/bis/user";
                case BisPartitionId.System:
                    return "/bis/system";
                default:
                    return string.Empty;
            }
        }
    }
}
