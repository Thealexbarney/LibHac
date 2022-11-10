using System.Diagnostics;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator;

/// <summary>
/// Configuration for <see cref="EmulatedBisFileSystemCreator"/> that specifies how each
/// BIS partition is opened.
/// </summary>
public class EmulatedBisFileSystemCreatorConfig
{
    private const int ValidPartitionCount = 4;

    private SharedRef<IFileSystem> _rootFileSystem;

    private SharedRef<IFileSystem>[] PartitionFileSystems { get; } = new SharedRef<IFileSystem>[ValidPartitionCount];
    private string[] PartitionPaths { get; } = new string[ValidPartitionCount];

    public Result SetRootFileSystem(ref SharedRef<IFileSystem> fileSystem)
    {
        if (!fileSystem.HasValue) return ResultFs.NullptrArgument.Log();
        if (_rootFileSystem.HasValue) return ResultFs.PreconditionViolation.Log();

        _rootFileSystem.SetByMove(ref fileSystem);

        return Result.Success;
    }

    public Result SetFileSystem(ref UniqueRef<IFileSystem> fileSystem, BisPartitionId partitionId)
    {
        if (!fileSystem.HasValue) return ResultFs.NullptrArgument.Log();
        if (!IsValidPartitionId(partitionId)) return ResultFs.InvalidArgument.Log();

        PartitionFileSystems[GetArrayIndex(partitionId)].Set(ref fileSystem);

        return Result.Success;
    }

    public Result SetPath(string path, BisPartitionId partitionId)
    {
        if (path == null) return ResultFs.NullptrArgument.Log();
        if (!IsValidPartitionId(partitionId)) return ResultFs.InvalidArgument.Log();

        PartitionPaths[GetArrayIndex(partitionId)] = path;

        return Result.Success;
    }

    public bool TryGetRootFileSystem(ref SharedRef<IFileSystem> outFileSystem)
    {
        outFileSystem.SetByCopy(in _rootFileSystem);

        return outFileSystem.HasValue;
    }

    public bool TryGetFileSystem(ref SharedRef<IFileSystem> outFileSystem, BisPartitionId partitionId)
    {
        if (!IsValidPartitionId(partitionId))
            return false;

        outFileSystem.SetByCopy(in PartitionFileSystems[GetArrayIndex(partitionId)]);

        return outFileSystem.HasValue;
    }

    public bool TryGetPartitionPath(out string path, BisPartitionId partitionId)
    {
        if (!IsValidPartitionId(partitionId))
        {
            UnsafeHelpers.SkipParamInit(out path);
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