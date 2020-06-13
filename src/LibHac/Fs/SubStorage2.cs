using System;

namespace LibHac.Fs
{
    public class SubStorage2 : IStorage
    {
        private IStorage BaseStorage { get; }
        private long Offset { get; }
        private long Size { get; set; }
        public bool IsResizable { get; set; }

        public SubStorage2(IStorage baseStorage, long offset, long size)
        {
            BaseStorage = baseStorage;
            Offset = offset;
            Size = size;
        }

        public SubStorage2(SubStorage2 baseStorage, long offset, long size)
        {
            BaseStorage = baseStorage.BaseStorage;
            Offset = baseStorage.Offset + offset;
            Size = size;
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            if (BaseStorage == null) return ResultFs.SubStorageNotInitialized.Log();
            if (destination.Length == 0) return Result.Success;

            if (!IsRangeValid(offset, destination.Length, Size)) return ResultFs.OutOfRange.Log();

            return BaseStorage.Read(Offset + offset, destination);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            if (BaseStorage == null) return ResultFs.SubStorageNotInitialized.Log();
            if (source.Length == 0) return Result.Success;

            if (!IsRangeValid(offset, source.Length, Size)) return ResultFs.OutOfRange.Log();

            return BaseStorage.Write(Offset + offset, source);
        }

        protected override Result DoFlush()
        {
            if (BaseStorage == null) return ResultFs.SubStorageNotInitialized.Log();

            return BaseStorage.Flush();
        }

        protected override Result DoSetSize(long size)
        {
            if (BaseStorage == null) return ResultFs.SubStorageNotInitialized.Log();
            if (!IsResizable) return ResultFs.SubStorageNotResizable.Log();
            if (size < 0 || Offset < 0) return ResultFs.InvalidSize.Log();

            Result rc = BaseStorage.GetSize(out long baseSize);
            if (rc.IsFailure()) return rc;

            if (baseSize != Offset + Size)
            {
                // SubStorage cannot be resized unless it is located at the end of the base storage.
                return ResultFs.SubStorageNotResizableMiddleOfFile.Log();
            }

            rc = BaseStorage.SetSize(Offset + size);
            if (rc.IsFailure()) return rc;

            Size = size;
            return Result.Success;
        }

        protected override Result DoGetSize(out long size)
        {
            size = default;

            if (BaseStorage == null) return ResultFs.SubStorageNotInitialized.Log();

            size = Size;
            return Result.Success;
        }
    }
}
