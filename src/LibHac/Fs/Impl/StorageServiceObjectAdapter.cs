using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Sf;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.Fs.Impl
{
    /// <summary>
    /// An adapter for using an <see cref="IStorageSf"/> service object as an <see cref="IStorage"/>. Used
    /// when receiving a Horizon IPC storage object so it can be used as an <see cref="IStorage"/> locally.
    /// </summary>
    /// <remarks>Based on FS 12.1.0 (nnSdk 12.3.1)</remarks>
    internal class StorageServiceObjectAdapter : IStorage
    {
        private SharedRef<IStorageSf> _baseStorage;

        public StorageServiceObjectAdapter(ref SharedRef<IStorageSf> baseStorage)
        {
            _baseStorage = SharedRef<IStorageSf>.CreateMove(ref baseStorage);
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            return _baseStorage.Get.Read(offset, new OutBuffer(destination), destination.Length);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            return _baseStorage.Get.Write(offset, new InBuffer(source), source.Length);
        }

        protected override Result DoFlush()
        {
            return _baseStorage.Get.Flush();
        }

        protected override Result DoSetSize(long size)
        {
            return _baseStorage.Get.SetSize(size);
        }

        protected override Result DoGetSize(out long size)
        {
            return _baseStorage.Get.GetSize(out size);
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            switch (operationId)
            {
                case OperationId.InvalidateCache:
                    return _baseStorage.Get.OperateRange(out _, (int)OperationId.InvalidateCache, offset, size);
                case OperationId.QueryRange:
                    if (outBuffer.Length != Unsafe.SizeOf<QueryRangeInfo>())
                        return ResultFs.InvalidSize.Log();

                    ref QueryRangeInfo info = ref SpanHelpers.AsStruct<QueryRangeInfo>(outBuffer);

                    return _baseStorage.Get.OperateRange(out info, (int)OperationId.QueryRange, offset, size);
                default:
                    return ResultFs.UnsupportedOperateRangeForStorageServiceObjectAdapter.Log();
            }
        }

        public override void Dispose()
        {
            _baseStorage.Destroy();
            base.Dispose();
        }
    }
}