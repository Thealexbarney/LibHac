using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Sf;
using LibHac.FsSrv.Storage.Sf;
using LibHac.FsSystem;
using LibHac.Sf;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.GcSrv;

/// <summary>
/// The game card manager used on consoles without a game card slot.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
public class DummyGameCardManager : IStorageDeviceManager, IStorageDeviceOperator, IGameCardKeyManager
{
    private SharedRef<DummyEventNotifier> _eventNotifier;

    // LibHac additions
    private WeakRef<DummyGameCardManager> _selfReference;

    private DummyGameCardManager()
    {
        _eventNotifier = new SharedRef<DummyEventNotifier>(new DummyEventNotifier());
    }

    public static SharedRef<DummyGameCardManager> CreateShared()
    {
        var manager = new DummyGameCardManager();

        using var sharedManager = new SharedRef<DummyGameCardManager>(manager);
        manager._selfReference.Set(in sharedManager);

        return SharedRef<DummyGameCardManager>.CreateMove(ref sharedManager.Ref);
    }

    public void Dispose()
    {
        _eventNotifier.Destroy();
        _selfReference.Destroy();
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
        outDetectionEvent.SetByCopy(in _eventNotifier);
        return Result.Success;
    }

    public Result OpenOperator(ref SharedRef<IStorageDeviceOperator> outDeviceOperator)
    {
        using SharedRef<DummyGameCardManager> deviceOperator = SharedRef<DummyGameCardManager>.Create(in _selfReference);

        if (!deviceOperator.HasValue)
            return ResultFs.AllocationMemoryFailedInGameCardManagerG.Log();

        outDeviceOperator.SetByMove(ref deviceOperator.Ref);

        return Result.Success;
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
                if (outBuffer.Size < sizeof(bool))
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

    public void PresetInternalKeys(ReadOnlySpan<byte> gameCardKey, ReadOnlySpan<byte> gameCardCertificate)
    {
        // Empty
    }
}