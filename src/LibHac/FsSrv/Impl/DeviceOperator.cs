using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSrv.Sf;
using LibHac.FsSrv.Storage;
using LibHac.FsSystem;
using LibHac.Gc;
using LibHac.Sdmmc;
using LibHac.Sf;
using LibHac.Util;

namespace LibHac.FsSrv.Impl;

/// <summary>
/// Verifies permissions for <see cref="IDeviceOperator"/> calls and forwards them
/// to the appropriate <see cref="LibHac.FsSrv.Storage"/> functions.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
public class DeviceOperator : IDeviceOperator
{
    private AccessControl _accessControl;
    // ReSharper disable once NotAccessedField.Local
    private ulong _processId;

    // LibHac addition
    private FileSystemServer _fsServer;

    public DeviceOperator(FileSystemServer fsServer, AccessControl accessControl, ulong processId)
    {
        _accessControl = accessControl;
        _processId = processId;

        _fsServer = fsServer;
    }

    public void Dispose() { }

    private static Span<byte> GetSpan(OutBuffer buffer, long size)
    {
        Assert.True(IntUtil.IsIntValueRepresentableAsInt(size));

        return buffer.Buffer.Slice(0, (int)size);
    }

    private static ReadOnlySpan<byte> GetSpan(InBuffer buffer, long size)
    {
        Assert.True(IntUtil.IsIntValueRepresentableAsInt(size));

        return buffer.Buffer.Slice(0, (int)size);
    }

    public Result IsSdCardInserted(out bool outIsInserted)
    {
        return _fsServer.Storage.IsSdCardInserted(out outIsInserted).Ret();
    }

    public Result GetSdCardCid(OutBuffer outBuffer, long outBufferSize)
    {
        if (outBuffer.Size < outBufferSize)
            return ResultFs.InvalidSize.Log();

        return _fsServer.Storage.GetSdCardCid(GetSpan(outBuffer, outBufferSize)).Ret();
    }

    public Result GetSdCardSpeedMode(out long outSpeedMode)
    {
        UnsafeHelpers.SkipParamInit(out outSpeedMode);

        Result res = _fsServer.Storage.GetSdCardSpeedMode(out SdCardSpeedMode speedMode);
        if (res.IsFailure()) return res.Miss();

        outSpeedMode = (long)speedMode;
        return Result.Success;
    }

    public Result GetSdCardUserAreaSize(out long outSize)
    {
        UnsafeHelpers.SkipParamInit(out outSize);

        Result res = _fsServer.Storage.GetSdCardUserAreaSize(out long size);
        if (res.IsFailure()) return res.Miss();

        outSize = size;
        return Result.Success;
    }

    public Result GetSdCardProtectedAreaSize(out long outSize)
    {
        UnsafeHelpers.SkipParamInit(out outSize);

        Result res = _fsServer.Storage.GetSdCardProtectedAreaSize(out long size);
        if (res.IsFailure()) return res.Miss();

        outSize = size;
        return Result.Success;
    }

    public Result GetAndClearSdCardErrorInfo(out StorageErrorInfo outStorageErrorInfo, out long outLogSize,
        OutBuffer logBuffer, long logBufferSize)
    {
        UnsafeHelpers.SkipParamInit(out outStorageErrorInfo, out outLogSize);

        if (logBuffer.Size < logBufferSize)
            return ResultFs.InvalidSize.Log();

        Result res = _fsServer.Storage.GetAndClearSdCardErrorInfo(out StorageErrorInfo storageErrorInfo,
            out long logSize, GetSpan(logBuffer, logBufferSize));
        if (res.IsFailure()) return res.Miss();

        outStorageErrorInfo = storageErrorInfo;
        outLogSize = logSize;
        return Result.Success;
    }

    public Result GetMmcCid(OutBuffer outBuffer, long outBufferSize)
    {
        if (outBuffer.Size < outBufferSize)
            return ResultFs.InvalidSize.Log();

        return _fsServer.Storage.GetMmcCid(GetSpan(outBuffer, outBufferSize));
    }

