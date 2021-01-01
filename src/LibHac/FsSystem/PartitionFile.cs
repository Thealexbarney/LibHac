using System;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    public class PartitionFile : IFile
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

        protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination,
            in ReadOption option)
        {
            bytesRead = 0;

            Result rc = DryRead(out long toRead, offset, destination.Length, in option, Mode);
            if (rc.IsFailure()) return rc;

            long storageOffset = Offset + offset;
            BaseStorage.Read(storageOffset, destination.Slice(0, (int)toRead));

            bytesRead = toRead;
            return Result.Success;
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
        {
            Result rc = DryWrite(out bool isResizeNeeded, offset, source.Length, in option, Mode);
            if (rc.IsFailure()) return rc;

            if (isResizeNeeded) return ResultFs.UnsupportedWriteForPartitionFile.Log();

            if (offset > Size) return ResultFs.OutOfRange.Log();

            rc = BaseStorage.Write(offset, source);
            if (rc.IsFailure()) return rc;

            // N doesn't flush if the flag is set
            if (option.HasFlushFlag())
            {
                return BaseStorage.Flush();
            }

            return Result.Success;
        }

        protected override Result DoFlush()
        {
            if (!Mode.HasFlag(OpenMode.Write))
            {
                return BaseStorage.Flush();
            }

            return Result.Success;
        }

        protected override Result DoGetSize(out long size)
        {
            size = Size;
            return Result.Success;
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            return ResultFs.NotImplemented.Log();
        }

        protected override Result DoSetSize(long size)
        {
            if (!Mode.HasFlag(OpenMode.Write))
            {
                return ResultFs.WriteUnpermitted.Log();
            }

            return ResultFs.UnsupportedWriteForPartitionFile.Log();
        }
    }
}
