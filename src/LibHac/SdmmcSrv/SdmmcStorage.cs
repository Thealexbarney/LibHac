using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Sdmmc;
using LibHac.Sf;
using LibHac.Util;
using static LibHac.Sdmmc.SdmmcApi;
using static LibHac.SdmmcSrv.Common;
using static LibHac.SdmmcSrv.SdmmcResultConverter;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.SdmmcSrv;

/// <summary>
/// Provides an <see cref="IStorage"/> interface for calling the sdmmc Read and Write functions.
/// The offset and size of reads and writes must be aligned to 0x200 bytes (<see cref="SdmmcApi.SectorSize"/>).
/// </summary>
/// <remarks>Based on nnSdk 15.3.0 (FS 15.0.0)</remarks>
internal class SdmmcStorage : IStorage
{
    private Port _port;

    // LibHac additions
    private SdmmcApi _sdmmc;

    public SdmmcStorage(Port port, SdmmcApi sdmmc)
    {
        _port = port;
        _sdmmc = sdmmc;
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        Assert.SdkRequiresAligned(offset, SectorSize);
        Assert.SdkRequiresAligned(destination.Length, SectorSize);

        if (destination.Length == 0)
            return Result.Success;

        // Missing: Allocate a device buffer if the destination buffer is not one

        return _sdmmc.Read(destination, _port, BytesToSectors(offset), BytesToSectors(destination.Length)).Ret();
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        const int alignment = 0x4000;
        Result res;

        Assert.SdkRequiresAligned(offset, SectorSize);
        Assert.SdkRequiresAligned(source.Length, SectorSize);

        if (source.Length == 0)
            return Result.Success;

        // Missing: Allocate a device buffer if the source buffer is not one

        // Check if we have any unaligned data at the head of the source buffer.
        long alignedUpOffset = Alignment.AlignUp(offset, alignment);
        int unalignedHeadSize = (int)(alignedUpOffset - offset);
        int remainingSize = source.Length;

        // The start offset must be aligned to 0x4000 bytes. The end offset does not need to be aligned to 0x4000 bytes.
        if (alignedUpOffset != offset)
        {
            // Get the number of bytes that come before the unaligned data.
            int paddingSize = alignment - unalignedHeadSize;

            // If the end offset is inside the first 0x4000-byte block, don't write past the end offset.
            // Otherwise write the entire aligned block.
            int writeSize = Math.Min(alignment, paddingSize + source.Length);

            using var pooledBuffer = new PooledBuffer(writeSize, alignment);

            // Get the number of bytes from source to be written to the aligned buffer, and copy that data to the buffer.
            int unalignedSize = writeSize - paddingSize;
            source.Slice(0, unalignedSize).CopyTo(pooledBuffer.GetBuffer().Slice(paddingSize));

            // Read the current data into the aligned buffer.
            res = GetFsResult(_port,
                _sdmmc.Read(pooledBuffer.GetBuffer().Slice(0, paddingSize), _port,
                    BytesToSectors(alignedUpOffset - alignment), BytesToSectors(paddingSize)));
            if (res.IsFailure()) return res.Miss();

            // Write the aligned buffer.
            res = GetFsResult(_port,
                _sdmmc.Write(_port, BytesToSectors(alignedUpOffset - alignment), BytesToSectors(writeSize),
                    pooledBuffer.GetBuffer().Slice(0, writeSize)));
            if (res.IsFailure()) return res.Miss();

            remainingSize -= unalignedSize;
        }

        // We've written any unaligned data. Write the remaining aligned data.
        if (remainingSize > 0)
        {
            res = GetFsResult(_port,
                _sdmmc.Write(_port, BytesToSectors(alignedUpOffset), BytesToSectors(remainingSize),
                    source.Slice(unalignedHeadSize, remainingSize)));
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    public override Result Flush()
    {
        return Result.Success;
    }

    public override Result SetSize(long size)
    {
        return ResultFs.UnsupportedSetSizeForSdmmcStorage.Log();
    }

    public override Result GetSize(out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        Result res = GetFsResult(_port, _sdmmc.GetDeviceMemoryCapacity(out uint numSectors, _port));
        if (res.IsFailure()) return res.Miss();

        size = numSectors * SectorSize;
        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        switch (operationId)
        {
            case OperationId.InvalidateCache:
                return Result.Success;
            case OperationId.QueryRange:
                if (outBuffer.Length != Unsafe.SizeOf<QueryRangeInfo>())
                    return ResultFs.InvalidSize.Log();

                SpanHelpers.AsStruct<QueryRangeInfo>(outBuffer).Clear();

                return Result.Success;
            default:
                return ResultFs.UnsupportedOperateRangeForSdmmcStorage.Log();
        }
    }
}

/// <summary>
/// An adapter that directly translates <see cref="IStorageSf"/> sf calls to <see cref="IStorage"/> calls with no checks
/// or validations.
/// </summary>
/// <remarks>Based on nnSdk 15.3.0 (FS 15.0.0)</remarks>
internal class SdmmcStorageInterfaceAdapter : IStorageSf
{
    private IStorage _baseStorage;

    public SdmmcStorageInterfaceAdapter(IStorage baseStorage)
    {
        _baseStorage = baseStorage;
    }

    public virtual void Dispose() { }

    public virtual Result Read(long offset, OutBuffer destination, long size)
    {
        return _baseStorage.Read(offset, destination.Buffer.Slice(0, (int)size)).Ret();
    }

    public virtual Result Write(long offset, InBuffer source, long size)
    {
        return _baseStorage.Write(offset, source.Buffer.Slice(0, (int)size)).Ret();
    }

    public virtual Result Flush()
    {
        return _baseStorage.Flush().Ret();
    }

    public virtual Result SetSize(long size)
    {
        return _baseStorage.SetSize(size).Ret();
    }

    public virtual Result GetSize(out long size)
    {
        return _baseStorage.GetSize(out size).Ret();
    }

    public virtual Result OperateRange(out QueryRangeInfo rangeInfo, int operationId, long offset, long size)
    {
        UnsafeHelpers.SkipParamInit(out rangeInfo);

        return _baseStorage.OperateRange(SpanHelpers.AsByteSpan(ref rangeInfo), (OperationId)operationId, offset,
            size, ReadOnlySpan<byte>.Empty).Ret();
    }
}