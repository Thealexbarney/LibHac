using System;
using System.IO;
using LibHac.Fs;

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

        private OpenMode Mode { get; }
        private Stream BaseStream { get; }
        private object Locker { get; } = new object();

        public StreamFile(Stream baseStream, OpenMode mode)
        {
            BaseStream = baseStream;
            Mode = mode;
        }

        protected override Result ReadImpl(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
            bytesRead = default;

            Result rc = ValidateReadParams(out long toRead, offset, destination.Length, Mode);
            if (rc.IsFailure()) return rc;

#if STREAM_SPAN
            lock (Locker)
            {
                if (BaseStream.Position != offset)
                {
                    BaseStream.Position = offset;
                }

                bytesRead = BaseStream.Read(destination.Slice(0, (int)toRead));
                return Result.Success;
            }
#else
            byte[] buffer = ArrayPool<byte>.Shared.Rent((int)toRead);
            try
            {
                lock (Locker)
                {
                    if (BaseStream.Position != offset)
                    {
                        BaseStream.Position = offset;
                    }

                    bytesRead = BaseStream.Read(buffer, 0, (int)toRead);
                }

                new Span<byte>(buffer, 0, (int)bytesRead).CopyTo(destination);

                return Result.Success;
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
#endif
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
            Result rc = ValidateWriteParams(offset, source.Length, Mode, out _);
            if (rc.IsFailure()) return rc;

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

            if (options.HasFlag(WriteOption.Flush))
            {
                return Flush();
            }

            return Result.Success;
        }

        protected override Result FlushImpl()
        {
            lock (Locker)
            {
                BaseStream.Flush();
                return Result.Success;
            }
        }

        protected override Result GetSizeImpl(out long size)
        {
            lock (Locker)
            {
                size = BaseStream.Length;
                return Result.Success;
            }
        }

        protected override Result SetSizeImpl(long size)
        {
            lock (Locker)
            {
                BaseStream.SetLength(size);
                return Result.Success;
            }
        }
    }
}
