using LibHac.Fs;
using LibHac.Sdmmc;

namespace LibHac.SdmmcSrv;

/// <summary>
/// Contains functions to convert <see cref="ResultSdmmc"/> <see cref="Result"/>s to their <see cref="ResultFs"/> equivalent.
/// </summary>
/// <remarks>Based on nnSdk 15.3.0 (FS 15.0.0)</remarks>
public static class SdmmcResultConverter
{
    public static Result GetFsResult(Port port, Result result)
    {
        if (result.IsSuccess())
            return Result.Success;

        if (port == Port.Mmc0)
        {
            return GetFsResultFromMmcResult(result).Ret();
        }
        else
        {
            return GetFsResultFromSdCardResult(result).Ret();
        }
    }

    private static Result GetFsResultFromMmcResult(Result result)
    {
        if (ResultSdmmc.NoDevice.Includes(result))
            return ResultFs.PortMmcNoDevice.LogConverted(result);

        if (ResultSdmmc.NotActivated.Includes(result))
            return ResultFs.PortMmcNotActivated.LogConverted(result);

        if (ResultSdmmc.DeviceRemoved.Includes(result))
            return ResultFs.PortMmcDeviceRemoved.LogConverted(result);

        if (ResultSdmmc.NotAwakened.Includes(result))
            return ResultFs.PortMmcNotAwakened.LogConverted(result);


        if (ResultSdmmc.ResponseIndexError.Includes(result))
            return ResultFs.PortMmcResponseIndexError.LogConverted(result);

        if (ResultSdmmc.ResponseEndBitError.Includes(result))
            return ResultFs.PortMmcResponseEndBitError.LogConverted(result);

        if (ResultSdmmc.ResponseCrcError.Includes(result))
            return ResultFs.PortMmcResponseCrcError.LogConverted(result);

        if (ResultSdmmc.ResponseTimeoutError.Includes(result))
            return ResultFs.PortMmcResponseTimeoutError.LogConverted(result);

        if (ResultSdmmc.DataEndBitError.Includes(result))
            return ResultFs.PortMmcDataEndBitError.LogConverted(result);

        if (ResultSdmmc.DataCrcError.Includes(result))
            return ResultFs.PortMmcDataCrcError.LogConverted(result);

        if (ResultSdmmc.DataTimeoutError.Includes(result))
            return ResultFs.PortMmcDataTimeoutError.LogConverted(result);

        if (ResultSdmmc.AutoCommandResponseIndexError.Includes(result))
            return ResultFs.PortMmcAutoCommandResponseIndexError.LogConverted(result);

        if (ResultSdmmc.AutoCommandResponseEndBitError.Includes(result))
            return ResultFs.PortMmcAutoCommandResponseEndBitError.LogConverted(result);

        if (ResultSdmmc.AutoCommandResponseCrcError.Includes(result))
            return ResultFs.PortMmcAutoCommandResponseCrcError.LogConverted(result);

        if (ResultSdmmc.AutoCommandResponseTimeoutError.Includes(result))
            return ResultFs.PortMmcAutoCommandResponseTimeoutError.LogConverted(result);

        if (ResultSdmmc.CommandCompleteSoftwareTimeout.Includes(result))
            return ResultFs.PortMmcCommandCompleteSwTimeout.LogConverted(result);

        if (ResultSdmmc.TransferCompleteSoftwareTimeout.Includes(result))
            return ResultFs.PortMmcTransferCompleteSwTimeout.LogConverted(result);


        if (ResultSdmmc.DeviceStatusAddressOutOfRange.Includes(result))
            return ResultFs.PortMmcDeviceStatusAddressOutOfRange.LogConverted(result);

        if (ResultSdmmc.DeviceStatusAddressMisaligned.Includes(result))
            return ResultFs.PortMmcDeviceStatusAddressMisalign.LogConverted(result);

        if (ResultSdmmc.DeviceStatusBlockLenError.Includes(result))
            return ResultFs.PortMmcDeviceStatusBlockLenError.LogConverted(result);

        if (ResultSdmmc.DeviceStatusEraseSeqError.Includes(result))
            return ResultFs.PortMmcDeviceStatusEraseSeqError.LogConverted(result);

        if (ResultSdmmc.DeviceStatusEraseParam.Includes(result))
            return ResultFs.PortMmcDeviceStatusEraseParam.LogConverted(result);

        if (ResultSdmmc.DeviceStatusWpViolation.Includes(result))
            return ResultFs.PortMmcDeviceStatusWpViolation.LogConverted(result);

        if (ResultSdmmc.DeviceStatusLockUnlockFailed.Includes(result))
            return ResultFs.PortMmcDeviceStatusLockUnlockFailed.LogConverted(result);

        if (ResultSdmmc.DeviceStatusComCrcError.Includes(result))
            return ResultFs.PortMmcDeviceStatusComCrcError.LogConverted(result);

        if (ResultSdmmc.DeviceStatusIllegalCommand.Includes(result))
            return ResultFs.PortMmcDeviceStatusIllegalCommand.LogConverted(result);

        if (ResultSdmmc.DeviceStatusDeviceEccFailed.Includes(result))
            return ResultFs.PortMmcDeviceStatusDeviceEccFailed.LogConverted(result);

        if (ResultSdmmc.DeviceStatusCcError.Includes(result))
            return ResultFs.PortMmcDeviceStatusCcError.LogConverted(result);

        if (ResultSdmmc.DeviceStatusError.Includes(result))
            return ResultFs.PortMmcDeviceStatusError.LogConverted(result);

        if (ResultSdmmc.DeviceStatusCidCsdOverwrite.Includes(result))
            return ResultFs.PortMmcDeviceStatusCidCsdOverwrite.LogConverted(result);

        if (ResultSdmmc.DeviceStatusWpEraseSkip.Includes(result))
            return ResultFs.PortMmcDeviceStatusWpEraseSkip.LogConverted(result);

        if (ResultSdmmc.DeviceStatusEraseReset.Includes(result))
            return ResultFs.PortMmcDeviceStatusEraseReset.LogConverted(result);

        if (ResultSdmmc.DeviceStatusSwitchError.Includes(result))
            return ResultFs.PortMmcDeviceStatusSwitchError.LogConverted(result);


        if (ResultSdmmc.UnexpectedDeviceState.Includes(result))
            return ResultFs.PortMmcUnexpectedDeviceState.LogConverted(result);

        if (ResultSdmmc.UnexpectedDeviceCsdValue.Includes(result))
            return ResultFs.PortMmcUnexpectedDeviceCsdValue.LogConverted(result);

        if (ResultSdmmc.AbortTransactionSoftwareTimeout.Includes(result))
            return ResultFs.PortMmcAbortTransactionSwTimeout.LogConverted(result);

        if (ResultSdmmc.CommandInhibitCmdSoftwareTimeout.Includes(result))
            return ResultFs.PortMmcCommandInhibitCmdSwTimeout.LogConverted(result);

        if (ResultSdmmc.CommandInhibitDatSoftwareTimeout.Includes(result))
            return ResultFs.PortMmcCommandInhibitDatSwTimeout.LogConverted(result);

        if (ResultSdmmc.BusySoftwareTimeout.Includes(result))
            return ResultFs.PortMmcBusySwTimeout.LogConverted(result);

        if (ResultSdmmc.IssueTuningCommandSoftwareTimeout.Includes(result))
            return ResultFs.PortMmcIssueTuningCommandSwTimeout.LogConverted(result);

        if (ResultSdmmc.TuningFailed.Includes(result))
            return ResultFs.PortMmcTuningFailed.LogConverted(result);

        if (ResultSdmmc.MmcInitializationSoftwareTimeout.Includes(result))
            return ResultFs.PortMmcMmcInitializationSwTimeout.LogConverted(result);

        if (ResultSdmmc.MmcNotSupportExtendedCsd.Includes(result))
            return ResultFs.PortMmcMmcNotSupportExtendedCsd.LogConverted(result);

        if (ResultSdmmc.UnexpectedMmcExtendedCsdValue.Includes(result))
            return ResultFs.PortMmcUnexpectedMmcExtendedCsdValue.LogConverted(result);

        if (ResultSdmmc.MmcEraseSoftwareTimeout.Includes(result))
            return ResultFs.PortMmcMmcEraseSwTimeout.LogConverted(result);

        if (ResultSdmmc.SdCardValidationError.Includes(result))
            return ResultFs.PortMmcSdCardValidationError.LogConverted(result);

        if (ResultSdmmc.SdCardInitializationSoftwareTimeout.Includes(result))
            return ResultFs.PortMmcSdCardInitializationSwTimeout.LogConverted(result);

        if (ResultSdmmc.SdCardGetValidRcaSoftwareTimeout.Includes(result))
            return ResultFs.PortMmcSdCardGetValidRcaSwTimeout.LogConverted(result);

        if (ResultSdmmc.UnexpectedSdCardAcmdDisabled.Includes(result))
            return ResultFs.PortMmcUnexpectedSdCardAcmdDisabled.LogConverted(result);

        if (ResultSdmmc.SdCardNotSupportSwitchFunctionStatus.Includes(result))
            return ResultFs.PortMmcSdCardNotSupportSwitchFunctionStatus.LogConverted(result);

        if (ResultSdmmc.UnexpectedSdCardSwitchFunctionStatus.Includes(result))
            return ResultFs.PortMmcUnexpectedSdCardSwitchFunctionStatus.LogConverted(result);

        if (ResultSdmmc.SdCardNotSupportAccessMode.Includes(result))
            return ResultFs.PortMmcSdCardNotSupportAccessMode.LogConverted(result);

        if (ResultSdmmc.SdCardNot4BitBusWidthAtUhsIMode.Includes(result))
            return ResultFs.PortMmcSdCardNot4BitBusWidthAtUhsIMode.LogConverted(result);

        if (ResultSdmmc.SdCardNotSupportSdr104AndSdr50.Includes(result))
            return ResultFs.PortMmcSdCardNotSupportSdr104AndSdr50.LogConverted(result);

        if (ResultSdmmc.SdCardCannotSwitchAccessMode.Includes(result))
            return ResultFs.PortMmcSdCardCannotSwitchedAccessMode.LogConverted(result);

        if (ResultSdmmc.SdCardFailedSwitchAccessMode.Includes(result))
            return ResultFs.PortMmcSdCardFailedSwitchedAccessMode.LogConverted(result);

        if (ResultSdmmc.SdCardUnacceptableCurrentConsumption.Includes(result))
            return ResultFs.PortMmcSdCardUnacceptableCurrentConsumption.LogConverted(result);

        if (ResultSdmmc.SdCardNotReadyToVoltageSwitch.Includes(result))
            return ResultFs.PortMmcSdCardNotReadyToVoltageSwitch.LogConverted(result);

        if (ResultSdmmc.SdCardNotCompleteVoltageSwitch.Includes(result))
            return ResultFs.PortMmcSdCardNotCompleteVoltageSwitch.LogConverted(result);


        if (ResultSdmmc.InternalClockStableSoftwareTimeout.Includes(result))
            return ResultFs.PortMmcInternalClockStableSwTimeout.LogConverted(result);

        if (ResultSdmmc.SdHostStandardUnknownAutoCmdError.Includes(result))
            return ResultFs.PortMmcSdHostStandardUnknownAutoCmdError.LogConverted(result);

        if (ResultSdmmc.SdHostStandardUnknownError.Includes(result))
            return ResultFs.PortMmcSdHostStandardUnknownError.LogConverted(result);

        if (ResultSdmmc.SdmmcDllCalibrationSoftwareTimeout.Includes(result))
            return ResultFs.PortMmcSdmmcDllCalibrationSwTimeout.LogConverted(result);

        if (ResultSdmmc.SdmmcDllApplicationSoftwareTimeout.Includes(result))
            return ResultFs.PortMmcSdmmcDllApplicationSwTimeout.LogConverted(result);

        if (ResultSdmmc.SdHostStandardFailSwitchTo18V.Includes(result))
            return ResultFs.PortMmcSdHostStandardFailSwitchTo18V.LogConverted(result);


        if (ResultSdmmc.NoWaitedInterrupt.Includes(result))
            return ResultFs.PortMmcNoWaitedInterrupt.LogConverted(result);

        if (ResultSdmmc.WaitInterruptSoftwareTimeout.Includes(result))
            return ResultFs.PortMmcWaitInterruptSwTimeout.LogConverted(result);


        if (ResultSdmmc.AbortCommandIssued.Includes(result))
            return ResultFs.PortMmcAbortCommandIssued.LogConverted(result);


        if (ResultSdmmc.NotSupported.Includes(result))
            return ResultFs.PortMmcNotSupported.LogConverted(result);

        if (ResultSdmmc.NotImplemented.Includes(result))
            return ResultFs.PortMmcNotImplemented.LogConverted(result);

        return ResultFs.PortMmcUnexpected.LogConverted(result);
    }

