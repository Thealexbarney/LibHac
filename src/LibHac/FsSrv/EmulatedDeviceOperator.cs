using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Sf;
using LibHac.Sf;

namespace LibHac.FsSrv;

public class EmulatedDeviceOperator : IDeviceOperator
{
    private EmulatedGameCard GameCard { get; set; }
    private EmulatedSdCard SdCard { get; set; }

    public EmulatedDeviceOperator(EmulatedGameCard gameCard, EmulatedSdCard sdCard)
    {
        GameCard = gameCard;
        SdCard = sdCard;
    }

    public void Dispose() { }

    public Result IsSdCardInserted(out bool outIsInserted)
    {
        outIsInserted = SdCard.IsSdCardInserted();
        return Result.Success;
    }

    public Result GetSdCardSpeedMode(out long outSpeedMode)
    {
        throw new NotImplementedException();
    }

    public Result GetSdCardCid(OutBuffer outBuffer, long outBufferSize)
    {
        throw new NotImplementedException();
    }

    public Result GetSdCardUserAreaSize(out long outSize)
    {
        throw new NotImplementedException();
    }

    public Result GetSdCardProtectedAreaSize(out long outSize)
    {
        throw new NotImplementedException();
    }

    public Result GetAndClearSdCardErrorInfo(out StorageErrorInfo outStorageErrorInfo, out long outLogSize,
        OutBuffer logBuffer, long logBufferSize)
    {
        throw new NotImplementedException();
    }

    public Result GetMmcCid(OutBuffer outBuffer, long outBufferSize)
    {
        throw new NotImplementedException();
    }

    public Result GetMmcSpeedMode(out long outSpeedMode)
    {
        throw new NotImplementedException();
    }

    public Result EraseMmc(uint partitionId)
    {
        throw new NotImplementedException();
    }

    public Result GetMmcPartitionSize(out long outSize, uint partitionId)
    {
        throw new NotImplementedException();
    }

    public Result GetMmcPatrolCount(out uint outCount)
    {
        throw new NotImplementedException();
    }

    public Result GetAndClearMmcErrorInfo(out StorageErrorInfo outStorageErrorInfo, out long outLogSize,
        OutBuffer logBuffer, long logBufferSize)
    {
        throw new NotImplementedException();
    }

    public Result GetMmcExtendedCsd(OutBuffer outBuffer, long outBufferSize)
    {
        throw new NotImplementedException();
    }

    public Result SuspendMmcPatrol()
    {
        throw new NotImplementedException();
    }

    public Result ResumeMmcPatrol()
    {
        throw new NotImplementedException();
    }

    public Result IsGameCardInserted(out bool outIsInserted)
    {
        outIsInserted = GameCard.IsGameCardInserted();
        return Result.Success;
    }

    public Result EraseGameCard(uint gameCardSize, ulong romAreaStartPageAddress)
    {
        throw new NotImplementedException();
    }

    public Result GetGameCardUpdatePartitionInfo(out uint outCupVersion, out ulong outCupId, uint handle)
    {
        throw new NotImplementedException();
    }

    public Result FinalizeGameCardDriver()
    {
        throw new NotImplementedException();
    }

    public Result GetGameCardAttribute(out byte outAttribute, uint handle)
    {
        throw new NotImplementedException();
    }

    public Result GetGameCardDeviceCertificate(OutBuffer outBuffer, long outBufferSize, uint handle)
    {
        throw new NotImplementedException();
    }

    public Result GetGameCardAsicInfo(OutBuffer outRmaInfoBuffer, long rmaInfoBufferSize, InBuffer asicFirmwareBuffer,
        long asicFirmwareBufferSize)
    {
        throw new NotImplementedException();
    }

    public Result GetGameCardIdSet(OutBuffer outBuffer, long outBufferSize)
    {
        throw new NotImplementedException();
    }

    public Result WriteToGameCardDirectly(long offset, OutBuffer buffer, long bufferSize)
    {
        throw new NotImplementedException();
    }

    public Result SetVerifyWriteEnableFlag(bool isEnabled)
    {
        throw new NotImplementedException();
    }

    public Result GetGameCardImageHash(OutBuffer outBuffer, long outBufferSize, uint handle)
    {
        throw new NotImplementedException();
    }

    public Result GetGameCardDeviceIdForProdCard(OutBuffer outBuffer, long outBufferSize, InBuffer devHeaderBuffer,
        long devHeaderBufferSize)
    {
        throw new NotImplementedException();
    }

    public Result EraseAndWriteParamDirectly(InBuffer inBuffer, long inBufferSize)
    {
        throw new NotImplementedException();
    }

    public Result ReadParamDirectly(OutBuffer outBuffer, long outBufferSize)
    {
        throw new NotImplementedException();
    }

    public Result ForceEraseGameCard()
    {
        throw new NotImplementedException();
    }

    public Result GetGameCardErrorInfo(out GameCardErrorInfo outErrorInfo)
    {
        throw new NotImplementedException();
    }

    public Result GetGameCardErrorReportInfo(out GameCardErrorReportInfo outErrorInfo)
    {
        throw new NotImplementedException();
    }

    public Result GetGameCardDeviceId(OutBuffer outBuffer, long outBufferSize)
    {
        throw new NotImplementedException();
    }

    public Result ChallengeCardExistence(OutBuffer outResponseBuffer, InBuffer challengeSeedBuffer,
        InBuffer challengeValueBuffer, uint handle)
    {
        throw new NotImplementedException();
    }

    public Result GetGameCardCompatibilityType(out byte outCompatibilityType, uint handle)
    {
        throw new NotImplementedException();
    }

    public Result SetSpeedEmulationMode(int mode)
    {
        throw new NotImplementedException();
    }

    public Result GetSpeedEmulationMode(out int outMode)
    {
        throw new NotImplementedException();
    }

    public Result SuspendSdmmcControl()
    {
        throw new NotImplementedException();
    }

    public Result ResumeSdmmcControl()
    {
        throw new NotImplementedException();
    }

    public Result GetSdmmcConnectionStatus(out int outSpeedMode, out int outBusWidth, int port)
    {
        throw new NotImplementedException();
    }

    public Result SetDeviceSimulationEvent(uint port, uint simulatedOperationType, uint simulatedFailureType,
        uint failureResult, bool autoClearEvent)
    {
        throw new NotImplementedException();
    }

    public Result ClearDeviceSimulationEvent(uint port)
    {
        throw new NotImplementedException();
    }

    public Result GetGameCardHandle(out GameCardHandle handle)
    {
        UnsafeHelpers.SkipParamInit(out handle);

        if (!GameCard.IsGameCardInserted())
            return ResultFs.GameCardFsGetHandleFailure.Log();

        handle = GameCard.GetGameCardHandle();
        return Result.Success;
    }
}