    public Result GetMmcSpeedMode(out long outSpeedMode)
    {
        UnsafeHelpers.SkipParamInit(out outSpeedMode);

        Result res = _fsServer.Storage.GetMmcSpeedMode(out MmcSpeedMode speedMode);
        if (res.IsFailure()) return res.Miss();

        outSpeedMode = (long)speedMode;
        return Result.Success;
    }

    public Result EraseMmc(uint partitionId)
    {
        if (!_accessControl.CanCall(OperationType.EraseMmc))
            return ResultFs.PermissionDenied.Log();

        return _fsServer.Storage.EraseMmc((MmcPartition)partitionId).Ret();
    }

    public Result GetMmcPartitionSize(out long outSize, uint partitionId)
    {
        UnsafeHelpers.SkipParamInit(out outSize);

        Result res = _fsServer.Storage.GetMmcPartitionSize(out long mmcPartitionSize, (MmcPartition)partitionId);
        if (res.IsFailure()) return res.Miss();

        outSize = mmcPartitionSize;
        return Result.Success;
    }

    public Result GetMmcPatrolCount(out uint outCount)
    {
        UnsafeHelpers.SkipParamInit(out outCount);

        Result res = _fsServer.Storage.GetMmcPatrolCount(out uint mmcPatrolCount);
        if (res.IsFailure()) return res.Miss();

        outCount = mmcPatrolCount;
        return Result.Success;
    }

    public Result GetAndClearMmcErrorInfo(out StorageErrorInfo outStorageErrorInfo, out long outLogSize,
        OutBuffer logBuffer, long logBufferSize)
    {
        UnsafeHelpers.SkipParamInit(out outStorageErrorInfo, out outLogSize);

        if (logBuffer.Size < logBufferSize)
            return ResultFs.InvalidSize.Log();

        Result res = _fsServer.Storage.GetAndClearMmcErrorInfo(out StorageErrorInfo storageErrorInfo, out long logSize,
            GetSpan(logBuffer, logBufferSize));
        if (res.IsFailure()) return res.Miss();

        outStorageErrorInfo = storageErrorInfo;
        outLogSize = logSize;
        return Result.Success;
    }

    public Result GetMmcExtendedCsd(OutBuffer outBuffer, long outBufferSize)
    {
        if (outBuffer.Size < outBufferSize)
            return ResultFs.InvalidSize.Log();

        return _fsServer.Storage.GetMmcExtendedCsd(GetSpan(outBuffer, outBufferSize)).Ret();
    }

    public Result SuspendMmcPatrol()
    {
        if (!_accessControl.CanCall(OperationType.ControlMmcPatrol))
            return ResultFs.PermissionDenied.Log();

        return _fsServer.Storage.SuspendMmcPatrol().Ret();
    }

    public Result ResumeMmcPatrol()
    {
        if (!_accessControl.CanCall(OperationType.ControlMmcPatrol))
            return ResultFs.PermissionDenied.Log();

        return _fsServer.Storage.ResumeMmcPatrol().Ret();
    }

    public Result IsGameCardInserted(out bool outIsInserted)
    {
        UnsafeHelpers.SkipParamInit(out outIsInserted);

        Result res = _fsServer.Storage.IsGameCardInserted(out bool isInserted);
        if (res.IsFailure()) return res.Miss();

        outIsInserted = isInserted;
        return Result.Success;
    }

    public Result EraseGameCard(uint gameCardSize, ulong romAreaStartPageAddress)
    {
        Accessibility accessibility = _accessControl.GetAccessibilityFor(AccessibilityType.OpenGameCardStorage);

        if (!accessibility.CanWrite)
            return ResultFs.PermissionDenied.Log();

        return _fsServer.Storage.EraseGameCard(gameCardSize, romAreaStartPageAddress).Ret();
    }

