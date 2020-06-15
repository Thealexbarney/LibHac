using System;
using System.IO;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class StreamStorage : IStorage
    {
        // todo: handle Stream exceptions

        private Stream BaseStream { get; }
        private object Locker { get; } = new object();
        private long Length { get; }
        private bool LeaveOpen { get; }

        public StreamStorage(Stream baseStream, bool leaveOpen)
        {
            BaseStream = baseStream;
            Length = BaseStream.Length;
            LeaveOpen = leaveOpen;
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            lock (Locker)
            {
                if (BaseStream.Position != offset)
                {
                    BaseStream.Position = offset;
                }

                BaseStream.Read(destination);
            }

            return Result.Success;
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            lock (Locker)
            {
                if (BaseStream.Position != offset)
                {
                    BaseStream.Position = offset;
                }

                BaseStream.Write(source);
            }

            return Result.Success;
        }

        protected override Result DoFlush()
        {
            lock (Locker)
            {
                BaseStream.Flush();

                return Result.Success;
            }
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!LeaveOpen)
                {
                    BaseStream?.Dispose();
                }
            }
        }
    }
}
