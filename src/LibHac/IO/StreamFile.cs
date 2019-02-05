using System;
using System.IO;

#if !STREAM_SPAN
using System.Buffers;
#endif

namespace LibHac.IO
{
    /// <summary>
    /// Provides an <see cref="IFile"/> interface for interacting with a <see cref="Stream"/>
    /// </summary>
    public class StreamFile : FileBase
    {
        private Stream BaseStream { get; }
        private object Locker { get; } = new object();

        public StreamFile(Stream baseStream, OpenMode mode)
        {
            BaseStream = baseStream;
            Mode = mode;
        }

        public override int Read(Span<byte> destination, long offset)
        {
#if STREAM_SPAN
            lock (Locker)
            {
                if (BaseStream.Position != offset)
                {
                    BaseStream.Position = offset;
                }

                return BaseStream.Read(destination);
            }
#else
            byte[] buffer = ArrayPool<byte>.Shared.Rent(destination.Length);
            try
            {
                int bytesRead;
                lock (Locker)
                {
                    if (BaseStream.Position != offset)
                    {
                        BaseStream.Position = offset;
                    }

                    bytesRead = BaseStream.Read(buffer, 0, destination.Length);
                }

                new Span<byte>(buffer, 0, destination.Length).CopyTo(destination);

                return bytesRead;
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
#endif
        }

        public override void Write(ReadOnlySpan<byte> source, long offset)
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
        }

        public override void Flush()
        {
            lock (Locker)
            {
                BaseStream.Flush();
            }
        }

        public override long GetSize()
        {
            lock (Locker)
            {
                return BaseStream.Length;
            }
        }

        public override void SetSize(long size)
        {
            lock (Locker)
            {
                BaseStream.SetLength(size);
            }
        }
    }
}
