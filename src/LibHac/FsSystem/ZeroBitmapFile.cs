// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem;

public class ZeroBitmapFile : IFile
{
    private UniqueRef<IFile> _baseFile;
    private byte[] _bitmap;
    private long _blockSize;
    private long _bitmapSize;
    private OpenMode _mode;

    public ZeroBitmapFile()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    private bool IsFree(long offset)
    {
        throw new NotImplementedException();
    }

    private void Clear(long offset)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(ref UniqueRef<IFile> baseFile, byte[] bitmap, long bitmapSize, long blockSize,
        OpenMode mode)
    {
        throw new NotImplementedException();
    }

    protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoFlush()
    {
        throw new NotImplementedException();
    }

    protected override Result DoSetSize(long size)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetSize(out long size)
    {
        throw new NotImplementedException();
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }
}

public class ZeroBitmapHashStorageFile : ZeroBitmapFile
{
    private IntegrityVerificationStorage.BlockHash _hash;

    public ZeroBitmapHashStorageFile()
    {
        throw new NotImplementedException();
    }

    public Result Initialize(ref UniqueRef<IFile> baseFile, byte[] bitmap, long bitmapSize, long blockSize,
        OpenMode mode, in IntegrityVerificationStorage.BlockHash blockHash)
    {
        throw new NotImplementedException();
    }

    protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoSetSize(long size)
    {
        throw new NotImplementedException();
    }
}