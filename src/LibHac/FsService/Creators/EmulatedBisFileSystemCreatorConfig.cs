﻿using System.Diagnostics;
using LibHac.Fs;

namespace LibHac.FsService.Creators
{
    public class EmulatedBisFileSystemCreatorConfig
    {
        private const int ValidPartitionCount = 4;

        public IFileSystem RootFileSystem { get; set; }

        private IFileSystem[] PartitionFileSystems { get; } = new IFileSystem[ValidPartitionCount];
        private string[] PartitionPaths { get; } = new string[ValidPartitionCount];

        public Result SetFileSystem(IFileSystem fileSystem, BisPartitionId partitionId)
        {
            if (fileSystem == null) return ResultFs.NullArgument.Log();
            if (!IsValidPartitionId(partitionId)) return ResultFs.InvalidArgument.Log();

            PartitionFileSystems[GetArrayIndex(partitionId)] = fileSystem;

            return Result.Success;
        }

        public Result SetPath(string path, BisPartitionId partitionId)
        {
            if (path == null) return ResultFs.NullArgument.Log();
            if (!IsValidPartitionId(partitionId)) return ResultFs.InvalidArgument.Log();

            PartitionPaths[GetArrayIndex(partitionId)] = path;

            return Result.Success;
        }

        public bool TryGetFileSystem(out IFileSystem fileSystem, BisPartitionId partitionId)
        {
            if (!IsValidPartitionId(partitionId))
            {
                fileSystem = default;
                return false;
            }

            fileSystem = PartitionFileSystems[GetArrayIndex(partitionId)];

            return fileSystem != null;
        }

        public bool TryGetPartitionPath(out string path, BisPartitionId partitionId)
        {
            if (!IsValidPartitionId(partitionId))
            {
                path = default;
                return false;
            }

            path = PartitionPaths[GetArrayIndex(partitionId)];

            return path != null;
        }

        private int GetArrayIndex(BisPartitionId id)
        {
            Debug.Assert(IsValidPartitionId(id));

            return id - BisPartitionId.CalibrationFile;
        }

        private bool IsValidPartitionId(BisPartitionId id)
        {
            return id >= BisPartitionId.CalibrationFile && id <= BisPartitionId.System;
        }
    }
}
