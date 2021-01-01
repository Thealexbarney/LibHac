using System;

namespace LibHac.Fs
{
    public class MemoryStorage : IStorage
    {
        private byte[] StorageBuffer { get; }

        public MemoryStorage(byte[] buffer)
        {
            StorageBuffer = buffer;
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            if (destination.Length == 0)
                return Result.Success;

            if (!IsRangeValid(offset, destination.Length, StorageBuffer.Length))
                return ResultFs.OutOfRange.Log();

            StorageBuffer.AsSpan((int)offset, destination.Length).CopyTo(destination);

            return Result.Success;
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            if (source.Length == 0)
                return Result.Success;

            if (!IsRangeValid(offset, source.Length, StorageBuffer.Length))
                return ResultFs.OutOfRange.Log();

            source.CopyTo(StorageBuffer.AsSpan((int)offset));

            return Result.Success;
        }

        protected override Result DoFlush()
        {
            return Result.Success;
        }

        protected override Result DoSetSize(long size)
        {
            return ResultFs.UnsupportedSetSizeForMemoryStorage.Log();
        }

        protected override Result DoGetSize(out long size)
        {
            size = StorageBuffer.Length;

            return Result.Success;
        }
    }
}