    public Result GetGameCardHandle(out GameCardHandle outHandle)
    {
        UnsafeHelpers.SkipParamInit(out outHandle);

        Result res = _fsServer.Storage.GetInitializationResult();
        if (res.IsFailure()) return res.Miss();

        _fsServer.Storage.IsGameCardInserted(out bool isInserted).IgnoreResult();
        if (!isInserted)
            return ResultFs.GameCardFsGetHandleFailure.Log();

        res = _fsServer.Storage.GetGameCardHandle(out GameCardHandle handle);
        if (res.IsFailure()) return res.Miss();

        outHandle = handle;
        return Result.Success;
    }

    public Result GetGameCardUpdatePartitionInfo(out uint outCupVersion, out ulong outCupId, GameCardHandle handle)
    {
        UnsafeHelpers.SkipParamInit(out outCupVersion, out outCupId);

        Result res = _fsServer.Storage.GetGameCardStatus(out GameCardStatus gameCardStatus, handle);
        if (res.IsFailure()) return res.Miss();

        outCupVersion = gameCardStatus.UpdatePartitionVersion;
        outCupId = gameCardStatus.UpdatePartitionId;

        return Result.Success;
    }

    public Result FinalizeGameCardDriver()
    {
        if (!_accessControl.CanCall(OperationType.FinalizeGameCardDriver))
            return ResultFs.PermissionDenied.Log();

        _fsServer.Storage.FinalizeGameCardLibrary().IgnoreResult();
        return Result.Success;
    }

    public Result GetGameCardAttribute(out byte outAttribute, GameCardHandle handle)
    {
        UnsafeHelpers.SkipParamInit(out outAttribute);

        Result res = _fsServer.Storage.GetGameCardStatus(out GameCardStatus gameCardStatus, handle);
        if (res.IsFailure()) return res.Miss();

        outAttribute = gameCardStatus.GameCardAttribute;
        return Result.Success;
    }

    public Result GetGameCardCompatibilityType(out byte outCompatibilityType, GameCardHandle handle)
    {
        UnsafeHelpers.SkipParamInit(out outCompatibilityType);

        Result res = _fsServer.Storage.GetGameCardStatus(out GameCardStatus gameCardStatus, handle);
        if (res.IsFailure()) return res.Miss();

        outCompatibilityType = gameCardStatus.CompatibilityType;
        return Result.Success;
    }

    public Result GetGameCardDeviceCertificate(OutBuffer outBuffer, long outBufferSize, GameCardHandle handle)
    {
        if (!_accessControl.CanCall(OperationType.GetGameCardDeviceCertificate))
            return ResultFs.PermissionDenied.Log();

        if (outBuffer.Size < outBufferSize)
            return ResultFs.InvalidSize.Log();

        return _fsServer.Storage.GetGameCardDeviceCertificate(GetSpan(outBuffer, outBufferSize), handle).Ret();
    }

    public Result ChallengeCardExistence(OutBuffer outResponseBuffer, InBuffer challengeSeedBuffer,
        InBuffer challengeValueBuffer, GameCardHandle handle)
    {
        if (!_accessControl.CanCall(OperationType.ChallengeCardExistence))
            return ResultFs.PermissionDenied.Log();

        return _fsServer.Storage.ChallengeCardExistence(outResponseBuffer.Buffer, challengeSeedBuffer.Buffer,
            challengeValueBuffer.Buffer, handle).Ret();
    }

    public Result GetGameCardAsicInfo(OutBuffer outRmaInfoBuffer, long rmaInfoBufferSize, InBuffer asicFirmwareBuffer,
        long asicFirmwareBufferSize)
    {
        if (!_accessControl.CanCall(OperationType.GetGameCardAsicInfo))
            return ResultFs.PermissionDenied.Log();

        if (outRmaInfoBuffer.Size < rmaInfoBufferSize)
            return ResultFs.InvalidSize.Log();

        if (asicFirmwareBuffer.Size < asicFirmwareBufferSize)
            return ResultFs.InvalidSize.Log();

        if (rmaInfoBufferSize != Unsafe.SizeOf<RmaInformation>())
            return ResultFs.InvalidArgument.Log();

        Result res = _fsServer.Storage.GetGameCardAsicInfo(out RmaInformation rmaInfo,
            GetSpan(asicFirmwareBuffer, asicFirmwareBufferSize));
        if (res.IsFailure()) return res.Miss();

        SpanHelpers.AsReadOnlyByteSpan(in rmaInfo).CopyTo(outRmaInfoBuffer.Buffer);
        return Result.Success;
    }

