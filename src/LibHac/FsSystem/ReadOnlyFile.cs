using System;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class ReadOnlyFile : FileBase
    {
        private IFile BaseFile { get; }

        public ReadOnlyFile(IFile baseFile)
        {
            BaseFile = baseFile;
        }

        public override Result ReadImpl(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
            return BaseFile.Read(out bytesRead, offset, destination, options);
        }

        public override Result GetSizeImpl(out long size)
        {
            return BaseFile.GetSize(out size);
        }

        public override Result FlushImpl()
        {
            return Result.Success;
        }

        public override Result WriteImpl(long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
            return ResultFs.InvalidOpenModeForWrite.Log();
        }

        public override Result SetSizeImpl(long size)
        {
            return ResultFs.InvalidOpenModeForWrite.Log();
        }
    }
}
