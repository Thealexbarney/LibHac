using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Sf;
using IFile = LibHac.Fs.Fsa.IFile;
using IFileSf = LibHac.FsSrv.Sf.IFile;

namespace LibHac.Fs.Impl
{
    /// <summary>
    /// An adapter for using an <see cref="IFileSf"/> service object as an <see cref="IFile"/>. Used
    /// when receiving a Horizon IPC file object so it can be used as an <see cref="IFile"/> locally.
    /// </summary>
    internal class FileServiceObjectAdapter : IFile
    {
        private ReferenceCountedDisposable<IFileSf> BaseFile { get; }

        public FileServiceObjectAdapter(ReferenceCountedDisposable<IFileSf> baseFile)
        {
            BaseFile = baseFile.AddReference();
        }

        protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
        {
            return BaseFile.Target.Read(out bytesRead, offset, new OutBuffer(destination), destination.Length, option);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
        {
            return BaseFile.Target.Write(offset, new InBuffer(source), source.Length, option);
        }

        protected override Result DoFlush()
        {
            return BaseFile.Target.Flush();
        }

        protected override Result DoSetSize(long size)
        {
            return BaseFile.Target.SetSize(size);
        }

        protected override Result DoGetSize(out long size)
        {
            return BaseFile.Target.GetSize(out size);
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            switch (operationId)
            {
                case OperationId.InvalidateCache:
                    return BaseFile.Target.OperateRange(out _, (int)OperationId.InvalidateCache, offset, size);
                case OperationId.QueryRange:
                    if (outBuffer.Length != Unsafe.SizeOf<QueryRangeInfo>())
                        return ResultFs.InvalidSize.Log();

                    ref QueryRangeInfo info = ref SpanHelpers.AsStruct<QueryRangeInfo>(outBuffer);

                    return BaseFile.Target.OperateRange(out info, (int)OperationId.QueryRange, offset, size);
                default:
                    return ResultFs.UnsupportedOperateRangeForFileServiceObjectAdapter.Log();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BaseFile?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}