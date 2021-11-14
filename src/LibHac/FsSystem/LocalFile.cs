using System;
using System.IO;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem;

public class LocalFile : IFile
{
    private FileStream Stream { get; }
    private StreamFile File { get; }
    private OpenMode Mode { get; }

    public LocalFile(string path, OpenMode mode)
    {
        LocalFileSystem.OpenFileInternal(out FileStream stream, path, mode).ThrowIfFailure();

        Mode = mode;
        Stream = stream;
        File = new StreamFile(Stream, mode);
    }

    public LocalFile(FileStream stream, OpenMode mode)
    {
        Mode = mode;
        Stream = stream;
        File = new StreamFile(Stream, mode);
    }

    protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination,
        in ReadOption option)
    {
        bytesRead = 0;

        Result rc = DryRead(out long toRead, offset, destination.Length, in option, Mode);
        if (rc.IsFailure()) return rc;

        return File.Read(out bytesRead, offset, destination.Slice(0, (int)toRead), option);
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
    {
        Result rc = DryWrite(out _, offset, source.Length, in option, Mode);
        if (rc.IsFailure()) return rc;

        return File.Write(offset, source, option);
    }

    protected override Result DoFlush()
    {
        try
        {
            return File.Flush();
        }
        catch (Exception ex) when (ex.HResult < 0)
        {
            return ResultFs.UnexpectedInLocalFileSystemC.Log();
        }
    }

    protected override Result DoGetSize(out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        try
        {
            return File.GetSize(out size);
        }
        catch (Exception ex) when (ex.HResult < 0)
        {
            return ResultFs.UnexpectedInLocalFileSystemD.Log();
        }
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
    {
        return ResultFs.NotImplemented.Log();
    }

    protected override Result DoSetSize(long size)
    {
        try
        {
            File.SetSize(size);
        }
        catch (Exception ex) when (ex.HResult < 0)
        {
            return HResult.HResultToHorizonResult(ex.HResult).Log();
        }

        return Result.Success;
    }

    public override void Dispose()
    {
        File?.Dispose();
        Stream?.Dispose();

        base.Dispose();
    }
}
