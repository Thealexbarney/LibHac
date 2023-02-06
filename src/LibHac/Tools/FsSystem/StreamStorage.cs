using System;
using System.IO;
using LibHac.Fs;

namespace LibHac.Tools.FsSystem;

public class StreamStorage : IStorage
{
    // todo: handle Stream exceptions

    private Stream BaseStream { get; }
    private object Locker { get; } = new object();
    private long Length { get; }
    private bool LeaveOpen { get; }

    public StreamStorage(Stream baseStream, bool leaveOpen)
    {
        BaseStream = baseStream;
        Length = BaseStream.Length;
        LeaveOpen = leaveOpen;
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        lock (Locker)
        {
            if (BaseStream.Position != offset)
            {
                BaseStream.Position = offset;
            }

            int bytesRead = BaseStream.Read(destination);
            if (bytesRead != destination.Length)
                return ResultFs.OutOfRange.Log();
        }

        return Result.Success;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        lock (Locker)
        {
            if (BaseStream.Position != offset)
            {
                BaseStream.Position = offset;
            }

            BaseStream.Write(source);
        }

        return Result.Success;
    }

    public override Result Flush()
    {
        lock (Locker)
        {
            BaseStream.Flush();

            return Result.Success;
        }
    }

    public override Result SetSize(long size)
    {
        return ResultFs.NotImplemented.Log();
    }

    public override Result GetSize(out long size)
    {
        size = Length;
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
            BaseStream?.Dispose();
        }

        base.Dispose();
    }
}