    public Result GetGameCardIdSet(OutBuffer outBuffer, long outBufferSize)
    {
        if (outBuffer.Size < outBufferSize)
            return ResultFs.InvalidSize.Log();

        if (outBufferSize != Unsafe.SizeOf<GameCardIdSet>())
            return ResultFs.InvalidArgument.Log();

        Result res = _fsServer.Storage.GetGameCardIdSet(out GameCardIdSet gcIdSet);
        if (res.IsFailure()) return res.Miss();

        SpanHelpers.AsReadOnlyByteSpan(in gcIdSet).CopyTo(outBuffer.Buffer);
        return Result.Success;
    }

    public Result WriteToGameCardDirectly(long offset, OutBuffer buffer, long bufferSize)
    {
        Accessibility accessibility = _accessControl.GetAccessibilityFor(AccessibilityType.OpenGameCardStorage);

        if (!accessibility.CanWrite)
            return ResultFs.PermissionDenied.Log();

        if (buffer.Size < bufferSize)
            return ResultFs.InvalidSize.Log();

        // Changed: Removed the alignment check for the buffer address

        if (!Alignment.IsAlignedPow2(bufferSize, 0x1000))
            return ResultFs.InvalidAlignment.Log();

        return _fsServer.Storage.WriteToGameCardDirectly(offset, GetSpan(buffer, bufferSize)).Ret();
    }

    public Result SetVerifyWriteEnableFlag(bool isEnabled)
    {
        _fsServer.Storage.SetVerifyWriteEnableFlag(isEnabled);
        return Result.Success;
    }

    public Result GetGameCardImageHash(OutBuffer outBuffer, long outBufferSize, GameCardHandle handle)
    {
        if (outBuffer.Size < outBufferSize)
            return ResultFs.InvalidSize.Log();

        return _fsServer.Storage.GetGameCardImageHash(GetSpan(outBuffer, outBufferSize), handle).Ret();
    }

    public Result GetGameCardDeviceIdForProdCard(OutBuffer outBuffer, long outBufferSize, InBuffer devHeaderBuffer,
        long devHeaderBufferSize)
    {
        Accessibility accessibility = _accessControl.GetAccessibilityFor(AccessibilityType.OpenGameCardStorage);

        if (!accessibility.CanWrite)
            return ResultFs.PermissionDenied.Log();

        if (outBuffer.Size < outBufferSize)
            return ResultFs.InvalidSize.Log();

        if (devHeaderBuffer.Size < devHeaderBufferSize)
            return ResultFs.InvalidSize.Log();

        return _fsServer.Storage.GetGameCardDeviceIdForProdCard(GetSpan(outBuffer, outBufferSize),
            GetSpan(devHeaderBuffer, devHeaderBufferSize)).Ret();
    }

    public Result EraseAndWriteParamDirectly(InBuffer inBuffer, long inBufferSize)
    {
        Accessibility accessibility = _accessControl.GetAccessibilityFor(AccessibilityType.OpenGameCardStorage);

        if (!accessibility.CanWrite)
            return ResultFs.PermissionDenied.Log();

        if (inBuffer.Size < inBufferSize)
            return ResultFs.InvalidSize.Log();

        return _fsServer.Storage.EraseAndWriteParamDirectly(GetSpan(inBuffer, inBufferSize)).Ret();
    }