    private static Result GetFsResultFromSdCardResult(Result result)
    {
        if (ResultSdmmc.NoDevice.Includes(result))
            return ResultFs.PortSdCardNoDevice.LogConverted(result);

        if (ResultSdmmc.NotActivated.Includes(result))
            return ResultFs.PortSdCardNotActivated.LogConverted(result);

        if (ResultSdmmc.DeviceRemoved.Includes(result))
            return ResultFs.PortSdCardDeviceRemoved.LogConverted(result);

        if (ResultSdmmc.NotAwakened.Includes(result))
            return ResultFs.PortSdCardNotAwakened.LogConverted(result);


        if (ResultSdmmc.ResponseIndexError.Includes(result))
            return ResultFs.PortSdCardResponseIndexError.LogConverted(result);

        if (ResultSdmmc.ResponseEndBitError.Includes(result))
            return ResultFs.PortSdCardResponseEndBitError.LogConverted(result);

        if (ResultSdmmc.ResponseCrcError.Includes(result))
            return ResultFs.PortSdCardResponseCrcError.LogConverted(result);

        if (ResultSdmmc.ResponseTimeoutError.Includes(result))
            return ResultFs.PortSdCardResponseTimeoutError.LogConverted(result);

        if (ResultSdmmc.DataEndBitError.Includes(result))
            return ResultFs.PortSdCardDataEndBitError.LogConverted(result);

        if (ResultSdmmc.DataCrcError.Includes(result))
            return ResultFs.PortSdCardDataCrcError.LogConverted(result);

        if (ResultSdmmc.DataTimeoutError.Includes(result))
            return ResultFs.PortSdCardDataTimeoutError.LogConverted(result);

        if (ResultSdmmc.AutoCommandResponseIndexError.Includes(result))
            return ResultFs.PortSdCardAutoCommandResponseIndexError.LogConverted(result);

        if (ResultSdmmc.AutoCommandResponseEndBitError.Includes(result))
            return ResultFs.PortSdCardAutoCommandResponseEndBitError.LogConverted(result);

        if (ResultSdmmc.AutoCommandResponseCrcError.Includes(result))
            return ResultFs.PortSdCardAutoCommandResponseCrcError.LogConverted(result);

        if (ResultSdmmc.AutoCommandResponseTimeoutError.Includes(result))
            return ResultFs.PortSdCardAutoCommandResponseTimeoutError.LogConverted(result);

        if (ResultSdmmc.CommandCompleteSoftwareTimeout.Includes(result))
            return ResultFs.PortSdCardCommandCompleteSwTimeout.LogConverted(result);

        if (ResultSdmmc.TransferCompleteSoftwareTimeout.Includes(result))
            return ResultFs.PortSdCardTransferCompleteSwTimeout.LogConverted(result);


        if (ResultSdmmc.DeviceStatusAddressOutOfRange.Includes(result))
            return ResultFs.PortSdCardDeviceStatusAddressOutOfRange.LogConverted(result);

        if (ResultSdmmc.DeviceStatusAddressMisaligned.Includes(result))
            return ResultFs.PortSdCardDeviceStatusAddressMisalign.LogConverted(result);

        if (ResultSdmmc.DeviceStatusBlockLenError.Includes(result))
            return ResultFs.PortSdCardDeviceStatusBlockLenError.LogConverted(result);

        if (ResultSdmmc.DeviceStatusEraseSeqError.Includes(result))
            return ResultFs.PortSdCardDeviceStatusEraseSeqError.LogConverted(result);

        if (ResultSdmmc.DeviceStatusEraseParam.Includes(result))
            return ResultFs.PortSdCardDeviceStatusEraseParam.LogConverted(result);

        if (ResultSdmmc.DeviceStatusWpViolation.Includes(result))
            return ResultFs.PortSdCardDeviceStatusWpViolation.LogConverted(result);

        if (ResultSdmmc.DeviceStatusLockUnlockFailed.Includes(result))
            return ResultFs.PortSdCardDeviceStatusLockUnlockFailed.LogConverted(result);

        if (ResultSdmmc.DeviceStatusComCrcError.Includes(result))
            return ResultFs.PortSdCardDeviceStatusComCrcError.LogConverted(result);

        if (ResultSdmmc.DeviceStatusIllegalCommand.Includes(result))
            return ResultFs.PortSdCardDeviceStatusIllegalCommand.LogConverted(result);

        if (ResultSdmmc.DeviceStatusDeviceEccFailed.Includes(result))
            return ResultFs.PortSdCardDeviceStatusDeviceEccFailed.LogConverted(result);

        if (ResultSdmmc.DeviceStatusCcError.Includes(result))
            return ResultFs.PortSdCardDeviceStatusCcError.LogConverted(result);

        if (ResultSdmmc.DeviceStatusError.Includes(result))
            return ResultFs.PortSdCardDeviceStatusError.LogConverted(result);

        if (ResultSdmmc.DeviceStatusCidCsdOverwrite.Includes(result))
            return ResultFs.PortSdCardDeviceStatusCidCsdOverwrite.LogConverted(result);

        if (ResultSdmmc.DeviceStatusWpEraseSkip.Includes(result))
            return ResultFs.PortSdCardDeviceStatusWpEraseSkip.LogConverted(result);

        if (ResultSdmmc.DeviceStatusEraseReset.Includes(result))
            return ResultFs.PortSdCardDeviceStatusEraseReset.LogConverted(result);

        if (ResultSdmmc.DeviceStatusSwitchError.Includes(result))
            return ResultFs.PortSdCardDeviceStatusSwitchError.LogConverted(result);


        if (ResultSdmmc.UnexpectedDeviceState.Includes(result))
            return ResultFs.PortSdCardUnexpectedDeviceState.LogConverted(result);

        if (ResultSdmmc.UnexpectedDeviceCsdValue.Includes(result))
            return ResultFs.PortSdCardUnexpectedDeviceCsdValue.LogConverted(result);

        if (ResultSdmmc.AbortTransactionSoftwareTimeout.Includes(result))
            return ResultFs.PortSdCardAbortTransactionSwTimeout.LogConverted(result);

        if (ResultSdmmc.CommandInhibitCmdSoftwareTimeout.Includes(result))
            return ResultFs.PortSdCardCommandInhibitCmdSwTimeout.LogConverted(result);

        if (ResultSdmmc.CommandInhibitDatSoftwareTimeout.Includes(result))
            return ResultFs.PortSdCardCommandInhibitDatSwTimeout.LogConverted(result);

        if (ResultSdmmc.BusySoftwareTimeout.Includes(result))
            return ResultFs.PortSdCardBusySwTimeout.LogConverted(result);

        if (ResultSdmmc.IssueTuningCommandSoftwareTimeout.Includes(result))
            return ResultFs.PortSdCardIssueTuningCommandSwTimeout.LogConverted(result);

        if (ResultSdmmc.TuningFailed.Includes(result))
            return ResultFs.PortSdCardTuningFailed.LogConverted(result);

        if (ResultSdmmc.MmcInitializationSoftwareTimeout.Includes(result))
            return ResultFs.PortSdCardMmcInitializationSwTimeout.LogConverted(result);

        if (ResultSdmmc.MmcNotSupportExtendedCsd.Includes(result))
            return ResultFs.PortSdCardMmcNotSupportExtendedCsd.LogConverted(result);

        if (ResultSdmmc.UnexpectedMmcExtendedCsdValue.Includes(result))
            return ResultFs.PortSdCardUnexpectedMmcExtendedCsdValue.LogConverted(result);

        if (ResultSdmmc.MmcEraseSoftwareTimeout.Includes(result))
            return ResultFs.PortSdCardMmcEraseSwTimeout.LogConverted(result);

        if (ResultSdmmc.SdCardValidationError.Includes(result))
            return ResultFs.PortSdCardSdCardValidationError.LogConverted(result);

        if (ResultSdmmc.SdCardInitializationSoftwareTimeout.Includes(result))
            return ResultFs.PortSdCardSdCardInitializationSwTimeout.LogConverted(result);

        if (ResultSdmmc.SdCardGetValidRcaSoftwareTimeout.Includes(result))
            return ResultFs.PortSdCardSdCardGetValidRcaSwTimeout.LogConverted(result);

        if (ResultSdmmc.UnexpectedSdCardAcmdDisabled.Includes(result))
            return ResultFs.PortSdCardUnexpectedSdCardAcmdDisabled.LogConverted(result);

        if (ResultSdmmc.SdCardNotSupportSwitchFunctionStatus.Includes(result))
            return ResultFs.PortSdCardSdCardNotSupportSwitchFunctionStatus.LogConverted(result);

        if (ResultSdmmc.UnexpectedSdCardSwitchFunctionStatus.Includes(result))
            return ResultFs.PortSdCardUnexpectedSdCardSwitchFunctionStatus.LogConverted(result);

        if (ResultSdmmc.SdCardNotSupportAccessMode.Includes(result))
            return ResultFs.PortSdCardSdCardNotSupportAccessMode.LogConverted(result);

        if (ResultSdmmc.SdCardNot4BitBusWidthAtUhsIMode.Includes(result))
            return ResultFs.PortSdCardSdCardNot4BitBusWidthAtUhsIMode.LogConverted(result);

        if (ResultSdmmc.SdCardNotSupportSdr104AndSdr50.Includes(result))
            return ResultFs.PortSdCardSdCardNotSupportSdr104AndSdr50.LogConverted(result);

        if (ResultSdmmc.SdCardCannotSwitchAccessMode.Includes(result))
            return ResultFs.PortSdCardSdCardCannotSwitchedAccessMode.LogConverted(result);

        if (ResultSdmmc.SdCardFailedSwitchAccessMode.Includes(result))
            return ResultFs.PortSdCardSdCardFailedSwitchedAccessMode.LogConverted(result);

        if (ResultSdmmc.SdCardUnacceptableCurrentConsumption.Includes(result))
            return ResultFs.PortSdCardSdCardUnacceptableCurrentConsumption.LogConverted(result);

        if (ResultSdmmc.SdCardNotReadyToVoltageSwitch.Includes(result))
            return ResultFs.PortSdCardSdCardNotReadyToVoltageSwitch.LogConverted(result);

        if (ResultSdmmc.SdCardNotCompleteVoltageSwitch.Includes(result))
            return ResultFs.PortSdCardSdCardNotCompleteVoltageSwitch.LogConverted(result);


        if (ResultSdmmc.InternalClockStableSoftwareTimeout.Includes(result))
            return ResultFs.PortSdCardInternalClockStableSwTimeout.LogConverted(result);

        if (ResultSdmmc.SdHostStandardUnknownAutoCmdError.Includes(result))
            return ResultFs.PortSdCardSdHostStandardUnknownAutoCmdError.LogConverted(result);

        if (ResultSdmmc.SdHostStandardUnknownError.Includes(result))
            return ResultFs.PortSdCardSdHostStandardUnknownError.LogConverted(result);

        if (ResultSdmmc.SdmmcDllCalibrationSoftwareTimeout.Includes(result))
            return ResultFs.PortSdCardSdmmcDllCalibrationSwTimeout.LogConverted(result);

        if (ResultSdmmc.SdmmcDllApplicationSoftwareTimeout.Includes(result))
            return ResultFs.PortSdCardSdmmcDllApplicationSwTimeout.LogConverted(result);

        if (ResultSdmmc.SdHostStandardFailSwitchTo18V.Includes(result))
            return ResultFs.PortSdCardSdHostStandardFailSwitchTo18V.LogConverted(result);


        if (ResultSdmmc.NoWaitedInterrupt.Includes(result))
            return ResultFs.PortSdCardNoWaitedInterrupt.LogConverted(result);

        if (ResultSdmmc.WaitInterruptSoftwareTimeout.Includes(result))
            return ResultFs.PortSdCardWaitInterruptSwTimeout.LogConverted(result);


        if (ResultSdmmc.AbortCommandIssued.Includes(result))
            return ResultFs.PortSdCardAbortCommandIssued.LogConverted(result);


        if (ResultSdmmc.NotSupported.Includes(result))
            return ResultFs.PortSdCardNotSupported.LogConverted(result);

        if (ResultSdmmc.NotImplemented.Includes(result))
            return ResultFs.PortSdCardNotImplemented.LogConverted(result);

        return ResultFs.PortSdCardUnexpected.LogConverted(result);
    }
}