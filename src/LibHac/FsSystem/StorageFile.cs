using System;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class StorageFile : FileBase
    {
        private IStorage BaseStorage { get; }
        private OpenMode Mode { get; }

        public StorageFile(IStorage baseStorage, OpenMode mode)
        {
            BaseStorage = baseStorage;
            Mode = mode;
        }

        protected override Result ReadImpl(out long bytesRead, long offset, Span<byte> destination, ReadOptionFlag options)
        {
            bytesRead = default;

            Result rc = ValidateReadParams(out long toRead, offset, destination.Length, Mode);
            if (rc.IsFailure()) return rc;

            if (toRead == 0)
            {
                bytesRead = 0;
                return Result.Success;
            }

            rc = BaseStorage.Read(offset, destination.Slice(0, (int)toRead));
            if (rc.IsFailure()) return rc;

            bytesRead = toRead;
            return Result.Success;
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source, WriteOptionFlag options)
        {
            Result rc = ValidateWriteParams(offset, source.Length, Mode, out bool isResizeNeeded);
            if (rc.IsFailure()) return rc;

            if (isResizeNeeded)
            {
                rc = SetSizeImpl(offset + source.Length);
                if (rc.IsFailure()) return rc;
            }

            rc = BaseStorage.Write(offset, source);
            if (rc.IsFailure()) return rc;

            if (options.HasFlag(WriteOptionFlag.Flush))
            {
                return Flush();
            }

            return Result.Success;
        }

        protected override Result FlushImpl()
        {
            if (!Mode.HasFlag(OpenMode.Write))
                return Result.Success;

            return BaseStorage.Flush();
        }

        protected override Result GetSizeImpl(out long size)
        {
            return BaseStorage.GetSize(out size);
        }

        protected override Result SetSizeImpl(long size)
        {
            if (!Mode.HasFlag(OpenMode.Write))
                return ResultFs.InvalidOpenModeForWrite.Log();

            return BaseStorage.SetSize(size);
        }
    }
}
