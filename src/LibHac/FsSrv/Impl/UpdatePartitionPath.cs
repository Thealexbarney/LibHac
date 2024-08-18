using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Impl;
using LibHac.Os;

namespace LibHac.FsSrv.Impl;

public class UpdatePartitionPath : IDisposable
{
    private Path.Stored _path;
    private ContentAttributes _contentAttributes;
    private ulong _updaterProgramId;
    private SdkMutexType _mutex;

    public UpdatePartitionPath()
    {
        _path = new Path.Stored();
        _mutex = new SdkMutexType();
        _path.InitializeAsEmpty();
    }

    public void Dispose()
    {
        _path.Dispose();
    }

    public Result Set(ulong updaterProgramId, ref readonly Path path, ContentAttributes contentAttributes)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        if (path.IsEmpty())
        {
            return _path.InitializeAsEmpty().Ret();
        }

        if (path.IsMatchHead(CommonMountNames.RegisteredUpdatePartitionMountName, CommonMountNames.RegisteredUpdatePartitionMountName.Length))
        {
            return ResultFs.InvalidPath.Log();
        }

        Result res = _path.Initialize(in path);
        if (res.IsFailure()) return res.Miss();

        _updaterProgramId = updaterProgramId;
        _contentAttributes = contentAttributes;

        return Result.Success;
    }

    public Result Get(ref Path outPath, out ContentAttributes outContentAttributes, out ulong outUpdaterProgramId)
    {
        UnsafeHelpers.SkipParamInit(out outContentAttributes);

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);
        using Path tempPath = _path.DangerousGetPath();

        outUpdaterProgramId = _updaterProgramId;

        Result res = _path.Initialize(in tempPath);
        if (res.IsFailure()) return res.Miss();

        outContentAttributes = _contentAttributes;

        return Result.Success;
    }
}