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

        public override int Read(Span<byte> destination, long offset, ReadOption options)
        {
            return BaseFile.Read(destination, offset, options);
        }

        public override long GetSize()
        {
            return BaseFile.GetSize();
        }

        public override void Flush() { }

        public override void Write(ReadOnlySpan<byte> source, long offset, WriteOption options) =>
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperationModifyReadOnlyFile);

        public override void SetSize(long size) =>
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperationModifyReadOnlyFile);
    }
}
