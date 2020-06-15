using System;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    public class FileStorage : IStorage
    {
        protected IFile BaseFile { get; }

        public FileStorage(IFile baseFile)
        {
            BaseFile = baseFile;
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            return BaseFile.Read(out long _, offset, destination);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            return BaseFile.Write(offset, source);
        }

        protected override Result DoFlush()
        {
            return BaseFile.Flush();
        }

        protected override Result DoGetSize(out long size)
        {
            return BaseFile.GetSize(out size);
        }

        protected override Result DoSetSize(long size)
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
