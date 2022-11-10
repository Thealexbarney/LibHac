using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSystem;

/// <summary>
/// An <see cref="IStorage"/> that will switch between forwarding requests to one of two different base
/// <see cref="IStorage"/>s. On each request the provided storage selection function will be called and the request
/// will be forwarded to the appropriate <see cref="IStorage"/> based on the return value.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public class SwitchStorage : IStorage
{
    private SharedRef<IStorage> _trueStorage;
    private SharedRef<IStorage> _falseStorage;
    private Func<bool> _storageSelectionFunction;

    public SwitchStorage(in SharedRef<IStorage> trueStorage, in SharedRef<IStorage> falseStorage,
        Func<bool> storageSelectionFunction)
    {
        _trueStorage = SharedRef<IStorage>.CreateCopy(in trueStorage);
        _falseStorage = SharedRef<IStorage>.CreateCopy(in falseStorage);
        _storageSelectionFunction = storageSelectionFunction;
    }

    public override void Dispose()
    {
        _trueStorage.Destroy();
        _falseStorage.Destroy();

        base.Dispose();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IStorage SelectStorage()
    {
        return (_storageSelectionFunction() ? _trueStorage : _falseStorage).Get;
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        Result rc = SelectStorage().Read(offset, destination);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        Result rc = SelectStorage().Write(offset, source);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public override Result Flush()
    {
        Result rc = SelectStorage().Flush();
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public override Result GetSize(out long size)
    {
        Result rc = SelectStorage().GetSize(out size);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public override Result SetSize(long size)
    {
        Result rc = SelectStorage().SetSize(size);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        switch (operationId)
        {
            case OperationId.InvalidateCache:
            {
                Result rc = _trueStorage.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer);
                if (rc.IsFailure()) return rc.Miss();

                rc = _falseStorage.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer);
                if (rc.IsFailure()) return rc.Miss();

                return Result.Success;
            }
            case OperationId.QueryRange:
            {
                Result rc = SelectStorage().OperateRange(outBuffer, operationId, offset, size, inBuffer);
                if (rc.IsFailure()) return rc.Miss();

                return Result.Success;
            }
            default:
                return ResultFs.UnsupportedOperateRangeForSwitchStorage.Log();
        }
    }
}

/// <summary>
/// Takes a <see cref="Region"/> and two base <see cref="IStorage"/>s upon construction. Requests inside
/// the provided <see cref="Region"/> will be forwarded to one <see cref="IStorage"/>, and requests outside
/// will be forwarded to the other.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public class RegionSwitchStorage : IStorage
{
    public struct Region
    {
        public long Offset;
        public long Size;
    }

    private SharedRef<IStorage> _insideRegionStorage;
    private SharedRef<IStorage> _outsideRegionStorage;
    private Region _region;

    public RegionSwitchStorage(in SharedRef<IStorage> insideRegionStorage,
        in SharedRef<IStorage> outsideRegionStorage, Region region)
    {
        _insideRegionStorage = SharedRef<IStorage>.CreateCopy(in insideRegionStorage);
        _outsideRegionStorage = SharedRef<IStorage>.CreateCopy(in outsideRegionStorage);
        _region = region;
    }

    public override void Dispose()
    {
        _insideRegionStorage.Destroy();
        _outsideRegionStorage.Destroy();

        base.Dispose();
    }

    /// <summary>
    /// Checks if the requested range is inside or outside the <see cref="Region"/>.
    /// </summary>
    /// <param name="currentSize">The size past the start of the range until entering or exiting the <see cref="Region"/>.</param>
    /// <param name="offset">The offset of the range to check.</param>
    /// <param name="size">The size of the range to check.</param>
    /// <returns><see langword="true"/> the start of the range is inside the <see cref="Region"/>;
    /// otherwise <see langword="false"/>.</returns>
    private bool CheckRegions(out long currentSize, long offset, long size)
    {
        if (_region.Offset > offset)
        {
            // The requested start offset is before the region's start offset.
            // Check if the requested end offset is inside the region.
            if (offset + size < _region.Offset)
            {
                // The request is completely outside the region.
                currentSize = size;
            }
            else
            {
                // The request ends inside the region. Calculate the length of the request outside the region.
                currentSize = _region.Offset - offset;
            }

            return false;
        }

        if (_region.Offset + _region.Size > offset)
        {
            // The requested start offset is inside the region.
            // Check if the requested end offset is also inside the region.
            if (offset + size < _region.Offset + _region.Size)
            {
                // The request is completely within the region.
                currentSize = size;
            }
            else
            {
                // The request ends outside the region. Calculate the length of the request inside the region.
                currentSize = _region.Offset + _region.Size - offset;
            }

            return true;
        }

        // The request starts after the end of the region.
        currentSize = size;
        return false;
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        int bytesRead = 0;
        while (bytesRead < destination.Length)
        {
            if (CheckRegions(out long currentSize, offset + bytesRead, destination.Length - bytesRead))
            {
                Result rc = _insideRegionStorage.Get.Read(offset + bytesRead,
                    destination.Slice(bytesRead, (int)currentSize));
                if (rc.IsFailure()) return rc.Miss();
            }
            else
            {
                Result rc = _outsideRegionStorage.Get.Read(offset + bytesRead,
                    destination.Slice(bytesRead, (int)currentSize));
                if (rc.IsFailure()) return rc.Miss();
            }

            bytesRead += (int)currentSize;
        }

        return Result.Success;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        int bytesWritten = 0;
        while (bytesWritten < source.Length)
        {
            if (CheckRegions(out long currentSize, offset + bytesWritten, source.Length - bytesWritten))
            {
                Result rc = _insideRegionStorage.Get.Write(offset + bytesWritten,
                    source.Slice(bytesWritten, (int)currentSize));
                if (rc.IsFailure()) return rc.Miss();
            }
            else
            {
                Result rc = _outsideRegionStorage.Get.Write(offset + bytesWritten,
                    source.Slice(bytesWritten, (int)currentSize));
                if (rc.IsFailure()) return rc.Miss();
            }

            bytesWritten += (int)currentSize;
        }

        return Result.Success;
    }

    public override Result Flush()
    {
        Result rc = _insideRegionStorage.Get.Flush();
        if (rc.IsFailure()) return rc.Miss();

        rc = _outsideRegionStorage.Get.Flush();
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public override Result GetSize(out long size)
    {
        Result rc = _insideRegionStorage.Get.GetSize(out size);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public override Result SetSize(long size)
    {
        Result rc = _insideRegionStorage.Get.SetSize(size);
        if (rc.IsFailure()) return rc.Miss();

        rc = _outsideRegionStorage.Get.SetSize(size);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
    {
        switch (operationId)
        {
            case OperationId.InvalidateCache:
            {
                Result rc = _insideRegionStorage.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer);
                if (rc.IsFailure()) return rc.Miss();

                rc = _outsideRegionStorage.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer);
                if (rc.IsFailure()) return rc.Miss();

                return Result.Success;
            }
            case OperationId.QueryRange:
            {
                Unsafe.SkipInit(out QueryRangeInfo mergedInfo);
                mergedInfo.Clear();

                long bytesProcessed = 0;
                while (bytesProcessed < size)
                {
                    Unsafe.SkipInit(out QueryRangeInfo currentInfo);

                    if (CheckRegions(out long currentSize, offset + bytesProcessed, size - bytesProcessed))
                    {
                        Result rc = _insideRegionStorage.Get.OperateRange(SpanHelpers.AsByteSpan(ref currentInfo),
                            operationId, offset + bytesProcessed, currentSize, inBuffer);
                        if (rc.IsFailure()) return rc.Miss();
                    }
                    else
                    {
                        Result rc = _outsideRegionStorage.Get.OperateRange(SpanHelpers.AsByteSpan(ref currentInfo),
                            operationId, offset + bytesProcessed, currentSize, inBuffer);
                        if (rc.IsFailure()) return rc.Miss();
                    }

                    mergedInfo.Merge(in currentInfo);
                    bytesProcessed += currentSize;
                }

                SpanHelpers.AsByteSpan(ref mergedInfo).CopyTo(outBuffer);
                return Result.Success;
            }
            default:
                return ResultFs.UnsupportedOperateRangeForRegionSwitchStorage.Log();
        }
    }
}