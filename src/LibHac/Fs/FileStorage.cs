using System;

namespace LibHac.Fs
{
    public class FileStorage : StorageBase
    {
        private IFile BaseFile { get; }

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

        public override Result Flush()
        {
            return BaseFile.Flush();
        }

        public override Result GetSize(out long size)
        {
            return BaseFile.GetSize(out size);
        }

        public override Result SetSize(long size)
        {
            return BaseFile.SetSize(size);
        }
    }
}
