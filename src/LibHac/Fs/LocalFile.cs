using System;
using System.IO;

namespace LibHac.Fs
{
    public class LocalFile : FileBase
    {
        private const int ErrorHandleDiskFull = unchecked((int)0x80070027);
        private const int ErrorDiskFull = unchecked((int)0x80070070);

        private FileStream Stream { get; }
        private StreamFile File { get; }

        public LocalFile(string path, OpenMode mode)
        {
            Mode = mode;
            Stream = OpenFile(path, mode);
            File = new StreamFile(Stream, mode);

            ToDispose.Add(File);
            ToDispose.Add(Stream);
        }

        public override int Read(Span<byte> destination, long offset, ReadOption options)
        {
            int toRead = ValidateReadParamsAndGetSize(destination, offset);

            File.Read(destination.Slice(0, toRead), offset, options);

            return toRead;
        }

        public override void Write(ReadOnlySpan<byte> source, long offset, WriteOption options)
        {
            ValidateWriteParams(source, offset);
            
            File.Write(source, offset, options);
        }

        public override void Flush()
        {
            File.Flush();
        }

        public override long GetSize()
        {
            return File.GetSize();
        }

        public override void SetSize(long size)
        {
            try
            {
                File.SetSize(size);
            }
            catch (IOException ex) when (ex.HResult == ErrorDiskFull || ex.HResult == ErrorHandleDiskFull)
            {
                ThrowHelper.ThrowResult(ResultFs.InsufficientFreeSpace, ex);
                throw;
            }
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
