using System;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    /// <summary>
    /// An <see cref="IStorage"/> that returns all zeros when read, and does nothing on write.
    /// </summary>
    public class NullStorage : IStorage
    {
        private long Length { get; }

        public NullStorage() { }
        public NullStorage(long length) => Length = length;


        protected override Result DoRead(long offset, Span<byte> destination)
        {
            destination.Clear();
            return Result.Success;
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            return Result.Success;
        }

        protected override Result DoFlush()
        {
            return Result.Success;
        }

        protected override Result DoSetSize(long size)
        {
            return ResultFs.NotImplemented.Log();
        }

        protected override Result DoGetSize(out long size)
        {
            size = Length;
            return Result.Success;
        }
    }
}
