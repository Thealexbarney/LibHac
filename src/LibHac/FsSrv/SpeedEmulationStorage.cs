using System;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSrv
{
    // Todo: Implement
    public class SpeedEmulationStorage : IStorage
    {
        private ReferenceCountedDisposable<IStorage> BaseStorage { get; }

        protected SpeedEmulationStorage(ref ReferenceCountedDisposable<IStorage> baseStorage)
        {
            BaseStorage = Shared.Move(ref baseStorage);
        }

        public static ReferenceCountedDisposable<IStorage> CreateShared(
            ref ReferenceCountedDisposable<IStorage> baseStorage)
        {
            return new ReferenceCountedDisposable<IStorage>(new SpeedEmulationStorage(ref baseStorage));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BaseStorage?.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            return BaseStorage.Target.Read(offset, destination);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            return BaseStorage.Target.Write(offset, source);
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
            return BaseStorage.Target.OperateRange(outBuffer, operationId, offset, size, inBuffer);
        }
    }
}
