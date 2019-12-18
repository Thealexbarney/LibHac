using System.Diagnostics;
using LibHac.Fs;

namespace LibHac.FsService.Creators
{
    public class EmulatedBisFileSystemCreator : IBuiltInStorageFileSystemCreator
    {
        private EmulatedBisFileSystemCreatorConfig Config { get; }

        public EmulatedBisFileSystemCreator(IFileSystem rootFileSystem)
        {
            Config = new EmulatedBisFileSystemCreatorConfig();
            Config.RootFileSystem = rootFileSystem;
        }

        public EmulatedBisFileSystemCreator(EmulatedBisFileSystemCreatorConfig config)
        {
            Config = config;
        }

        public Result Create(out IFileSystem fileSystem, string rootPath, BisPartitionId partitionId)
        {
            fileSystem = default;

            if (!IsValidPartitionId(partitionId)) return ResultFs.InvalidArgument.Log();
            if (rootPath == null) return ResultFs.NullArgument.Log();

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

        public Result CreateFatFileSystem(out IFileSystem fileSystem, BisPartitionId partitionId)
        {
            fileSystem = default;
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
