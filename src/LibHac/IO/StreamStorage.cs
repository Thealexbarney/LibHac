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
            if (baseStream.CanRead && baseStream.CanWrite)
                Access = FileAccess.ReadWrite;
            else if (baseStream.CanRead)
                Access = FileAccess.Read;
            else if (baseStream.CanWrite)
                Access = FileAccess.Write;
            else
                Access = FileAccess.Read;
                
            if (!leaveOpen) ToDispose.Add(BaseStream);
        }

        public override void Read(byte[] buffer, long offset, int count, int bufferOffset)
        {
            lock (Locker)
            {
                BaseStream.Position = offset;
                BaseStream.Read(buffer, bufferOffset, count);
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

        protected override void ReadImpl(Span<byte> destination, long offset)
        {
#if STREAM_SPAN
            lock (Locker)
            {
                if (BaseStream.Position != offset)
                {
                    BaseStream.Position = offset;
                }

                BaseStream.Read(destination);
            }
#else
            byte[] buffer = ArrayPool<byte>.Shared.Rent(destination.Length);
            try
            {
                Read(buffer, offset, destination.Length, 0);

                new Span<byte>(buffer, 0, destination.Length).CopyTo(destination);
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
#endif
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
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
