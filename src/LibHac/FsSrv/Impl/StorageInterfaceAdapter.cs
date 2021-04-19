using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Sf;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.FsSrv.Impl
{
    public class StorageInterfaceAdapter : IStorageSf
    {
        private ReferenceCountedDisposable<IStorage> BaseStorage { get; }

        private StorageInterfaceAdapter(ref ReferenceCountedDisposable<IStorage> baseStorage)
        {
            BaseStorage = Shared.Move(ref baseStorage);
        }

        public static ReferenceCountedDisposable<IStorageSf> CreateShared(
            ref ReferenceCountedDisposable<IStorage> baseStorage)
        {
            var adapter = new StorageInterfaceAdapter(ref baseStorage);
            return new ReferenceCountedDisposable<IStorageSf>(adapter);
        }

        public void Dispose()
        {
            BaseStorage?.Dispose();
        }

        public Result Read(long offset, OutBuffer destination, long size)
        {
            const int maxTryCount = 2;

            if (offset < 0)
                return ResultFs.InvalidOffset.Log();

            if (destination.Size < 0)
                return ResultFs.InvalidSize.Log();

            Result rc = Result.Success;

            for (int tryNum = 0; tryNum < maxTryCount; tryNum++)
            {
                rc = BaseStorage.Target.Read(offset, destination.Buffer.Slice(0, (int)size));

                // Retry on ResultDataCorrupted
                if (!ResultFs.DataCorrupted.Includes(rc))
                    break;
            }

            return rc;
        }

        public Result Write(long offset, InBuffer source, long size)
        {
            if (offset < 0)
                return ResultFs.InvalidOffset.Log();

            if (source.Size < 0)
                return ResultFs.InvalidSize.Log();

            // Note: Thread priority is temporarily increased when writing in FS

            return BaseStorage.Target.Write(offset, source.Buffer.Slice(0, (int)size));
        }

        public Result Flush()
        {
            return BaseStorage.Target.Flush();
        }

        public Result SetSize(long size)
        {
            if (size < 0)
                return ResultFs.InvalidSize.Log();

            return BaseStorage.Target.SetSize(size);
        }

        public Result GetSize(out long size)
        {
            return BaseStorage.Target.GetSize(out size);
        }

        public Result OperateRange(out QueryRangeInfo rangeInfo, int operationId, long offset, long size)
        {
            UnsafeHelpers.SkipParamInit(out rangeInfo);
            rangeInfo.Clear();

            if (operationId == (int)OperationId.InvalidateCache)
            {
                Result rc = BaseStorage.Target.OperateRange(Span<byte>.Empty, OperationId.InvalidateCache, offset, size,
                    ReadOnlySpan<byte>.Empty);
                if (rc.IsFailure()) return rc;
            }
            else if (operationId == (int)OperationId.QueryRange)
            {
                Unsafe.SkipInit(out QueryRangeInfo info);

                Result rc = BaseStorage.Target.OperateRange(SpanHelpers.AsByteSpan(ref info), OperationId.QueryRange,
                    offset, size, ReadOnlySpan<byte>.Empty);
                if (rc.IsFailure()) return rc;

                rangeInfo.Merge(in info);
            }

            return Result.Success;
        }
    }
}