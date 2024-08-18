﻿using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Sf;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.FsSrv.Impl;

/// <summary>
/// Wraps an <see cref="IStorage"/> to allow interfacing with it via the <see cref="IStorageSf"/> interface over IPC.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class StorageInterfaceAdapter : IStorageSf
{
    private SharedRef<IStorage> _baseStorage;

    public StorageInterfaceAdapter(ref readonly SharedRef<IStorage> baseStorage)
    {
        _baseStorage = SharedRef<IStorage>.CreateCopy(in baseStorage);
    }

    public void Dispose()
    {
        _baseStorage.Destroy();
    }

    public Result Read(long offset, OutBuffer destination, long size)
    {
        const int maxTryCount = 2;

        if (offset < 0)
            return ResultFs.InvalidOffset.Log();

        if (size < 0)
            return ResultFs.InvalidSize.Log();

        if (destination.Size < size)
            return ResultFs.InvalidSize.Log();

        Result res = Result.Success;

        for (int tryNum = 0; tryNum < maxTryCount; tryNum++)
        {
            res = _baseStorage.Get.Read(offset, destination.Buffer.Slice(0, (int)size));

            // Retry on ResultDataCorrupted
            if (!ResultFs.DataCorrupted.Includes(res))
                break;
        }

        return res;
    }

    public Result Write(long offset, InBuffer source, long size)
    {
        if (offset < 0)
            return ResultFs.InvalidOffset.Log();

        if (size < 0)
            return ResultFs.InvalidSize.Log();

        if (source.Size < size)
            return ResultFs.InvalidSize.Log();

        using var scopedPriorityChanger = new ScopedThreadPriorityChangerByAccessPriority(
            ScopedThreadPriorityChangerByAccessPriority.AccessMode.Write);

        return _baseStorage.Get.Write(offset, source.Buffer.Slice(0, (int)size));
    }

    public Result Flush()
    {
        return _baseStorage.Get.Flush();
    }

    public Result SetSize(long size)
    {
        return _baseStorage.Get.SetSize(size);
    }

    public Result GetSize(out long size)
    {
        return _baseStorage.Get.GetSize(out size);
    }

    public Result OperateRange(out QueryRangeInfo rangeInfo, int operationId, long offset, long size)
    {
        UnsafeHelpers.SkipParamInit(out rangeInfo);
        rangeInfo.Clear();

        if (operationId == (int)OperationId.QueryRange)
        {
            Unsafe.SkipInit(out QueryRangeInfo info);

            Result res = _baseStorage.Get.OperateRange(SpanHelpers.AsByteSpan(ref info), OperationId.QueryRange,
                offset, size, ReadOnlySpan<byte>.Empty);
            if (res.IsFailure()) return res.Miss();

            rangeInfo.Merge(in info);
        }
        else if (operationId == (int)OperationId.InvalidateCache)
        {
            Result res = _baseStorage.Get.OperateRange(Span<byte>.Empty, OperationId.InvalidateCache, offset, size,
                ReadOnlySpan<byte>.Empty);
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }
}