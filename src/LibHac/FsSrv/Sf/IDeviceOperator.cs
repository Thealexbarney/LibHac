using System;
using LibHac.Fs;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf;

public interface IDeviceOperator : IDisposable
{
    Result IsSdCardInserted(out bool outIsInserted);
    Result GetSdCardSpeedMode(out long outSpeedMode);
    Result GetSdCardCid(OutBuffer outBuffer, long outBufferSize);
    Result GetSdCardUserAreaSize(out long outSize);
    Result GetSdCardProtectedAreaSize(out long outSize);
    Result GetAndClearSdCardErrorInfo(out StorageErrorInfo outStorageErrorInfo, out long outLogSize, OutBuffer logBuffer, long logBufferSize);
    Result GetMmcCid(OutBuffer outBuffer, long outBufferSize);
    Result GetMmcSpeedMode(out long outSpeedMode);
    Result EraseMmc(uint partitionId);
    Result GetMmcPartitionSize(out long outSize, uint partitionId);
    Result GetMmcPatrolCount(out uint outCount);
    Result GetAndClearMmcErrorInfo(out StorageErrorInfo outStorageErrorInfo, out long outLogSize, OutBuffer logBuffer, long logBufferSize);
    Result GetMmcExtendedCsd(OutBuffer outBuffer, long outBufferSize);
    Result SuspendMmcPatrol();
    Result ResumeMmcPatrol();
    Result IsGameCardInserted(out bool outIsInserted);
    Result EraseGameCard(uint gameCardSize, ulong romAreaStartPageAddress);
    Result GetGameCardHandle(out uint outHandle);
    Result GetGameCardUpdatePartitionInfo(out uint outCupVersion, out ulong outCupId, uint handle);
    Result FinalizeGameCardDriver();
    Result GetGameCardAttribute(out byte outAttribute, uint handle);
    Result GetGameCardDeviceCertificate(OutBuffer outBuffer, long outBufferSize, uint handle);
    Result GetGameCardAsicInfo(OutBuffer outRmaInfoBuffer, long rmaInfoBufferSize, InBuffer asicFirmwareBuffer, long asicFirmwareBufferSize);
    Result GetGameCardIdSet(OutBuffer outBuffer, long outBufferSize);
    Result WriteToGameCardDirectly(long offset, OutBuffer buffer, long bufferSize);
    Result SetVerifyWriteEnableFlag(bool isEnabled);
    Result GetGameCardImageHash(OutBuffer outBuffer, long outBufferSize, uint handle);
    Result GetGameCardDeviceIdForProdCard(OutBuffer outBuffer, long outBufferSize, InBuffer devHeaderBuffer, long devHeaderBufferSize);
    Result EraseAndWriteParamDirectly(InBuffer inBuffer, long inBufferSize);
    Result ReadParamDirectly(OutBuffer outBuffer, long outBufferSize);
    Result ForceEraseGameCard();
    Result GetGameCardErrorInfo(out GameCardErrorInfo outErrorInfo);
    Result GetGameCardErrorReportInfo(out GameCardErrorReportInfo outErrorInfo);
    Result GetGameCardDeviceId(OutBuffer outBuffer, long outBufferSize);
    Result ChallengeCardExistence(OutBuffer outResponseBuffer, InBuffer challengeSeedBuffer, InBuffer challengeValueBuffer, uint handle);
    Result GetGameCardCompatibilityType(out byte outCompatibilityType, uint handle);
    Result SetSpeedEmulationMode(int mode);
    Result GetSpeedEmulationMode(out int outMode);
    Result SuspendSdmmcControl();
    Result ResumeSdmmcControl();
    Result GetSdmmcConnectionStatus(out int outSpeedMode, out int outBusWidth, int port);
    Result SetDeviceSimulationEvent(uint port, uint simulatedOperationType, uint simulatedFailureType, uint failureResult, bool autoClearEvent);
    Result ClearDeviceSimulationEvent(uint port);
}