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

        protected override void ReadImpl(Span<byte> destination, long offset)
        {
            BaseFile.Read(destination, offset);
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            BaseFile.Write(source, offset);
        }

        public override void Flush()
        {
            BaseFile.Flush();
        }

        public override long GetSize() => BaseFile.GetSize();

        public override void SetSize(long size)
        {
            BaseFile.SetSize(size);
        }
    }
}
