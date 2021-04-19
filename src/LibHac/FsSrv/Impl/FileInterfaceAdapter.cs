using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Sf;
using IFile = LibHac.Fs.Fsa.IFile;
using IFileSf = LibHac.FsSrv.Sf.IFile;

namespace LibHac.FsSrv.Impl
{
    public class FileInterfaceAdapter : IFileSf
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

        public Result Read(out long bytesRead, long offset, OutBuffer destination, long size, ReadOption option)
        {
            const int maxTryCount = 2;
            UnsafeHelpers.SkipParamInit(out bytesRead);

            if (offset < 0)
                return ResultFs.InvalidOffset.Log();

            if (destination.Size < 0)
                return ResultFs.InvalidSize.Log();

            Result rc = Result.Success;
            long tmpBytesRead = 0;

            for (int tryNum = 0; tryNum < maxTryCount; tryNum++)
            {
                rc = BaseFile.Read(out tmpBytesRead, offset, destination.Buffer.Slice(0, (int)size), option);

                // Retry on ResultDataCorrupted
                if (!ResultFs.DataCorrupted.Includes(rc))
                    break;
            }

            if (rc.IsFailure()) return rc;

            bytesRead = tmpBytesRead;
            return Result.Success;
        }

        public Result Write(long offset, InBuffer source, long size, WriteOption option)
        {
            if (offset < 0)
                return ResultFs.InvalidOffset.Log();

            if (source.Size < 0)
                return ResultFs.InvalidSize.Log();

            // Note: Thread priority is temporarily increased when writing in FS

            return BaseFile.Write(offset, source.Buffer.Slice(0, (int)size), option);
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
            UnsafeHelpers.SkipParamInit(out size);

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
            UnsafeHelpers.SkipParamInit(out rangeInfo);
            rangeInfo.Clear();

            if (operationId == (int)OperationId.InvalidateCache)
            {
                Result rc = BaseFile.OperateRange(Span<byte>.Empty, OperationId.InvalidateCache, offset, size,
                    ReadOnlySpan<byte>.Empty);
                if (rc.IsFailure()) return rc;
            }
            else if (operationId == (int)OperationId.QueryRange)
            {
                Unsafe.SkipInit(out QueryRangeInfo info);

                Result rc = BaseFile.OperateRange(SpanHelpers.AsByteSpan(ref info), OperationId.QueryRange, offset,
                    size, ReadOnlySpan<byte>.Empty);
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