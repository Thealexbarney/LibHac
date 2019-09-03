using System;

namespace LibHac.Fs.RomFs
{
    public class RomFsFile : FileBase
    {
        private IStorage BaseStorage { get; }
        private long Offset { get; }
        private long Size { get; }

        public RomFsFile(IStorage baseStorage, long offset, long size)
        {
            Mode = OpenMode.Read;
            BaseStorage = baseStorage;
            Offset = offset;
            Size = size;
        }

        public override Result Read(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
            bytesRead = default;

            int toRead = ValidateReadParamsAndGetSize(destination, offset);

            long storageOffset = Offset + offset;

            Result rc = BaseStorage.Read(storageOffset, destination.Slice(0, toRead));
            if (rc.IsFailure()) return rc;

            bytesRead = toRead;

            return Result.Success;
        }

        public override Result Write(long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
            return ResultFs.UnsupportedOperationModifyRomFsFile.Log();
        }

        public override Result Flush()
        {
            return Result.Success;
        }

        public override Result GetSize(out long size)
        {
            size = Size;
            return Result.Success;
        }

        public override Result SetSize(long size)
        {
            return ResultFs.UnsupportedOperationModifyRomFsFile.Log();
        }
    }
}
