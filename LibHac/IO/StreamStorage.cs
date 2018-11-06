using System;
using System.IO;

#if !STREAM_SPAN
using System.Buffers;
#endif

namespace LibHac.IO
{
    public class StreamStorage : Storage
    {
        private Stream BaseStream { get; }
        private object Locker { get; } = new object();
        public override long Length { get; }

        public StreamStorage(Stream baseStream, bool leaveOpen)
        {
            BaseStream = baseStream;
            Length = BaseStream.Length;
            if (!leaveOpen) ToDispose.Add(BaseStream);
        }

        public override int Read(byte[] buffer, long offset, int count, int bufferOffset)
        {
            lock (Locker)
            {
                BaseStream.Position = offset;
                return BaseStream.Read(buffer, bufferOffset, count);
            }
        }

        public override void Write(byte[] buffer, long offset, int count, int bufferOffset)
        {
            lock (Locker)
            {
                BaseStream.Position = offset;
                BaseStream.Write(buffer, bufferOffset, count);
            }
        }

        protected override int ReadSpan(Span<byte> destination, long offset)
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
                int numRead = Read(buffer, offset, destination.Length, 0);

                new Span<byte>(buffer, 0, numRead).CopyTo(destination);
                return numRead;
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
#endif
        }

        protected override void WriteSpan(ReadOnlySpan<byte> source, long offset)
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
                Write(buffer, offset, source.Length, 0);
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
    }
}
