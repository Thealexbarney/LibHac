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

        protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, ReadOptionFlag options)
        {
            return BaseFile.Read(out bytesRead, offset, destination, options);
        }

        protected override Result DoGetSize(out long size)
        {
            return BaseFile.GetSize(out size);
        }

        protected override Result DoFlush()
        {
            return Result.Success;
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, WriteOptionFlag options)
        {
            return ResultFs.InvalidOpenModeForWrite.Log();
        }

        protected override Result DoSetSize(long size)
        {
            return ResultFs.InvalidOpenModeForWrite.Log();
        }
    }
}
