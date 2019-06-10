using System;

namespace LibHac.Fs.Accessors
{
    public class FileAccessor
    {
        private IFile File { get; }

        public FileSystemAccessor Parent { get; }
        public WriteState WriteState { get; private set; }
        public OpenMode OpenMode { get; }

        public FileAccessor(IFile baseFile, FileSystemAccessor parent, OpenMode mode)
        {
            File = baseFile;
            Parent = parent;
            OpenMode = mode;
        }

        public int Read(Span<byte> destination, long offset, ReadOption options)
        {
            return File.Read(destination, offset, options);
        }

        public void Write(ReadOnlySpan<byte> source, long offset, WriteOption options)
        {
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
            File.Flush();

            WriteState = WriteState.None;
        }

        public long GetSize()
        {
            return File.GetSize();
        }

        public void SetSize(long size)
        {
            File.SetSize(size);
        }
    }

    public enum WriteState
    {
        None,
        Unflushed,
        Error
    }
}
