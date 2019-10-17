using System;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class FileStorage : StorageBase
    {
        protected IFile BaseFile { get; }

        public FileStorage(IFile baseFile)
        {
            BaseFile = baseFile;
        }

        protected override Result ReadImpl(long offset, Span<byte> destination)
        {
            return BaseFile.Read(out long _, offset, destination);
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source)
        {
            return BaseFile.Write(offset, source);
        }

        protected override Result FlushImpl()
        {
            return BaseFile.Flush();
        }

        protected override Result GetSizeImpl(out long size)
        {
            return BaseFile.GetSize(out size);
        }

        protected override Result SetSizeImpl(long size)
        {
            return BaseFile.SetSize(size);
        }
    }

    public class DisposingFileStorage : FileStorage
    {
        public DisposingFileStorage(IFile baseFile) : base(baseFile) { }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BaseFile?.Dispose();
            }
        }
    }
}
