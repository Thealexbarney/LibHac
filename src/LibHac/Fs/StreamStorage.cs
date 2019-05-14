using System;
using System.IO;

#if !STREAM_SPAN
using System.Buffers;
#endif

namespace LibHac.Fs
{
    public class StreamStorage : StorageBase
    {
        private Stream BaseStream { get; }
        private object Locker { get; } = new object();
        private long _length;

        public StreamStorage(Stream baseStream, bool leaveOpen)
        {
            BaseStream = baseStream;
            _length = BaseStream.Length;
            if (!leaveOpen) ToDispose.Add(BaseStream);
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
                lock (Locker)
                {
                    if (BaseStream.Position != offset)
                    {
                        BaseStream.Position = offset;
                    }

                    BaseStream.Read(buffer, 0, destination.Length);
                }

                buffer.AsSpan(0, destination.Length).CopyTo(destination);
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
#endif
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
#if STREAM_SPAN
            lock (Locker)
            {
                if (BaseStream.Position != offset)
                {
                    BaseStream.Position = offset;
                }

                BaseStream.Write(source);
            }
#else
            byte[] buffer = ArrayPool<byte>.Shared.Rent(source.Length);
            try
            {
                source.CopyTo(buffer);

                lock (Locker)
                {
                    if (BaseStream.Position != offset)
                    {
                        BaseStream.Position = offset;
                    }

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

        public override long GetSize() => _length;
    }
}
