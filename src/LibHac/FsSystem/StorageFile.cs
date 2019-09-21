using System;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class StorageFile : FileBase
    {
        private IStorage BaseStorage { get; }

        public StorageFile(IStorage baseStorage, OpenMode mode)
        {
            BaseStorage = baseStorage;
            Mode = mode;
        }

        public override Result Read(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
            bytesRead = default;
            int toRead = ValidateReadParamsAndGetSize(destination, offset);

            Result rc = BaseStorage.Read(offset, destination.Slice(0, toRead));
            if (rc.IsFailure()) return rc;

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
                return Flush();
            }

            return Result.Success;
        }

        public override Result Flush()
        {
            return BaseStorage.Flush();
        }

        public override Result GetSize(out long size)
        {
            return BaseStorage.GetSize(out size);
        }

        public override Result SetSize(long size)
        {
            return BaseStorage.SetSize(size);
        }
    }
}
