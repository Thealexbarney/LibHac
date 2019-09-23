using System;
using System.IO;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class LocalFile : FileBase
    {
        private const int ErrorHandleDiskFull = unchecked((int)0x80070027);
        private const int ErrorDiskFull = unchecked((int)0x80070070);

        private FileStream Stream { get; }
        private StreamFile File { get; }
        private OpenMode Mode { get; }

        public LocalFile(string path, OpenMode mode)
        {
            Mode = mode;
            Stream = OpenFile(path, mode);
            File = new StreamFile(Stream, mode);
        }

        public override Result ReadImpl(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
            bytesRead = 0;

            Result rc = ValidateReadParams(out long toRead, offset, destination.Length, Mode);
            if (rc.IsFailure()) return rc;

            return File.Read(out bytesRead, offset, destination.Slice(0, (int)toRead), options);
        }

        public override Result WriteImpl(long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
            Result rc = ValidateWriteParams(offset, source.Length, Mode, out _);
            if (rc.IsFailure()) return rc;

            return File.Write(offset, source, options);
        }

        public override Result FlushImpl()
        {
            return File.Flush();
        }

        public override Result GetSizeImpl(out long size)
        {
            return File.GetSize(out size);
        }

        public override Result SetSizeImpl(long size)
        {
            try
            {
                File.SetSize(size);
            }
            catch (IOException ex) when (ex.HResult == ErrorDiskFull || ex.HResult == ErrorHandleDiskFull)
            {
                return ResultFs.InsufficientFreeSpace.Log();
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

        private static FileAccess GetFileAccess(OpenMode mode)
        {
            // FileAccess and OpenMode have the same flags
            return (FileAccess)(mode & OpenMode.ReadWrite);
        }

        private static FileShare GetFileShare(OpenMode mode)
        {
            return mode.HasFlag(OpenMode.Write) ? FileShare.Read : FileShare.ReadWrite;
        }

        private static FileStream OpenFile(string path, OpenMode mode)
        {
            try
            {
                return new FileStream(path, FileMode.Open, GetFileAccess(mode), GetFileShare(mode));
            }
            catch (Exception ex) when (ex is ArgumentNullException || ex is ArgumentException ||
                                       ex is PathTooLongException || ex is DirectoryNotFoundException ||
                                       ex is FileNotFoundException || ex is NotSupportedException)
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound, ex);
                throw;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // todo: Should a HorizonResultException be thrown?
                throw;
            }
        }
    }
}
