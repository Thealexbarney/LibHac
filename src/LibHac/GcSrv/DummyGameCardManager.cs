using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Sf;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Sf;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.GcSrv;

public class DummyGameCardManager : IStorageDeviceManager, IStorageDeviceOperator
{
    public DummyGameCardManager()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result IsInserted(out bool isInserted)
    {
        isInserted = false;
        return Result.Success;
    }

    public Result IsHandleValid(out bool isValid, uint handle)
    {
        isValid = false;
        return Result.Success;
    }

    public Result OpenDetectionEvent(ref SharedRef<IEventNotifier> outDetectionEvent)
    {
        throw new NotImplementedException();
    }

    public Result OpenOperator(ref SharedRef<IStorageDeviceOperator> outDeviceOperator)
    {
        throw new NotImplementedException();
    }

    public Result OpenDevice(ref SharedRef<IStorageDevice> outStorageDevice, ulong attribute)
    {
        return ResultFs.GameCardCardNotInserted.Log();
    }

    public Result OpenStorage(ref SharedRef<IStorageSf> outStorage, ulong attribute)
    {
        return ResultFs.GameCardCardNotInserted.Log();
    }

    public Result Invalidate()
    {
        return Result.Success;
    }

    public Result Operate(int operationId)
    {
        var operation = (GameCardManagerOperationIdValue)operationId;

        switch (operation)
        {
            case GameCardManagerOperationIdValue.Finalize:
                return Result.Success;

            case GameCardManagerOperationIdValue.GetInitializationResult:
                return Result.Success;

            case GameCardManagerOperationIdValue.ForceErase:
                return ResultFs.GameCardCardNotInserted.Log();

            case GameCardManagerOperationIdValue.SimulateDetectionEventSignaled:
                return ResultFs.GameCardNotSupportedOnDeviceModel.Log();

            default:
                return ResultFs.InvalidArgument.Log();
        }
    }

    public Result OperateIn(InBuffer buffer, long offset, long size, int operationId)
    {
        var operation = (GameCardManagerOperationIdValue)operationId;

        switch (operation)
        {
            case GameCardManagerOperationIdValue.SetVerifyEnableFlag:
                return Result.Success;

            case GameCardManagerOperationIdValue.EraseAndWriteParamDirectly:
                return ResultFs.GameCardCardNotInserted.Log();

            default:
                return ResultFs.InvalidArgument.Log();
        }
    }

    public Result OperateOut(out long bytesWritten, OutBuffer buffer, int operationId)
    {
        var operation = (GameCardManagerOperationIdValue)operationId;
        bytesWritten = 0;

        switch (operation)
        {
            case GameCardManagerOperationIdValue.GetHandle:
            {
                return ResultFs.GameCardCardNotInserted.Log();
            }
            case GameCardManagerOperationIdValue.GetGameCardErrorInfo:
            {
                if (buffer.Size < Unsafe.SizeOf<GameCardErrorInfo>())
                    return ResultFs.InvalidArgument.Log();

                buffer.As<GameCardErrorInfo>() = default;
                bytesWritten = Unsafe.SizeOf<GameCardErrorInfo>();

                return Result.Success;
            }
            case GameCardManagerOperationIdValue.GetGameCardErrorReportInfo:
            {
                if (buffer.Size < Unsafe.SizeOf<GameCardErrorReportInfo>())
                    return ResultFs.InvalidArgument.Log();

                buffer.As<GameCardErrorReportInfo>() = default;
                bytesWritten = Unsafe.SizeOf<GameCardErrorReportInfo>();

                return Result.Success;
            }
            case GameCardManagerOperationIdValue.ReadParamDirectly:
            {
                return ResultFs.GameCardCardNotInserted.Log();
            }
            default:
                return ResultFs.InvalidArgument.Log();
        }
    }

    public Result OperateOut2(out long bytesWrittenBuffer1, OutBuffer buffer1, out long bytesWrittenBuffer2,
        OutBuffer buffer2, int operationId)
    {
        bytesWrittenBuffer1 = 0;
        bytesWrittenBuffer2 = 0;

        return ResultFs.NotImplemented.Log();
    }

    public Result OperateInOut(out long bytesWritten, OutBuffer outBuffer, InBuffer inBuffer, long offset, long size,
        int operationId)
    {
        var operation = (GameCardManagerOperationIdValue)operationId;
        bytesWritten = 0;

        switch (operation)
        {
            case GameCardManagerOperationIdValue.IsGameCardActivationValid:
            {
                if (outBuffer.Size < Unsafe.SizeOf<bool>())
                    return ResultFs.InvalidArgument.Log();

                outBuffer.As<bool>() = false;
                bytesWritten = sizeof(bool);

                return Result.Success;
            }
            case GameCardManagerOperationIdValue.GetGameCardAsicInfo:
                return ResultFs.GameCardAccessFailed.Log();

            case GameCardManagerOperationIdValue.GetGameCardDeviceIdForProdCard:
                return ResultFs.GameCardCardNotInserted.Log();

            case GameCardManagerOperationIdValue.WriteToGameCardDirectly:
                return ResultFs.GameCardCardNotInserted.Log();

            default:
                return ResultFs.InvalidArgument.Log();
        }
    }

    public Result OperateIn2Out(out long bytesWritten, OutBuffer outBuffer, InBuffer inBuffer1, InBuffer inBuffer2,
        long offset, long size, int operationId)
    {
        bytesWritten = 0;

        return ResultFs.NotImplemented.Log();
    }
}