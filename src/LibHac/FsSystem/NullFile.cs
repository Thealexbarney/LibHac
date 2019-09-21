using System;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class NullFile : FileBase
    {
        public NullFile()
        {
            Mode = OpenMode.ReadWrite;
        }

        public NullFile(long length) : this() => Length = length;

        private long Length { get; }

        public override Result Read(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
            int toRead = ValidateReadParamsAndGetSize(destination, offset);
            destination.Slice(0, toRead).Clear();

            bytesRead = toRead;
            return Result.Success;
        }

        public override Result Write(long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
            return Result.Success;
        }

        public override Result Flush()
        {
            return Result.Success;
        }

        public override Result GetSize(out long size)
        {
            size = Length;
            return Result.Success;
        }

        public override Result SetSize(long size)
        {
            return ResultFs.UnsupportedOperation.Log();
        }
    }
}
