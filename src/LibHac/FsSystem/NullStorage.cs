using System;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    /// <summary>
    /// An <see cref="IStorage"/> that returns all zeros when read, and does nothing on write.
    /// </summary>
    public class NullStorage : StorageBase
    {
        private long Length { get; }

        public NullStorage() { }
        public NullStorage(long length) => Length = length;


        protected override Result ReadImpl(long offset, Span<byte> destination)
        {
            destination.Clear();
            return Result.Success;
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source)
        {
            return Result.Success;
        }

        protected override Result FlushImpl()
        {
            return Result.Success;
        }

        protected override Result SetSizeImpl(long size)
        {
            return ResultFs.NotImplemented.Log();
        }

        protected override Result GetSizeImpl(out long size)
        {
            size = Length;
            return Result.Success;
        }
    }
}
