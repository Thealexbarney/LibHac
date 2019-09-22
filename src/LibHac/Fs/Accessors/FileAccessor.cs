using System;

namespace LibHac.Fs.Accessors
{
    public class FileAccessor : IFile
    {
        private IFile File { get; set; }

        public FileSystemAccessor Parent { get; }
        public WriteState WriteState { get; private set; }
        public OpenMode OpenMode { get; }

        public FileAccessor(IFile baseFile, FileSystemAccessor parent, OpenMode mode)
        {
            File = baseFile;
            Parent = parent;
            OpenMode = mode;
        }

        public Result Read(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
            CheckIfDisposed();

            return File.Read(out bytesRead, offset, destination, options);
        }

        public Result Write(long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
            CheckIfDisposed();

            if (source.Length == 0)
            {
                WriteState = (WriteState)(~options & WriteOption.Flush);

                return Result.Success;
            }

            // 
            Result rc = File.Write(offset, source, options);

            if (rc.IsSuccess())
            {
                WriteState = (WriteState)(~options & WriteOption.Flush);
            }

            return rc;
        }

        public Result Flush()
        {
            CheckIfDisposed();

            Result rc = File.Flush();

            if (rc.IsSuccess())
            {
                WriteState = WriteState.None;
            }

            return rc;
        }

        public Result GetSize(out long size)
        {
            CheckIfDisposed();

            return File.GetSize(out size);
        }

        public Result SetSize(long size)
        {
            CheckIfDisposed();

            return File.SetSize(size);
        }

        public void Dispose()
        {
            if (File == null) return;

            if (WriteState == WriteState.Unflushed)
            {
                // Original FS code would return an error:
                // ThrowHelper.ThrowResult(ResultsFs.ResultFsWriteStateUnflushed);

                Flush();
            }

            File.Dispose();
            Parent?.NotifyCloseFile(this);

            File = null;
        }

        private void CheckIfDisposed()
        {
            if (File == null) throw new ObjectDisposedException(null, "Cannot access closed file.");
        }
    }

    public enum WriteState
    {
        None,
        Unflushed,
        Error
    }
}
