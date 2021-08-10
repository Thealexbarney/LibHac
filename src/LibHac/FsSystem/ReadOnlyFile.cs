using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    public class ReadOnlyFile : IFile
    {
        private UniqueRef<IFile> _baseFile;

        public ReadOnlyFile(ref UniqueRef<IFile> baseFile)
        {
            _baseFile = new UniqueRef<IFile>(ref baseFile);
        }

        protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination,
            in ReadOption option)
        {
            return _baseFile.Get.Read(out bytesRead, offset, destination, option);
        }

        protected override Result DoGetSize(out long size)
        {
            return _baseFile.Get.GetSize(out size);
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
                    return _baseFile.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer);
                default:
                    return ResultFs.UnsupportedOperateRangeForReadOnlyFile.Log();
            }
        }
    }
}
