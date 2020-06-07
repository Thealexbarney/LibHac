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

        protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination,
            in ReadOption option)
        {
            CheckIfDisposed();

            return File.Read(out bytesRead, offset, destination, in option);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
        {
            CheckIfDisposed();

            if (source.Length == 0)
            {
                WriteState = (WriteState)(~option.Flags & WriteOptionFlag.Flush);

                return Result.Success;
            }

            Result rc = File.Write(offset, source, in option);

            if (rc.IsSuccess())
            {
                WriteState = (WriteState)(~option.Flags & WriteOptionFlag.Flush);
            }

            return rc;
        }

        protected override Result DoFlush()
        {
            CheckIfDisposed();

            Result rc = File.Flush();

            if (rc.IsSuccess())
            {
                WriteState = WriteState.None;
            }

            return rc;
        }

        protected override Result DoGetSize(out long size)
        {
            CheckIfDisposed();

            return File.GetSize(out size);
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
        {
            return ResultFs.NotImplemented.Log();
        }

        protected override Result DoSetSize(long size)
        {
            CheckIfDisposed();

            return File.SetSize(size);
        }

        protected override void Dispose(bool disposing)
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
