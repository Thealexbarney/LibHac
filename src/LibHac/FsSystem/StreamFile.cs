using System;
using System.IO;

#if !STREAM_SPAN
using System.Buffers;
#endif

namespace LibHac.FsSystem
{
    /// <summary>
    /// Provides an <see cref="IFile"/> interface for interacting with a <see cref="Stream"/>
    /// </summary>
    public class StreamFile : FileBase
    {
        // todo: handle Stream exceptions

        private Stream BaseStream { get; }
        private object Locker { get; } = new object();

        public StreamFile(Stream baseStream, OpenMode mode)
        {
            BaseStream = baseStream;
            Mode = mode;
        }

        public override Result Read(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
#if STREAM_SPAN
            lock (Locker)
            {
                if (BaseStream.Position != offset)
                {
                    BaseStream.Position = offset;
                }

                bytesRead = BaseStream.Read(destination);
                return Result.Success;
            }
#else
            byte[] buffer = ArrayPool<byte>.Shared.Rent(destination.Length);
            try
            {
                lock (Locker)
                {
                    if (BaseStream.Position != offset)
                    {
                        BaseStream.Position = offset;
                    }

                    bytesRead = BaseStream.Read(buffer, 0, destination.Length);
                }

                new Span<byte>(buffer, 0, destination.Length).CopyTo(destination);

                return Result.Success;
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
#endif
        }

        public override Result Write(long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
#if STREAM_SPAN
            lock (Locker)
            {
                BaseStream.Position = offset;
                BaseStream.Write(source);
            }
#else
            byte[] buffer = ArrayPool<byte>.Shared.Rent(source.Length);
            try
            {
                source.CopyTo(buffer);

                lock (Locker)
                {
                    BaseStream.Position = offset;
                    BaseStream.Write(buffer, 0, source.Length);
                }
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
#endif

            if ((options & WriteOption.Flush) != 0)
            {
                return Flush();
            }

            return Result.Success;
        }

        public override Result Flush()
        {
            lock (Locker)
            {
                BaseStream.Flush();
                return Result.Success;
            }
        }

        public override Result GetSize(out long size)
        {
            lock (Locker)
            {
                size = BaseStream.Length;
                return Result.Success;
            }
        }

        public override Result SetSize(long size)
        {
            lock (Locker)
            {
                BaseStream.SetLength(size);
                return Result.Success;
            }
        }
    }
}