    public Result ReadParamDirectly(OutBuffer outBuffer, long outBufferSize)
    {
        Accessibility accessibility = _accessControl.GetAccessibilityFor(AccessibilityType.OpenGameCardStorage);

        if (!accessibility.CanWrite)
            return ResultFs.PermissionDenied.Log();

        if (outBuffer.Size < outBufferSize)
            return ResultFs.InvalidSize.Log();

        return _fsServer.Storage.ReadParamDirectly(GetSpan(outBuffer, outBufferSize)).Ret();
    }

    public Result ForceEraseGameCard()
    {
        Accessibility accessibility = _accessControl.GetAccessibilityFor(AccessibilityType.OpenGameCardStorage);

        if (!accessibility.CanWrite)
            return ResultFs.PermissionDenied.Log();

        return _fsServer.Storage.ForceEraseGameCard().Ret();
    }

    public Result GetGameCardErrorInfo(out GameCardErrorInfo outErrorInfo)
    {
        UnsafeHelpers.SkipParamInit(out outErrorInfo);

        Result res = _fsServer.Storage.GetGameCardErrorInfo(out GameCardErrorInfo gameCardErrorInfo);
        if (res.IsFailure()) return res.Miss();

        outErrorInfo = gameCardErrorInfo;
        return Result.Success;
    }

    public Result GetGameCardErrorReportInfo(out GameCardErrorReportInfo outErrorInfo)
    {
        UnsafeHelpers.SkipParamInit(out outErrorInfo);

        Result res = _fsServer.Storage.GetGameCardErrorReportInfo(out GameCardErrorReportInfo gameCardErrorReportInfo);
        if (res.IsFailure()) return res.Miss();

        outErrorInfo = gameCardErrorReportInfo;
        return Result.Success;
    }

    public Result GetGameCardDeviceId(OutBuffer outBuffer, long outBufferSize)
    {
        if (outBuffer.Size < outBufferSize)
            return ResultFs.InvalidSize.Log();

        return _fsServer.Storage.GetGameCardDeviceId(GetSpan(outBuffer, outBufferSize)).Ret();
    }

    public Result SetSpeedEmulationMode(int mode)
    {
        if (!_accessControl.CanCall(OperationType.SetSpeedEmulationMode))
            return ResultFs.PermissionDenied.Log();

        _fsServer.SetSpeedEmulationMode((SpeedEmulationMode)mode);
        return Result.Success;
    }

    public Result GetSpeedEmulationMode(out int outMode)
    {
        outMode = (int)_fsServer.GetSpeedEmulationMode();
        return Result.Success;
    }

    public Result SuspendSdmmcControl()
    {
        Result res = _fsServer.Storage.SuspendSdCardControl();
        if (res.IsFailure()) return res.Miss();

        // Missing: Detach SD card device buffer

        res = _fsServer.Storage.SuspendMmcControl();
        if (res.IsFailure()) return res.Miss();

        // Missing: Detach MMC device buffer

        return Result.Success;
    }

