using System;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    public class ReadOnlyFile : IFile
    {
        private IFile BaseFile { get; }

        public ReadOnlyFile(IFile baseFile)
        {
            BaseFile = baseFile;
        }

        protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination,
            in ReadOption option)
        {
            return BaseFile.Read(out bytesRead, offset, destination, option);
        }

        protected override Result DoGetSize(out long size)
        {
            return BaseFile.GetSize(out size);
        }

        protected override Result DoFlush()
        {
            return Result.Success;
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
        {
            return ResultFs.WriteUnpermitted.Log();
        }

        protected override Result DoSetSize(long size)
        {
            return ResultFs.WriteUnpermitted.Log();
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
        {
            switch (operationId)
            {
                case OperationId.InvalidateCache:
                case OperationId.QueryRange:
                    return BaseFile.OperateRange(outBuffer, operationId, offset, size, inBuffer);
                default:
                    return ResultFs.UnsupportedOperateRangeForReadOnlyFile.Log();
            }
        }
    }
}
