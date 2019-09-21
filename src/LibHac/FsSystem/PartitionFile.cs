using System;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class PartitionFile : FileBase
    {
        private IStorage BaseStorage { get; }
        private long Offset { get; }
        private long Size { get; }

        public PartitionFile(IStorage baseStorage, long offset, long size, OpenMode mode)
        {
            Mode = mode;
            BaseStorage = baseStorage;
            Offset = offset;
            Size = size;
        }

        public override Result Read(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
            bytesRead = default;

            int toRead = ValidateReadParamsAndGetSize(destination, offset);

            long storageOffset = Offset + offset;
            BaseStorage.Read(storageOffset, destination.Slice(0, toRead));

            bytesRead = toRead;
            return Result.Success;
        }

        public override Result Write(long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
            ValidateWriteParams(source, offset);

            Result rc = BaseStorage.Write(offset, source);
            if (rc.IsFailure()) return rc;

            if ((options & WriteOption.Flush) != 0)
            {
                return BaseStorage.Flush();
            }

            return Result.Success;
        }

        public override Result Flush()
        {
            if ((Mode & OpenMode.Write) != 0)
            {
                return BaseStorage.Flush();
            }

            return Result.Success;
        }

        public override Result GetSize(out long size)
        {
            size = Size;
            return Result.Success;
        }

        public override Result SetSize(long size)
        {
            return ResultFs.UnsupportedOperationInPartitionFileSetSize.Log();
        }
    }
}
