using System;

namespace LibHac.FsSystem
{
    public class ReadOnlyFile : FileBase
    {
        private IFile BaseFile { get; }

        public ReadOnlyFile(IFile baseFile)
        {
            BaseFile = baseFile;
        }

        public override Result Read(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
            return BaseFile.Read(out bytesRead, offset, destination, options);
        }

        public override Result GetSize(out long size)
        {
            return BaseFile.GetSize(out size);
        }

        public override Result Flush()
        {
            return Result.Success;
        }

        public override Result Write(long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
            return ResultFs.UnsupportedOperationModifyReadOnlyFile.Log();
        }

        public override Result SetSize(long size)
        {
            return ResultFs.UnsupportedOperationModifyReadOnlyFile.Log();
        }
    }
}
