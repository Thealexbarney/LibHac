using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem;

/// <summary>
/// Allows interacting with an <see cref="IStorage"/> via an <see cref="IFile"/> interface.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public class StorageFile : IFile
{
    private IStorage _baseStorage;
    private OpenMode _mode;

    public StorageFile(IStorage baseStorage, OpenMode mode)
    {
        _baseStorage = baseStorage;
        _mode = mode;
    }

    protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
    {
        UnsafeHelpers.SkipParamInit(out bytesRead);

        Assert.SdkRequiresNotNull(_baseStorage);

        Result rc = DryRead(out long readSize, offset, destination.Length, in option, _mode);
        if (rc.IsFailure()) return rc;

        if (readSize == 0)
        {
            bytesRead = 0;
            return Result.Success;
        }

        rc = _baseStorage.Read(offset, destination.Slice(0, (int)readSize));
        if (rc.IsFailure()) return rc;

        bytesRead = readSize;
        return Result.Success;
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
    {
        Assert.SdkRequiresNotNull(_baseStorage);

        Result rc = DryWrite(out bool isAppendNeeded, offset, source.Length, in option, _mode);
        if (rc.IsFailure()) return rc;

        if (isAppendNeeded)
        {
            rc = DoSetSize(offset + source.Length);
            if (rc.IsFailure()) return rc;
        }

        rc = _baseStorage.Write(offset, source);
        if (rc.IsFailure()) return rc;

        if (option.HasFlushFlag())
        {
            rc = Flush();
            if (rc.IsFailure()) return rc.Miss();
        }

        return Result.Success;
    }

    protected override Result DoFlush()
    {
        Assert.SdkRequiresNotNull(_baseStorage);

        if (!_mode.HasFlag(OpenMode.Write))
            return Result.Success;

        return _baseStorage.Flush();
    }

    protected override Result DoGetSize(out long size)
    {
        Assert.SdkRequiresNotNull(_baseStorage);

        return _baseStorage.GetSize(out size);
    }

    protected override Result DoSetSize(long size)
    {
        Assert.SdkRequiresNotNull(_baseStorage);

        Result rc = DrySetSize(size, _mode);
        if (rc.IsFailure()) return rc.Miss();

        return _baseStorage.SetSize(size);
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        Assert.SdkRequiresNotNull(_baseStorage);

        switch (operationId)
        {
            case OperationId.InvalidateCache:
            {
                if (!_mode.HasFlag(OpenMode.Read))
                    return ResultFs.ReadUnpermitted.Log();

                Result rc = _baseStorage.OperateRange(OperationId.InvalidateCache, offset, size);
                if (rc.IsFailure()) return rc.Miss();

                break;
            }
            case OperationId.QueryRange:
            {
                if (offset < 0)
                    return ResultFs.InvalidOffset.Log();

                Result rc = GetSize(out long fileSize);
                if (rc.IsFailure()) return rc.Miss();

                long operableSize = Math.Max(0, fileSize - offset);
                long operateSize = Math.Min(operableSize, size);

                rc = _baseStorage.OperateRange(outBuffer, operationId, offset, operateSize, inBuffer);
                if (rc.IsFailure()) return rc.Miss();

                break;
            }
            default:
                return ResultFs.UnsupportedOperateRangeForStorageFile.Log();
        }

        return Result.Success;
    }
}