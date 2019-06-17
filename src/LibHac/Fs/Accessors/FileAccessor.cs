using System;

namespace LibHac.Fs.Accessors
{
    public class FileAccessor : IFile
    {
        private IFile File { get; set; }

        public FileSystemAccessor Parent { get; }
        public WriteState WriteState { get; private set; }
        public OpenMode OpenMode { get; }

        // Todo: Consider removing Mode from interface because OpenMode is in FileAccessor
        OpenMode IFile.Mode => OpenMode;
        
        public FileAccessor(IFile baseFile, FileSystemAccessor parent, OpenMode mode)
        {
            File = baseFile;
            Parent = parent;
            OpenMode = mode;
        }

        public int Read(Span<byte> destination, long offset, ReadOption options)
        {
            CheckIfDisposed();

            return File.Read(destination, offset, options);
        }

        public void Write(ReadOnlySpan<byte> source, long offset, WriteOption options)
        {
            CheckIfDisposed();

            if (source.Length == 0)
            {
                WriteState = (WriteState)(~options & WriteOption.Flush);

                return;
            }

            File.Write(source, offset, options);

            WriteState = (WriteState)(~options & WriteOption.Flush);
        }

        public void Flush()
        {
            CheckIfDisposed();

            File.Flush();

            WriteState = WriteState.None;
        }

        public long GetSize()
        {
            CheckIfDisposed();

            return File.GetSize();
        }

        public void SetSize(long size)
        {
            CheckIfDisposed();

            File.SetSize(size);
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
