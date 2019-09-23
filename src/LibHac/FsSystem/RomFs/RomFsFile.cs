using System;
using LibHac.Fs;

namespace LibHac.FsSystem.RomFs
{
    public class RomFsFile : FileBase
    {
        private IStorage BaseStorage { get; }
        private long Offset { get; }
        private long Size { get; }

        public RomFsFile(IStorage baseStorage, long offset, long size)
        {
            BaseStorage = baseStorage;
            Offset = offset;
            Size = size;
        }

        public override Result ReadImpl(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
            bytesRead = default;

            Result rc = ValidateReadParams(out long toRead, offset, destination.Length, OpenMode.Read);
            if (rc.IsFailure()) return rc;

            long storageOffset = Offset + offset;

            rc = BaseStorage.Read(storageOffset, destination.Slice(0, (int)toRead));
            if (rc.IsFailure()) return rc;

            bytesRead = toRead;

            return Result.Success;
        }

        public override Result WriteImpl(long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
            return ResultFs.UnsupportedOperationModifyRomFsFile.Log();
        }

        public override Result FlushImpl()
        {
            return Result.Success;
        }

        public override Result GetSizeImpl(out long size)
        {
            size = Size;
            return Result.Success;
        }

        public override Result SetSizeImpl(long size)
        {
            return ResultFs.UnsupportedOperationModifyRomFsFile.Log();
        }
    }
}
