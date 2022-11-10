using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem;

/// <summary>
/// Allows interacting with an <see cref="IStorage"/> via an <see cref="IFile"/> interface.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
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

        Result res = DryRead(out long readSize, offset, destination.Length, in option, _mode);
        if (res.IsFailure()) return res.Miss();

        if (readSize == 0)
        {
            bytesRead = 0;
            return Result.Success;
        }

        res = _baseStorage.Read(offset, destination.Slice(0, (int)readSize));
        if (res.IsFailure()) return res.Miss();

        bytesRead = readSize;
        return Result.Success;
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
    {
        Assert.SdkRequiresNotNull(_baseStorage);

        Result res = DryWrite(out bool isAppendNeeded, offset, source.Length, in option, _mode);
        if (res.IsFailure()) return res.Miss();

        if (isAppendNeeded)
        {
            res = DoSetSize(offset + source.Length);
            if (res.IsFailure()) return res.Miss();
        }

        res = _baseStorage.Write(offset, source);
        if (res.IsFailure()) return res.Miss();

        if (option.HasFlushFlag())
        {
            res = Flush();
            if (res.IsFailure()) return res.Miss();
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

        Result res = DrySetSize(size, _mode);
        if (res.IsFailure()) return res.Miss();

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

                Result res = _baseStorage.OperateRange(OperationId.InvalidateCache, offset, size);
                if (res.IsFailure()) return res.Miss();

                break;
            }
            case OperationId.QueryRange:
            {
                if (offset < 0)
                    return ResultFs.InvalidOffset.Log();

                Result res = GetSize(out long fileSize);
                if (res.IsFailure()) return res.Miss();

                long operableSize = Math.Max(0, fileSize - offset);
                long operateSize = Math.Min(operableSize, size);

                res = _baseStorage.OperateRange(outBuffer, operationId, offset, operateSize, inBuffer);
                if (res.IsFailure()) return res.Miss();

                break;
            }
            default:
                return ResultFs.UnsupportedOperateRangeForStorageFile.Log();
        }

        return Result.Success;
    }
}