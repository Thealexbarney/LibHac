using System;

namespace LibHac.Fs
{
    public class ReadOnlyFile : FileBase
    {
        private IFile BaseFile { get; }

        public ReadOnlyFile(IFile baseFile)
        {
            BaseFile = baseFile;
        }

        public override int Read(Span<byte> destination, long offset)
        {
            return BaseFile.Read(destination, offset);
        }

        public override long GetSize()
        {
            return BaseFile.GetSize();
        }

        public override void Flush() { }
        public override void Write(ReadOnlySpan<byte> source, long offset) => throw new NotSupportedException();
        public override void SetSize(long size) => throw new NotSupportedException();
    }
}
