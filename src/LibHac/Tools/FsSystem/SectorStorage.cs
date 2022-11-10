using System;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.Tools.FsSystem;

public class SectorStorage : IStorage
{
    protected IStorage BaseStorage { get; }

    public int SectorSize { get; }
    public int SectorCount { get; private set; }

    private long Length { get; set; }
    private bool LeaveOpen { get; }

    public SectorStorage(IStorage baseStorage, int sectorSize, bool leaveOpen)
    {
        BaseStorage = baseStorage;
        SectorSize = sectorSize;

        baseStorage.GetSize(out long baseSize).ThrowIfFailure();

        SectorCount = (int)BitUtil.DivideUp(baseSize, SectorSize);
        Length = baseSize;

        LeaveOpen = leaveOpen;
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        ValidateSize(destination.Length, offset);
        return BaseStorage.Read(offset, destination);
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        ValidateSize(source.Length, offset);
        return BaseStorage.Write(offset, source);
    }

    public override Result Flush()
    {
        return BaseStorage.Flush();
    }

    public override Result GetSize(out long size)
    {
        size = Length;
        return Result.Success;
    }

    public override Result SetSize(long size)
    {
        Result res = BaseStorage.SetSize(size);
        if (res.IsFailure()) return res.Miss();

        res = BaseStorage.GetSize(out long newSize);
        if (res.IsFailure()) return res.Miss();

        SectorCount = (int)BitUtil.DivideUp(newSize, SectorSize);
        Length = newSize;

        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        if (!LeaveOpen)
        {
            BaseStorage?.Dispose();
        }
    }

    /// <summary>
    /// Validates that the size is a multiple of the sector size
    /// </summary>
    protected void ValidateSize(long size, long offset)
    {
        if (size < 0)
            throw new ArgumentException("Size must be non-negative");
        if (offset < 0)
            throw new ArgumentException("Offset must be non-negative");
        if (offset % SectorSize != 0)
            throw new ArgumentException($"Offset must be a multiple of {SectorSize}");
    }
}