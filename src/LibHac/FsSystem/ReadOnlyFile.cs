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

        protected override Result ReadImpl(out long bytesRead, long offset, Span<byte> destination, ReadOptionFlag options)
        {
            return BaseFile.Read(out bytesRead, offset, destination, options);
        }

        protected override Result GetSizeImpl(out long size)
        {
            return BaseFile.GetSize(out size);
        }

        protected override Result FlushImpl()
        {
            return Result.Success;
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source, WriteOptionFlag options)
        {
            return ResultFs.InvalidOpenModeForWrite.Log();
        }

        protected override Result SetSizeImpl(long size)
        {
            return ResultFs.InvalidOpenModeForWrite.Log();
        }
    }
}
