using System;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    public class NullFile : IFile
    {
        private OpenMode Mode { get; }

        public NullFile()
        {
            Mode = OpenMode.ReadWrite;
        }

        public NullFile(long length) : this() => Length = length;

        private long Length { get; }

        protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination,
            in ReadOption option)
        {
            bytesRead = 0;

            Result rc = DryRead(out long toRead, offset, destination.Length, in option, Mode);
            if (rc.IsFailure()) return rc;

            destination.Slice(0, (int)toRead).Clear();

            bytesRead = toRead;
            return Result.Success;
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
        {
            return Result.Success;
        }

        protected override Result DoFlush()
        {
            return Result.Success;
        }

        protected override Result DoGetSize(out long size)
        {
            size = Length;
            return Result.Success;
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            return Result.Success;
        }

        protected override Result DoSetSize(long size)
        {
            return ResultFs.UnsupportedOperation.Log();
        }
    }
}
