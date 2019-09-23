using System;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class PartitionFile : FileBase
    {
        private IStorage BaseStorage { get; }
        private long Offset { get; }
        private long Size { get; }
        private OpenMode Mode { get; }

        public PartitionFile(IStorage baseStorage, long offset, long size, OpenMode mode)
        {
            Mode = mode;
            BaseStorage = baseStorage;
            Offset = offset;
            Size = size;
        }

        public override Result ReadImpl(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
            bytesRead = 0;

            Result rc = ValidateReadParams(out long toRead, offset, destination.Length, Mode);
            if (rc.IsFailure()) return rc;

            long storageOffset = Offset + offset;
            BaseStorage.Read(storageOffset, destination.Slice(0, (int)toRead));

            bytesRead = toRead;
            return Result.Success;
        }

        public override Result WriteImpl(long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
            Result rc = ValidateWriteParams(offset, source.Length, Mode, out bool isResizeNeeded);
            if (rc.IsFailure()) return rc;

            if (isResizeNeeded) return ResultFs.UnsupportedOperationInPartitionFileSetSize.Log();

            if (offset > Size) return ResultFs.ValueOutOfRange.Log();

            rc = BaseStorage.Write(offset, source);
            if (rc.IsFailure()) return rc;

            // N doesn't flush if the flag is set
            if (options.HasFlag(WriteOption.Flush))
            {
                return BaseStorage.Flush();
            }

            return Result.Success;
        }

        public override Result FlushImpl()
        {
            if (!Mode.HasFlag(OpenMode.Write))
            {
                return BaseStorage.Flush();
            }

            return Result.Success;
        }

        public override Result GetSizeImpl(out long size)
        {
            size = Size;
            return Result.Success;
        }

        public override Result SetSizeImpl(long size)
        {
            if (!Mode.HasFlag(OpenMode.Write))
            {
                return ResultFs.InvalidOpenModeForWrite.Log();
            }

            return ResultFs.UnsupportedOperationInPartitionFileSetSize.Log();
        }
    }
}
