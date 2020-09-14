using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.Sf;

namespace LibHac.FsSrv.Impl
{
    internal class FileInterfaceAdapter : IFileSf
    {
        private ReferenceCountedDisposable<FileSystemInterfaceAdapter> ParentFs { get; }
        private IFile BaseFile { get; }

        public FileInterfaceAdapter(IFile baseFile,
            ref ReferenceCountedDisposable<FileSystemInterfaceAdapter> parentFileSystem)
        {
            BaseFile = baseFile;
            ParentFs = parentFileSystem;
            parentFileSystem = null;
        }

        public Result Read(out long bytesRead, long offset, Span<byte> destination, ReadOption option)
        {
            const int maxTryCount = 2;
            bytesRead = default;

            if (offset < 0)
                return ResultFs.InvalidOffset.Log();

            if (destination.Length < 0)
                return ResultFs.InvalidSize.Log();

            Result rc = Result.Success;
            long tmpBytesRead = 0;

            for (int tryNum = 0; tryNum < maxTryCount; tryNum++)
            {
                rc = BaseFile.Read(out tmpBytesRead, offset, destination, option);

                // Retry on ResultDataCorrupted
                if (!ResultFs.DataCorrupted.Includes(rc))
                    break;
            }

            if (rc.IsFailure()) return rc;

            bytesRead = tmpBytesRead;
            return Result.Success;
        }

        public Result Write(long offset, ReadOnlySpan<byte> source, WriteOption option)
        {
            if (offset < 0)
                return ResultFs.InvalidOffset.Log();

            if (source.Length < 0)
                return ResultFs.InvalidSize.Log();

            // Note: Thread priority is temporarily when writing in FS

            return BaseFile.Write(offset, source, option);
        }

        public Result Flush()
        {
            return BaseFile.Flush();
        }

        public Result SetSize(long size)
        {
            if (size < 0)
                return ResultFs.InvalidSize.Log();

            return BaseFile.SetSize(size);
        }

        public Result GetSize(out long size)
        {
            const int maxTryCount = 2;
            size = default;

            Result rc = Result.Success;
            long tmpSize = 0;

            for (int tryNum = 0; tryNum < maxTryCount; tryNum++)
            {
                rc = BaseFile.GetSize(out tmpSize);

                // Retry on ResultDataCorrupted
                if (!ResultFs.DataCorrupted.Includes(rc))
                    break;
            }

            if (rc.IsFailure()) return rc;

            size = tmpSize;
            return Result.Success;
        }

        public Result OperateRange(out QueryRangeInfo rangeInfo, int operationId, long offset, long size)
        {
            rangeInfo = new QueryRangeInfo();

            if (operationId == (int)OperationId.InvalidateCache)
            {
                Result rc = BaseFile.OperateRange(Span<byte>.Empty, OperationId.InvalidateCache, offset, size,
                    ReadOnlySpan<byte>.Empty);
                if (rc.IsFailure()) return rc;
            }
            else if (operationId == (int)OperationId.QueryRange)
            {
                Unsafe.SkipInit(out QueryRangeInfo info);

                Result rc = BaseFile.OperateRange(SpanHelpers.AsByteSpan(ref info), OperationId.QueryRange, offset, size,
                    ReadOnlySpan<byte>.Empty);
                if (rc.IsFailure()) return rc;

                rangeInfo.Merge(in info);
            }

            return Result.Success;
        }

        public void Dispose()
        {
            BaseFile?.Dispose();
            ParentFs?.Dispose();
        }
    }
}
