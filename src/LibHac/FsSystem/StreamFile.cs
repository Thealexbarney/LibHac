using System;
using System.IO;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    /// <summary>
    /// Provides an <see cref="IFile"/> interface for interacting with a <see cref="Stream"/>
    /// </summary>
    public class StreamFile : IFile
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

        protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination,
            in ReadOption option)
        {
            UnsafeHelpers.SkipParamInit(out bytesRead);

            Result rc = DryRead(out long toRead, offset, destination.Length, in option, Mode);
            if (rc.IsFailure()) return rc;

            lock (Locker)
            {
                if (BaseStream.Position != offset)
                {
                    BaseStream.Position = offset;
                }

                bytesRead = BaseStream.Read(destination.Slice(0, (int)toRead));
                return Result.Success;
            }
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
        {
            Result rc = DryWrite(out _, offset, source.Length, in option, Mode);
            if (rc.IsFailure()) return rc;

            lock (Locker)
            {
                BaseStream.Position = offset;
                BaseStream.Write(source);
            }

            if (option.HasFlushFlag())
            {
                return Flush();
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

        protected override Result DoGetSize(out long size)
        {
            lock (Locker)
            {
                size = BaseStream.Length;
                return Result.Success;
            }
        }

        protected override Result DoSetSize(long size)
        {
            lock (Locker)
            {
                BaseStream.SetLength(size);
                return Result.Success;
            }
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            return ResultFs.NotImplemented.Log();
        }
    }
}
