using System;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSrv.Impl
{
    /// <summary>
    /// An <see cref="IStorage"/> for simulating device failures
    /// </summary>
    /// <remarks>Based on FS 12.1.0 (nnSdk 12.3.1)</remarks>
    internal class DeviceEventSimulationStorage : IStorage
    {
        private SharedRef<IStorage> _baseStorage;
        private IDeviceEventSimulator _eventSimulator;

        public DeviceEventSimulationStorage(ref SharedRef<IStorage> baseStorage, IDeviceEventSimulator eventSimulator)
        {
            _baseStorage = SharedRef<IStorage>.CreateMove(ref baseStorage);
            _eventSimulator = eventSimulator;
        }

        public override void Dispose()
        {
            _baseStorage.Destroy();
            base.Dispose();
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            Result rc = _eventSimulator.CheckSimulatedAccessFailureEvent(SimulatingDeviceTargetOperation.Read);
            if (rc.IsFailure()) return rc;

            return _baseStorage.Get.Read(offset, destination);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            Result rc = _eventSimulator.CheckSimulatedAccessFailureEvent(SimulatingDeviceTargetOperation.Write);
            if (rc.IsFailure()) return rc;

            return _baseStorage.Get.Write(offset, source);
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
            return _baseStorage.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer);
        }
    }
}
