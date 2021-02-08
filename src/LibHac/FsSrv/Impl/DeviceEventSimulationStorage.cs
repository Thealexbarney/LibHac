using System;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSrv.Impl
{
    internal class DeviceEventSimulationStorage : IStorage
    {
        private ReferenceCountedDisposable<IStorage> _baseStorage;
        private IDeviceEventSimulator _eventSimulator;

        private DeviceEventSimulationStorage(ref ReferenceCountedDisposable<IStorage> baseStorage,
            IDeviceEventSimulator eventSimulator)
        {
            _baseStorage = Shared.Move(ref baseStorage);
            _eventSimulator = eventSimulator;
        }

        public static ReferenceCountedDisposable<IStorage> CreateShared(
            ref ReferenceCountedDisposable<IStorage> baseStorage, IDeviceEventSimulator eventSimulator)
        {
            var storage = new DeviceEventSimulationStorage(ref baseStorage, eventSimulator);
            return new ReferenceCountedDisposable<IStorage>(storage);
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            Result rc = _eventSimulator.CheckSimulatedAccessFailureEvent(SimulatingDeviceTargetOperation.Read);
            if (rc.IsFailure()) return rc;

            return _baseStorage.Target.Read(offset, destination);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            Result rc = _eventSimulator.CheckSimulatedAccessFailureEvent(SimulatingDeviceTargetOperation.Write);
            if (rc.IsFailure()) return rc;

            return _baseStorage.Target.Write(offset, source);
        }

        protected override Result DoFlush()
        {
            return _baseStorage.Target.Flush();
        }

        protected override Result DoSetSize(long size)
        {
            return _baseStorage.Target.SetSize(size);
        }

        protected override Result DoGetSize(out long size)
        {
            return _baseStorage.Target.GetSize(out size);
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            return _baseStorage.Target.OperateRange(outBuffer, operationId, offset, size, inBuffer);
        }
    }
}
