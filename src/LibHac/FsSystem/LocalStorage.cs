using System;
using System.IO;
using LibHac.Fs;
using LibHac.Tools.FsSystem;

namespace LibHac.FsSystem;

public class LocalStorage : IStorage
{
    private string Path { get; }
    private FileStream Stream { get; }
    private StreamStorage Storage { get; }

    public LocalStorage(string path, FileAccess access) : this(path, access, FileMode.Open) { }

    public LocalStorage(string path, FileAccess access, FileMode mode)
    {
        Path = path;
        Stream = new FileStream(Path, mode, access);
        Storage = new StreamStorage(Stream, false);
    }

    public override void Dispose()
    {
        Storage?.Dispose();
        Stream?.Dispose();
        base.Dispose();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        return Storage.Read(offset, destination);
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        return Storage.Write(offset, source);
    }

    public override Result Flush()
    {
        return Storage.Flush();
    }

    public override Result SetSize(long size)
    {
        return ResultFs.NotImplemented.Log();
    }

    public override Result GetSize(out long size)
    {
        return Storage.GetSize(out size);
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }
}