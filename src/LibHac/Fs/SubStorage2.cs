using System;

namespace LibHac.Fs
{
    public class SubStorage2 : StorageBase
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

        public override Result Read(long offset, Span<byte> destination)
        {
            if (BaseStorage == null) return ResultFs.Result6902.Log();
            if (destination.Length == 0) return Result.Success;
            if (Size < 0 || offset < 0) return ResultFs.ValueOutOfRange.Log();
            if (!IsRangeValid(offset, destination.Length, Size)) return ResultFs.ValueOutOfRange.Log();

            return BaseStorage.Read(Offset + offset, destination);
        }

        public override Result Write(long offset, ReadOnlySpan<byte> source)
        {
            if (BaseStorage == null) return ResultFs.Result6902.Log();
            if (source.Length == 0) return Result.Success;
            if (Size < 0 || offset < 0) return ResultFs.ValueOutOfRange.Log();
            if (!IsRangeValid(offset, source.Length, Size)) return ResultFs.ValueOutOfRange.Log();

            return BaseStorage.Write(Offset + offset, source);
        }

        public override Result Flush()
        {
            if (BaseStorage == null) return ResultFs.Result6902.Log();

            return BaseStorage.Flush();
        }

        public override Result SetSize(long size)
        {
            if (BaseStorage == null) return ResultFs.Result6902.Log();
            if (!IsResizable) return ResultFs.SubStorageNotResizable.Log();
            if (size < 0 || Offset < 0) return ResultFs.ValueOutOfRange.Log();

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

        public override Result GetSize(out long size)
        {
            size = default;

            if (BaseStorage == null) return ResultFs.Result6902.Log();

            size = Size;
            return Result.Success;
        }
    }
}