    public Result ResumeSdmmcControl()
    {
        // Missing: Attach MMC device buffer

        Result res = _fsServer.Storage.ResumeMmcControl();
        if (res.IsFailure()) return res.Miss();

        // Missing: Attach SD card device buffer

        res = _fsServer.Storage.ResumeSdCardControl();
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result GetSdmmcConnectionStatus(out int outSpeedMode, out int outBusWidth, int port)
    {
        UnsafeHelpers.SkipParamInit(out outSpeedMode, out outBusWidth);

        if ((uint)port > (uint)Port.GcAsic0)
            return ResultFs.InvalidArgument.Log();

        Result res = SdmmcSrv.Common.CheckConnection(out SpeedMode speedMode, out BusWidth busWidth, (Port)port);
        if (res.IsFailure()) return res.Miss();

        SdmmcSpeedMode sdmmcSpeedMode = speedMode switch
        {
            SpeedMode.MmcIdentification => SdmmcSpeedMode.MmcIdentification,
            SpeedMode.MmcLegacySpeed => SdmmcSpeedMode.MmcLegacySpeed,
            SpeedMode.MmcHighSpeed => SdmmcSpeedMode.MmcHighSpeed,
            SpeedMode.MmcHs200 => SdmmcSpeedMode.MmcHs200,
            SpeedMode.MmcHs400 => SdmmcSpeedMode.MmcHs400,
            SpeedMode.SdCardIdentification => SdmmcSpeedMode.SdCardIdentification,
            SpeedMode.SdCardDefaultSpeed => SdmmcSpeedMode.SdCardDefaultSpeed,
            SpeedMode.SdCardHighSpeed => SdmmcSpeedMode.SdCardHighSpeed,
            SpeedMode.SdCardSdr12 => SdmmcSpeedMode.SdCardSdr12,
            SpeedMode.SdCardSdr25 => SdmmcSpeedMode.SdCardSdr25,
            SpeedMode.SdCardSdr50 => SdmmcSpeedMode.SdCardSdr50,
            SpeedMode.SdCardSdr104 => SdmmcSpeedMode.SdCardSdr104,
            SpeedMode.SdCardDdr50 => SdmmcSpeedMode.SdCardDdr50,
            SpeedMode.GcAsicFpgaSpeed => SdmmcSpeedMode.GcAsicFpgaSpeed,
            SpeedMode.GcAsicSpeed => SdmmcSpeedMode.GcAsicSpeed,
            _ => SdmmcSpeedMode.Unknown
        };

        SdmmcBusWidth sdmmcBusWidth = busWidth switch
        {
            BusWidth.Width1Bit => SdmmcBusWidth.Width1Bit,
            BusWidth.Width4Bit => SdmmcBusWidth.Width4Bit,
            BusWidth.Width8Bit => SdmmcBusWidth.Width8Bit,
            _ => SdmmcBusWidth.Unknown
        };

        outSpeedMode = (int)sdmmcSpeedMode;
        outBusWidth = (int)sdmmcBusWidth;

        return Result.Success;
    }

    public Result SetDeviceSimulationEvent(uint port, uint simulatedOperationType, uint simulatedFailureType,
        uint failureResult, bool autoClearEvent)
    {
        if (!_accessControl.CanCall(OperationType.SimulateDevice))
            return ResultFs.PermissionDenied.Log();

        var respondingFailureResult = new Result(failureResult);

        switch ((Port)port)
        {
            case Port.GcAsic0:
                _fsServer.Impl.GetGameCardEventSimulator().SetDeviceEvent(
                    (SimulatingDeviceTargetOperation)simulatedOperationType,
                    (SimulatingDeviceAccessFailureEventType)simulatedFailureType, respondingFailureResult,
                    autoClearEvent);
                break;
            case Port.SdCard0:
                _fsServer.Impl.GetSdCardEventSimulator().SetDeviceEvent(
                    (SimulatingDeviceTargetOperation)simulatedOperationType,
                    (SimulatingDeviceAccessFailureEventType)simulatedFailureType, respondingFailureResult,
                    autoClearEvent);
                break;
            case Port.Mmc0:
                return ResultFs.NotImplemented.Log();
            default:
                return ResultFs.StorageDeviceInvalidOperation.Log();
        }

        return Result.Success;
    }

    public Result ClearDeviceSimulationEvent(uint port)
    {
        if (!_accessControl.CanCall(OperationType.SimulateDevice))
            return ResultFs.PermissionDenied.Log();

        switch ((Port)port)
        {
            case Port.GcAsic0:
                _fsServer.Impl.GetGameCardEventSimulator().ClearDeviceEvent();
                break;
            case Port.SdCard0:
                _fsServer.Impl.GetSdCardEventSimulator().ClearDeviceEvent();
                break;
            case Port.Mmc0:
                return ResultFs.NotImplemented.Log();
            default:
                return ResultFs.StorageDeviceInvalidOperation.Log();
        }

        return Result.Success;
    }
}