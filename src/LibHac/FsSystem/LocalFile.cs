using System;
using System.IO;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class LocalFile : FileBase
    {
        private FileStream Stream { get; }
        private StreamFile File { get; }
        private OpenMode Mode { get; }

        public LocalFile(string path, OpenMode mode)
        {
            LocalFileSystem.OpenFileInternal(out FileStream stream, path, mode).ThrowIfFailure();

            Mode = mode;
            Stream = stream;
            File = new StreamFile(Stream, mode);
        }

        public LocalFile(FileStream stream, OpenMode mode)
        {
            Mode = mode;
            Stream = stream;
            File = new StreamFile(Stream, mode);
        }

        protected override Result ReadImpl(out long bytesRead, long offset, Span<byte> destination, ReadOptionFlag options)
        {
            bytesRead = 0;

            Result rc = ValidateReadParams(out long toRead, offset, destination.Length, Mode);
            if (rc.IsFailure()) return rc;

            return File.Read(out bytesRead, offset, destination.Slice(0, (int)toRead), options);
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source, WriteOptionFlag options)
        {
            Result rc = ValidateWriteParams(offset, source.Length, Mode, out _);
            if (rc.IsFailure()) return rc;

            return File.Write(offset, source, options);
        }

        protected override Result FlushImpl()
        {
            try
            {
                return File.Flush();
            }
            catch (Exception ex) when (ex.HResult < 0)
            {
                return ResultFs.UnexpectedErrorInHostFileFlush.Log();
            }
        }

        protected override Result GetSizeImpl(out long size)
        {
            try
            {
                return File.GetSize(out size);
            }
            catch (Exception ex) when (ex.HResult < 0)
            {
                size = default;
                return ResultFs.UnexpectedErrorInHostFileGetSize.Log();
            }
        }

        protected override Result SetSizeImpl(long size)
        {
            try
            {
                File.SetSize(size);
            }
            catch (Exception ex) when (ex.HResult < 0)
            {
                return HResult.HResultToHorizonResult(ex.HResult).Log();
            }

            return Result.Success;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                File?.Dispose();
            }

            Stream?.Dispose();
        }
    }
}
