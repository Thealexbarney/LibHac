using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;

namespace LibHac.FsSrv.Impl;

/// <summary>
/// An <see cref="IStorage"/> for simulating device failures
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
internal class DeviceEventSimulationStorage : IStorage
{
    private SharedRef<IStorage> _baseStorage;
    private IDeviceEventSimulator _deviceEventSimulator;

    public DeviceEventSimulationStorage(ref readonly SharedRef<IStorage> baseStorage, IDeviceEventSimulator deviceEventSimulator)
    {
        _baseStorage = SharedRef<IStorage>.CreateCopy(in baseStorage);
        _deviceEventSimulator = deviceEventSimulator;
    }

    public override void Dispose()
    {
        _baseStorage.Destroy();
        base.Dispose();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        Assert.SdkNotNull(_deviceEventSimulator);

        Result res = _deviceEventSimulator.CheckSimulatedAccessFailureEvent(SimulatingDeviceTargetOperation.Read);
        if (res.IsFailure()) return res.Miss();

        return _baseStorage.Get.Read(offset, destination);
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        Assert.SdkNotNull(_deviceEventSimulator);

        Result res = _deviceEventSimulator.CheckSimulatedAccessFailureEvent(SimulatingDeviceTargetOperation.Write);
        if (res.IsFailure()) return res.Miss();

        return _baseStorage.Get.Write(offset, source);
    }

    public override Result Flush()
    {
        return _baseStorage.Get.Flush();
    }

    public override Result SetSize(long size)
    {
        return _baseStorage.Get.SetSize(size);
    }

    public override Result GetSize(out long size)
    {
        return _baseStorage.Get.GetSize(out size);
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        return _baseStorage.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer);
    }
}