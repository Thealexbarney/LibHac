using System;

namespace LibHac.Fs
{
    public class MemoryStorage : StorageBase
    {
        private byte[] StorageBuffer { get; }

        public MemoryStorage(byte[] buffer)
        {
            StorageBuffer = buffer;
        }

        protected override Result ReadImpl(long offset, Span<byte> destination)
        {
            if (destination.Length == 0)
                return Result.Success;

            if (!IsRangeValid(offset, destination.Length, StorageBuffer.Length))
                return ResultFs.ValueOutOfRange.Log();

            StorageBuffer.AsSpan((int)offset, destination.Length).CopyTo(destination);

            return Result.Success;
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source)
        {
            if (source.Length == 0)
                return Result.Success;

            if (!IsRangeValid(offset, source.Length, StorageBuffer.Length))
                return ResultFs.ValueOutOfRange.Log();

            source.CopyTo(StorageBuffer.AsSpan((int)offset));

            return Result.Success;
        }

        protected override Result FlushImpl()
        {
            return Result.Success;
        }

        protected override Result SetSizeImpl(long size)
        {
            return ResultFs.UnsupportedOperationInMemoryStorageSetSize.Log();
        }

        protected override Result GetSizeImpl(out long size)
        {
            size = StorageBuffer.Length;

            return Result.Success;
        }
    }
}
