//-----------------------------------------------------------------------------
// This file was automatically generated.
// Changes to this file will be lost when the file is regenerated.
//
// To change this file, modify /build/CodeGen/results.csv at the root of this
// repo and run the build script.
//
// The script can be run with the "codegen" option to run only the
// code generation portion of the build.
//-----------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace LibHac.Sdmmc
{
    public static class ResultSdmmc
    {
        public const int ModuleSdmmc = 24;

        /// <summary>Error code: 2024-0001; Inner value: 0x218</summary>
        public static Result.Base NoDevice => new Result.Base(ModuleSdmmc, 1);
        /// <summary>Error code: 2024-0002; Inner value: 0x418</summary>
        public static Result.Base NotActivated => new Result.Base(ModuleSdmmc, 2);
        /// <summary>Error code: 2024-0003; Inner value: 0x618</summary>
        public static Result.Base DeviceRemoved => new Result.Base(ModuleSdmmc, 3);
        /// <summary>Error code: 2024-0004; Inner value: 0x818</summary>
        public static Result.Base NotAwakened => new Result.Base(ModuleSdmmc, 4);

        /// <summary>Error code: 2024-0032; Range: 32-126; Inner value: 0x4018</summary>
        public static Result.Base CommunicationError => new Result.Base(ModuleSdmmc, 32, 126);
            /// <summary>Error code: 2024-0033; Range: 33-46; Inner value: 0x4218</summary>
            public static Result.Base CommunicationNotAttained => new Result.Base(ModuleSdmmc, 33, 46);
                /// <summary>Error code: 2024-0034; Inner value: 0x4418</summary>
                public static Result.Base ResponseIndexError => new Result.Base(ModuleSdmmc, 34);
                /// <summary>Error code: 2024-0035; Inner value: 0x4618</summary>
                public static Result.Base ResponseEndBitError => new Result.Base(ModuleSdmmc, 35);
                /// <summary>Error code: 2024-0036; Inner value: 0x4818</summary>
                public static Result.Base ResponseCrcError => new Result.Base(ModuleSdmmc, 36);
                /// <summary>Error code: 2024-0037; Inner value: 0x4a18</summary>
                public static Result.Base ResponseTimeoutError => new Result.Base(ModuleSdmmc, 37);
                /// <summary>Error code: 2024-0038; Inner value: 0x4c18</summary>
                public static Result.Base DataEndBitError => new Result.Base(ModuleSdmmc, 38);
                /// <summary>Error code: 2024-0039; Inner value: 0x4e18</summary>
                public static Result.Base DataCrcError => new Result.Base(ModuleSdmmc, 39);
                /// <summary>Error code: 2024-0040; Inner value: 0x5018</summary>
                public static Result.Base DataTimeoutError => new Result.Base(ModuleSdmmc, 40);
                /// <summary>Error code: 2024-0041; Inner value: 0x5218</summary>
                public static Result.Base AutoCommandResponseIndexError => new Result.Base(ModuleSdmmc, 41);
                /// <summary>Error code: 2024-0042; Inner value: 0x5418</summary>
                public static Result.Base AutoCommandResponseEndBitError => new Result.Base(ModuleSdmmc, 42);
                /// <summary>Error code: 2024-0043; Inner value: 0x5618</summary>
                public static Result.Base AutoCommandResponseCrcError => new Result.Base(ModuleSdmmc, 43);
                /// <summary>Error code: 2024-0044; Inner value: 0x5818</summary>
                public static Result.Base AutoCommandResponseTimeoutError => new Result.Base(ModuleSdmmc, 44);
                /// <summary>Error code: 2024-0045; Inner value: 0x5a18</summary>
                public static Result.Base CommandCompleteSoftwareTimeout => new Result.Base(ModuleSdmmc, 45);
                /// <summary>Error code: 2024-0046; Inner value: 0x5c18</summary>
                public static Result.Base TransferCompleteSoftwareTimeout => new Result.Base(ModuleSdmmc, 46);

            /// <summary>Error code: 2024-0048; Range: 48-70; Inner value: 0x6018</summary>
            public static Result.Base DeviceStatusHasError => new Result.Base(ModuleSdmmc, 48, 70);
                /// <summary>Error code: 2024-0049; Inner value: 0x6218</summary>
                public static Result.Base DeviceStatusAddressOutOfRange => new Result.Base(ModuleSdmmc, 49);
                /// <summary>Error code: 2024-0050; Inner value: 0x6418</summary>
                public static Result.Base DeviceStatusAddressMisaligned => new Result.Base(ModuleSdmmc, 50);
                /// <summary>Error code: 2024-0051; Inner value: 0x6618</summary>
                public static Result.Base DeviceStatusBlockLenError => new Result.Base(ModuleSdmmc, 51);
                /// <summary>Error code: 2024-0052; Inner value: 0x6818</summary>
                public static Result.Base DeviceStatusEraseSeqError => new Result.Base(ModuleSdmmc, 52);
                /// <summary>Error code: 2024-0053; Inner value: 0x6a18</summary>
                public static Result.Base DeviceStatusEraseParam => new Result.Base(ModuleSdmmc, 53);
                /// <summary>Error code: 2024-0054; Inner value: 0x6c18</summary>
                public static Result.Base DeviceStatusWpViolation => new Result.Base(ModuleSdmmc, 54);
                /// <summary>Error code: 2024-0055; Inner value: 0x6e18</summary>
                public static Result.Base DeviceStatusLockUnlockFailed => new Result.Base(ModuleSdmmc, 55);
                /// <summary>Error code: 2024-0056; Inner value: 0x7018</summary>
                public static Result.Base DeviceStatusComCrcError => new Result.Base(ModuleSdmmc, 56);
                /// <summary>Error code: 2024-0057; Inner value: 0x7218</summary>
                public static Result.Base DeviceStatusIllegalCommand => new Result.Base(ModuleSdmmc, 57);
                /// <summary>Error code: 2024-0058; Inner value: 0x7418</summary>
                public static Result.Base DeviceStatusDeviceEccFailed => new Result.Base(ModuleSdmmc, 58);
                /// <summary>Error code: 2024-0059; Inner value: 0x7618</summary>
                public static Result.Base DeviceStatusCcError => new Result.Base(ModuleSdmmc, 59);
                /// <summary>Error code: 2024-0060; Inner value: 0x7818</summary>
                public static Result.Base DeviceStatusError => new Result.Base(ModuleSdmmc, 60);
                /// <summary>Error code: 2024-0061; Inner value: 0x7a18</summary>
                public static Result.Base DeviceStatusCidCsdOverwrite => new Result.Base(ModuleSdmmc, 61);
                /// <summary>Error code: 2024-0062; Inner value: 0x7c18</summary>
                public static Result.Base DeviceStatusWpEraseSkip => new Result.Base(ModuleSdmmc, 62);
                /// <summary>Error code: 2024-0063; Inner value: 0x7e18</summary>
                public static Result.Base DeviceStatusEraseReset => new Result.Base(ModuleSdmmc, 63);
                /// <summary>Error code: 2024-0064; Inner value: 0x8018</summary>
                public static Result.Base DeviceStatusSwitchError => new Result.Base(ModuleSdmmc, 64);

            /// <summary>Error code: 2024-0072; Inner value: 0x9018</summary>
            public static Result.Base UnexpectedDeviceState => new Result.Base(ModuleSdmmc, 72);
            /// <summary>Error code: 2024-0073; Inner value: 0x9218</summary>
            public static Result.Base UnexpectedDeviceCsdValue => new Result.Base(ModuleSdmmc, 73);
            /// <summary>Error code: 2024-0074; Inner value: 0x9418</summary>
            public static Result.Base AbortTransactionSoftwareTimeout => new Result.Base(ModuleSdmmc, 74);
            /// <summary>Error code: 2024-0075; Inner value: 0x9618</summary>
            public static Result.Base CommandInhibitCmdSoftwareTimeout => new Result.Base(ModuleSdmmc, 75);
            /// <summary>Error code: 2024-0076; Inner value: 0x9818</summary>
            public static Result.Base CommandInhibitDatSoftwareTimeout => new Result.Base(ModuleSdmmc, 76);
            /// <summary>Error code: 2024-0077; Inner value: 0x9a18</summary>
            public static Result.Base BusySoftwareTimeout => new Result.Base(ModuleSdmmc, 77);
            /// <summary>Error code: 2024-0078; Inner value: 0x9c18</summary>
            public static Result.Base IssueTuningCommandSoftwareTimeout => new Result.Base(ModuleSdmmc, 78);
            /// <summary>Error code: 2024-0079; Inner value: 0x9e18</summary>
            public static Result.Base TuningFailed => new Result.Base(ModuleSdmmc, 79);
            /// <summary>Error code: 2024-0080; Inner value: 0xa018</summary>
            public static Result.Base MmcInitializationSoftwareTimeout => new Result.Base(ModuleSdmmc, 80);
            /// <summary>Error code: 2024-0081; Inner value: 0xa218</summary>
            public static Result.Base MmcNotSupportExtendedCsd => new Result.Base(ModuleSdmmc, 81);
            /// <summary>Error code: 2024-0082; Inner value: 0xa418</summary>
            public static Result.Base UnexpectedMmcExtendedCsdValue => new Result.Base(ModuleSdmmc, 82);
            /// <summary>Error code: 2024-0083; Inner value: 0xa618</summary>
            public static Result.Base MmcEraseSoftwareTimeout => new Result.Base(ModuleSdmmc, 83);
            /// <summary>Error code: 2024-0084; Inner value: 0xa818</summary>
            public static Result.Base SdCardValidationError => new Result.Base(ModuleSdmmc, 84);
            /// <summary>Error code: 2024-0085; Inner value: 0xaa18</summary>
            public static Result.Base SdCardInitializationSoftwareTimeout => new Result.Base(ModuleSdmmc, 85);
            /// <summary>Error code: 2024-0086; Inner value: 0xac18</summary>
            public static Result.Base SdCardGetValidRcaSoftwareTimeout => new Result.Base(ModuleSdmmc, 86);
            /// <summary>Error code: 2024-0087; Inner value: 0xae18</summary>
            public static Result.Base UnexpectedSdCardAcmdDisabled => new Result.Base(ModuleSdmmc, 87);
            /// <summary>Error code: 2024-0088; Inner value: 0xb018</summary>
            public static Result.Base SdCardNotSupportSwitchFunctionStatus => new Result.Base(ModuleSdmmc, 88);
            /// <summary>Error code: 2024-0089; Inner value: 0xb218</summary>
            public static Result.Base UnexpectedSdCardSwitchFunctionStatus => new Result.Base(ModuleSdmmc, 89);
            /// <summary>Error code: 2024-0090; Inner value: 0xb418</summary>
            public static Result.Base SdCardNotSupportAccessMode => new Result.Base(ModuleSdmmc, 90);
            /// <summary>Error code: 2024-0091; Inner value: 0xb618</summary>
            public static Result.Base SdCardNot4BitBusWidthAtUhsIMode => new Result.Base(ModuleSdmmc, 91);
            /// <summary>Error code: 2024-0092; Inner value: 0xb818</summary>
            public static Result.Base SdCardNotSupportSdr104AndSdr50 => new Result.Base(ModuleSdmmc, 92);
            /// <summary>Error code: 2024-0093; Inner value: 0xba18</summary>
            public static Result.Base SdCardCannotSwitchAccessMode => new Result.Base(ModuleSdmmc, 93);
            /// <summary>Error code: 2024-0094; Inner value: 0xbc18</summary>
            public static Result.Base SdCardFailedSwitchAccessMode => new Result.Base(ModuleSdmmc, 94);
            /// <summary>Error code: 2024-0095; Inner value: 0xbe18</summary>
            public static Result.Base SdCardUnacceptableCurrentConsumption => new Result.Base(ModuleSdmmc, 95);
            /// <summary>Error code: 2024-0096; Inner value: 0xc018</summary>
            public static Result.Base SdCardNotReadyToVoltageSwitch => new Result.Base(ModuleSdmmc, 96);
            /// <summary>Error code: 2024-0097; Inner value: 0xc218</summary>
            public static Result.Base SdCardNotCompleteVoltageSwitch => new Result.Base(ModuleSdmmc, 97);

        /// <summary>Error code: 2024-0128; Range: 128-158; Inner value: 0x10018</summary>
        public static Result.Base HostControllerUnexpected { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleSdmmc, 128, 158); }
            /// <summary>Error code: 2024-0129; Inner value: 0x10218</summary>
            public static Result.Base InternalClockStableSoftwareTimeout => new Result.Base(ModuleSdmmc, 129);
            /// <summary>Error code: 2024-0130; Inner value: 0x10418</summary>
            public static Result.Base SdHostStandardUnknownAutoCmdError => new Result.Base(ModuleSdmmc, 130);
            /// <summary>Error code: 2024-0131; Inner value: 0x10618</summary>
            public static Result.Base SdHostStandardUnknownError => new Result.Base(ModuleSdmmc, 131);
            /// <summary>Error code: 2024-0132; Inner value: 0x10818</summary>
            public static Result.Base SdmmcDllCalibrationSoftwareTimeout => new Result.Base(ModuleSdmmc, 132);
            /// <summary>Error code: 2024-0133; Inner value: 0x10a18</summary>
            public static Result.Base SdmmcDllApplicationSoftwareTimeout => new Result.Base(ModuleSdmmc, 133);
            /// <summary>Error code: 2024-0134; Inner value: 0x10c18</summary>
            public static Result.Base SdHostStandardFailSwitchTo18V => new Result.Base(ModuleSdmmc, 134);
            /// <summary>Error code: 2024-0135; Inner value: 0x10e18</summary>
            public static Result.Base DriveStrengthCalibrationNotCompleted => new Result.Base(ModuleSdmmc, 135);
            /// <summary>Error code: 2024-0136; Inner value: 0x11018</summary>
            public static Result.Base DriveStrengthCalibrationSoftwareTimeout => new Result.Base(ModuleSdmmc, 136);
            /// <summary>Error code: 2024-0137; Inner value: 0x11218</summary>
            public static Result.Base SdmmcCompShortToGnd => new Result.Base(ModuleSdmmc, 137);
            /// <summary>Error code: 2024-0138; Inner value: 0x11418</summary>
            public static Result.Base SdmmcCompOpen => new Result.Base(ModuleSdmmc, 138);

        /// <summary>Error code: 2024-0160; Range: 160-190; Inner value: 0x14018</summary>
        public static Result.Base InternalError { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleSdmmc, 160, 190); }
            /// <summary>Error code: 2024-0161; Inner value: 0x14218</summary>
            public static Result.Base NoWaitedInterrupt => new Result.Base(ModuleSdmmc, 161);
            /// <summary>Error code: 2024-0162; Inner value: 0x14418</summary>
            public static Result.Base WaitInterruptSoftwareTimeout => new Result.Base(ModuleSdmmc, 162);

        /// <summary>Error code: 2024-0192; Inner value: 0x18018</summary>
        public static Result.Base AbortCommandIssued => new Result.Base(ModuleSdmmc, 192);
        /// <summary>Error code: 2024-0200; Inner value: 0x19018</summary>
        public static Result.Base NotSupported => new Result.Base(ModuleSdmmc, 200);
        /// <summary>Error code: 2024-0201; Inner value: 0x19218</summary>
        public static Result.Base NotImplemented => new Result.Base(ModuleSdmmc, 201);
    }
}
