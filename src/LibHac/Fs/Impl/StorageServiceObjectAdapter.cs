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
    internal class StorageServiceObjectAdapter : IStorage
    {
        private ReferenceCountedDisposable<IStorageSf> BaseStorage { get; }

        public StorageServiceObjectAdapter(ReferenceCountedDisposable<IStorageSf> baseStorage)
        {
            BaseStorage = baseStorage.AddReference();
        }

        protected StorageServiceObjectAdapter(ref ReferenceCountedDisposable<IStorageSf> baseStorage)
        {
            BaseStorage = Shared.Move(ref baseStorage);
        }

        public static ReferenceCountedDisposable<IStorage> CreateShared(
            ref ReferenceCountedDisposable<IStorageSf> baseStorage)
        {
            return new ReferenceCountedDisposable<IStorage>(new StorageServiceObjectAdapter(ref baseStorage));
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            return BaseStorage.Target.Read(offset, new OutBuffer(destination), destination.Length);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            return BaseStorage.Target.Write(offset, new InBuffer(source), source.Length);
        }

        protected override Result DoFlush()
        {
            return BaseStorage.Target.Flush();
        }

        protected override Result DoSetSize(long size)
        {
            return BaseStorage.Target.SetSize(size);
        }

        protected override Result DoGetSize(out long size)
        {
            return BaseStorage.Target.GetSize(out size);
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            switch (operationId)
            {
                case OperationId.InvalidateCache:
                    return BaseStorage.Target.OperateRange(out _, (int)OperationId.InvalidateCache, offset, size);
                case OperationId.QueryRange:
                    if (outBuffer.Length != Unsafe.SizeOf<QueryRangeInfo>())
                        return ResultFs.InvalidSize.Log();

                    ref QueryRangeInfo info = ref SpanHelpers.AsStruct<QueryRangeInfo>(outBuffer);

                    return BaseStorage.Target.OperateRange(out info, (int)OperationId.QueryRange, offset, size);
                default:
                    return ResultFs.UnsupportedOperateRangeForFileServiceObjectAdapter.Log();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BaseStorage?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}