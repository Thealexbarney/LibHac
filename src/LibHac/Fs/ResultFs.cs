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

namespace LibHac.Fs
{
    public static class ResultFs
    {
        public const int ModuleFs = 2;

        /// <summary>Error code: 2002-0000; Range: 0-999; Inner value: 0x2</summary>
        public static Result.Base HandledByAllProcess => new Result.Base(ModuleFs, 0, 999);
            /// <summary>Specified path does not exist<br/>Error code: 2002-0001; Inner value: 0x202</summary>
            public static Result.Base PathNotFound => new Result.Base(ModuleFs, 1);
            /// <summary>Specified path already exists<br/>Error code: 2002-0002; Inner value: 0x402</summary>
            public static Result.Base PathAlreadyExists => new Result.Base(ModuleFs, 2);
            /// <summary>Resource already in use (file already opened, savedata filesystem already mounted)<br/>Error code: 2002-0007; Inner value: 0xe02</summary>
            public static Result.Base TargetLocked => new Result.Base(ModuleFs, 7);
            /// <summary>Specified directory is not empty when trying to delete it<br/>Error code: 2002-0008; Inner value: 0x1002</summary>
            public static Result.Base DirectoryNotEmpty => new Result.Base(ModuleFs, 8);
            /// <summary>Error code: 2002-0013; Inner value: 0x1a02</summary>
            public static Result.Base DirectoryStatusChanged => new Result.Base(ModuleFs, 13);

            /// <summary>Error code: 2002-0030; Range: 30-45; Inner value: 0x3c02</summary>
            public static Result.Base UsableSpaceNotEnough => new Result.Base(ModuleFs, 30, 45);
                /// <summary>Error code: 2002-0031; Inner value: 0x3e02</summary>
                public static Result.Base UsableSpaceNotEnoughForSaveData => new Result.Base(ModuleFs, 31);
                /// <summary>Error code: 2002-0032; Inner value: 0x4002</summary>
                public static Result.Base UsableSpaceNotEnoughAfterDataErase => new Result.Base(ModuleFs, 32);
                /// <summary>Error code: 2002-0033; Inner value: 0x4202</summary>
                public static Result.Base UsableSpaceNotEnoughForCacheStorage => new Result.Base(ModuleFs, 33);

                /// <summary>Error code: 2002-0034; Range: 34-38; Inner value: 0x4402</summary>
                public static Result.Base UsableSpaceNotEnoughForBis => new Result.Base(ModuleFs, 34, 38);
                    /// <summary>Error code: 2002-0035; Inner value: 0x4602</summary>
                    public static Result.Base UsableSpaceNotEnoughForBisCalibration => new Result.Base(ModuleFs, 35);
                    /// <summary>Error code: 2002-0036; Inner value: 0x4802</summary>
                    public static Result.Base UsableSpaceNotEnoughForBisSafe => new Result.Base(ModuleFs, 36);
                    /// <summary>Error code: 2002-0037; Inner value: 0x4a02</summary>
                    public static Result.Base UsableSpaceNotEnoughForBisUser => new Result.Base(ModuleFs, 37);
                    /// <summary>Error code: 2002-0038; Inner value: 0x4c02</summary>
                    public static Result.Base UsableSpaceNotEnoughForBisSystem => new Result.Base(ModuleFs, 38);

                /// <summary>Error code: 2002-0039; Inner value: 0x4e02</summary>
                public static Result.Base UsableSpaceNotEnoughForSdCard => new Result.Base(ModuleFs, 39);

            /// <summary>Error code: 2002-0050; Inner value: 0x6402</summary>
            public static Result.Base UnsupportedSdkVersion => new Result.Base(ModuleFs, 50);
            /// <summary>Error code: 2002-0060; Inner value: 0x7802</summary>
            public static Result.Base MountNameAlreadyExists => new Result.Base(ModuleFs, 60);
            /// <summary>Error code: 2002-0070; Inner value: 0x8c02</summary>
            public static Result.Base IndividualFileDataCacheAlreadyEnabled => new Result.Base(ModuleFs, 70);

        /// <summary>Error code: 2002-1000; Range: 1000-2999; Inner value: 0x7d002</summary>
        public static Result.Base HandledBySystemProcess { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 1000, 2999); }
            /// <summary>Error code: 2002-1001; Inner value: 0x7d202</summary>
            public static Result.Base PartitionNotFound => new Result.Base(ModuleFs, 1001);
            /// <summary>Error code: 2002-1002; Inner value: 0x7d402</summary>
            public static Result.Base TargetNotFound => new Result.Base(ModuleFs, 1002);
            /// <summary>Error code: 2002-1003; Inner value: 0x7d602</summary>
            public static Result.Base MmcPatrolDataNotInitialized => new Result.Base(ModuleFs, 1003);
            /// <summary>The requested external key was not found<br/>Error code: 2002-1004; Inner value: 0x7d802</summary>
            public static Result.Base NcaExternalKeyUnavailable => new Result.Base(ModuleFs, 1004);

            /// <summary>Error code: 2002-2000; Range: 2000-2499; Inner value: 0xfa002</summary>
            public static Result.Base SdCardAccessFailed { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 2000, 2499); }
                /// <summary>Error code: 2002-2001; Inner value: 0xfa202</summary>
                public static Result.Base PortSdCardNoDevice => new Result.Base(ModuleFs, 2001);
                /// <summary>Error code: 2002-2002; Inner value: 0xfa402</summary>
                public static Result.Base PortSdCardNotActivated => new Result.Base(ModuleFs, 2002);
                /// <summary>Error code: 2002-2003; Inner value: 0xfa602</summary>
                public static Result.Base PortSdCardDeviceRemoved => new Result.Base(ModuleFs, 2003);
                /// <summary>Error code: 2002-2004; Inner value: 0xfa802</summary>
                public static Result.Base PortSdCardNotAwakened => new Result.Base(ModuleFs, 2004);

                /// <summary>Error code: 2002-2032; Range: 2032-2126; Inner value: 0xfe002</summary>
                public static Result.Base PortSdCardCommunicationError { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 2032, 2126); }
                    /// <summary>Error code: 2002-2033; Range: 2033-2046; Inner value: 0xfe202</summary>
                    public static Result.Base PortSdCardCommunicationNotAttained { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 2033, 2046); }
                        /// <summary>Error code: 2002-2034; Inner value: 0xfe402</summary>
                        public static Result.Base PortSdCardResponseIndexError => new Result.Base(ModuleFs, 2034);
                        /// <summary>Error code: 2002-2035; Inner value: 0xfe602</summary>
                        public static Result.Base PortSdCardResponseEndBitError => new Result.Base(ModuleFs, 2035);
                        /// <summary>Error code: 2002-2036; Inner value: 0xfe802</summary>
                        public static Result.Base PortSdCardResponseCrcError => new Result.Base(ModuleFs, 2036);
                        /// <summary>Error code: 2002-2037; Inner value: 0xfea02</summary>
                        public static Result.Base PortSdCardResponseTimeoutError => new Result.Base(ModuleFs, 2037);
                        /// <summary>Error code: 2002-2038; Inner value: 0xfec02</summary>
                        public static Result.Base PortSdCardDataEndBitError => new Result.Base(ModuleFs, 2038);
                        /// <summary>Error code: 2002-2039; Inner value: 0xfee02</summary>
                        public static Result.Base PortSdCardDataCrcError => new Result.Base(ModuleFs, 2039);
                        /// <summary>Error code: 2002-2040; Inner value: 0xff002</summary>
                        public static Result.Base PortSdCardDataTimeoutError => new Result.Base(ModuleFs, 2040);
                        /// <summary>Error code: 2002-2041; Inner value: 0xff202</summary>
                        public static Result.Base PortSdCardAutoCommandResponseIndexError => new Result.Base(ModuleFs, 2041);
                        /// <summary>Error code: 2002-2042; Inner value: 0xff402</summary>
                        public static Result.Base PortSdCardAutoCommandResponseEndBitError => new Result.Base(ModuleFs, 2042);
                        /// <summary>Error code: 2002-2043; Inner value: 0xff602</summary>
                        public static Result.Base PortSdCardAutoCommandResponseCrcError => new Result.Base(ModuleFs, 2043);
                        /// <summary>Error code: 2002-2044; Inner value: 0xff802</summary>
                        public static Result.Base PortSdCardAutoCommandResponseTimeoutError => new Result.Base(ModuleFs, 2044);
                        /// <summary>Error code: 2002-2045; Inner value: 0xffa02</summary>
                        public static Result.Base PortSdCardCommandCompleteSoftwareTimeout => new Result.Base(ModuleFs, 2045);
                        /// <summary>Error code: 2002-2046; Inner value: 0xffc02</summary>
                        public static Result.Base PortSdCardTransferCompleteSoftwareTimeout => new Result.Base(ModuleFs, 2046);

                    /// <summary>Error code: 2002-2048; Range: 2048-2070; Inner value: 0x100002</summary>
                    public static Result.Base PortSdCardDeviceStatusHasError { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 2048, 2070); }
                        /// <summary>Error code: 2002-2049; Inner value: 0x100202</summary>
                        public static Result.Base PortSdCardDeviceStatusAddressOutOfRange => new Result.Base(ModuleFs, 2049);
                        /// <summary>Error code: 2002-2050; Inner value: 0x100402</summary>
                        public static Result.Base PortSdCardDeviceStatusAddressMisaligned => new Result.Base(ModuleFs, 2050);
                        /// <summary>Error code: 2002-2051; Inner value: 0x100602</summary>
                        public static Result.Base PortSdCardDeviceStatusBlockLenError => new Result.Base(ModuleFs, 2051);
                        /// <summary>Error code: 2002-2052; Inner value: 0x100802</summary>
                        public static Result.Base PortSdCardDeviceStatusEraseSeqError => new Result.Base(ModuleFs, 2052);
                        /// <summary>Error code: 2002-2053; Inner value: 0x100a02</summary>
                        public static Result.Base PortSdCardDeviceStatusEraseParam => new Result.Base(ModuleFs, 2053);
                        /// <summary>Error code: 2002-2054; Inner value: 0x100c02</summary>
                        public static Result.Base PortSdCardDeviceStatusWpViolation => new Result.Base(ModuleFs, 2054);
                        /// <summary>Error code: 2002-2055; Inner value: 0x100e02</summary>
                        public static Result.Base PortSdCardDeviceStatusLockUnlockFailed => new Result.Base(ModuleFs, 2055);
                        /// <summary>Error code: 2002-2056; Inner value: 0x101002</summary>
                        public static Result.Base PortSdCardDeviceStatusComCrcError => new Result.Base(ModuleFs, 2056);
                        /// <summary>Error code: 2002-2057; Inner value: 0x101202</summary>
                        public static Result.Base PortSdCardDeviceStatusIllegalCommand => new Result.Base(ModuleFs, 2057);
                        /// <summary>Error code: 2002-2058; Inner value: 0x101402</summary>
                        public static Result.Base PortSdCardDeviceStatusDeviceEccFailed => new Result.Base(ModuleFs, 2058);
                        /// <summary>Error code: 2002-2059; Inner value: 0x101602</summary>
                        public static Result.Base PortSdCardDeviceStatusCcError => new Result.Base(ModuleFs, 2059);
                        /// <summary>Error code: 2002-2060; Inner value: 0x101802</summary>
                        public static Result.Base PortSdCardDeviceStatusError => new Result.Base(ModuleFs, 2060);
                        /// <summary>Error code: 2002-2061; Inner value: 0x101a02</summary>
                        public static Result.Base PortSdCardDeviceStatusCidCsdOverwrite => new Result.Base(ModuleFs, 2061);
                        /// <summary>Error code: 2002-2062; Inner value: 0x101c02</summary>
                        public static Result.Base PortSdCardDeviceStatusWpEraseSkip => new Result.Base(ModuleFs, 2062);
                        /// <summary>Error code: 2002-2063; Inner value: 0x101e02</summary>
                        public static Result.Base PortSdCardDeviceStatusEraseReset => new Result.Base(ModuleFs, 2063);
                        /// <summary>Error code: 2002-2064; Inner value: 0x102002</summary>
                        public static Result.Base PortSdCardDeviceStatusSwitchError => new Result.Base(ModuleFs, 2064);

                    /// <summary>Error code: 2002-2072; Inner value: 0x103002</summary>
                    public static Result.Base PortSdCardUnexpectedDeviceState => new Result.Base(ModuleFs, 2072);
                    /// <summary>Error code: 2002-2073; Inner value: 0x103202</summary>
                    public static Result.Base PortSdCardUnexpectedDeviceCsdValue => new Result.Base(ModuleFs, 2073);
                    /// <summary>Error code: 2002-2074; Inner value: 0x103402</summary>
                    public static Result.Base PortSdCardAbortTransactionSoftwareTimeout => new Result.Base(ModuleFs, 2074);
                    /// <summary>Error code: 2002-2075; Inner value: 0x103602</summary>
                    public static Result.Base PortSdCardCommandInhibitCmdSoftwareTimeout => new Result.Base(ModuleFs, 2075);
                    /// <summary>Error code: 2002-2076; Inner value: 0x103802</summary>
                    public static Result.Base PortSdCardCommandInhibitDatSoftwareTimeout => new Result.Base(ModuleFs, 2076);
                    /// <summary>Error code: 2002-2077; Inner value: 0x103a02</summary>
                    public static Result.Base PortSdCardBusySoftwareTimeout => new Result.Base(ModuleFs, 2077);
                    /// <summary>Error code: 2002-2078; Inner value: 0x103c02</summary>
                    public static Result.Base PortSdCardIssueTuningCommandSoftwareTimeout => new Result.Base(ModuleFs, 2078);
                    /// <summary>Error code: 2002-2079; Inner value: 0x103e02</summary>
                    public static Result.Base PortSdCardTuningFailed => new Result.Base(ModuleFs, 2079);
                    /// <summary>Error code: 2002-2080; Inner value: 0x104002</summary>
                    public static Result.Base PortSdCardMmcInitializationSoftwareTimeout => new Result.Base(ModuleFs, 2080);
                    /// <summary>Error code: 2002-2081; Inner value: 0x104202</summary>
                    public static Result.Base PortSdCardMmcNotSupportExtendedCsd => new Result.Base(ModuleFs, 2081);
                    /// <summary>Error code: 2002-2082; Inner value: 0x104402</summary>
                    public static Result.Base PortSdCardUnexpectedMmcExtendedCsdValue => new Result.Base(ModuleFs, 2082);
                    /// <summary>Error code: 2002-2083; Inner value: 0x104602</summary>
                    public static Result.Base PortSdCardMmcEraseSoftwareTimeout => new Result.Base(ModuleFs, 2083);
                    /// <summary>Error code: 2002-2084; Inner value: 0x104802</summary>
                    public static Result.Base PortSdCardSdCardValidationError => new Result.Base(ModuleFs, 2084);
                    /// <summary>Error code: 2002-2085; Inner value: 0x104a02</summary>
                    public static Result.Base PortSdCardSdCardInitializationSoftwareTimeout => new Result.Base(ModuleFs, 2085);
                    /// <summary>Error code: 2002-2086; Inner value: 0x104c02</summary>
                    public static Result.Base PortSdCardSdCardGetValidRcaSoftwareTimeout => new Result.Base(ModuleFs, 2086);
                    /// <summary>Error code: 2002-2087; Inner value: 0x104e02</summary>
                    public static Result.Base PortSdCardUnexpectedSdCardAcmdDisabled => new Result.Base(ModuleFs, 2087);
                    /// <summary>Error code: 2002-2088; Inner value: 0x105002</summary>
                    public static Result.Base PortSdCardSdCardNotSupportSwitchFunctionStatus => new Result.Base(ModuleFs, 2088);
                    /// <summary>Error code: 2002-2089; Inner value: 0x105202</summary>
                    public static Result.Base PortSdCardUnexpectedSdCardSwitchFunctionStatus => new Result.Base(ModuleFs, 2089);
                    /// <summary>Error code: 2002-2090; Inner value: 0x105402</summary>
                    public static Result.Base PortSdCardSdCardNotSupportAccessMode => new Result.Base(ModuleFs, 2090);
                    /// <summary>Error code: 2002-2091; Inner value: 0x105602</summary>
                    public static Result.Base PortSdCardSdCardNot4BitBusWidthAtUhsIMode => new Result.Base(ModuleFs, 2091);
                    /// <summary>Error code: 2002-2092; Inner value: 0x105802</summary>
                    public static Result.Base PortSdCardSdCardNotSupportSdr104AndSdr50 => new Result.Base(ModuleFs, 2092);
                    /// <summary>Error code: 2002-2093; Inner value: 0x105a02</summary>
                    public static Result.Base PortSdCardSdCardCannotSwitchAccessMode => new Result.Base(ModuleFs, 2093);
                    /// <summary>Error code: 2002-2094; Inner value: 0x105c02</summary>
                    public static Result.Base PortSdCardSdCardFailedSwitchAccessMode => new Result.Base(ModuleFs, 2094);
                    /// <summary>Error code: 2002-2095; Inner value: 0x105e02</summary>
                    public static Result.Base PortSdCardSdCardUnacceptableCurrentConsumption => new Result.Base(ModuleFs, 2095);
                    /// <summary>Error code: 2002-2096; Inner value: 0x106002</summary>
                    public static Result.Base PortSdCardSdCardNotReadyToVoltageSwitch => new Result.Base(ModuleFs, 2096);
                    /// <summary>Error code: 2002-2097; Inner value: 0x106202</summary>
                    public static Result.Base PortSdCardSdCardNotCompleteVoltageSwitch => new Result.Base(ModuleFs, 2097);

                /// <summary>Error code: 2002-2128; Range: 2128-2158; Inner value: 0x10a002</summary>
                public static Result.Base PortSdCardHostControllerUnexpected { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 2128, 2158); }
                    /// <summary>Error code: 2002-2129; Inner value: 0x10a202</summary>
                    public static Result.Base PortSdCardInternalClockStableSoftwareTimeout => new Result.Base(ModuleFs, 2129);
                    /// <summary>Error code: 2002-2130; Inner value: 0x10a402</summary>
                    public static Result.Base PortSdCardSdHostStandardUnknownAutoCmdError => new Result.Base(ModuleFs, 2130);
                    /// <summary>Error code: 2002-2131; Inner value: 0x10a602</summary>
                    public static Result.Base PortSdCardSdHostStandardUnknownError => new Result.Base(ModuleFs, 2131);
                    /// <summary>Error code: 2002-2132; Inner value: 0x10a802</summary>
                    public static Result.Base PortSdCardSdmmcDllCalibrationSoftwareTimeout => new Result.Base(ModuleFs, 2132);
                    /// <summary>Error code: 2002-2133; Inner value: 0x10aa02</summary>
                    public static Result.Base PortSdCardSdmmcDllApplicationSoftwareTimeout => new Result.Base(ModuleFs, 2133);
                    /// <summary>Error code: 2002-2134; Inner value: 0x10ac02</summary>
                    public static Result.Base PortSdCardSdHostStandardFailSwitchTo18V => new Result.Base(ModuleFs, 2134);

                /// <summary>Error code: 2002-2160; Range: 2160-2190; Inner value: 0x10e002</summary>
                public static Result.Base PortSdCardInternalError { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 2160, 2190); }
                    /// <summary>Error code: 2002-2161; Inner value: 0x10e202</summary>
                    public static Result.Base PortSdCardNoWaitedInterrupt => new Result.Base(ModuleFs, 2161);
                    /// <summary>Error code: 2002-2162; Inner value: 0x10e402</summary>
                    public static Result.Base PortSdCardWaitInterruptSoftwareTimeout => new Result.Base(ModuleFs, 2162);

                /// <summary>Error code: 2002-2192; Inner value: 0x112002</summary>
                public static Result.Base PortSdCardAbortCommandIssued => new Result.Base(ModuleFs, 2192);
                /// <summary>Error code: 2002-2200; Inner value: 0x113002</summary>
                public static Result.Base PortSdCardNotSupported => new Result.Base(ModuleFs, 2200);
                /// <summary>Error code: 2002-2201; Inner value: 0x113202</summary>
                public static Result.Base PortSdCardNotImplemented => new Result.Base(ModuleFs, 2201);
                /// <summary>Error code: 2002-2496; Inner value: 0x138002</summary>
                public static Result.Base SdCardStorageDeviceInvalidated => new Result.Base(ModuleFs, 2496);
                /// <summary>Error code: 2002-2497; Inner value: 0x138202</summary>
                public static Result.Base SdCardFormatWriteVerificationFailed => new Result.Base(ModuleFs, 2497);
                /// <summary>Error code: 2002-2498; Inner value: 0x138402</summary>
                public static Result.Base SdCardFileSystemInvalidatedByRemoved => new Result.Base(ModuleFs, 2498);
                /// <summary>Error code: 2002-2499; Inner value: 0x138602</summary>
                public static Result.Base SdCardDeviceUnknownError => new Result.Base(ModuleFs, 2499);

            /// <summary>Error code: 2002-2500; Range: 2500-2999; Inner value: 0x138802</summary>
            public static Result.Base GameCardAccessFailed { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 2500, 2999); }
                /// <summary>Error code: 2002-2503; Inner value: 0x138e02</summary>
                public static Result.Base GameCardPreconditionViolation => new Result.Base(ModuleFs, 2503);
                /// <summary>Error code: 2002-2504; Inner value: 0x139002</summary>
                public static Result.Base GameCardNotImplemented => new Result.Base(ModuleFs, 2504);
                /// <summary>Error code: 2002-2510; Inner value: 0x139c02</summary>
                public static Result.Base GameCardNoAvailableLockers => new Result.Base(ModuleFs, 2510);
                /// <summary>Error code: 2002-2511; Inner value: 0x139e02</summary>
                public static Result.Base GameCardLockerIndexOutOfRange => new Result.Base(ModuleFs, 2511);
                /// <summary>Error code: 2002-2520; Inner value: 0x13b002</summary>
                public static Result.Base GameCardNotInserted => new Result.Base(ModuleFs, 2520);
                /// <summary>Error code: 2002-2521; Inner value: 0x13b202</summary>
                public static Result.Base InvalidGameCardIdInSpecificData => new Result.Base(ModuleFs, 2521);
                /// <summary>Error code: 2002-2522; Inner value: 0x13b402</summary>
                public static Result.Base GameCardCardNotActivated => new Result.Base(ModuleFs, 2522);
                /// <summary>Error code: 2002-2523; Inner value: 0x13b602</summary>
                public static Result.Base InvalidCommandForDeactivatedGameCardAsic => new Result.Base(ModuleFs, 2523);
                /// <summary>Error code: 2002-2531; Inner value: 0x13c602</summary>
                public static Result.Base GameCardCardAccessTimeout => new Result.Base(ModuleFs, 2531);
                /// <summary>Error code: 2002-2532; Inner value: 0x13c802</summary>
                public static Result.Base GameCardStatusFatalError => new Result.Base(ModuleFs, 2532);
                /// <summary>Error code: 2002-2533; Inner value: 0x13ca02</summary>
                public static Result.Base GameCardReadFailure => new Result.Base(ModuleFs, 2533);
                /// <summary>Error code: 2002-2536; Inner value: 0x13d002</summary>
                public static Result.Base GameCardRetryLimitHit => new Result.Base(ModuleFs, 2536);
                /// <summary>Error code: 2002-2537; Inner value: 0x13d202</summary>
                public static Result.Base GameCardStatusRefreshRequested => new Result.Base(ModuleFs, 2537);
                /// <summary>Error code: 2002-2538; Inner value: 0x13d402</summary>
                public static Result.Base GameCardStatusCrcErrorAndRefreshRequested => new Result.Base(ModuleFs, 2538);
                /// <summary>Error code: 2002-2540; Inner value: 0x13d802</summary>
                public static Result.Base InvalidSecureAccess => new Result.Base(ModuleFs, 2540);
                /// <summary>Error code: 2002-2541; Inner value: 0x13da02</summary>
                public static Result.Base InvalidNormalAccess => new Result.Base(ModuleFs, 2541);
                /// <summary>Error code: 2002-2542; Inner value: 0x13dc02</summary>
                public static Result.Base InvalidAccessAcrossMode => new Result.Base(ModuleFs, 2542);

                /// <summary>Error code: 2002-2543; Range: 2543-2546; Inner value: 0x13de02</summary>
                public static Result.Base GameCardWrongCard { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 2543, 2546); }
                    /// <summary>Error code: 2002-2544; Inner value: 0x13e002</summary>
                    public static Result.Base InvalidGameCardInitialDataHash => new Result.Base(ModuleFs, 2544);
                    /// <summary>Error code: 2002-2545; Inner value: 0x13e202</summary>
                    public static Result.Base InvalidGameCardInitialDataReservedArea => new Result.Base(ModuleFs, 2545);
                    /// <summary>Error code: 2002-2546; Inner value: 0x13e402</summary>
                    public static Result.Base InvalidGameCardCertificateKekIndex => new Result.Base(ModuleFs, 2546);

                /// <summary>Error code: 2002-2548; Inner value: 0x13e802</summary>
                public static Result.Base InvalidGameCardModeForGetCardDeviceCertificate => new Result.Base(ModuleFs, 2548);
                /// <summary>Error code: 2002-2549; Inner value: 0x13ea02</summary>
                public static Result.Base NotSupportedForGameCardSecurityMode => new Result.Base(ModuleFs, 2549);
                /// <summary>Error code: 2002-2550; Inner value: 0x13ec02</summary>
                public static Result.Base Result2550 => new Result.Base(ModuleFs, 2550);
                /// <summary>Error code: 2002-2551; Inner value: 0x13ee02</summary>
                public static Result.Base GameCardReadHeaderTryTimeoutForActivation => new Result.Base(ModuleFs, 2551);
                /// <summary>Error code: 2002-2552; Inner value: 0x13f002</summary>
                public static Result.Base Result2552 => new Result.Base(ModuleFs, 2552);
                /// <summary>Error code: 2002-2553; Inner value: 0x13f202</summary>
                public static Result.Base InvalidGameCardModeForGetChallengeCardExistence => new Result.Base(ModuleFs, 2553);
                /// <summary>Error code: 2002-2554; Inner value: 0x13f402</summary>
                public static Result.Base InvalidGameCardHeader => new Result.Base(ModuleFs, 2554);
                /// <summary>Error code: 2002-2555; Inner value: 0x13f602</summary>
                public static Result.Base InvalidGameCardCertificate => new Result.Base(ModuleFs, 2555);
                /// <summary>Error code: 2002-2557; Inner value: 0x13fa02</summary>
                public static Result.Base Result2557 => new Result.Base(ModuleFs, 2557);
                /// <summary>Error code: 2002-2558; Inner value: 0x13fc02</summary>
                public static Result.Base Result2558 => new Result.Base(ModuleFs, 2558);

                /// <summary>Error code: 2002-2565; Range: 2565-2595; Inner value: 0x140a02</summary>
                public static Result.Base GameCardCommunicationFailure { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 2565, 2595); }

                /// <summary>Error code: 2002-2599; Inner value: 0x144e02</summary>
                public static Result.Base GameCardInvalidStateTransition => new Result.Base(ModuleFs, 2599);
                /// <summary>Error code: 2002-2600; Inner value: 0x145002</summary>
                public static Result.Base GameCardAsicInvalidTransitionToNormalMode => new Result.Base(ModuleFs, 2600);
                /// <summary>Error code: 2002-2601; Inner value: 0x145202</summary>
                public static Result.Base GameCardAsicInvalidTransitionToSecureMode => new Result.Base(ModuleFs, 2601);
                /// <summary>Error code: 2002-2602; Inner value: 0x145402</summary>
                public static Result.Base GameCardAsicInvalidTransitionToWriteMode => new Result.Base(ModuleFs, 2602);
                /// <summary>Error code: 2002-2629; Inner value: 0x148a02</summary>
                public static Result.Base GameCardAsicInitializationFailureForWriterFirmware => new Result.Base(ModuleFs, 2629);

                /// <summary>Error code: 2002-2630; Range: 2630-2669; Inner value: 0x148c02</summary>
                public static Result.Base GameCardAsicInitializationFailure { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 2630, 2669); }
                    /// <summary>Error code: 2002-2631; Inner value: 0x148e02</summary>
                    public static Result.Base GameCardAsicGetDeviceStatusFailure => new Result.Base(ModuleFs, 2631);
                    /// <summary>Error code: 2002-2632; Inner value: 0x149002</summary>
                    public static Result.Base GameCardAsicActivationFailure => new Result.Base(ModuleFs, 2632);
                    /// <summary>Error code: 2002-2634; Inner value: 0x149402</summary>
                    public static Result.Base GameCardAsicSetUserAsicFirmwareFailure => new Result.Base(ModuleFs, 2634);
                    /// <summary>Error code: 2002-2637; Inner value: 0x149a02</summary>
                    public static Result.Base GameCardAsicGetAsicCertFailure => new Result.Base(ModuleFs, 2637);
                    /// <summary>Error code: 2002-2638; Inner value: 0x149c02</summary>
                    public static Result.Base GameCardParseCertificateFailure => new Result.Base(ModuleFs, 2638);
                    /// <summary>Error code: 2002-2639; Inner value: 0x149e02</summary>
                    public static Result.Base InvalidGameCardAsicCertificate => new Result.Base(ModuleFs, 2639);
                    /// <summary>Error code: 2002-2640; Inner value: 0x14a002</summary>
                    public static Result.Base GameCardAsicSetEmmcEmbeddedSocCertificateFailure => new Result.Base(ModuleFs, 2640);
                    /// <summary>Error code: 2002-2645; Inner value: 0x14aa02</summary>
                    public static Result.Base GameCardAsicGetAsicEncryptedMessageFailure => new Result.Base(ModuleFs, 2645);
                    /// <summary>Error code: 2002-2646; Inner value: 0x14ac02</summary>
                    public static Result.Base GameCardAsicSetLibraryEncryptedMessageFailure => new Result.Base(ModuleFs, 2646);
                    /// <summary>Error code: 2002-2651; Inner value: 0x14b602</summary>
                    public static Result.Base GameCardAsicGetAsicAuthenticationDataFailure => new Result.Base(ModuleFs, 2651);
                    /// <summary>Error code: 2002-2652; Inner value: 0x14b802</summary>
                    public static Result.Base GameCardAsicSetAsicAuthenticationDataHashFailure => new Result.Base(ModuleFs, 2652);
                    /// <summary>Error code: 2002-2653; Inner value: 0x14ba02</summary>
                    public static Result.Base GameCardAsicSetLibraryAuthenticationDataFailure => new Result.Base(ModuleFs, 2653);
                    /// <summary>Error code: 2002-2654; Inner value: 0x14bc02</summary>
                    public static Result.Base GameCardAsicGetLibraryAuthenticationDataHashFailure => new Result.Base(ModuleFs, 2654);
                    /// <summary>Error code: 2002-2655; Inner value: 0x14be02</summary>
                    public static Result.Base GameCardInvalidLibraryAuthenticationDataHash => new Result.Base(ModuleFs, 2655);
                    /// <summary>Error code: 2002-2658; Inner value: 0x14c402</summary>
                    public static Result.Base GameCardAsicEnterSecureAsicModeFailure => new Result.Base(ModuleFs, 2658);
                    /// <summary>Error code: 2002-2659; Inner value: 0x14c602</summary>
                    public static Result.Base GameCardAsicExchangeRandomValuesInSecureModeFailure => new Result.Base(ModuleFs, 2659);
                    /// <summary>Error code: 2002-2660; Inner value: 0x14c802</summary>
                    public static Result.Base GameCardAsicChallengeCardExistenceFailure => new Result.Base(ModuleFs, 2660);
                    /// <summary>Error code: 2002-2663; Inner value: 0x14ce02</summary>
                    public static Result.Base GameCardAsicActivationTimeout => new Result.Base(ModuleFs, 2663);

                    /// <summary>Error code: 2002-2665; Range: 2665-2669; Inner value: 0x14d202</summary>
                    public static Result.Base GameCardSplFailure { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 2665, 2669); }
                        /// <summary>Error code: 2002-2666; Inner value: 0x14d402</summary>
                        public static Result.Base GameCardSplDecryptAesKeyFailure => new Result.Base(ModuleFs, 2666);
                        /// <summary>Error code: 2002-2667; Inner value: 0x14d602</summary>
                        public static Result.Base GameCardSplDecryptAndStoreGcKeyFailure => new Result.Base(ModuleFs, 2667);
                        /// <summary>Error code: 2002-2668; Inner value: 0x14d802</summary>
                        public static Result.Base GameCardSplGenerateRandomBytesFailure => new Result.Base(ModuleFs, 2668);
                        /// <summary>Error code: 2002-2669; Inner value: 0x14da02</summary>
                        public static Result.Base GameCardSplDecryptGcMessageFailure => new Result.Base(ModuleFs, 2669);

                /// <summary>Error code: 2002-2671; Inner value: 0x14de02</summary>
                public static Result.Base GameCardAsicReadAsicRegisterFailure => new Result.Base(ModuleFs, 2671);
                /// <summary>Error code: 2002-2672; Inner value: 0x14e002</summary>
                public static Result.Base GameCardAsicWriteAsicRegisterFailure => new Result.Base(ModuleFs, 2672);
                /// <summary>Error code: 2002-2673; Inner value: 0x14e202</summary>
                public static Result.Base GameCardAsicEnableCardBusFailure => new Result.Base(ModuleFs, 2673);
                /// <summary>Error code: 2002-2674; Inner value: 0x14e402</summary>
                public static Result.Base GameCardAsicGetCardHeaderFailure => new Result.Base(ModuleFs, 2674);
                /// <summary>Error code: 2002-2675; Inner value: 0x14e602</summary>
                public static Result.Base GameCardAsicStatusError => new Result.Base(ModuleFs, 2675);
                /// <summary>Error code: 2002-2676; Inner value: 0x14e802</summary>
                public static Result.Base GameCardAsicGetCardKeyAreaFailure => new Result.Base(ModuleFs, 2676);
                /// <summary>Error code: 2002-2677; Inner value: 0x14ea02</summary>
                public static Result.Base GameCardAsicChangeDebugModeFailure => new Result.Base(ModuleFs, 2677);
                /// <summary>Error code: 2002-2678; Inner value: 0x14ec02</summary>
                public static Result.Base GameCardAsicGetRmaInformationFailure => new Result.Base(ModuleFs, 2678);
                /// <summary>Error code: 2002-2680; Inner value: 0x14f002</summary>
                public static Result.Base GameCardAsicStatusBit22Set => new Result.Base(ModuleFs, 2680);
                /// <summary>Error code: 2002-2681; Inner value: 0x14f202</summary>
                public static Result.Base GameCardSecureValuesNotInitialized => new Result.Base(ModuleFs, 2681);
                /// <summary>Error code: 2002-2692; Inner value: 0x150802</summary>
                public static Result.Base InvalidSecureGameCardCommand => new Result.Base(ModuleFs, 2692);
                /// <summary>Error code: 2002-2693; Inner value: 0x150a02</summary>
                public static Result.Base InvalidWriteGameCardCommand => new Result.Base(ModuleFs, 2693);
                /// <summary>Error code: 2002-2703; Inner value: 0x151e02</summary>
                public static Result.Base GameCardSetVoltageFailure => new Result.Base(ModuleFs, 2703);
                /// <summary>Error code: 2002-2731; Inner value: 0x155602</summary>
                public static Result.Base GameCardCommandReadId1Failure => new Result.Base(ModuleFs, 2731);
                /// <summary>Error code: 2002-2732; Inner value: 0x155802</summary>
                public static Result.Base GameCardCommandReadId2Failure => new Result.Base(ModuleFs, 2732);
                /// <summary>Error code: 2002-2733; Inner value: 0x155a02</summary>
                public static Result.Base GameCardCommandReadId3Failure => new Result.Base(ModuleFs, 2733);
                /// <summary>Error code: 2002-2735; Inner value: 0x155e02</summary>
                public static Result.Base GameCardCommandReadPageFailure => new Result.Base(ModuleFs, 2735);
                /// <summary>Error code: 2002-2736; Inner value: 0x156002</summary>
                public static Result.Base GameCardCommandReadPageUnalignedFailure => new Result.Base(ModuleFs, 2736);
                /// <summary>Error code: 2002-2737; Inner value: 0x156202</summary>
                public static Result.Base GameCardCommandWritePageFailure => new Result.Base(ModuleFs, 2737);
                /// <summary>Error code: 2002-2738; Inner value: 0x156402</summary>
                public static Result.Base GameCardCommandRefreshFailure => new Result.Base(ModuleFs, 2738);
                /// <summary>Error code: 2002-2739; Inner value: 0x156602</summary>
                public static Result.Base GameCardCommandUpdateKeyFailure => new Result.Base(ModuleFs, 2739);
                /// <summary>Error code: 2002-2742; Inner value: 0x156c02</summary>
                public static Result.Base GameCardCommandReadCrcFailure => new Result.Base(ModuleFs, 2742);
                /// <summary>Error code: 2002-2743; Inner value: 0x156e02</summary>
                public static Result.Base GameCardCommandEraseFailure => new Result.Base(ModuleFs, 2743);
                /// <summary>Error code: 2002-2744; Inner value: 0x157002</summary>
                public static Result.Base GameCardCommandReadDevParamFailure => new Result.Base(ModuleFs, 2744);
                /// <summary>Error code: 2002-2745; Inner value: 0x157202</summary>
                public static Result.Base GameCardCommandWriteDevParamFailure => new Result.Base(ModuleFs, 2745);
                /// <summary>Error code: 2002-2901; Inner value: 0x16aa02</summary>
                public static Result.Base GameCardParameterError => new Result.Base(ModuleFs, 2901);
                /// <summary>Error code: 2002-2902; Inner value: 0x16ac02</summary>
                public static Result.Base Result2902 => new Result.Base(ModuleFs, 2902);
                /// <summary>Error code: 2002-2903; Inner value: 0x16ae02</summary>
                public static Result.Base Result2903 => new Result.Base(ModuleFs, 2903);
                /// <summary>Error code: 2002-2904; Inner value: 0x16b002</summary>
                public static Result.Base Result2904 => new Result.Base(ModuleFs, 2904);
                /// <summary>Error code: 2002-2905; Inner value: 0x16b202</summary>
                public static Result.Base Result2905 => new Result.Base(ModuleFs, 2905);
                /// <summary>Error code: 2002-2906; Inner value: 0x16b402</summary>
                public static Result.Base Result2906 => new Result.Base(ModuleFs, 2906);
                /// <summary>Error code: 2002-2950; Inner value: 0x170c02</summary>
                public static Result.Base InvalidGameCardStorageAttribute => new Result.Base(ModuleFs, 2950);
                /// <summary>Error code: 2002-2951; Inner value: 0x170e02</summary>
                public static Result.Base GameCardNotInsertedOnGetHandle => new Result.Base(ModuleFs, 2951);
                /// <summary>Error code: 2002-2952; Inner value: 0x171002</summary>
                public static Result.Base InvalidGameCardHandleOnRead => new Result.Base(ModuleFs, 2952);
                /// <summary>Error code: 2002-2954; Inner value: 0x171402</summary>
                public static Result.Base InvalidGameCardHandleOnGetCardInfo => new Result.Base(ModuleFs, 2954);
                /// <summary>Error code: 2002-2955; Inner value: 0x171602</summary>
                public static Result.Base InvalidGameCardHandleOnGetGameCardDeviceCertificate => new Result.Base(ModuleFs, 2955);
                /// <summary>Error code: 2002-2956; Inner value: 0x171802</summary>
                public static Result.Base InvalidGameCardHandleOnGetGameCardImageHash => new Result.Base(ModuleFs, 2956);
                /// <summary>Error code: 2002-2957; Inner value: 0x171a02</summary>
                public static Result.Base InvalidGameCardHandleOnChallengeCardExistence => new Result.Base(ModuleFs, 2957);
                /// <summary>Error code: 2002-2958; Inner value: 0x171c02</summary>
                public static Result.Base InvalidGameCardHandleOnOnAcquireLock => new Result.Base(ModuleFs, 2958);
                /// <summary>Error code: 2002-2959; Inner value: 0x171e02</summary>
                public static Result.Base InvalidGameCardModeOnAcquireSecureLock => new Result.Base(ModuleFs, 2959);
                /// <summary>Error code: 2002-2960; Inner value: 0x172002</summary>
                public static Result.Base InvalidGameCardHandleOnOpenNormalPartition => new Result.Base(ModuleFs, 2960);
                /// <summary>Error code: 2002-2961; Inner value: 0x172202</summary>
                public static Result.Base InvalidGameCardHandleOnOpenSecurePartition => new Result.Base(ModuleFs, 2961);
                /// <summary>Error code: 2002-2962; Inner value: 0x172402</summary>
                public static Result.Base InvalidGameCardCompatibilityType => new Result.Base(ModuleFs, 2962);
                /// <summary>Error code: 2002-2963; Inner value: 0x172602</summary>
                public static Result.Base GameCardsNotSupportedOnDeviceModel => new Result.Base(ModuleFs, 2963);

        /// <summary>Error code: 2002-3000; Range: 3000-7999; Inner value: 0x177002</summary>
        public static Result.Base Internal { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 3000, 7999); }
            /// <summary>Error code: 2002-3001; Inner value: 0x177202</summary>
            public static Result.Base NotImplemented => new Result.Base(ModuleFs, 3001);
            /// <summary>Error code: 2002-3002; Inner value: 0x177402</summary>
            public static Result.Base UnsupportedVersion => new Result.Base(ModuleFs, 3002);
            /// <summary>Error code: 2002-3003; Inner value: 0x177602</summary>
            public static Result.Base AlreadyExists => new Result.Base(ModuleFs, 3003);
            /// <summary>Error code: 2002-3005; Inner value: 0x177a02</summary>
            public static Result.Base OutOfRange => new Result.Base(ModuleFs, 3005);
            /// <summary>Error code: 2002-3099; Inner value: 0x183602</summary>
            public static Result.Base Result3099 => new Result.Base(ModuleFs, 3099);
            /// <summary>Error code: 2002-3100; Inner value: 0x183802</summary>
            public static Result.Base SystemPartitionNotReady => new Result.Base(ModuleFs, 3100);
            /// <summary>Error code: 2002-3101; Inner value: 0x183a02</summary>
            public static Result.Base StorageDeviceNotReady => new Result.Base(ModuleFs, 3101);

            /// <summary>Error code: 2002-3200; Range: 3200-3499; Inner value: 0x190002</summary>
            public static Result.Base AllocationMemoryFailed { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 3200, 3499); }
                /// <summary>Error code: 2002-3201; Inner value: 0x190202</summary>
                public static Result.Base AllocationMemoryFailedInPrFile2 => new Result.Base(ModuleFs, 3201);
                /// <summary>Error code: 2002-3203; Inner value: 0x190602</summary>
                public static Result.Base AllocationMemoryFailedInFatFileSystemA => new Result.Base(ModuleFs, 3203);
                /// <summary>Error code: 2002-3204; Inner value: 0x190802</summary>
                public static Result.Base AllocationMemoryFailedInFatFileSystemB => new Result.Base(ModuleFs, 3204);
                /// <summary>Error code: 2002-3205; Inner value: 0x190a02</summary>
                public static Result.Base AllocationMemoryFailedInFatFileSystemC => new Result.Base(ModuleFs, 3205);
                /// <summary>Error code: 2002-3206; Inner value: 0x190c02</summary>
                public static Result.Base AllocationMemoryFailedInFatFileSystemD => new Result.Base(ModuleFs, 3206);
                /// <summary>Error code: 2002-3208; Inner value: 0x191002</summary>
                public static Result.Base AllocationMemoryFailedInFatFileSystemE => new Result.Base(ModuleFs, 3208);
                /// <summary>Error code: 2002-3211; Inner value: 0x191602</summary>
                public static Result.Base AllocationMemoryFailedInFileSystemAccessorA => new Result.Base(ModuleFs, 3211);
                /// <summary>Error code: 2002-3212; Inner value: 0x191802</summary>
                public static Result.Base AllocationMemoryFailedInFileSystemAccessorB => new Result.Base(ModuleFs, 3212);
                /// <summary>Error code: 2002-3213; Inner value: 0x191a02</summary>
                public static Result.Base AllocationMemoryFailedInApplicationA => new Result.Base(ModuleFs, 3213);
                /// <summary>Error code: 2002-3214; Inner value: 0x191c02</summary>
                public static Result.Base AllocationMemoryFailedInBcatSaveDataA => new Result.Base(ModuleFs, 3214);
                /// <summary>Error code: 2002-3215; Inner value: 0x191e02</summary>
                public static Result.Base AllocationMemoryFailedInBisA => new Result.Base(ModuleFs, 3215);
                /// <summary>Error code: 2002-3216; Inner value: 0x192002</summary>
                public static Result.Base AllocationMemoryFailedInBisB => new Result.Base(ModuleFs, 3216);
                /// <summary>Error code: 2002-3217; Inner value: 0x192202</summary>
                public static Result.Base AllocationMemoryFailedInBisC => new Result.Base(ModuleFs, 3217);
                /// <summary>Error code: 2002-3218; Inner value: 0x192402</summary>
                public static Result.Base AllocationMemoryFailedInCodeA => new Result.Base(ModuleFs, 3218);
                /// <summary>Error code: 2002-3219; Inner value: 0x192602</summary>
                public static Result.Base AllocationMemoryFailedInContentA => new Result.Base(ModuleFs, 3219);
                /// <summary>Error code: 2002-3220; Inner value: 0x192802</summary>
                public static Result.Base AllocationMemoryFailedInContentStorageA => new Result.Base(ModuleFs, 3220);
                /// <summary>Error code: 2002-3221; Inner value: 0x192a02</summary>
                public static Result.Base AllocationMemoryFailedInContentStorageB => new Result.Base(ModuleFs, 3221);
                /// <summary>Error code: 2002-3222; Inner value: 0x192c02</summary>
                public static Result.Base AllocationMemoryFailedInDataA => new Result.Base(ModuleFs, 3222);
                /// <summary>Error code: 2002-3223; Inner value: 0x192e02</summary>
                public static Result.Base AllocationMemoryFailedInDataB => new Result.Base(ModuleFs, 3223);
                /// <summary>Error code: 2002-3224; Inner value: 0x193002</summary>
                public static Result.Base AllocationMemoryFailedInDeviceSaveDataA => new Result.Base(ModuleFs, 3224);
                /// <summary>Error code: 2002-3225; Inner value: 0x193202</summary>
                public static Result.Base AllocationMemoryFailedInGameCardA => new Result.Base(ModuleFs, 3225);
                /// <summary>Error code: 2002-3226; Inner value: 0x193402</summary>
                public static Result.Base AllocationMemoryFailedInGameCardB => new Result.Base(ModuleFs, 3226);
                /// <summary>Error code: 2002-3227; Inner value: 0x193602</summary>
                public static Result.Base AllocationMemoryFailedInGameCardC => new Result.Base(ModuleFs, 3227);
                /// <summary>Error code: 2002-3228; Inner value: 0x193802</summary>
                public static Result.Base AllocationMemoryFailedInGameCardD => new Result.Base(ModuleFs, 3228);
                /// <summary>Error code: 2002-3229; Inner value: 0x193a02</summary>
                public static Result.Base AllocationMemoryFailedInHostA => new Result.Base(ModuleFs, 3229);
                /// <summary>Error code: 2002-3230; Inner value: 0x193c02</summary>
                public static Result.Base AllocationMemoryFailedInHostB => new Result.Base(ModuleFs, 3230);
                /// <summary>Error code: 2002-3231; Inner value: 0x193e02</summary>
                public static Result.Base AllocationMemoryFailedInHostC => new Result.Base(ModuleFs, 3231);
                /// <summary>Error code: 2002-3232; Inner value: 0x194002</summary>
                public static Result.Base AllocationMemoryFailedInImageDirectoryA => new Result.Base(ModuleFs, 3232);
                /// <summary>Error code: 2002-3233; Inner value: 0x194202</summary>
                public static Result.Base AllocationMemoryFailedInLogoA => new Result.Base(ModuleFs, 3233);
                /// <summary>Error code: 2002-3234; Inner value: 0x194402</summary>
                public static Result.Base AllocationMemoryFailedInRomA => new Result.Base(ModuleFs, 3234);
                /// <summary>Error code: 2002-3235; Inner value: 0x194602</summary>
                public static Result.Base AllocationMemoryFailedInRomB => new Result.Base(ModuleFs, 3235);
                /// <summary>Error code: 2002-3236; Inner value: 0x194802</summary>
                public static Result.Base AllocationMemoryFailedInRomC => new Result.Base(ModuleFs, 3236);
                /// <summary>Error code: 2002-3237; Inner value: 0x194a02</summary>
                public static Result.Base AllocationMemoryFailedInRomD => new Result.Base(ModuleFs, 3237);
                /// <summary>Error code: 2002-3238; Inner value: 0x194c02</summary>
                public static Result.Base AllocationMemoryFailedInRomE => new Result.Base(ModuleFs, 3238);
                /// <summary>Error code: 2002-3239; Inner value: 0x194e02</summary>
                public static Result.Base AllocationMemoryFailedInRomF => new Result.Base(ModuleFs, 3239);
                /// <summary>Error code: 2002-3242; Inner value: 0x195402</summary>
                public static Result.Base AllocationMemoryFailedInSaveDataManagementA => new Result.Base(ModuleFs, 3242);
                /// <summary>Error code: 2002-3243; Inner value: 0x195602</summary>
                public static Result.Base AllocationMemoryFailedInSaveDataThumbnailA => new Result.Base(ModuleFs, 3243);
                /// <summary>Error code: 2002-3244; Inner value: 0x195802</summary>
                public static Result.Base AllocationMemoryFailedInSdCardA => new Result.Base(ModuleFs, 3244);
                /// <summary>Error code: 2002-3245; Inner value: 0x195a02</summary>
                public static Result.Base AllocationMemoryFailedInSdCardB => new Result.Base(ModuleFs, 3245);
                /// <summary>Error code: 2002-3246; Inner value: 0x195c02</summary>
                public static Result.Base AllocationMemoryFailedInSystemSaveDataA => new Result.Base(ModuleFs, 3246);
                /// <summary>Error code: 2002-3247; Inner value: 0x195e02</summary>
                public static Result.Base AllocationMemoryFailedInRomFsFileSystemA => new Result.Base(ModuleFs, 3247);
                /// <summary>Error code: 2002-3248; Inner value: 0x196002</summary>
                public static Result.Base AllocationMemoryFailedInRomFsFileSystemB => new Result.Base(ModuleFs, 3248);
                /// <summary>Error code: 2002-3249; Inner value: 0x196202</summary>
                public static Result.Base AllocationMemoryFailedInRomFsFileSystemC => new Result.Base(ModuleFs, 3249);
                /// <summary>Error code: 2002-3251; Inner value: 0x196602</summary>
                public static Result.Base AllocationMemoryFailedInGuidPartitionTableA => new Result.Base(ModuleFs, 3251);
                /// <summary>Error code: 2002-3252; Inner value: 0x196802</summary>
                public static Result.Base AllocationMemoryFailedInDeviceDetectionEventManagerA => new Result.Base(ModuleFs, 3252);
                /// <summary>Error code: 2002-3253; Inner value: 0x196a02</summary>
                public static Result.Base AllocationMemoryFailedInSaveDataFileSystemServiceImplA => new Result.Base(ModuleFs, 3253);
                /// <summary>Error code: 2002-3254; Inner value: 0x196c02</summary>
                public static Result.Base AllocationMemoryFailedInFileSystemProxyCoreImplB => new Result.Base(ModuleFs, 3254);
                /// <summary>Error code: 2002-3255; Inner value: 0x196e02</summary>
                public static Result.Base AllocationMemoryFailedInSdCardProxyFileSystemCreatorA => new Result.Base(ModuleFs, 3255);
                /// <summary>In ParseNsp allocating FileStorageBasedFileSystem<br/>Error code: 2002-3256; Inner value: 0x197002</summary>
                public static Result.Base AllocationMemoryFailedInNcaFileSystemServiceImplA => new Result.Base(ModuleFs, 3256);
                /// <summary>In ParseNca allocating FileStorageBasedFileSystem<br/>Error code: 2002-3257; Inner value: 0x197202</summary>
                public static Result.Base AllocationMemoryFailedInNcaFileSystemServiceImplB => new Result.Base(ModuleFs, 3257);
                /// <summary>In RegisterProgram allocating ProgramInfoNode<br/>Error code: 2002-3258; Inner value: 0x197402</summary>
                public static Result.Base AllocationMemoryFailedInProgramRegistryManagerA => new Result.Base(ModuleFs, 3258);
                /// <summary>Error code: 2002-3259; Inner value: 0x197602</summary>
                public static Result.Base AllocationMemoryFailedInSdmmcStorageServiceA => new Result.Base(ModuleFs, 3259);
                /// <summary>Error code: 2002-3260; Inner value: 0x197802</summary>
                public static Result.Base AllocationMemoryFailedInBuiltInStorageCreatorA => new Result.Base(ModuleFs, 3260);
                /// <summary>Error code: 2002-3261; Inner value: 0x197a02</summary>
                public static Result.Base AllocationMemoryFailedInBuiltInStorageCreatorB => new Result.Base(ModuleFs, 3261);
                /// <summary>Error code: 2002-3262; Inner value: 0x197c02</summary>
                public static Result.Base AllocationMemoryFailedInBuiltInStorageCreatorC => new Result.Base(ModuleFs, 3262);
                /// <summary>In Initialize allocating ProgramInfoNode<br/>Error code: 2002-3264; Inner value: 0x198002</summary>
                public static Result.Base AllocationMemoryFailedFatFileSystemWithBufferA => new Result.Base(ModuleFs, 3264);
                /// <summary>Error code: 2002-3265; Inner value: 0x198202</summary>
                public static Result.Base AllocationMemoryFailedInFatFileSystemCreatorA => new Result.Base(ModuleFs, 3265);
                /// <summary>Error code: 2002-3266; Inner value: 0x198402</summary>
                public static Result.Base AllocationMemoryFailedInFatFileSystemCreatorB => new Result.Base(ModuleFs, 3266);
                /// <summary>Error code: 2002-3267; Inner value: 0x198602</summary>
                public static Result.Base AllocationMemoryFailedInGameCardFileSystemCreatorA => new Result.Base(ModuleFs, 3267);
                /// <summary>Error code: 2002-3268; Inner value: 0x198802</summary>
                public static Result.Base AllocationMemoryFailedInGameCardFileSystemCreatorB => new Result.Base(ModuleFs, 3268);
                /// <summary>Error code: 2002-3269; Inner value: 0x198a02</summary>
                public static Result.Base AllocationMemoryFailedInGameCardFileSystemCreatorC => new Result.Base(ModuleFs, 3269);
                /// <summary>Error code: 2002-3270; Inner value: 0x198c02</summary>
                public static Result.Base AllocationMemoryFailedInGameCardFileSystemCreatorD => new Result.Base(ModuleFs, 3270);
                /// <summary>Error code: 2002-3271; Inner value: 0x198e02</summary>
                public static Result.Base AllocationMemoryFailedInGameCardFileSystemCreatorE => new Result.Base(ModuleFs, 3271);
                /// <summary>Error code: 2002-3272; Inner value: 0x199002</summary>
                public static Result.Base AllocationMemoryFailedInGameCardFileSystemCreatorF => new Result.Base(ModuleFs, 3272);
                /// <summary>Error code: 2002-3273; Inner value: 0x199202</summary>
                public static Result.Base AllocationMemoryFailedInGameCardManagerA => new Result.Base(ModuleFs, 3273);
                /// <summary>Error code: 2002-3274; Inner value: 0x199402</summary>
                public static Result.Base AllocationMemoryFailedInGameCardManagerB => new Result.Base(ModuleFs, 3274);
                /// <summary>Error code: 2002-3275; Inner value: 0x199602</summary>
                public static Result.Base AllocationMemoryFailedInGameCardManagerC => new Result.Base(ModuleFs, 3275);
                /// <summary>Error code: 2002-3276; Inner value: 0x199802</summary>
                public static Result.Base AllocationMemoryFailedInGameCardManagerD => new Result.Base(ModuleFs, 3276);
                /// <summary>Error code: 2002-3277; Inner value: 0x199a02</summary>
                public static Result.Base AllocationMemoryFailedInGameCardManagerE => new Result.Base(ModuleFs, 3277);
                /// <summary>Error code: 2002-3278; Inner value: 0x199c02</summary>
                public static Result.Base AllocationMemoryFailedInGameCardManagerF => new Result.Base(ModuleFs, 3278);
                /// <summary>Error code: 2002-3279; Inner value: 0x199e02</summary>
                public static Result.Base AllocationMemoryFailedInLocalFileSystemCreatorA => new Result.Base(ModuleFs, 3279);
                /// <summary>In Create allocating PartitionFileSystemCore<br/>Error code: 2002-3280; Inner value: 0x19a002</summary>
                public static Result.Base AllocationMemoryFailedInPartitionFileSystemCreatorA => new Result.Base(ModuleFs, 3280);
                /// <summary>Error code: 2002-3281; Inner value: 0x19a202</summary>
                public static Result.Base AllocationMemoryFailedInRomFileSystemCreatorA => new Result.Base(ModuleFs, 3281);
                /// <summary>Error code: 2002-3282; Inner value: 0x19a402</summary>
                public static Result.Base AllocationMemoryFailedInSaveDataFileSystemCreatorA => new Result.Base(ModuleFs, 3282);
                /// <summary>Error code: 2002-3283; Inner value: 0x19a602</summary>
                public static Result.Base AllocationMemoryFailedInSaveDataFileSystemCreatorB => new Result.Base(ModuleFs, 3283);
                /// <summary>Error code: 2002-3284; Inner value: 0x19a802</summary>
                public static Result.Base AllocationMemoryFailedInSaveDataFileSystemCreatorC => new Result.Base(ModuleFs, 3284);
                /// <summary>Error code: 2002-3285; Inner value: 0x19aa02</summary>
                public static Result.Base AllocationMemoryFailedInSaveDataFileSystemCreatorD => new Result.Base(ModuleFs, 3285);
                /// <summary>Error code: 2002-3286; Inner value: 0x19ac02</summary>
                public static Result.Base AllocationMemoryFailedInSaveDataFileSystemCreatorE => new Result.Base(ModuleFs, 3286);
                /// <summary>Error code: 2002-3288; Inner value: 0x19b002</summary>
                public static Result.Base AllocationMemoryFailedInStorageOnNcaCreatorA => new Result.Base(ModuleFs, 3288);
                /// <summary>Error code: 2002-3289; Inner value: 0x19b202</summary>
                public static Result.Base AllocationMemoryFailedInStorageOnNcaCreatorB => new Result.Base(ModuleFs, 3289);
                /// <summary>Error code: 2002-3290; Inner value: 0x19b402</summary>
                public static Result.Base AllocationMemoryFailedInSubDirectoryFileSystemCreatorA => new Result.Base(ModuleFs, 3290);
                /// <summary>Error code: 2002-3291; Inner value: 0x19b602</summary>
                public static Result.Base AllocationMemoryFailedInTargetManagerFileSystemCreatorA => new Result.Base(ModuleFs, 3291);
                /// <summary>Error code: 2002-3292; Inner value: 0x19b802</summary>
                public static Result.Base AllocationMemoryFailedInSaveDataIndexerA => new Result.Base(ModuleFs, 3292);
                /// <summary>Error code: 2002-3293; Inner value: 0x19ba02</summary>
                public static Result.Base AllocationMemoryFailedInSaveDataIndexerB => new Result.Base(ModuleFs, 3293);
                /// <summary>Error code: 2002-3294; Inner value: 0x19bc02</summary>
                public static Result.Base AllocationMemoryFailedInFileSystemBuddyHeapA => new Result.Base(ModuleFs, 3294);
                /// <summary>Error code: 2002-3295; Inner value: 0x19be02</summary>
                public static Result.Base AllocationMemoryFailedInFileSystemBufferManagerA => new Result.Base(ModuleFs, 3295);
                /// <summary>Error code: 2002-3296; Inner value: 0x19c002</summary>
                public static Result.Base AllocationMemoryFailedInBlockCacheBufferedStorageA => new Result.Base(ModuleFs, 3296);
                /// <summary>Error code: 2002-3297; Inner value: 0x19c202</summary>
                public static Result.Base AllocationMemoryFailedInBlockCacheBufferedStorageB => new Result.Base(ModuleFs, 3297);
                /// <summary>Error code: 2002-3298; Inner value: 0x19c402</summary>
                public static Result.Base AllocationMemoryFailedInDuplexStorageA => new Result.Base(ModuleFs, 3298);
                /// <summary>Error code: 2002-3304; Inner value: 0x19d002</summary>
                public static Result.Base AllocationMemoryFailedInIntegrityVerificationStorageA => new Result.Base(ModuleFs, 3304);
                /// <summary>Error code: 2002-3305; Inner value: 0x19d202</summary>
                public static Result.Base AllocationMemoryFailedInIntegrityVerificationStorageB => new Result.Base(ModuleFs, 3305);
                /// <summary>Error code: 2002-3306; Inner value: 0x19d402</summary>
                public static Result.Base AllocationMemoryFailedInJournalStorageA => new Result.Base(ModuleFs, 3306);
                /// <summary>Error code: 2002-3307; Inner value: 0x19d602</summary>
                public static Result.Base AllocationMemoryFailedInJournalStorageB => new Result.Base(ModuleFs, 3307);
                /// <summary>Error code: 2002-3310; Inner value: 0x19dc02</summary>
                public static Result.Base AllocationMemoryFailedInSaveDataFileSystemCoreA => new Result.Base(ModuleFs, 3310);
                /// <summary>Error code: 2002-3311; Inner value: 0x19de02</summary>
                public static Result.Base AllocationMemoryFailedInSaveDataFileSystemCoreB => new Result.Base(ModuleFs, 3311);
                /// <summary>In Initialize allocating FileStorage<br/>Error code: 2002-3312; Inner value: 0x19e002</summary>
                public static Result.Base AllocationMemoryFailedInAesXtsFileA => new Result.Base(ModuleFs, 3312);
                /// <summary>In Initialize allocating AesXtsStorage<br/>Error code: 2002-3313; Inner value: 0x19e202</summary>
                public static Result.Base AllocationMemoryFailedInAesXtsFileB => new Result.Base(ModuleFs, 3313);
                /// <summary>In Initialize allocating AlignmentMatchingStoragePooledBuffer<br/>Error code: 2002-3314; Inner value: 0x19e402</summary>
                public static Result.Base AllocationMemoryFailedInAesXtsFileC => new Result.Base(ModuleFs, 3314);
                /// <summary>In Initialize allocating StorageFile<br/>Error code: 2002-3315; Inner value: 0x19e602</summary>
                public static Result.Base AllocationMemoryFailedInAesXtsFileD => new Result.Base(ModuleFs, 3315);
                /// <summary>Error code: 2002-3316; Inner value: 0x19e802</summary>
                public static Result.Base AllocationMemoryFailedInAesXtsFileSystemA => new Result.Base(ModuleFs, 3316);
                /// <summary>Error code: 2002-3319; Inner value: 0x19ee02</summary>
                public static Result.Base AllocationMemoryFailedInConcatenationFileSystemA => new Result.Base(ModuleFs, 3319);
                /// <summary>Error code: 2002-3320; Inner value: 0x19f002</summary>
                public static Result.Base AllocationMemoryFailedInConcatenationFileSystemB => new Result.Base(ModuleFs, 3320);
                /// <summary>Error code: 2002-3321; Inner value: 0x19f202</summary>
                public static Result.Base AllocationMemoryFailedInDirectorySaveDataFileSystemA => new Result.Base(ModuleFs, 3321);
                /// <summary>Error code: 2002-3322; Inner value: 0x19f402</summary>
                public static Result.Base AllocationMemoryFailedInLocalFileSystemA => new Result.Base(ModuleFs, 3322);
                /// <summary>Error code: 2002-3323; Inner value: 0x19f602</summary>
                public static Result.Base AllocationMemoryFailedInLocalFileSystemB => new Result.Base(ModuleFs, 3323);
                /// <summary>Error code: 2002-3341; Inner value: 0x1a1a02</summary>
                public static Result.Base AllocationMemoryFailedInNcaFileSystemDriverI => new Result.Base(ModuleFs, 3341);
                /// <summary>In Initialize allocating PartitionFileSystemMetaCore<br/>Error code: 2002-3347; Inner value: 0x1a2602</summary>
                public static Result.Base AllocationMemoryFailedInPartitionFileSystemA => new Result.Base(ModuleFs, 3347);
                /// <summary>In DoOpenFile allocating PartitionFile<br/>Error code: 2002-3348; Inner value: 0x1a2802</summary>
                public static Result.Base AllocationMemoryFailedInPartitionFileSystemB => new Result.Base(ModuleFs, 3348);
                /// <summary>In DoOpenDirectory allocating PartitionDirectory<br/>Error code: 2002-3349; Inner value: 0x1a2a02</summary>
                public static Result.Base AllocationMemoryFailedInPartitionFileSystemC => new Result.Base(ModuleFs, 3349);
                /// <summary>In Initialize allocating metadata buffer<br/>Error code: 2002-3350; Inner value: 0x1a2c02</summary>
                public static Result.Base AllocationMemoryFailedInPartitionFileSystemMetaA => new Result.Base(ModuleFs, 3350);
                /// <summary>In Sha256 Initialize allocating metadata buffer<br/>Error code: 2002-3351; Inner value: 0x1a2e02</summary>
                public static Result.Base AllocationMemoryFailedInPartitionFileSystemMetaB => new Result.Base(ModuleFs, 3351);
                /// <summary>Error code: 2002-3352; Inner value: 0x1a3002</summary>
                public static Result.Base AllocationMemoryFailedInRomFsFileSystemD => new Result.Base(ModuleFs, 3352);
                /// <summary>In Initialize allocating RootPathBuffer<br/>Error code: 2002-3355; Inner value: 0x1a3602</summary>
                public static Result.Base AllocationMemoryFailedInSubdirectoryFileSystemA => new Result.Base(ModuleFs, 3355);
                /// <summary>Error code: 2002-3356; Inner value: 0x1a3802</summary>
                public static Result.Base AllocationMemoryFailedInTmFileSystemA => new Result.Base(ModuleFs, 3356);
                /// <summary>Error code: 2002-3357; Inner value: 0x1a3a02</summary>
                public static Result.Base AllocationMemoryFailedInTmFileSystemB => new Result.Base(ModuleFs, 3357);
                /// <summary>Error code: 2002-3359; Inner value: 0x1a3e02</summary>
                public static Result.Base AllocationMemoryFailedInProxyFileSystemA => new Result.Base(ModuleFs, 3359);
                /// <summary>Error code: 2002-3360; Inner value: 0x1a4002</summary>
                public static Result.Base AllocationMemoryFailedInProxyFileSystemB => new Result.Base(ModuleFs, 3360);
                /// <summary>Error code: 2002-3362; Inner value: 0x1a4402</summary>
                public static Result.Base AllocationMemoryFailedInSaveDataExtraDataAccessorCacheManagerA => new Result.Base(ModuleFs, 3362);
                /// <summary>Error code: 2002-3363; Inner value: 0x1a4602</summary>
                public static Result.Base AllocationMemoryFailedInNcaReaderA => new Result.Base(ModuleFs, 3363);
                /// <summary>Error code: 2002-3365; Inner value: 0x1a4a02</summary>
                public static Result.Base AllocationMemoryFailedInRegisterA => new Result.Base(ModuleFs, 3365);
                /// <summary>Error code: 2002-3366; Inner value: 0x1a4c02</summary>
                public static Result.Base AllocationMemoryFailedInRegisterB => new Result.Base(ModuleFs, 3366);
                /// <summary>Error code: 2002-3367; Inner value: 0x1a4e02</summary>
                public static Result.Base AllocationMemoryFailedInPathNormalizer => new Result.Base(ModuleFs, 3367);
                /// <summary>Error code: 2002-3375; Inner value: 0x1a5e02</summary>
                public static Result.Base AllocationMemoryFailedInDbmRomKeyValueStorage => new Result.Base(ModuleFs, 3375);
                /// <summary>Error code: 2002-3376; Inner value: 0x1a6002</summary>
                public static Result.Base AllocationMemoryFailedInDbmHierarchicalRomFileTable => new Result.Base(ModuleFs, 3376);
                /// <summary>Error code: 2002-3377; Inner value: 0x1a6202</summary>
                public static Result.Base AllocationMemoryFailedInRomFsFileSystemE => new Result.Base(ModuleFs, 3377);
                /// <summary>Error code: 2002-3378; Inner value: 0x1a6402</summary>
                public static Result.Base AllocationMemoryFailedInISaveFileSystemA => new Result.Base(ModuleFs, 3378);
                /// <summary>Error code: 2002-3379; Inner value: 0x1a6602</summary>
                public static Result.Base AllocationMemoryFailedInISaveFileSystemB => new Result.Base(ModuleFs, 3379);
                /// <summary>Error code: 2002-3380; Inner value: 0x1a6802</summary>
                public static Result.Base AllocationMemoryFailedInRomOnFileA => new Result.Base(ModuleFs, 3380);
                /// <summary>Error code: 2002-3381; Inner value: 0x1a6a02</summary>
                public static Result.Base AllocationMemoryFailedInRomOnFileB => new Result.Base(ModuleFs, 3381);
                /// <summary>Error code: 2002-3382; Inner value: 0x1a6c02</summary>
                public static Result.Base AllocationMemoryFailedInRomOnFileC => new Result.Base(ModuleFs, 3382);
                /// <summary>In Initialize<br/>Error code: 2002-3383; Inner value: 0x1a6e02</summary>
                public static Result.Base AllocationMemoryFailedInAesXtsFileE => new Result.Base(ModuleFs, 3383);
                /// <summary>Error code: 2002-3384; Inner value: 0x1a7002</summary>
                public static Result.Base AllocationMemoryFailedInAesXtsFileSystemB => new Result.Base(ModuleFs, 3384);
                /// <summary>Error code: 2002-3385; Inner value: 0x1a7202</summary>
                public static Result.Base AllocationMemoryFailedInAesXtsFileSystemC => new Result.Base(ModuleFs, 3385);
                /// <summary>Error code: 2002-3386; Inner value: 0x1a7402</summary>
                public static Result.Base AllocationMemoryFailedInReadOnlyFileSystemA => new Result.Base(ModuleFs, 3386);
                /// <summary>In Create allocating AesXtsFileSystem<br/>Error code: 2002-3394; Inner value: 0x1a8402</summary>
                public static Result.Base AllocationMemoryFailedInEncryptedFileSystemCreatorA => new Result.Base(ModuleFs, 3394);
                /// <summary>Error code: 2002-3399; Inner value: 0x1a8e02</summary>
                public static Result.Base AllocationMemoryFailedInAesCtrCounterExtendedStorageA => new Result.Base(ModuleFs, 3399);
                /// <summary>Error code: 2002-3400; Inner value: 0x1a9002</summary>
                public static Result.Base AllocationMemoryFailedInAesCtrCounterExtendedStorageB => new Result.Base(ModuleFs, 3400);
                /// <summary>Error code: 2002-3406; Inner value: 0x1a9c02</summary>
                public static Result.Base AllocationMemoryFailedInSdmmcStorageServiceB => new Result.Base(ModuleFs, 3406);
                /// <summary>In OpenFile or OpenDirectory<br/>Error code: 2002-3407; Inner value: 0x1a9e02</summary>
                public static Result.Base AllocationMemoryFailedInFileSystemInterfaceAdapter => new Result.Base(ModuleFs, 3407);
                /// <summary>Error code: 2002-3408; Inner value: 0x1aa002</summary>
                public static Result.Base AllocationMemoryFailedInGameCardFileSystemCreatorG => new Result.Base(ModuleFs, 3408);
                /// <summary>Error code: 2002-3409; Inner value: 0x1aa202</summary>
                public static Result.Base AllocationMemoryFailedInGameCardFileSystemCreatorH => new Result.Base(ModuleFs, 3409);
                /// <summary>Error code: 2002-3411; Inner value: 0x1aa602</summary>
                public static Result.Base AllocationMemoryFailedInBufferedStorageA => new Result.Base(ModuleFs, 3411);
                /// <summary>Error code: 2002-3412; Inner value: 0x1aa802</summary>
                public static Result.Base AllocationMemoryFailedInIntegrityRomFsStorageA => new Result.Base(ModuleFs, 3412);
                /// <summary>Error code: 2002-3416; Inner value: 0x1ab002</summary>
                public static Result.Base AllocationMemoryFailedInSaveDataFileSystemServiceImplB => new Result.Base(ModuleFs, 3416);
                /// <summary>Error code: 2002-3420; Inner value: 0x1ab802</summary>
                public static Result.Base AllocationMemoryFailedNew => new Result.Base(ModuleFs, 3420);
                /// <summary>Error code: 2002-3421; Inner value: 0x1aba02</summary>
                public static Result.Base AllocationMemoryFailedCreateShared => new Result.Base(ModuleFs, 3421);
                /// <summary>Error code: 2002-3422; Inner value: 0x1abc02</summary>
                public static Result.Base AllocationMemoryFailedMakeUnique => new Result.Base(ModuleFs, 3422);
                /// <summary>Error code: 2002-3423; Inner value: 0x1abe02</summary>
                public static Result.Base AllocationMemoryFailedAllocateShared => new Result.Base(ModuleFs, 3423);
                /// <summary>Error code: 2002-3424; Inner value: 0x1ac002</summary>
                public static Result.Base AllocationPooledBufferNotEnoughSize => new Result.Base(ModuleFs, 3424);
                /// <summary>Error code: 2002-3428; Inner value: 0x1ac802</summary>
                public static Result.Base AllocationMemoryFailedInWriteThroughCacheStorageA => new Result.Base(ModuleFs, 3428);
                /// <summary>Error code: 2002-3429; Inner value: 0x1aca02</summary>
                public static Result.Base AllocationMemoryFailedInSaveDataTransferManagerA => new Result.Base(ModuleFs, 3429);
                /// <summary>Error code: 2002-3430; Inner value: 0x1acc02</summary>
                public static Result.Base AllocationMemoryFailedInSaveDataTransferManagerB => new Result.Base(ModuleFs, 3430);
                /// <summary>Error code: 2002-3431; Inner value: 0x1ace02</summary>
                public static Result.Base AllocationMemoryFailedInHtcFileSystemA => new Result.Base(ModuleFs, 3431);
                /// <summary>Error code: 2002-3432; Inner value: 0x1ad002</summary>
                public static Result.Base AllocationMemoryFailedInHtcFileSystemB => new Result.Base(ModuleFs, 3432);
                /// <summary>Error code: 2002-3433; Inner value: 0x1ad202</summary>
                public static Result.Base AllocationMemoryFailedInGameCardManagerG => new Result.Base(ModuleFs, 3433);

            /// <summary>Error code: 2002-3500; Range: 3500-3999; Inner value: 0x1b5802</summary>
            public static Result.Base MmcAccessFailed { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 3500, 3999); }
                /// <summary>Error code: 2002-3501; Inner value: 0x1b5a02</summary>
                public static Result.Base PortMmcNoDevice => new Result.Base(ModuleFs, 3501);
                /// <summary>Error code: 2002-3502; Inner value: 0x1b5c02</summary>
                public static Result.Base PortMmcNotActivated => new Result.Base(ModuleFs, 3502);
                /// <summary>Error code: 2002-3503; Inner value: 0x1b5e02</summary>
                public static Result.Base PortMmcDeviceRemoved => new Result.Base(ModuleFs, 3503);
                /// <summary>Error code: 2002-3504; Inner value: 0x1b6002</summary>
                public static Result.Base PortMmcNotAwakened => new Result.Base(ModuleFs, 3504);

                /// <summary>Error code: 2002-3532; Range: 3532-3626; Inner value: 0x1b9802</summary>
                public static Result.Base PortMmcCommunicationError { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 3532, 3626); }
                    /// <summary>Error code: 2002-3533; Range: 3533-3546; Inner value: 0x1b9a02</summary>
                    public static Result.Base PortMmcCommunicationNotAttained { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 3533, 3546); }
                        /// <summary>Error code: 2002-3534; Inner value: 0x1b9c02</summary>
                        public static Result.Base PortMmcResponseIndexError => new Result.Base(ModuleFs, 3534);
                        /// <summary>Error code: 2002-3535; Inner value: 0x1b9e02</summary>
                        public static Result.Base PortMmcResponseEndBitError => new Result.Base(ModuleFs, 3535);
                        /// <summary>Error code: 2002-3536; Inner value: 0x1ba002</summary>
                        public static Result.Base PortMmcResponseCrcError => new Result.Base(ModuleFs, 3536);
                        /// <summary>Error code: 2002-3537; Inner value: 0x1ba202</summary>
                        public static Result.Base PortMmcResponseTimeoutError => new Result.Base(ModuleFs, 3537);
                        /// <summary>Error code: 2002-3538; Inner value: 0x1ba402</summary>
                        public static Result.Base PortMmcDataEndBitError => new Result.Base(ModuleFs, 3538);
                        /// <summary>Error code: 2002-3539; Inner value: 0x1ba602</summary>
                        public static Result.Base PortMmcDataCrcError => new Result.Base(ModuleFs, 3539);
                        /// <summary>Error code: 2002-3540; Inner value: 0x1ba802</summary>
                        public static Result.Base PortMmcDataTimeoutError => new Result.Base(ModuleFs, 3540);
                        /// <summary>Error code: 2002-3541; Inner value: 0x1baa02</summary>
                        public static Result.Base PortMmcAutoCommandResponseIndexError => new Result.Base(ModuleFs, 3541);
                        /// <summary>Error code: 2002-3542; Inner value: 0x1bac02</summary>
                        public static Result.Base PortMmcAutoCommandResponseEndBitError => new Result.Base(ModuleFs, 3542);
                        /// <summary>Error code: 2002-3543; Inner value: 0x1bae02</summary>
                        public static Result.Base PortMmcAutoCommandResponseCrcError => new Result.Base(ModuleFs, 3543);
                        /// <summary>Error code: 2002-3544; Inner value: 0x1bb002</summary>
                        public static Result.Base PortMmcAutoCommandResponseTimeoutError => new Result.Base(ModuleFs, 3544);
                        /// <summary>Error code: 2002-3545; Inner value: 0x1bb202</summary>
                        public static Result.Base PortMmcCommandCompleteSoftwareTimeout => new Result.Base(ModuleFs, 3545);
                        /// <summary>Error code: 2002-3546; Inner value: 0x1bb402</summary>
                        public static Result.Base PortMmcTransferCompleteSoftwareTimeout => new Result.Base(ModuleFs, 3546);

                    /// <summary>Error code: 2002-3548; Range: 3548-3570; Inner value: 0x1bb802</summary>
                    public static Result.Base PortMmcDeviceStatusHasError { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 3548, 3570); }
                        /// <summary>Error code: 2002-3549; Inner value: 0x1bba02</summary>
                        public static Result.Base PortMmcDeviceStatusAddressOutOfRange => new Result.Base(ModuleFs, 3549);
                        /// <summary>Error code: 2002-3550; Inner value: 0x1bbc02</summary>
                        public static Result.Base PortMmcDeviceStatusAddressMisaligned => new Result.Base(ModuleFs, 3550);
                        /// <summary>Error code: 2002-3551; Inner value: 0x1bbe02</summary>
                        public static Result.Base PortMmcDeviceStatusBlockLenError => new Result.Base(ModuleFs, 3551);
                        /// <summary>Error code: 2002-3552; Inner value: 0x1bc002</summary>
                        public static Result.Base PortMmcDeviceStatusEraseSeqError => new Result.Base(ModuleFs, 3552);
                        /// <summary>Error code: 2002-3553; Inner value: 0x1bc202</summary>
                        public static Result.Base PortMmcDeviceStatusEraseParam => new Result.Base(ModuleFs, 3553);
                        /// <summary>Error code: 2002-3554; Inner value: 0x1bc402</summary>
                        public static Result.Base PortMmcDeviceStatusWpViolation => new Result.Base(ModuleFs, 3554);
                        /// <summary>Error code: 2002-3555; Inner value: 0x1bc602</summary>
                        public static Result.Base PortMmcDeviceStatusLockUnlockFailed => new Result.Base(ModuleFs, 3555);
                        /// <summary>Error code: 2002-3556; Inner value: 0x1bc802</summary>
                        public static Result.Base PortMmcDeviceStatusComCrcError => new Result.Base(ModuleFs, 3556);
                        /// <summary>Error code: 2002-3557; Inner value: 0x1bca02</summary>
                        public static Result.Base PortMmcDeviceStatusIllegalCommand => new Result.Base(ModuleFs, 3557);
                        /// <summary>Error code: 2002-3558; Inner value: 0x1bcc02</summary>
                        public static Result.Base PortMmcDeviceStatusDeviceEccFailed => new Result.Base(ModuleFs, 3558);
                        /// <summary>Error code: 2002-3559; Inner value: 0x1bce02</summary>
                        public static Result.Base PortMmcDeviceStatusCcError => new Result.Base(ModuleFs, 3559);
                        /// <summary>Error code: 2002-3560; Inner value: 0x1bd002</summary>
                        public static Result.Base PortMmcDeviceStatusError => new Result.Base(ModuleFs, 3560);
                        /// <summary>Error code: 2002-3561; Inner value: 0x1bd202</summary>
                        public static Result.Base PortMmcDeviceStatusCidCsdOverwrite => new Result.Base(ModuleFs, 3561);
                        /// <summary>Error code: 2002-3562; Inner value: 0x1bd402</summary>
                        public static Result.Base PortMmcDeviceStatusWpEraseSkip => new Result.Base(ModuleFs, 3562);
                        /// <summary>Error code: 2002-3563; Inner value: 0x1bd602</summary>
                        public static Result.Base PortMmcDeviceStatusEraseReset => new Result.Base(ModuleFs, 3563);
                        /// <summary>Error code: 2002-3564; Inner value: 0x1bd802</summary>
                        public static Result.Base PortMmcDeviceStatusSwitchError => new Result.Base(ModuleFs, 3564);

                    /// <summary>Error code: 2002-3572; Inner value: 0x1be802</summary>
                    public static Result.Base PortMmcUnexpectedDeviceState => new Result.Base(ModuleFs, 3572);
                    /// <summary>Error code: 2002-3573; Inner value: 0x1bea02</summary>
                    public static Result.Base PortMmcUnexpectedDeviceCsdValue => new Result.Base(ModuleFs, 3573);
                    /// <summary>Error code: 2002-3574; Inner value: 0x1bec02</summary>
                    public static Result.Base PortMmcAbortTransactionSoftwareTimeout => new Result.Base(ModuleFs, 3574);
                    /// <summary>Error code: 2002-3575; Inner value: 0x1bee02</summary>
                    public static Result.Base PortMmcCommandInhibitCmdSoftwareTimeout => new Result.Base(ModuleFs, 3575);
                    /// <summary>Error code: 2002-3576; Inner value: 0x1bf002</summary>
                    public static Result.Base PortMmcCommandInhibitDatSoftwareTimeout => new Result.Base(ModuleFs, 3576);
                    /// <summary>Error code: 2002-3577; Inner value: 0x1bf202</summary>
                    public static Result.Base PortMmcBusySoftwareTimeout => new Result.Base(ModuleFs, 3577);
                    /// <summary>Error code: 2002-3578; Inner value: 0x1bf402</summary>
                    public static Result.Base PortMmcIssueTuningCommandSoftwareTimeout => new Result.Base(ModuleFs, 3578);
                    /// <summary>Error code: 2002-3579; Inner value: 0x1bf602</summary>
                    public static Result.Base PortMmcTuningFailed => new Result.Base(ModuleFs, 3579);
                    /// <summary>Error code: 2002-3580; Inner value: 0x1bf802</summary>
                    public static Result.Base PortMmcMmcInitializationSoftwareTimeout => new Result.Base(ModuleFs, 3580);
                    /// <summary>Error code: 2002-3581; Inner value: 0x1bfa02</summary>
                    public static Result.Base PortMmcMmcNotSupportExtendedCsd => new Result.Base(ModuleFs, 3581);
                    /// <summary>Error code: 2002-3582; Inner value: 0x1bfc02</summary>
                    public static Result.Base PortMmcUnexpectedMmcExtendedCsdValue => new Result.Base(ModuleFs, 3582);
                    /// <summary>Error code: 2002-3583; Inner value: 0x1bfe02</summary>
                    public static Result.Base PortMmcMmcEraseSoftwareTimeout => new Result.Base(ModuleFs, 3583);
                    /// <summary>Error code: 2002-3584; Inner value: 0x1c0002</summary>
                    public static Result.Base PortMmcSdCardValidationError => new Result.Base(ModuleFs, 3584);
                    /// <summary>Error code: 2002-3585; Inner value: 0x1c0202</summary>
                    public static Result.Base PortMmcSdCardInitializationSoftwareTimeout => new Result.Base(ModuleFs, 3585);
                    /// <summary>Error code: 2002-3586; Inner value: 0x1c0402</summary>
                    public static Result.Base PortMmcSdCardGetValidRcaSoftwareTimeout => new Result.Base(ModuleFs, 3586);
                    /// <summary>Error code: 2002-3587; Inner value: 0x1c0602</summary>
                    public static Result.Base PortMmcUnexpectedSdCardAcmdDisabled => new Result.Base(ModuleFs, 3587);
                    /// <summary>Error code: 2002-3588; Inner value: 0x1c0802</summary>
                    public static Result.Base PortMmcSdCardNotSupportSwitchFunctionStatus => new Result.Base(ModuleFs, 3588);
                    /// <summary>Error code: 2002-3589; Inner value: 0x1c0a02</summary>
                    public static Result.Base PortMmcUnexpectedSdCardSwitchFunctionStatus => new Result.Base(ModuleFs, 3589);
                    /// <summary>Error code: 2002-3590; Inner value: 0x1c0c02</summary>
                    public static Result.Base PortMmcSdCardNotSupportAccessMode => new Result.Base(ModuleFs, 3590);
                    /// <summary>Error code: 2002-3591; Inner value: 0x1c0e02</summary>
                    public static Result.Base PortMmcSdCardNot4BitBusWidthAtUhsIMode => new Result.Base(ModuleFs, 3591);
                    /// <summary>Error code: 2002-3592; Inner value: 0x1c1002</summary>
                    public static Result.Base PortMmcSdCardNotSupportSdr104AndSdr50 => new Result.Base(ModuleFs, 3592);
                    /// <summary>Error code: 2002-3593; Inner value: 0x1c1202</summary>
                    public static Result.Base PortMmcSdCardCannotSwitchAccessMode => new Result.Base(ModuleFs, 3593);
                    /// <summary>Error code: 2002-3594; Inner value: 0x1c1402</summary>
                    public static Result.Base PortMmcSdCardFailedSwitchAccessMode => new Result.Base(ModuleFs, 3594);
                    /// <summary>Error code: 2002-3595; Inner value: 0x1c1602</summary>
                    public static Result.Base PortMmcSdCardUnacceptableCurrentConsumption => new Result.Base(ModuleFs, 3595);
                    /// <summary>Error code: 2002-3596; Inner value: 0x1c1802</summary>
                    public static Result.Base PortMmcSdCardNotReadyToVoltageSwitch => new Result.Base(ModuleFs, 3596);
                    /// <summary>Error code: 2002-3597; Inner value: 0x1c1a02</summary>
                    public static Result.Base PortMmcSdCardNotCompleteVoltageSwitch => new Result.Base(ModuleFs, 3597);

                /// <summary>Error code: 2002-3628; Range: 3628-3658; Inner value: 0x1c5802</summary>
                public static Result.Base PortMmcHostControllerUnexpected { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 3628, 3658); }
                    /// <summary>Error code: 2002-3629; Inner value: 0x1c5a02</summary>
                    public static Result.Base PortMmcInternalClockStableSoftwareTimeout => new Result.Base(ModuleFs, 3629);
                    /// <summary>Error code: 2002-3630; Inner value: 0x1c5c02</summary>
                    public static Result.Base PortMmcSdHostStandardUnknownAutoCmdError => new Result.Base(ModuleFs, 3630);
                    /// <summary>Error code: 2002-3631; Inner value: 0x1c5e02</summary>
                    public static Result.Base PortMmcSdHostStandardUnknownError => new Result.Base(ModuleFs, 3631);
                    /// <summary>Error code: 2002-3632; Inner value: 0x1c6002</summary>
                    public static Result.Base PortMmcSdmmcDllCalibrationSoftwareTimeout => new Result.Base(ModuleFs, 3632);
                    /// <summary>Error code: 2002-3633; Inner value: 0x1c6202</summary>
                    public static Result.Base PortMmcSdmmcDllApplicationSoftwareTimeout => new Result.Base(ModuleFs, 3633);
                    /// <summary>Error code: 2002-3634; Inner value: 0x1c6402</summary>
                    public static Result.Base PortMmcSdHostStandardFailSwitchTo18V => new Result.Base(ModuleFs, 3634);

                /// <summary>Error code: 2002-3660; Range: 3660-3690; Inner value: 0x1c9802</summary>
                public static Result.Base PortMmcInternalError { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 3660, 3690); }
                    /// <summary>Error code: 2002-3661; Inner value: 0x1c9a02</summary>
                    public static Result.Base PortMmcNoWaitedInterrupt => new Result.Base(ModuleFs, 3661);
                    /// <summary>Error code: 2002-3662; Inner value: 0x1c9c02</summary>
                    public static Result.Base PortMmcWaitInterruptSoftwareTimeout => new Result.Base(ModuleFs, 3662);

                /// <summary>Error code: 2002-3692; Inner value: 0x1cd802</summary>
                public static Result.Base PortMmcAbortCommandIssued => new Result.Base(ModuleFs, 3692);
                /// <summary>Error code: 2002-3700; Inner value: 0x1ce802</summary>
                public static Result.Base PortMmcNotSupported => new Result.Base(ModuleFs, 3700);
                /// <summary>Error code: 2002-3701; Inner value: 0x1cea02</summary>
                public static Result.Base PortMmcNotImplemented => new Result.Base(ModuleFs, 3701);
                /// <summary>Error code: 2002-3998; Inner value: 0x1f3c02</summary>
                public static Result.Base Result3998 => new Result.Base(ModuleFs, 3998);
                /// <summary>Error code: 2002-3999; Inner value: 0x1f3e02</summary>
                public static Result.Base PortMmcUnexpected => new Result.Base(ModuleFs, 3999);

            /// <summary>Error code: 2002-4000; Range: 4000-4999; Inner value: 0x1f4002</summary>
            public static Result.Base DataCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4000, 4999); }
                /// <summary>Error code: 2002-4001; Range: 4001-4299; Inner value: 0x1f4202</summary>
                public static Result.Base RomCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4001, 4299); }
                    /// <summary>Error code: 2002-4002; Inner value: 0x1f4402</summary>
                    public static Result.Base UnsupportedRomVersion => new Result.Base(ModuleFs, 4002);

                    /// <summary>Error code: 2002-4011; Range: 4011-4019; Inner value: 0x1f5602</summary>
                    public static Result.Base AesCtrCounterExtendedStorageCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4011, 4019); }
                        /// <summary>Error code: 2002-4012; Inner value: 0x1f5802</summary>
                        public static Result.Base InvalidAesCtrCounterExtendedEntryOffset => new Result.Base(ModuleFs, 4012);
                        /// <summary>Error code: 2002-4013; Inner value: 0x1f5a02</summary>
                        public static Result.Base InvalidAesCtrCounterExtendedTableSize => new Result.Base(ModuleFs, 4013);
                        /// <summary>Error code: 2002-4014; Inner value: 0x1f5c02</summary>
                        public static Result.Base InvalidAesCtrCounterExtendedGeneration => new Result.Base(ModuleFs, 4014);
                        /// <summary>Error code: 2002-4015; Inner value: 0x1f5e02</summary>
                        public static Result.Base InvalidAesCtrCounterExtendedOffset => new Result.Base(ModuleFs, 4015);
                        /// <summary>Error code: 2002-4016; Inner value: 0x1f6002</summary>
                        public static Result.Base Result4016 => new Result.Base(ModuleFs, 4016);

                    /// <summary>Error code: 2002-4021; Range: 4021-4029; Inner value: 0x1f6a02</summary>
                    public static Result.Base IndirectStorageCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4021, 4029); }
                        /// <summary>Error code: 2002-4022; Inner value: 0x1f6c02</summary>
                        public static Result.Base InvalidIndirectEntryOffset => new Result.Base(ModuleFs, 4022);
                        /// <summary>Error code: 2002-4023; Inner value: 0x1f6e02</summary>
                        public static Result.Base InvalidIndirectEntryStorageIndex => new Result.Base(ModuleFs, 4023);
                        /// <summary>Error code: 2002-4024; Inner value: 0x1f7002</summary>
                        public static Result.Base InvalidIndirectStorageSize => new Result.Base(ModuleFs, 4024);
                        /// <summary>Error code: 2002-4025; Inner value: 0x1f7202</summary>
                        public static Result.Base InvalidIndirectVirtualOffset => new Result.Base(ModuleFs, 4025);
                        /// <summary>Error code: 2002-4026; Inner value: 0x1f7402</summary>
                        public static Result.Base InvalidIndirectPhysicalOffset => new Result.Base(ModuleFs, 4026);
                        /// <summary>Error code: 2002-4027; Inner value: 0x1f7602</summary>
                        public static Result.Base InvalidIndirectStorageIndex => new Result.Base(ModuleFs, 4027);
                        /// <summary>Error code: 2002-4028; Inner value: 0x1f7802</summary>
                        public static Result.Base InvalidIndirectStorageBucketTreeSize => new Result.Base(ModuleFs, 4028);

                    /// <summary>Error code: 2002-4031; Range: 4031-4039; Inner value: 0x1f7e02</summary>
                    public static Result.Base BucketTreeCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4031, 4039); }
                        /// <summary>Error code: 2002-4032; Inner value: 0x1f8002</summary>
                        public static Result.Base InvalidBucketTreeSignature => new Result.Base(ModuleFs, 4032);
                        /// <summary>Error code: 2002-4033; Inner value: 0x1f8202</summary>
                        public static Result.Base InvalidBucketTreeEntryCount => new Result.Base(ModuleFs, 4033);
                        /// <summary>Error code: 2002-4034; Inner value: 0x1f8402</summary>
                        public static Result.Base InvalidBucketTreeNodeEntryCount => new Result.Base(ModuleFs, 4034);
                        /// <summary>Error code: 2002-4035; Inner value: 0x1f8602</summary>
                        public static Result.Base InvalidBucketTreeNodeOffset => new Result.Base(ModuleFs, 4035);
                        /// <summary>Error code: 2002-4036; Inner value: 0x1f8802</summary>
                        public static Result.Base InvalidBucketTreeEntryOffset => new Result.Base(ModuleFs, 4036);
                        /// <summary>Error code: 2002-4037; Inner value: 0x1f8a02</summary>
                        public static Result.Base InvalidBucketTreeEntrySetOffset => new Result.Base(ModuleFs, 4037);
                        /// <summary>Error code: 2002-4038; Inner value: 0x1f8c02</summary>
                        public static Result.Base InvalidBucketTreeNodeIndex => new Result.Base(ModuleFs, 4038);
                        /// <summary>Error code: 2002-4039; Inner value: 0x1f8e02</summary>
                        public static Result.Base InvalidBucketTreeVirtualOffset => new Result.Base(ModuleFs, 4039);

                    /// <summary>Error code: 2002-4041; Range: 4041-4139; Inner value: 0x1f9202</summary>
                    public static Result.Base RomNcaCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4041, 4139); }
                        /// <summary>Error code: 2002-4051; Range: 4051-4069; Inner value: 0x1fa602</summary>
                        public static Result.Base RomNcaFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4051, 4069); }
                            /// <summary>Error code: 2002-4052; Inner value: 0x1fa802</summary>
                            public static Result.Base InvalidRomNcaFileSystemType => new Result.Base(ModuleFs, 4052);
                            /// <summary>Error code: 2002-4053; Inner value: 0x1faa02</summary>
                            public static Result.Base InvalidRomAcidFileSize => new Result.Base(ModuleFs, 4053);
                            /// <summary>Error code: 2002-4054; Inner value: 0x1fac02</summary>
                            public static Result.Base InvalidRomAcidSize => new Result.Base(ModuleFs, 4054);
                            /// <summary>Error code: 2002-4055; Inner value: 0x1fae02</summary>
                            public static Result.Base InvalidRomAcid => new Result.Base(ModuleFs, 4055);
                            /// <summary>Error code: 2002-4056; Inner value: 0x1fb002</summary>
                            public static Result.Base RomAcidVerificationFailed => new Result.Base(ModuleFs, 4056);
                            /// <summary>Error code: 2002-4057; Inner value: 0x1fb202</summary>
                            public static Result.Base InvalidRomNcaSignature => new Result.Base(ModuleFs, 4057);
                            /// <summary>Error code: 2002-4058; Inner value: 0x1fb402</summary>
                            public static Result.Base RomNcaHeaderSignature1VerificationFailed => new Result.Base(ModuleFs, 4058);
                            /// <summary>Error code: 2002-4059; Inner value: 0x1fb602</summary>
                            public static Result.Base RomNcaHeaderSignature2VerificationFailed => new Result.Base(ModuleFs, 4059);
                            /// <summary>Error code: 2002-4060; Inner value: 0x1fb802</summary>
                            public static Result.Base RomNcaFsHeaderHashVerificationFailed => new Result.Base(ModuleFs, 4060);
                            /// <summary>Error code: 2002-4061; Inner value: 0x1fba02</summary>
                            public static Result.Base InvalidRomNcaKeyIndex => new Result.Base(ModuleFs, 4061);
                            /// <summary>Error code: 2002-4062; Inner value: 0x1fbc02</summary>
                            public static Result.Base InvalidRomNcaFsHeaderHashType => new Result.Base(ModuleFs, 4062);
                            /// <summary>Error code: 2002-4063; Inner value: 0x1fbe02</summary>
                            public static Result.Base InvalidRomNcaFsHeaderEncryptionType => new Result.Base(ModuleFs, 4063);
                            /// <summary>Error code: 2002-4064; Inner value: 0x1fc002</summary>
                            public static Result.Base InvalidRomNcaPatchInfoIndirectSize => new Result.Base(ModuleFs, 4064);
                            /// <summary>Error code: 2002-4065; Inner value: 0x1fc202</summary>
                            public static Result.Base InvalidRomNcaPatchInfoAesCtrExSize => new Result.Base(ModuleFs, 4065);
                            /// <summary>Error code: 2002-4066; Inner value: 0x1fc402</summary>
                            public static Result.Base InvalidRomNcaPatchInfoAesCtrExOffset => new Result.Base(ModuleFs, 4066);
                            /// <summary>Error code: 2002-4067; Inner value: 0x1fc602</summary>
                            public static Result.Base InvalidRomNcaId => new Result.Base(ModuleFs, 4067);
                            /// <summary>Error code: 2002-4068; Inner value: 0x1fc802</summary>
                            public static Result.Base InvalidRomNcaHeader => new Result.Base(ModuleFs, 4068);
                            /// <summary>Error code: 2002-4069; Inner value: 0x1fca02</summary>
                            public static Result.Base InvalidRomNcaFsHeader => new Result.Base(ModuleFs, 4069);

                        /// <summary>Error code: 2002-4070; Inner value: 0x1fcc02</summary>
                        public static Result.Base InvalidRomNcaPatchInfoIndirectOffset => new Result.Base(ModuleFs, 4070);

                        /// <summary>Error code: 2002-4071; Range: 4071-4079; Inner value: 0x1fce02</summary>
                        public static Result.Base RomNcaHierarchicalSha256StorageCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4071, 4079); }
                            /// <summary>Error code: 2002-4072; Inner value: 0x1fd002</summary>
                            public static Result.Base InvalidRomHierarchicalSha256BlockSize => new Result.Base(ModuleFs, 4072);
                            /// <summary>Error code: 2002-4073; Inner value: 0x1fd202</summary>
                            public static Result.Base InvalidRomHierarchicalSha256LayerCount => new Result.Base(ModuleFs, 4073);
                            /// <summary>Error code: 2002-4074; Inner value: 0x1fd402</summary>
                            public static Result.Base RomHierarchicalSha256BaseStorageTooLarge => new Result.Base(ModuleFs, 4074);
                            /// <summary>Error code: 2002-4075; Inner value: 0x1fd602</summary>
                            public static Result.Base RomHierarchicalSha256HashVerificationFailed => new Result.Base(ModuleFs, 4075);

                        /// <summary>Error code: 2002-4081; Inner value: 0x1fe202</summary>
                        public static Result.Base InvalidRomHierarchicalIntegrityVerificationLayerCount => new Result.Base(ModuleFs, 4081);
                        /// <summary>Error code: 2002-4082; Inner value: 0x1fe402</summary>
                        public static Result.Base RomNcaIndirectStorageOutOfRange => new Result.Base(ModuleFs, 4082);

                    /// <summary>Error code: 2002-4141; Range: 4141-4179; Inner value: 0x205a02</summary>
                    public static Result.Base RomIntegrityVerificationStorageCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4141, 4179); }
                        /// <summary>Error code: 2002-4142; Inner value: 0x205c02</summary>
                        public static Result.Base IncorrectRomIntegrityVerificationMagic => new Result.Base(ModuleFs, 4142);
                        /// <summary>Error code: 2002-4143; Inner value: 0x205e02</summary>
                        public static Result.Base InvalidRomZeroSignature => new Result.Base(ModuleFs, 4143);
                        /// <summary>Error code: 2002-4144; Inner value: 0x206002</summary>
                        public static Result.Base RomNonRealDataVerificationFailed => new Result.Base(ModuleFs, 4144);

                        /// <summary>Error code: 2002-4151; Range: 4151-4159; Inner value: 0x206e02</summary>
                        public static Result.Base RomRealDataVerificationFailed { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4151, 4159); }
                            /// <summary>Error code: 2002-4152; Inner value: 0x207002</summary>
                            public static Result.Base ClearedRomRealDataVerificationFailed => new Result.Base(ModuleFs, 4152);
                            /// <summary>Error code: 2002-4153; Inner value: 0x207202</summary>
                            public static Result.Base UnclearedRomRealDataVerificationFailed => new Result.Base(ModuleFs, 4153);

                    /// <summary>Error code: 2002-4181; Range: 4181-4199; Inner value: 0x20aa02</summary>
                    public static Result.Base RomPartitionFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4181, 4199); }
                        /// <summary>Error code: 2002-4182; Inner value: 0x20ac02</summary>
                        public static Result.Base InvalidRomSha256PartitionHashTarget => new Result.Base(ModuleFs, 4182);
                        /// <summary>Error code: 2002-4183; Inner value: 0x20ae02</summary>
                        public static Result.Base RomSha256PartitionHashVerificationFailed => new Result.Base(ModuleFs, 4183);
                        /// <summary>Error code: 2002-4184; Inner value: 0x20b002</summary>
                        public static Result.Base RomPartitionSignatureVerificationFailed => new Result.Base(ModuleFs, 4184);
                        /// <summary>Error code: 2002-4185; Inner value: 0x20b202</summary>
                        public static Result.Base RomSha256PartitionSignatureVerificationFailed => new Result.Base(ModuleFs, 4185);
                        /// <summary>Error code: 2002-4186; Inner value: 0x20b402</summary>
                        public static Result.Base InvalidRomPartitionEntryOffset => new Result.Base(ModuleFs, 4186);
                        /// <summary>Error code: 2002-4187; Inner value: 0x20b602</summary>
                        public static Result.Base InvalidRomSha256PartitionMetaDataSize => new Result.Base(ModuleFs, 4187);

                    /// <summary>Error code: 2002-4201; Range: 4201-4219; Inner value: 0x20d202</summary>
                    public static Result.Base RomBuiltInStorageCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4201, 4219); }
                        /// <summary>Error code: 2002-4202; Inner value: 0x20d402</summary>
                        public static Result.Base RomGptHeaderSignatureVerificationFailed => new Result.Base(ModuleFs, 4202);

                    /// <summary>Error code: 2002-4241; Range: 4241-4259; Inner value: 0x212202</summary>
                    public static Result.Base RomHostFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4241, 4259); }
                        /// <summary>Error code: 2002-4242; Inner value: 0x212402</summary>
                        public static Result.Base RomHostEntryCorrupted => new Result.Base(ModuleFs, 4242);
                        /// <summary>Error code: 2002-4243; Inner value: 0x212602</summary>
                        public static Result.Base RomHostFileDataCorrupted => new Result.Base(ModuleFs, 4243);
                        /// <summary>Error code: 2002-4244; Inner value: 0x212802</summary>
                        public static Result.Base RomHostFileCorrupted => new Result.Base(ModuleFs, 4244);
                        /// <summary>Error code: 2002-4245; Inner value: 0x212a02</summary>
                        public static Result.Base InvalidRomHostHandle => new Result.Base(ModuleFs, 4245);

                    /// <summary>Error code: 2002-4261; Range: 4261-4279; Inner value: 0x214a02</summary>
                    public static Result.Base RomDatabaseCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4261, 4279); }
                        /// <summary>Error code: 2002-4262; Inner value: 0x214c02</summary>
                        public static Result.Base InvalidRomAllocationTableBlock => new Result.Base(ModuleFs, 4262);
                        /// <summary>Error code: 2002-4263; Inner value: 0x214e02</summary>
                        public static Result.Base InvalidRomKeyValueListElementIndex => new Result.Base(ModuleFs, 4263);

                    /// <summary>Returned directly when the header says the total size of the RomFs metadata is 0 bytes.<br/>Error code: 2002-4280; Range: 4280-4284; Inner value: 0x217002</summary>
                    public static Result.Base RomStorageCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4280, 4284); }
                        /// <summary>The RomFs metadata is located beyond the end of the provided storage.<br/>Error code: 2002-4281; Inner value: 0x217202</summary>
                        public static Result.Base InvalidRomStorageSize => new Result.Base(ModuleFs, 4281);

                /// <summary>Error code: 2002-4301; Range: 4301-4499; Inner value: 0x219a02</summary>
                public static Result.Base SaveDataCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4301, 4499); }
                    /// <summary>Error code: 2002-4302; Inner value: 0x219c02</summary>
                    public static Result.Base UnsupportedSaveDataVersion => new Result.Base(ModuleFs, 4302);
                    /// <summary>Error code: 2002-4303; Inner value: 0x219e02</summary>
                    public static Result.Base InvalidSaveDataEntryType => new Result.Base(ModuleFs, 4303);

                    /// <summary>Error code: 2002-4311; Range: 4311-4319; Inner value: 0x21ae02</summary>
                    public static Result.Base SaveDataFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4311, 4319); }
                        /// <summary>Error code: 2002-4312; Inner value: 0x21b002</summary>
                        public static Result.Base InvalidJournalIntegritySaveDataHashSize => new Result.Base(ModuleFs, 4312);
                        /// <summary>Error code: 2002-4313; Inner value: 0x21b202</summary>
                        public static Result.Base InvalidJournalIntegritySaveDataCommitState => new Result.Base(ModuleFs, 4313);
                        /// <summary>Error code: 2002-4314; Inner value: 0x21b402</summary>
                        public static Result.Base InvalidJournalIntegritySaveDataControlAreaSize => new Result.Base(ModuleFs, 4314);
                        /// <summary>Error code: 2002-4315; Inner value: 0x21b602</summary>
                        public static Result.Base JournalIntegritySaveDataControlAreaVerificationFailed => new Result.Base(ModuleFs, 4315);
                        /// <summary>Error code: 2002-4316; Inner value: 0x21b802</summary>
                        public static Result.Base JournalIntegritySaveDataMasterSignatureVerificationFailed => new Result.Base(ModuleFs, 4316);
                        /// <summary>Error code: 2002-4317; Inner value: 0x21ba02</summary>
                        public static Result.Base IncorrectJournalIntegritySaveDataMagicCode => new Result.Base(ModuleFs, 4317);

                    /// <summary>Error code: 2002-4321; Range: 4321-4329; Inner value: 0x21c202</summary>
                    public static Result.Base SaveDataDuplexStorageCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4321, 4329); }
                        /// <summary>Error code: 2002-4322; Inner value: 0x21c402</summary>
                        public static Result.Base IncorrectDuplexMagicCode => new Result.Base(ModuleFs, 4322);
                        /// <summary>Error code: 2002-4323; Inner value: 0x21c602</summary>
                        public static Result.Base DuplexStorageAccessOutOfRange => new Result.Base(ModuleFs, 4323);

                    /// <summary>Error code: 2002-4331; Range: 4331-4339; Inner value: 0x21d602</summary>
                    public static Result.Base SaveDataMapCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4331, 4339); }
                        /// <summary>Error code: 2002-4332; Inner value: 0x21d802</summary>
                        public static Result.Base InvalidMapEntryCount => new Result.Base(ModuleFs, 4332);
                        /// <summary>Error code: 2002-4333; Inner value: 0x21da02</summary>
                        public static Result.Base InvalidMapOffset => new Result.Base(ModuleFs, 4333);
                        /// <summary>Error code: 2002-4334; Inner value: 0x21dc02</summary>
                        public static Result.Base InvalidMapSize => new Result.Base(ModuleFs, 4334);
                        /// <summary>Error code: 2002-4335; Inner value: 0x21de02</summary>
                        public static Result.Base InvalidMapAlignment => new Result.Base(ModuleFs, 4335);
                        /// <summary>Error code: 2002-4336; Inner value: 0x21e002</summary>
                        public static Result.Base InvalidMapStorageType => new Result.Base(ModuleFs, 4336);
                        /// <summary>The range of the new map entry overlaps with the range of an existing map entry.<br/>Error code: 2002-4337; Inner value: 0x21e202</summary>
                        public static Result.Base MapAddressAlreadyRegistered => new Result.Base(ModuleFs, 4337);
                        /// <summary>The storage for the map entry's storage type hasn't been registered.<br/>Error code: 2002-4338; Inner value: 0x21e402</summary>
                        public static Result.Base MapStorageNotFound => new Result.Base(ModuleFs, 4338);
                        /// <summary>The storage registered for the map entry's storage type is too short to contain the physical range specified in the map entry.<br/>Error code: 2002-4339; Inner value: 0x21e602</summary>
                        public static Result.Base InvalidMapStorageSize => new Result.Base(ModuleFs, 4339);

                    /// <summary>Error code: 2002-4341; Range: 4341-4349; Inner value: 0x21ea02</summary>
                    public static Result.Base SaveDataLogCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4341, 4349); }
                        /// <summary>Error code: 2002-4342; Inner value: 0x21ec02</summary>
                        public static Result.Base InvalidLogBlockSize => new Result.Base(ModuleFs, 4342);
                        /// <summary>Error code: 2002-4343; Inner value: 0x21ee02</summary>
                        public static Result.Base InvalidLogOffset => new Result.Base(ModuleFs, 4343);
                        /// <summary>Error code: 2002-4344; Inner value: 0x21f002</summary>
                        public static Result.Base UnexpectedEndOfLog => new Result.Base(ModuleFs, 4344);
                        /// <summary>Error code: 2002-4345; Inner value: 0x21f202</summary>
                        public static Result.Base LogNotFound => new Result.Base(ModuleFs, 4345);

                    /// <summary>Error code: 2002-4352; Inner value: 0x220002</summary>
                    public static Result.Base ThumbnailHashVerificationFailed => new Result.Base(ModuleFs, 4352);
                    /// <summary>Error code: 2002-4357; Inner value: 0x220a02</summary>
                    public static Result.Base InvalidSaveDataInternalStorageIntegritySeedSize => new Result.Base(ModuleFs, 4357);
                    /// <summary>Error code: 2002-4358; Inner value: 0x220c02</summary>
                    public static Result.Base Result4358 => new Result.Base(ModuleFs, 4358);
                    /// <summary>Error code: 2002-4359; Inner value: 0x220e02</summary>
                    public static Result.Base Result4359 => new Result.Base(ModuleFs, 4359);

                    /// <summary>Error code: 2002-4361; Range: 4361-4399; Inner value: 0x221202</summary>
                    public static Result.Base SaveDataIntegrityVerificationStorageCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4361, 4399); }
                        /// <summary>Error code: 2002-4362; Inner value: 0x221402</summary>
                        public static Result.Base IncorrectSaveDataIntegrityVerificationMagic => new Result.Base(ModuleFs, 4362);
                        /// <summary>Error code: 2002-4363; Inner value: 0x221602</summary>
                        public static Result.Base InvalidSaveDataZeroHash => new Result.Base(ModuleFs, 4363);
                        /// <summary>Error code: 2002-4364; Inner value: 0x221802</summary>
                        public static Result.Base SaveDataNonRealDataVerificationFailed => new Result.Base(ModuleFs, 4364);
                        /// <summary>Error code: 2002-4372; Inner value: 0x222802</summary>
                        public static Result.Base ClearedSaveDataRealDataVerificationFailed => new Result.Base(ModuleFs, 4372);
                        /// <summary>Error code: 2002-4373; Inner value: 0x222a02</summary>
                        public static Result.Base UnclearedSaveDataRealDataVerificationFailed => new Result.Base(ModuleFs, 4373);

                    /// <summary>Error code: 2002-4402; Inner value: 0x226402</summary>
                    public static Result.Base SaveDataGptHeaderSignatureVerificationFailed => new Result.Base(ModuleFs, 4402);

                    /// <summary>Error code: 2002-4411; Range: 4411-4419; Inner value: 0x227602</summary>
                    public static Result.Base SaveDataCoreFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4411, 4419); }
                        /// <summary>Error code: 2002-4412; Inner value: 0x227802</summary>
                        public static Result.Base IncorrectSaveDataControlAreaMagic => new Result.Base(ModuleFs, 4412);
                        /// <summary>Error code: 2002-4413; Inner value: 0x227a02</summary>
                        public static Result.Base InvalidSaveDataFileReadOffset => new Result.Base(ModuleFs, 4413);
                        /// <summary>Error code: 2002-4414; Inner value: 0x227c02</summary>
                        public static Result.Base InvalidSaveDataCoreDataStorageSize => new Result.Base(ModuleFs, 4414);

                    /// <summary>Error code: 2002-4427; Inner value: 0x229602</summary>
                    public static Result.Base IncompleteBlockInZeroBitmapHashStorageFileSaveData => new Result.Base(ModuleFs, 4427);

                    /// <summary>Error code: 2002-4431; Range: 4431-4439; Inner value: 0x229e02</summary>
                    public static Result.Base JournalStorageCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4431, 4439); }
                        /// <summary>Error code: 2002-4432; Inner value: 0x22a002</summary>
                        public static Result.Base JournalStorageAccessOutOfRange => new Result.Base(ModuleFs, 4432);
                        /// <summary>Error code: 2002-4433; Inner value: 0x22a202</summary>
                        public static Result.Base InvalidJournalStorageDataStorageSize => new Result.Base(ModuleFs, 4433);

                    /// <summary>Error code: 2002-4441; Range: 4441-4459; Inner value: 0x22b202</summary>
                    public static Result.Base SaveDataHostFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4441, 4459); }
                        /// <summary>Error code: 2002-4442; Inner value: 0x22b402</summary>
                        public static Result.Base SaveDataHostEntryCorrupted => new Result.Base(ModuleFs, 4442);
                        /// <summary>Error code: 2002-4443; Inner value: 0x22b602</summary>
                        public static Result.Base SaveDataHostFileDataCorrupted => new Result.Base(ModuleFs, 4443);
                        /// <summary>Error code: 2002-4444; Inner value: 0x22b802</summary>
                        public static Result.Base SaveDataHostFileCorrupted => new Result.Base(ModuleFs, 4444);
                        /// <summary>Error code: 2002-4445; Inner value: 0x22ba02</summary>
                        public static Result.Base InvalidSaveDataHostHandle => new Result.Base(ModuleFs, 4445);

                        /// <summary>Error code: 2002-4451; Range: 4451-4459; Inner value: 0x22c602</summary>
                        public static Result.Base MappingTableCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4451, 4459); }
                            /// <summary>Error code: 2002-4452; Inner value: 0x22c802</summary>
                            public static Result.Base InvalidMappingTableEntryCount => new Result.Base(ModuleFs, 4452);
                            /// <summary>Error code: 2002-4453; Inner value: 0x22ca02</summary>
                            public static Result.Base InvalidMappingTablePhysicalIndex => new Result.Base(ModuleFs, 4453);
                            /// <summary>Error code: 2002-4454; Inner value: 0x22cc02</summary>
                            public static Result.Base InvalidMappingTableVirtualIndex => new Result.Base(ModuleFs, 4454);

                    /// <summary>Error code: 2002-4461; Range: 4461-4479; Inner value: 0x22da02</summary>
                    public static Result.Base SaveDataDatabaseCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4461, 4479); }
                        /// <summary>Error code: 2002-4462; Inner value: 0x22dc02</summary>
                        public static Result.Base InvalidSaveDataAllocationTableBlock => new Result.Base(ModuleFs, 4462);
                        /// <summary>Error code: 2002-4463; Inner value: 0x22de02</summary>
                        public static Result.Base InvalidSaveDataKeyValueListElementIndex => new Result.Base(ModuleFs, 4463);
                        /// <summary>Error code: 2002-4464; Inner value: 0x22e002</summary>
                        public static Result.Base SaveDataAllocationTableIteratedRangeEntry => new Result.Base(ModuleFs, 4464);
                        /// <summary>Error code: 2002-4465; Inner value: 0x22e202</summary>
                        public static Result.Base InvalidSaveDataAllocationTableOffset => new Result.Base(ModuleFs, 4465);
                        /// <summary>Error code: 2002-4466; Inner value: 0x22e402</summary>
                        public static Result.Base InvalidSaveDataAllocationTableBlockCount => new Result.Base(ModuleFs, 4466);
                        /// <summary>Error code: 2002-4467; Inner value: 0x22e602</summary>
                        public static Result.Base InvalidSaveDataKeyValueListEntryIndex => new Result.Base(ModuleFs, 4467);
                        /// <summary>Error code: 2002-4468; Inner value: 0x22e802</summary>
                        public static Result.Base InvalidSaveDataBitmapIndex => new Result.Base(ModuleFs, 4468);

                    /// <summary>Error code: 2002-4481; Range: 4481-4489; Inner value: 0x230202</summary>
                    public static Result.Base SaveDataExtensionContextCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4481, 4489); }
                        /// <summary>Error code: 2002-4482; Inner value: 0x230402</summary>
                        public static Result.Base IncorrectSaveDataExtensionContextMagicCode => new Result.Base(ModuleFs, 4482);
                        /// <summary>Error code: 2002-4483; Inner value: 0x230602</summary>
                        public static Result.Base InvalidSaveDataExtensionContextState => new Result.Base(ModuleFs, 4483);
                        /// <summary>The extension context doesn't match the current save data.<br/>Error code: 2002-4484; Inner value: 0x230802</summary>
                        public static Result.Base DifferentSaveDataExtensionContextParameter => new Result.Base(ModuleFs, 4484);
                        /// <summary>Error code: 2002-4485; Inner value: 0x230a02</summary>
                        public static Result.Base InvalidSaveDataExtensionContextParameter => new Result.Base(ModuleFs, 4485);

                    /// <summary>Error code: 2002-4491; Range: 4491-4499; Inner value: 0x231602</summary>
                    public static Result.Base IntegritySaveDataCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4491, 4499); }
                        /// <summary>Error code: 2002-4492; Inner value: 0x231802</summary>
                        public static Result.Base InvalidIntegritySaveDataHashSize => new Result.Base(ModuleFs, 4492);
                        /// <summary>Error code: 2002-4494; Inner value: 0x231c02</summary>
                        public static Result.Base InvalidIntegritySaveDataControlAreaSize => new Result.Base(ModuleFs, 4494);
                        /// <summary>Error code: 2002-4495; Inner value: 0x231e02</summary>
                        public static Result.Base IntegritySaveDataControlAreaVerificationFailed => new Result.Base(ModuleFs, 4495);
                        /// <summary>Error code: 2002-4496; Inner value: 0x232002</summary>
                        public static Result.Base IntegritySaveDataMasterSignatureVerificationFailed => new Result.Base(ModuleFs, 4496);
                        /// <summary>Error code: 2002-4497; Inner value: 0x232202</summary>
                        public static Result.Base IncorrectIntegritySaveDataMagicCode => new Result.Base(ModuleFs, 4497);

                /// <summary>Error code: 2002-4501; Range: 4501-4599; Inner value: 0x232a02</summary>
                public static Result.Base NcaCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4501, 4599); }
                    /// <summary>Error code: 2002-4508; Inner value: 0x233802</summary>
                    public static Result.Base NcaBaseStorageOutOfRangeA => new Result.Base(ModuleFs, 4508);
                    /// <summary>Error code: 2002-4509; Inner value: 0x233a02</summary>
                    public static Result.Base NcaBaseStorageOutOfRangeB => new Result.Base(ModuleFs, 4509);
                    /// <summary>Error code: 2002-4510; Inner value: 0x233c02</summary>
                    public static Result.Base NcaBaseStorageOutOfRangeC => new Result.Base(ModuleFs, 4510);

                    /// <summary>Error code: 2002-4511; Range: 4511-4529; Inner value: 0x233e02</summary>
                    public static Result.Base NcaFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4511, 4529); }
                        /// <summary>Error code: 2002-4512; Inner value: 0x234002</summary>
                        public static Result.Base InvalidNcaFileSystemType => new Result.Base(ModuleFs, 4512);
                        /// <summary>Error code: 2002-4513; Inner value: 0x234202</summary>
                        public static Result.Base InvalidAcidFileSize => new Result.Base(ModuleFs, 4513);
                        /// <summary>Error code: 2002-4514; Inner value: 0x234402</summary>
                        public static Result.Base InvalidAcidSize => new Result.Base(ModuleFs, 4514);
                        /// <summary>Error code: 2002-4515; Inner value: 0x234602</summary>
                        public static Result.Base InvalidAcid => new Result.Base(ModuleFs, 4515);
                        /// <summary>Error code: 2002-4516; Inner value: 0x234802</summary>
                        public static Result.Base AcidVerificationFailed => new Result.Base(ModuleFs, 4516);
                        /// <summary>Error code: 2002-4517; Inner value: 0x234a02</summary>
                        public static Result.Base InvalidNcaSignature => new Result.Base(ModuleFs, 4517);
                        /// <summary>Error code: 2002-4518; Inner value: 0x234c02</summary>
                        public static Result.Base NcaHeaderSignature1VerificationFailed => new Result.Base(ModuleFs, 4518);
                        /// <summary>Error code: 2002-4519; Inner value: 0x234e02</summary>
                        public static Result.Base NcaHeaderSignature2VerificationFailed => new Result.Base(ModuleFs, 4519);
                        /// <summary>Error code: 2002-4520; Inner value: 0x235002</summary>
                        public static Result.Base NcaFsHeaderHashVerificationFailed => new Result.Base(ModuleFs, 4520);
                        /// <summary>Error code: 2002-4521; Inner value: 0x235202</summary>
                        public static Result.Base InvalidNcaKeyIndex => new Result.Base(ModuleFs, 4521);
                        /// <summary>Error code: 2002-4522; Inner value: 0x235402</summary>
                        public static Result.Base InvalidNcaFsHeaderHashType => new Result.Base(ModuleFs, 4522);
                        /// <summary>Error code: 2002-4523; Inner value: 0x235602</summary>
                        public static Result.Base InvalidNcaFsHeaderEncryptionType => new Result.Base(ModuleFs, 4523);
                        /// <summary>Error code: 2002-4524; Inner value: 0x235802</summary>
                        public static Result.Base InvalidNcaPatchInfoIndirectSize => new Result.Base(ModuleFs, 4524);
                        /// <summary>Error code: 2002-4525; Inner value: 0x235a02</summary>
                        public static Result.Base InvalidNcaPatchInfoAesCtrExSize => new Result.Base(ModuleFs, 4525);
                        /// <summary>Error code: 2002-4526; Inner value: 0x235c02</summary>
                        public static Result.Base InvalidNcaPatchInfoAesCtrExOffset => new Result.Base(ModuleFs, 4526);
                        /// <summary>Error code: 2002-4527; Inner value: 0x235e02</summary>
                        public static Result.Base InvalidNcaId => new Result.Base(ModuleFs, 4527);
                        /// <summary>Error code: 2002-4528; Inner value: 0x236002</summary>
                        public static Result.Base InvalidNcaHeader => new Result.Base(ModuleFs, 4528);
                        /// <summary>Error code: 2002-4529; Inner value: 0x236202</summary>
                        public static Result.Base InvalidNcaFsHeader => new Result.Base(ModuleFs, 4529);

                    /// <summary>Error code: 2002-4530; Inner value: 0x236402</summary>
                    public static Result.Base InvalidNcaPatchInfoIndirectOffset => new Result.Base(ModuleFs, 4530);

                    /// <summary>Error code: 2002-4531; Range: 4531-4539; Inner value: 0x236602</summary>
                    public static Result.Base NcaHierarchicalSha256StorageCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4531, 4539); }
                        /// <summary>Error code: 2002-4532; Inner value: 0x236802</summary>
                        public static Result.Base InvalidHierarchicalSha256BlockSize => new Result.Base(ModuleFs, 4532);
                        /// <summary>Error code: 2002-4533; Inner value: 0x236a02</summary>
                        public static Result.Base InvalidHierarchicalSha256LayerCount => new Result.Base(ModuleFs, 4533);
                        /// <summary>Error code: 2002-4534; Inner value: 0x236c02</summary>
                        public static Result.Base HierarchicalSha256BaseStorageTooLarge => new Result.Base(ModuleFs, 4534);
                        /// <summary>Error code: 2002-4535; Inner value: 0x236e02</summary>
                        public static Result.Base HierarchicalSha256HashVerificationFailed => new Result.Base(ModuleFs, 4535);

                    /// <summary>Error code: 2002-4541; Inner value: 0x237a02</summary>
                    public static Result.Base InvalidHierarchicalIntegrityVerificationLayerCount => new Result.Base(ModuleFs, 4541);
                    /// <summary>Error code: 2002-4542; Inner value: 0x237c02</summary>
                    public static Result.Base NcaIndirectStorageOutOfRange => new Result.Base(ModuleFs, 4542);
                    /// <summary>Error code: 2002-4543; Inner value: 0x237e02</summary>
                    public static Result.Base InvalidNcaHeader1SignatureKeyGeneration => new Result.Base(ModuleFs, 4543);
                    /// <summary>Error code: 2002-4545; Inner value: 0x238202</summary>
                    public static Result.Base InvalidNspdVerificationData => new Result.Base(ModuleFs, 4545);
                    /// <summary>Error code: 2002-4546; Inner value: 0x238402</summary>
                    public static Result.Base MissingNspdVerificationData => new Result.Base(ModuleFs, 4546);

                /// <summary>Error code: 2002-4601; Range: 4601-4639; Inner value: 0x23f202</summary>
                public static Result.Base IntegrityVerificationStorageCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4601, 4639); }
                    /// <summary>Error code: 2002-4602; Inner value: 0x23f402</summary>
                    public static Result.Base IncorrectIntegrityVerificationMagic => new Result.Base(ModuleFs, 4602);
                    /// <summary>Error code: 2002-4603; Inner value: 0x23f602</summary>
                    public static Result.Base InvalidZeroHash => new Result.Base(ModuleFs, 4603);
                    /// <summary>Error code: 2002-4604; Inner value: 0x23f802</summary>
                    public static Result.Base NonRealDataVerificationFailed => new Result.Base(ModuleFs, 4604);

                    /// <summary>Error code: 2002-4611; Range: 4611-4619; Inner value: 0x240602</summary>
                    public static Result.Base RealDataVerificationFailed { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4611, 4619); }
                        /// <summary>Error code: 2002-4612; Inner value: 0x240802</summary>
                        public static Result.Base ClearedRealDataVerificationFailed => new Result.Base(ModuleFs, 4612);
                        /// <summary>Error code: 2002-4613; Inner value: 0x240a02</summary>
                        public static Result.Base UnclearedRealDataVerificationFailed => new Result.Base(ModuleFs, 4613);

                /// <summary>Error code: 2002-4641; Range: 4641-4659; Inner value: 0x244202</summary>
                public static Result.Base PartitionFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4641, 4659); }
                    /// <summary>Error code: 2002-4642; Inner value: 0x244402</summary>
                    public static Result.Base InvalidSha256PartitionHashTarget => new Result.Base(ModuleFs, 4642);
                    /// <summary>Error code: 2002-4643; Inner value: 0x244602</summary>
                    public static Result.Base Sha256PartitionHashVerificationFailed => new Result.Base(ModuleFs, 4643);
                    /// <summary>Error code: 2002-4644; Inner value: 0x244802</summary>
                    public static Result.Base PartitionSignatureVerificationFailed => new Result.Base(ModuleFs, 4644);
                    /// <summary>Error code: 2002-4645; Inner value: 0x244a02</summary>
                    public static Result.Base Sha256PartitionSignatureVerificationFailed => new Result.Base(ModuleFs, 4645);
                    /// <summary>Error code: 2002-4646; Inner value: 0x244c02</summary>
                    public static Result.Base InvalidPartitionEntryOffset => new Result.Base(ModuleFs, 4646);
                    /// <summary>Error code: 2002-4647; Inner value: 0x244e02</summary>
                    public static Result.Base InvalidSha256PartitionMetaDataSize => new Result.Base(ModuleFs, 4647);

                /// <summary>Error code: 2002-4661; Range: 4661-4679; Inner value: 0x246a02</summary>
                public static Result.Base BuiltInStorageCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4661, 4679); }
                    /// <summary>Error code: 2002-4662; Inner value: 0x246c02</summary>
                    public static Result.Base InvalidGptPartitionSignature => new Result.Base(ModuleFs, 4662);
                    /// <summary>Error code: 2002-4664; Inner value: 0x247002</summary>
                    public static Result.Base InvalidGptPartitionStorageSize => new Result.Base(ModuleFs, 4664);

                /// <summary>Error code: 2002-4681; Range: 4681-4699; Inner value: 0x249202</summary>
                public static Result.Base FatFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4681, 4699); }
                    /// <summary>Error code: 2002-4683; Inner value: 0x249602</summary>
                    public static Result.Base InvalidFatFormat => new Result.Base(ModuleFs, 4683);
                    /// <summary>Error code: 2002-4684; Inner value: 0x249802</summary>
                    public static Result.Base InvalidFatFileNumber => new Result.Base(ModuleFs, 4684);
                    /// <summary>Error code: 2002-4685; Inner value: 0x249a02</summary>
                    public static Result.Base ExFatUnavailable => new Result.Base(ModuleFs, 4685);
                    /// <summary>Error code: 2002-4686; Inner value: 0x249c02</summary>
                    public static Result.Base InvalidFatFormatForBisUser => new Result.Base(ModuleFs, 4686);
                    /// <summary>Error code: 2002-4687; Inner value: 0x249e02</summary>
                    public static Result.Base InvalidFatFormatForBisSystem => new Result.Base(ModuleFs, 4687);
                    /// <summary>Error code: 2002-4688; Inner value: 0x24a002</summary>
                    public static Result.Base InvalidFatFormatForBisSafe => new Result.Base(ModuleFs, 4688);
                    /// <summary>Error code: 2002-4689; Inner value: 0x24a202</summary>
                    public static Result.Base InvalidFatFormatForBisCalibration => new Result.Base(ModuleFs, 4689);
                    /// <summary>Error code: 2002-4690; Inner value: 0x24a402</summary>
                    public static Result.Base InvalidFatFormatForSdCard => new Result.Base(ModuleFs, 4690);

                /// <summary>Error code: 2002-4701; Range: 4701-4719; Inner value: 0x24ba02</summary>
                public static Result.Base HostFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4701, 4719); }
                    /// <summary>Error code: 2002-4702; Inner value: 0x24bc02</summary>
                    public static Result.Base HostEntryCorrupted => new Result.Base(ModuleFs, 4702);
                    /// <summary>Error code: 2002-4703; Inner value: 0x24be02</summary>
                    public static Result.Base HostFileDataCorrupted => new Result.Base(ModuleFs, 4703);
                    /// <summary>Error code: 2002-4704; Inner value: 0x24c002</summary>
                    public static Result.Base HostFileCorrupted => new Result.Base(ModuleFs, 4704);
                    /// <summary>Error code: 2002-4705; Inner value: 0x24c202</summary>
                    public static Result.Base InvalidHostHandle => new Result.Base(ModuleFs, 4705);

                /// <summary>Error code: 2002-4721; Range: 4721-4739; Inner value: 0x24e202</summary>
                public static Result.Base DatabaseCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4721, 4739); }
                    /// <summary>Error code: 2002-4722; Inner value: 0x24e402</summary>
                    public static Result.Base InvalidAllocationTableBlock => new Result.Base(ModuleFs, 4722);
                    /// <summary>Error code: 2002-4723; Inner value: 0x24e602</summary>
                    public static Result.Base InvalidKeyValueListElementIndex => new Result.Base(ModuleFs, 4723);
                    /// <summary>Error code: 2002-4724; Inner value: 0x24e802</summary>
                    public static Result.Base AllocationTableIteratedRangeEntry => new Result.Base(ModuleFs, 4724);
                    /// <summary>Error code: 2002-4725; Inner value: 0x24ea02</summary>
                    public static Result.Base InvalidAllocationTableOffset => new Result.Base(ModuleFs, 4725);
                    /// <summary>Error code: 2002-4726; Inner value: 0x24ec02</summary>
                    public static Result.Base InvalidAllocationTableBlockCount => new Result.Base(ModuleFs, 4726);
                    /// <summary>Error code: 2002-4727; Inner value: 0x24ee02</summary>
                    public static Result.Base InvalidKeyValueListEntryIndex => new Result.Base(ModuleFs, 4727);
                    /// <summary>Error code: 2002-4728; Inner value: 0x24f002</summary>
                    public static Result.Base InvalidBitmapIndex => new Result.Base(ModuleFs, 4728);

                /// <summary>Error code: 2002-4741; Range: 4741-4759; Inner value: 0x250a02</summary>
                public static Result.Base AesXtsFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4741, 4759); }
                    /// <summary>Error code: 2002-4742; Inner value: 0x250c02</summary>
                    public static Result.Base AesXtsFileHeaderTooShort => new Result.Base(ModuleFs, 4742);
                    /// <summary>Error code: 2002-4743; Inner value: 0x250e02</summary>
                    public static Result.Base AesXtsFileHeaderInvalidKeys => new Result.Base(ModuleFs, 4743);
                    /// <summary>Error code: 2002-4744; Inner value: 0x251002</summary>
                    public static Result.Base AesXtsFileHeaderInvalidMagic => new Result.Base(ModuleFs, 4744);
                    /// <summary>Error code: 2002-4745; Inner value: 0x251202</summary>
                    public static Result.Base AesXtsFileTooShort => new Result.Base(ModuleFs, 4745);
                    /// <summary>Error code: 2002-4746; Inner value: 0x251402</summary>
                    public static Result.Base AesXtsFileHeaderTooShortInSetSize => new Result.Base(ModuleFs, 4746);
                    /// <summary>Error code: 2002-4747; Inner value: 0x251602</summary>
                    public static Result.Base AesXtsFileHeaderInvalidKeysInRenameFile => new Result.Base(ModuleFs, 4747);
                    /// <summary>Error code: 2002-4748; Inner value: 0x251802</summary>
                    public static Result.Base AesXtsFileHeaderInvalidKeysInSetSize => new Result.Base(ModuleFs, 4748);

                /// <summary>Error code: 2002-4761; Range: 4761-4769; Inner value: 0x253202</summary>
                public static Result.Base SaveDataTransferDataCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4761, 4769); }
                    /// <summary>Error code: 2002-4762; Inner value: 0x253402</summary>
                    public static Result.Base Result4762 => new Result.Base(ModuleFs, 4762);
                    /// <summary>Error code: 2002-4763; Inner value: 0x253602</summary>
                    public static Result.Base Result4763 => new Result.Base(ModuleFs, 4763);
                    /// <summary>Error code: 2002-4764; Inner value: 0x253802</summary>
                    public static Result.Base Result4764 => new Result.Base(ModuleFs, 4764);
                    /// <summary>Error code: 2002-4765; Inner value: 0x253a02</summary>
                    public static Result.Base Result4765 => new Result.Base(ModuleFs, 4765);
                    /// <summary>Error code: 2002-4766; Inner value: 0x253c02</summary>
                    public static Result.Base Result4766 => new Result.Base(ModuleFs, 4766);
                    /// <summary>Error code: 2002-4767; Inner value: 0x253e02</summary>
                    public static Result.Base Result4767 => new Result.Base(ModuleFs, 4767);

                /// <summary>Error code: 2002-4771; Range: 4771-4779; Inner value: 0x254602</summary>
                public static Result.Base SignedSystemPartitionDataCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4771, 4779); }
                    /// <summary>Error code: 2002-4772; Inner value: 0x254802</summary>
                    public static Result.Base Result4772 => new Result.Base(ModuleFs, 4772);
                    /// <summary>Error code: 2002-4773; Inner value: 0x254a02</summary>
                    public static Result.Base Result4773 => new Result.Base(ModuleFs, 4773);
                    /// <summary>Error code: 2002-4774; Inner value: 0x254c02</summary>
                    public static Result.Base Result4774 => new Result.Base(ModuleFs, 4774);
                    /// <summary>Error code: 2002-4775; Inner value: 0x254e02</summary>
                    public static Result.Base Result4775 => new Result.Base(ModuleFs, 4775);
                    /// <summary>Error code: 2002-4776; Inner value: 0x255002</summary>
                    public static Result.Base Result4776 => new Result.Base(ModuleFs, 4776);

                /// <summary>Error code: 2002-4781; Inner value: 0x255a02</summary>
                public static Result.Base GameCardLogoDataCorrupted => new Result.Base(ModuleFs, 4781);
                /// <summary>Error code: 2002-4785; Inner value: 0x256202</summary>
                public static Result.Base SimulatedDeviceDataCorrupted => new Result.Base(ModuleFs, 4785);

                /// <summary>Error code: 2002-4790; Range: 4790-4799; Inner value: 0x256c02</summary>
                public static Result.Base MultiCommitContextCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4790, 4799); }
                    /// <summary>The version of the multi-commit context file is too high for the current MultiCommitManager implementation.<br/>Error code: 2002-4791; Inner value: 0x256e02</summary>
                    public static Result.Base InvalidMultiCommitContextVersion => new Result.Base(ModuleFs, 4791);
                    /// <summary>The multi-commit has not been provisionally committed.<br/>Error code: 2002-4792; Inner value: 0x257002</summary>
                    public static Result.Base InvalidMultiCommitContextState => new Result.Base(ModuleFs, 4792);

                /// <summary>Error code: 2002-4811; Range: 4811-4819; Inner value: 0x259602</summary>
                public static Result.Base ZeroBitmapFileCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4811, 4819); }
                    /// <summary>Error code: 2002-4812; Inner value: 0x259802</summary>
                    public static Result.Base IncompleteBlockInZeroBitmapHashStorageFile => new Result.Base(ModuleFs, 4812);

            /// <summary>Error code: 2002-5000; Range: 5000-5999; Inner value: 0x271002</summary>
            public static Result.Base Unexpected { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 5000, 5999); }
                /// <summary>Error code: 2002-5002; Inner value: 0x271402</summary>
                public static Result.Base UnexpectedFatFileSystemError => new Result.Base(ModuleFs, 5002);
                /// <summary>Error code: 2002-5003; Inner value: 0x271602</summary>
                public static Result.Base FatFileSystemDriverError => new Result.Base(ModuleFs, 5003);
                /// <summary>Error code: 2002-5024; Inner value: 0x274002</summary>
                public static Result.Base FatSystemFileOpenCountLimit => new Result.Base(ModuleFs, 5024);
                /// <summary>Error code: 2002-5025; Inner value: 0x274202</summary>
                public static Result.Base FatUserFileOpenCountLimit => new Result.Base(ModuleFs, 5025);
                /// <summary>Error code: 2002-5026; Inner value: 0x274402</summary>
                public static Result.Base FatEntryIsDirectory => new Result.Base(ModuleFs, 5026);
                /// <summary>Error code: 2002-5028; Inner value: 0x274802</summary>
                public static Result.Base FatFileSystemPermissionDenied => new Result.Base(ModuleFs, 5028);
                /// <summary>Error code: 2002-5029; Inner value: 0x274a02</summary>
                public static Result.Base FatDriverInitializationFailed => new Result.Base(ModuleFs, 5029);
                /// <summary>Error code: 2002-5055; Inner value: 0x277e02</summary>
                public static Result.Base UnexpectedFatSafeError => new Result.Base(ModuleFs, 5055);
                /// <summary>Error code: 2002-5110; Inner value: 0x27ec02</summary>
                public static Result.Base Result5110 => new Result.Base(ModuleFs, 5110);
                /// <summary>Error code: 2002-5121; Inner value: 0x280202</summary>
                public static Result.Base UnexpectedFatFileSystemSectorCount => new Result.Base(ModuleFs, 5121);
                /// <summary>Error code: 2002-5122; Inner value: 0x280402</summary>
                public static Result.Base Result5122 => new Result.Base(ModuleFs, 5122);
                /// <summary>Error code: 2002-5123; Inner value: 0x280602</summary>
                public static Result.Base NullptrArgumentForFatFormat => new Result.Base(ModuleFs, 5123);
                /// <summary>Error code: 2002-5124; Inner value: 0x280802</summary>
                public static Result.Base InvalidFatFormatParamsA => new Result.Base(ModuleFs, 5124);
                /// <summary>Error code: 2002-5125; Inner value: 0x280a02</summary>
                public static Result.Base InvalidFatFormatParamsB => new Result.Base(ModuleFs, 5125);
                /// <summary>Error code: 2002-5126; Inner value: 0x280c02</summary>
                public static Result.Base InvalidFatFormatParamsC => new Result.Base(ModuleFs, 5126);
                /// <summary>Error code: 2002-5127; Inner value: 0x280e02</summary>
                public static Result.Base InvalidFatFormatParamsD => new Result.Base(ModuleFs, 5127);
                /// <summary>Error code: 2002-5131; Inner value: 0x281602</summary>
                public static Result.Base FatSectorWriteVerificationFailed => new Result.Base(ModuleFs, 5131);
                /// <summary>Tried to write to an IntegrityFilteredFile that is provisionally committed.<br/>Error code: 2002-5304; Inner value: 0x297002</summary>
                public static Result.Base UnexpectedInJournalIntegritySaveDataFileSystemC => new Result.Base(ModuleFs, 5304);
                /// <summary>Error code: 2002-5305; Inner value: 0x297202</summary>
                public static Result.Base UnexpectedInLocalFileSystemA => new Result.Base(ModuleFs, 5305);
                /// <summary>Error code: 2002-5306; Inner value: 0x297402</summary>
                public static Result.Base UnexpectedInLocalFileSystemB => new Result.Base(ModuleFs, 5306);
                /// <summary>Error code: 2002-5307; Inner value: 0x297602</summary>
                public static Result.Base UnexpectedInLocalFileSystemC => new Result.Base(ModuleFs, 5307);
                /// <summary>Error code: 2002-5308; Inner value: 0x297802</summary>
                public static Result.Base UnexpectedInLocalFileSystemD => new Result.Base(ModuleFs, 5308);
                /// <summary>Error code: 2002-5309; Inner value: 0x297a02</summary>
                public static Result.Base UnexpectedInLocalFileSystemE => new Result.Base(ModuleFs, 5309);
                /// <summary>Error code: 2002-5310; Inner value: 0x297c02</summary>
                public static Result.Base UnexpectedInLocalFileSystemF => new Result.Base(ModuleFs, 5310);
                /// <summary>Error code: 2002-5315; Inner value: 0x298602</summary>
                public static Result.Base UnexpectedInAesCtrStorageA => new Result.Base(ModuleFs, 5315);
                /// <summary>Error code: 2002-5316; Inner value: 0x298802</summary>
                public static Result.Base UnexpectedInAesXtsStorageA => new Result.Base(ModuleFs, 5316);
                /// <summary>Error code: 2002-5317; Inner value: 0x298a02</summary>
                public static Result.Base Result5317 => new Result.Base(ModuleFs, 5317);
                /// <summary>Error code: 2002-5318; Inner value: 0x298c02</summary>
                public static Result.Base Result5318 => new Result.Base(ModuleFs, 5318);
                /// <summary>Error code: 2002-5319; Inner value: 0x298e02</summary>
                public static Result.Base UnexpectedInMountUtilityA => new Result.Base(ModuleFs, 5319);
                /// <summary>Error code: 2002-5320; Inner value: 0x299002</summary>
                public static Result.Base InvalidNcaMountPoint => new Result.Base(ModuleFs, 5320);
                /// <summary>Error code: 2002-5321; Inner value: 0x299202</summary>
                public static Result.Base UnexpectedInRamDiskFileSystemA => new Result.Base(ModuleFs, 5321);
                /// <summary>Error code: 2002-5322; Inner value: 0x299402</summary>
                public static Result.Base UnexpectedInBisWiperA => new Result.Base(ModuleFs, 5322);
                /// <summary>Error code: 2002-5323; Inner value: 0x299602</summary>
                public static Result.Base UnexpectedInBisWiperB => new Result.Base(ModuleFs, 5323);

            /// <summary>Error code: 2002-6000; Range: 6000-6499; Inner value: 0x2ee002</summary>
            public static Result.Base PreconditionViolation { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6000, 6499); }
                /// <summary>Error code: 2002-6001; Range: 6001-6199; Inner value: 0x2ee202</summary>
                public static Result.Base InvalidArgument { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6001, 6199); }
                    /// <summary>Error code: 2002-6002; Range: 6002-6029; Inner value: 0x2ee402</summary>
                    public static Result.Base InvalidPath { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6002, 6029); }
                        /// <summary>Error code: 2002-6003; Inner value: 0x2ee602</summary>
                        public static Result.Base TooLongPath => new Result.Base(ModuleFs, 6003);
                        /// <summary>Error code: 2002-6004; Inner value: 0x2ee802</summary>
                        public static Result.Base InvalidCharacter => new Result.Base(ModuleFs, 6004);
                        /// <summary>Error code: 2002-6005; Inner value: 0x2eea02</summary>
                        public static Result.Base InvalidPathFormat => new Result.Base(ModuleFs, 6005);
                        /// <summary>Error code: 2002-6006; Inner value: 0x2eec02</summary>
                        public static Result.Base DirectoryUnobtainable => new Result.Base(ModuleFs, 6006);
                        /// <summary>Error code: 2002-6007; Inner value: 0x2eee02</summary>
                        public static Result.Base NotNormalized => new Result.Base(ModuleFs, 6007);

                    /// <summary>Error code: 2002-6030; Range: 6030-6059; Inner value: 0x2f1c02</summary>
                    public static Result.Base InvalidPathForOperation { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6030, 6059); }
                        /// <summary>Error code: 2002-6031; Inner value: 0x2f1e02</summary>
                        public static Result.Base DirectoryNotDeletable => new Result.Base(ModuleFs, 6031);
                        /// <summary>Error code: 2002-6032; Inner value: 0x2f2002</summary>
                        public static Result.Base DirectoryNotRenamable => new Result.Base(ModuleFs, 6032);
                        /// <summary>Error code: 2002-6033; Inner value: 0x2f2202</summary>
                        public static Result.Base IncompatiblePath => new Result.Base(ModuleFs, 6033);
                        /// <summary>Error code: 2002-6034; Inner value: 0x2f2402</summary>
                        public static Result.Base RenameToOtherFileSystem => new Result.Base(ModuleFs, 6034);

                    /// <summary>Error code: 2002-6061; Inner value: 0x2f5a02</summary>
                    public static Result.Base InvalidOffset => new Result.Base(ModuleFs, 6061);
                    /// <summary>Error code: 2002-6062; Inner value: 0x2f5c02</summary>
                    public static Result.Base InvalidSize => new Result.Base(ModuleFs, 6062);
                    /// <summary>Error code: 2002-6063; Inner value: 0x2f5e02</summary>
                    public static Result.Base NullptrArgument => new Result.Base(ModuleFs, 6063);
                    /// <summary>Error code: 2002-6064; Inner value: 0x2f6002</summary>
                    public static Result.Base InvalidAlignment => new Result.Base(ModuleFs, 6064);
                    /// <summary>Error code: 2002-6065; Inner value: 0x2f6202</summary>
                    public static Result.Base InvalidMountName => new Result.Base(ModuleFs, 6065);
                    /// <summary>Error code: 2002-6066; Inner value: 0x2f6402</summary>
                    public static Result.Base ExtensionSizeTooLarge => new Result.Base(ModuleFs, 6066);
                    /// <summary>Error code: 2002-6067; Inner value: 0x2f6602</summary>
                    public static Result.Base ExtensionSizeInvalid => new Result.Base(ModuleFs, 6067);
                    /// <summary>Error code: 2002-6068; Inner value: 0x2f6802</summary>
                    public static Result.Base InvalidHandle => new Result.Base(ModuleFs, 6068);
                    /// <summary>Error code: 2002-6069; Inner value: 0x2f6a02</summary>
                    public static Result.Base CacheStorageSizeTooLarge => new Result.Base(ModuleFs, 6069);
                    /// <summary>Error code: 2002-6070; Inner value: 0x2f6c02</summary>
                    public static Result.Base CacheStorageIndexTooLarge => new Result.Base(ModuleFs, 6070);
                    /// <summary>Up to 10 file systems can be committed at the same time.<br/>Error code: 2002-6071; Inner value: 0x2f6e02</summary>
                    public static Result.Base InvalidCommitNameCount => new Result.Base(ModuleFs, 6071);
                    /// <summary>Error code: 2002-6072; Inner value: 0x2f7002</summary>
                    public static Result.Base InvalidOpenMode => new Result.Base(ModuleFs, 6072);
                    /// <summary>Error code: 2002-6073; Inner value: 0x2f7202</summary>
                    public static Result.Base InvalidFileSize => new Result.Base(ModuleFs, 6073);
                    /// <summary>Error code: 2002-6074; Inner value: 0x2f7402</summary>
                    public static Result.Base InvalidDirectoryOpenMode => new Result.Base(ModuleFs, 6074);
                    /// <summary>Error code: 2002-6075; Inner value: 0x2f7602</summary>
                    public static Result.Base InvalidCommitOption => new Result.Base(ModuleFs, 6075);

                    /// <summary>Error code: 2002-6080; Range: 6080-6099; Inner value: 0x2f8002</summary>
                    public static Result.Base InvalidEnumValue { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6080, 6099); }
                        /// <summary>Error code: 2002-6081; Inner value: 0x2f8202</summary>
                        public static Result.Base InvalidSaveDataState => new Result.Base(ModuleFs, 6081);
                        /// <summary>Error code: 2002-6082; Inner value: 0x2f8402</summary>
                        public static Result.Base InvalidSaveDataSpaceId => new Result.Base(ModuleFs, 6082);

                    /// <summary>Error code: 2002-6101; Inner value: 0x2faa02</summary>
                    public static Result.Base GameCardLogoDataTooLarge => new Result.Base(ModuleFs, 6101);
                    /// <summary>Error code: 2002-6102; Inner value: 0x2fac02</summary>
                    public static Result.Base FileDataCacheMemorySizeTooSmall => new Result.Base(ModuleFs, 6102);

                /// <summary>Error code: 2002-6200; Range: 6200-6299; Inner value: 0x307002</summary>
                public static Result.Base InvalidOperationForOpenMode { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6200, 6299); }
                    /// <summary>Error code: 2002-6201; Inner value: 0x307202</summary>
                    public static Result.Base FileExtensionWithoutOpenModeAllowAppend => new Result.Base(ModuleFs, 6201);
                    /// <summary>Error code: 2002-6202; Inner value: 0x307402</summary>
                    public static Result.Base ReadUnpermitted => new Result.Base(ModuleFs, 6202);
                    /// <summary>Error code: 2002-6203; Inner value: 0x307602</summary>
                    public static Result.Base WriteUnpermitted => new Result.Base(ModuleFs, 6203);

                /// <summary>Error code: 2002-6300; Range: 6300-6399; Inner value: 0x313802</summary>
                public static Result.Base UnsupportedOperation { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6300, 6399); }
                    /// <summary>Error code: 2002-6301; Inner value: 0x313a02</summary>
                    public static Result.Base UnsupportedCommitTarget => new Result.Base(ModuleFs, 6301);
                    /// <summary>Attempted to resize a non-resizable SubStorage.<br/>Error code: 2002-6302; Inner value: 0x313c02</summary>
                    public static Result.Base UnsupportedSetSizeForNotResizableSubStorage => new Result.Base(ModuleFs, 6302);
                    /// <summary>Attempted to resize a SubStorage that wasn't located at the end of the base storage.<br/>Error code: 2002-6303; Inner value: 0x313e02</summary>
                    public static Result.Base UnsupportedSetSizeForResizableSubStorage => new Result.Base(ModuleFs, 6303);
                    /// <summary>Error code: 2002-6304; Inner value: 0x314002</summary>
                    public static Result.Base UnsupportedSetSizeForMemoryStorage => new Result.Base(ModuleFs, 6304);
                    /// <summary>Error code: 2002-6305; Inner value: 0x314202</summary>
                    public static Result.Base UnsupportedOperateRangeForMemoryStorage => new Result.Base(ModuleFs, 6305);
                    /// <summary>Error code: 2002-6306; Inner value: 0x314402</summary>
                    public static Result.Base UnsupportedOperateRangeForFileStorage => new Result.Base(ModuleFs, 6306);
                    /// <summary>Error code: 2002-6307; Inner value: 0x314602</summary>
                    public static Result.Base UnsupportedOperateRangeForFileHandleStorage => new Result.Base(ModuleFs, 6307);
                    /// <summary>Error code: 2002-6308; Inner value: 0x314802</summary>
                    public static Result.Base UnsupportedOperateRangeForSwitchStorage => new Result.Base(ModuleFs, 6308);
                    /// <summary>Error code: 2002-6309; Inner value: 0x314a02</summary>
                    public static Result.Base UnsupportedOperateRangeForStorageServiceObjectAdapter => new Result.Base(ModuleFs, 6309);
                    /// <summary>Error code: 2002-6310; Inner value: 0x314c02</summary>
                    public static Result.Base UnsupportedWriteForAesCtrCounterExtendedStorage => new Result.Base(ModuleFs, 6310);
                    /// <summary>Error code: 2002-6311; Inner value: 0x314e02</summary>
                    public static Result.Base UnsupportedSetSizeForAesCtrCounterExtendedStorage => new Result.Base(ModuleFs, 6311);
                    /// <summary>Error code: 2002-6312; Inner value: 0x315002</summary>
                    public static Result.Base UnsupportedOperateRangeForAesCtrCounterExtendedStorage => new Result.Base(ModuleFs, 6312);
                    /// <summary>Error code: 2002-6313; Inner value: 0x315202</summary>
                    public static Result.Base UnsupportedWriteForAesCtrStorageExternal => new Result.Base(ModuleFs, 6313);
                    /// <summary>Error code: 2002-6314; Inner value: 0x315402</summary>
                    public static Result.Base UnsupportedSetSizeForAesCtrStorageExternal => new Result.Base(ModuleFs, 6314);
                    /// <summary>Error code: 2002-6315; Inner value: 0x315602</summary>
                    public static Result.Base UnsupportedSetSizeForAesCtrStorage => new Result.Base(ModuleFs, 6315);
                    /// <summary>Error code: 2002-6316; Inner value: 0x315802</summary>
                    public static Result.Base UnsupportedSetSizeForHierarchicalIntegrityVerificationStorage => new Result.Base(ModuleFs, 6316);
                    /// <summary>Error code: 2002-6317; Inner value: 0x315a02</summary>
                    public static Result.Base UnsupportedOperateRangeForHierarchicalIntegrityVerificationStorage => new Result.Base(ModuleFs, 6317);
                    /// <summary>Error code: 2002-6318; Inner value: 0x315c02</summary>
                    public static Result.Base UnsupportedSetSizeForIntegrityVerificationStorage => new Result.Base(ModuleFs, 6318);
                    /// <summary>Error code: 2002-6319; Inner value: 0x315e02</summary>
                    public static Result.Base UnsupportedOperateRangeForNonSaveDataIntegrityVerificationStorage => new Result.Base(ModuleFs, 6319);
                    /// <summary>Error code: 2002-6320; Inner value: 0x316002</summary>
                    public static Result.Base UnsupportedOperateRangeForIntegrityVerificationStorage => new Result.Base(ModuleFs, 6320);
                    /// <summary>Error code: 2002-6321; Inner value: 0x316202</summary>
                    public static Result.Base UnsupportedSetSizeForBlockCacheBufferedStorage => new Result.Base(ModuleFs, 6321);
                    /// <summary>Error code: 2002-6322; Inner value: 0x316402</summary>
                    public static Result.Base UnsupportedOperateRangeForNonSaveDataBlockCacheBufferedStorage => new Result.Base(ModuleFs, 6322);
                    /// <summary>Error code: 2002-6323; Inner value: 0x316602</summary>
                    public static Result.Base UnsupportedOperateRangeForBlockCacheBufferedStorage => new Result.Base(ModuleFs, 6323);
                    /// <summary>Error code: 2002-6324; Inner value: 0x316802</summary>
                    public static Result.Base UnsupportedWriteForIndirectStorage => new Result.Base(ModuleFs, 6324);
                    /// <summary>Error code: 2002-6325; Inner value: 0x316a02</summary>
                    public static Result.Base UnsupportedSetSizeForIndirectStorage => new Result.Base(ModuleFs, 6325);
                    /// <summary>Error code: 2002-6326; Inner value: 0x316c02</summary>
                    public static Result.Base UnsupportedOperateRangeForIndirectStorage => new Result.Base(ModuleFs, 6326);
                    /// <summary>Error code: 2002-6327; Inner value: 0x316e02</summary>
                    public static Result.Base UnsupportedWriteForZeroStorage => new Result.Base(ModuleFs, 6327);
                    /// <summary>Error code: 2002-6328; Inner value: 0x317002</summary>
                    public static Result.Base UnsupportedSetSizeForZeroStorage => new Result.Base(ModuleFs, 6328);
                    /// <summary>Error code: 2002-6329; Inner value: 0x317202</summary>
                    public static Result.Base UnsupportedSetSizeForHierarchicalSha256Storage => new Result.Base(ModuleFs, 6329);
                    /// <summary>Error code: 2002-6330; Inner value: 0x317402</summary>
                    public static Result.Base UnsupportedWriteForReadOnlyBlockCacheStorage => new Result.Base(ModuleFs, 6330);
                    /// <summary>Error code: 2002-6331; Inner value: 0x317602</summary>
                    public static Result.Base UnsupportedSetSizeForReadOnlyBlockCacheStorage => new Result.Base(ModuleFs, 6331);
                    /// <summary>Error code: 2002-6332; Inner value: 0x317802</summary>
                    public static Result.Base UnsupportedSetSizeForIntegrityRomFsStorage => new Result.Base(ModuleFs, 6332);
                    /// <summary>Error code: 2002-6333; Inner value: 0x317a02</summary>
                    public static Result.Base UnsupportedSetSizeForDuplexStorage => new Result.Base(ModuleFs, 6333);
                    /// <summary>Error code: 2002-6334; Inner value: 0x317c02</summary>
                    public static Result.Base UnsupportedOperateRangeForDuplexStorage => new Result.Base(ModuleFs, 6334);
                    /// <summary>Error code: 2002-6335; Inner value: 0x317e02</summary>
                    public static Result.Base UnsupportedSetSizeForHierarchicalDuplexStorage => new Result.Base(ModuleFs, 6335);
                    /// <summary>Error code: 2002-6336; Inner value: 0x318002</summary>
                    public static Result.Base UnsupportedGetSizeForRemapStorage => new Result.Base(ModuleFs, 6336);
                    /// <summary>Error code: 2002-6337; Inner value: 0x318202</summary>
                    public static Result.Base UnsupportedSetSizeForRemapStorage => new Result.Base(ModuleFs, 6337);
                    /// <summary>Error code: 2002-6338; Inner value: 0x318402</summary>
                    public static Result.Base UnsupportedOperateRangeForRemapStorage => new Result.Base(ModuleFs, 6338);
                    /// <summary>Error code: 2002-6339; Inner value: 0x318602</summary>
                    public static Result.Base UnsupportedSetSizeForIntegritySaveDataStorage => new Result.Base(ModuleFs, 6339);
                    /// <summary>Error code: 2002-6340; Inner value: 0x318802</summary>
                    public static Result.Base UnsupportedOperateRangeForIntegritySaveDataStorage => new Result.Base(ModuleFs, 6340);
                    /// <summary>Error code: 2002-6341; Inner value: 0x318a02</summary>
                    public static Result.Base UnsupportedSetSizeForJournalIntegritySaveDataStorage => new Result.Base(ModuleFs, 6341);
                    /// <summary>Error code: 2002-6342; Inner value: 0x318c02</summary>
                    public static Result.Base UnsupportedOperateRangeForJournalIntegritySaveDataStorage => new Result.Base(ModuleFs, 6342);
                    /// <summary>Error code: 2002-6343; Inner value: 0x318e02</summary>
                    public static Result.Base UnsupportedGetSizeForJournalStorage => new Result.Base(ModuleFs, 6343);
                    /// <summary>Error code: 2002-6344; Inner value: 0x319002</summary>
                    public static Result.Base UnsupportedSetSizeForJournalStorage => new Result.Base(ModuleFs, 6344);
                    /// <summary>Error code: 2002-6345; Inner value: 0x319202</summary>
                    public static Result.Base UnsupportedOperateRangeForJournalStorage => new Result.Base(ModuleFs, 6345);
                    /// <summary>Error code: 2002-6346; Inner value: 0x319402</summary>
                    public static Result.Base UnsupportedSetSizeForUnionStorage => new Result.Base(ModuleFs, 6346);
                    /// <summary>Error code: 2002-6347; Inner value: 0x319602</summary>
                    public static Result.Base UnsupportedSetSizeForAllocationTableStorage => new Result.Base(ModuleFs, 6347);
                    /// <summary>Error code: 2002-6348; Inner value: 0x319802</summary>
                    public static Result.Base UnsupportedReadForWriteOnlyGameCardStorage => new Result.Base(ModuleFs, 6348);
                    /// <summary>Error code: 2002-6349; Inner value: 0x319a02</summary>
                    public static Result.Base UnsupportedSetSizeForWriteOnlyGameCardStorage => new Result.Base(ModuleFs, 6349);
                    /// <summary>Error code: 2002-6350; Inner value: 0x319c02</summary>
                    public static Result.Base UnsupportedWriteForReadOnlyGameCardStorage => new Result.Base(ModuleFs, 6350);
                    /// <summary>Error code: 2002-6351; Inner value: 0x319e02</summary>
                    public static Result.Base UnsupportedSetSizeForReadOnlyGameCardStorage => new Result.Base(ModuleFs, 6351);
                    /// <summary>Error code: 2002-6352; Inner value: 0x31a002</summary>
                    public static Result.Base UnsupportedOperateRangeForReadOnlyGameCardStorage => new Result.Base(ModuleFs, 6352);
                    /// <summary>Error code: 2002-6353; Inner value: 0x31a202</summary>
                    public static Result.Base UnsupportedSetSizeForSdmmcStorage => new Result.Base(ModuleFs, 6353);
                    /// <summary>Error code: 2002-6354; Inner value: 0x31a402</summary>
                    public static Result.Base UnsupportedOperateRangeForSdmmcStorage => new Result.Base(ModuleFs, 6354);
                    /// <summary>Error code: 2002-6355; Inner value: 0x31a602</summary>
                    public static Result.Base UnsupportedOperateRangeForFatFile => new Result.Base(ModuleFs, 6355);
                    /// <summary>Error code: 2002-6356; Inner value: 0x31a802</summary>
                    public static Result.Base UnsupportedOperateRangeForStorageFile => new Result.Base(ModuleFs, 6356);
                    /// <summary>Error code: 2002-6357; Inner value: 0x31aa02</summary>
                    public static Result.Base UnsupportedSetSizeForInternalStorageConcatenationFile => new Result.Base(ModuleFs, 6357);
                    /// <summary>Error code: 2002-6358; Inner value: 0x31ac02</summary>
                    public static Result.Base UnsupportedOperateRangeForInternalStorageConcatenationFile => new Result.Base(ModuleFs, 6358);
                    /// <summary>Error code: 2002-6359; Inner value: 0x31ae02</summary>
                    public static Result.Base UnsupportedQueryEntryForConcatenationFileSystem => new Result.Base(ModuleFs, 6359);
                    /// <summary>Error code: 2002-6360; Inner value: 0x31b002</summary>
                    public static Result.Base UnsupportedOperateRangeForConcatenationFile => new Result.Base(ModuleFs, 6360);
                    /// <summary>Error code: 2002-6361; Inner value: 0x31b202</summary>
                    public static Result.Base UnsupportedSetSizeForZeroBitmapFile => new Result.Base(ModuleFs, 6361);
                    /// <summary>Called OperateRange with an invalid operation ID.<br/>Error code: 2002-6362; Inner value: 0x31b402</summary>
                    public static Result.Base UnsupportedOperateRangeForFileServiceObjectAdapter => new Result.Base(ModuleFs, 6362);
                    /// <summary>Error code: 2002-6363; Inner value: 0x31b602</summary>
                    public static Result.Base UnsupportedOperateRangeForAesXtsFile => new Result.Base(ModuleFs, 6363);
                    /// <summary>Error code: 2002-6364; Inner value: 0x31b802</summary>
                    public static Result.Base UnsupportedWriteForRomFsFileSystem => new Result.Base(ModuleFs, 6364);
                    /// <summary>Called RomFsFileSystem::CommitProvisionally.<br/>Error code: 2002-6365; Inner value: 0x31ba02</summary>
                    public static Result.Base UnsupportedCommitProvisionallyForRomFsFileSystem => new Result.Base(ModuleFs, 6365);
                    /// <summary>Error code: 2002-6366; Inner value: 0x31bc02</summary>
                    public static Result.Base UnsupportedGetTotalSpaceSizeForRomFsFileSystem => new Result.Base(ModuleFs, 6366);
                    /// <summary>Error code: 2002-6367; Inner value: 0x31be02</summary>
                    public static Result.Base UnsupportedWriteForRomFsFile => new Result.Base(ModuleFs, 6367);
                    /// <summary>Error code: 2002-6368; Inner value: 0x31c002</summary>
                    public static Result.Base UnsupportedOperateRangeForRomFsFile => new Result.Base(ModuleFs, 6368);
                    /// <summary>Error code: 2002-6369; Inner value: 0x31c202</summary>
                    public static Result.Base UnsupportedWriteForReadOnlyFileSystem => new Result.Base(ModuleFs, 6369);
                    /// <summary>Error code: 2002-6370; Inner value: 0x31c402</summary>
                    public static Result.Base UnsupportedCommitProvisionallyForReadOnlyFileSystem => new Result.Base(ModuleFs, 6370);
                    /// <summary>Error code: 2002-6371; Inner value: 0x31c602</summary>
                    public static Result.Base UnsupportedGetTotalSpaceSizeForReadOnlyFileSystem => new Result.Base(ModuleFs, 6371);
                    /// <summary>Error code: 2002-6372; Inner value: 0x31c802</summary>
                    public static Result.Base UnsupportedWriteForReadOnlyFile => new Result.Base(ModuleFs, 6372);
                    /// <summary>Error code: 2002-6373; Inner value: 0x31ca02</summary>
                    public static Result.Base UnsupportedOperateRangeForReadOnlyFile => new Result.Base(ModuleFs, 6373);
                    /// <summary>Error code: 2002-6374; Inner value: 0x31cc02</summary>
                    public static Result.Base UnsupportedWriteForPartitionFileSystem => new Result.Base(ModuleFs, 6374);
                    /// <summary>Called PartitionFileSystemCore::CommitProvisionally.<br/>Error code: 2002-6375; Inner value: 0x31ce02</summary>
                    public static Result.Base UnsupportedCommitProvisionallyForPartitionFileSystem => new Result.Base(ModuleFs, 6375);
                    /// <summary>Error code: 2002-6376; Inner value: 0x31d002</summary>
                    public static Result.Base UnsupportedWriteForPartitionFile => new Result.Base(ModuleFs, 6376);
                    /// <summary>Error code: 2002-6377; Inner value: 0x31d202</summary>
                    public static Result.Base UnsupportedOperateRangeForPartitionFile => new Result.Base(ModuleFs, 6377);
                    /// <summary>Error code: 2002-6378; Inner value: 0x31d402</summary>
                    public static Result.Base UnsupportedOperateRangeForTmFileSystemFile => new Result.Base(ModuleFs, 6378);
                    /// <summary>Error code: 2002-6379; Inner value: 0x31d602</summary>
                    public static Result.Base UnsupportedWriteForSaveDataInternalStorageFileSystem => new Result.Base(ModuleFs, 6379);
                    /// <summary>Error code: 2002-6382; Inner value: 0x31dc02</summary>
                    public static Result.Base UnsupportedCommitProvisionallyForApplicationTemporaryFileSystem => new Result.Base(ModuleFs, 6382);
                    /// <summary>Error code: 2002-6383; Inner value: 0x31de02</summary>
                    public static Result.Base UnsupportedCommitProvisionallyForSaveDataFileSystem => new Result.Base(ModuleFs, 6383);
                    /// <summary>Called DirectorySaveDataFileSystem::CommitProvisionally on a non-user savedata.<br/>Error code: 2002-6384; Inner value: 0x31e002</summary>
                    public static Result.Base UnsupportedCommitProvisionallyForDirectorySaveDataFileSystem => new Result.Base(ModuleFs, 6384);
                    /// <summary>Error code: 2002-6385; Inner value: 0x31e202</summary>
                    public static Result.Base UnsupportedWriteForZeroBitmapHashStorageFile => new Result.Base(ModuleFs, 6385);
                    /// <summary>Error code: 2002-6386; Inner value: 0x31e402</summary>
                    public static Result.Base UnsupportedSetSizeForZeroBitmapHashStorageFile => new Result.Base(ModuleFs, 6386);

                /// <summary>Error code: 2002-6400; Range: 6400-6449; Inner value: 0x320002</summary>
                public static Result.Base PermissionDenied { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6400, 6449); }
                    /// <summary>Returned when opening a host FS on a retail device.<br/>Error code: 2002-6403; Inner value: 0x320602</summary>
                    public static Result.Base PermissionDeniedForCreateHostFileSystem => new Result.Base(ModuleFs, 6403);

                /// <summary>Error code: 2002-6450; Inner value: 0x326402</summary>
                public static Result.Base PortAcceptableCountLimited => new Result.Base(ModuleFs, 6450);
                /// <summary>Error code: 2002-6452; Inner value: 0x326802</summary>
                public static Result.Base ExternalKeyAlreadyRegistered => new Result.Base(ModuleFs, 6452);
                /// <summary>Error code: 2002-6454; Inner value: 0x326c02</summary>
                public static Result.Base NeedFlush => new Result.Base(ModuleFs, 6454);
                /// <summary>Error code: 2002-6455; Inner value: 0x326e02</summary>
                public static Result.Base FileNotClosed => new Result.Base(ModuleFs, 6455);
                /// <summary>Error code: 2002-6456; Inner value: 0x327002</summary>
                public static Result.Base DirectoryNotClosed => new Result.Base(ModuleFs, 6456);
                /// <summary>Error code: 2002-6457; Inner value: 0x327202</summary>
                public static Result.Base WriteModeFileNotClosed => new Result.Base(ModuleFs, 6457);
                /// <summary>Error code: 2002-6458; Inner value: 0x327402</summary>
                public static Result.Base AllocatorAlreadyRegistered => new Result.Base(ModuleFs, 6458);
                /// <summary>Error code: 2002-6459; Inner value: 0x327602</summary>
                public static Result.Base DefaultAllocatorUsed => new Result.Base(ModuleFs, 6459);
                /// <summary>Error code: 2002-6460; Inner value: 0x327802</summary>
                public static Result.Base GameCardLogoDataNotReadable => new Result.Base(ModuleFs, 6460);
                /// <summary>Error code: 2002-6461; Inner value: 0x327a02</summary>
                public static Result.Base AllocatorAlignmentViolation => new Result.Base(ModuleFs, 6461);
                /// <summary>Error code: 2002-6462; Inner value: 0x327c02</summary>
                public static Result.Base GlobalFileDataCacheAlreadyEnabled => new Result.Base(ModuleFs, 6462);
                /// <summary>The provided file system has already been added to the multi-commit manager.<br/>Error code: 2002-6463; Inner value: 0x327e02</summary>
                public static Result.Base MultiCommitFileSystemAlreadyAdded => new Result.Base(ModuleFs, 6463);
                /// <summary>Error code: 2002-6464; Inner value: 0x328002</summary>
                public static Result.Base Result6464 => new Result.Base(ModuleFs, 6464);
                /// <summary>Error code: 2002-6465; Inner value: 0x328202</summary>
                public static Result.Base UserNotExist => new Result.Base(ModuleFs, 6465);
                /// <summary>Error code: 2002-6466; Inner value: 0x328402</summary>
                public static Result.Base DefaultGlobalFileDataCacheEnabled => new Result.Base(ModuleFs, 6466);
                /// <summary>Error code: 2002-6467; Inner value: 0x328602</summary>
                public static Result.Base SaveDataRootPathUnavailable => new Result.Base(ModuleFs, 6467);

            /// <summary>Error code: 2002-6600; Range: 6600-6699; Inner value: 0x339002</summary>
            public static Result.Base NotFound { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6600, 6699); }
                /// <summary>Error code: 2002-6602; Inner value: 0x339402</summary>
                public static Result.Base FileNotFound => new Result.Base(ModuleFs, 6602);
                /// <summary>Error code: 2002-6603; Inner value: 0x339602</summary>
                public static Result.Base DirectoryNotFound => new Result.Base(ModuleFs, 6603);
                /// <summary>Error code: 2002-6604; Inner value: 0x339802</summary>
                public static Result.Base DatabaseKeyNotFound => new Result.Base(ModuleFs, 6604);
                /// <summary>Specified program is not found in the program registry.<br/>Error code: 2002-6605; Inner value: 0x339a02</summary>
                public static Result.Base ProgramInfoNotFound => new Result.Base(ModuleFs, 6605);
                /// <summary>Specified program index is not found<br/>Error code: 2002-6606; Inner value: 0x339c02</summary>
                public static Result.Base ProgramIndexNotFound => new Result.Base(ModuleFs, 6606);

            /// <summary>Error code: 2002-6700; Range: 6700-6799; Inner value: 0x345802</summary>
            public static Result.Base OutOfResource { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6700, 6799); }
                /// <summary>Error code: 2002-6705; Inner value: 0x346202</summary>
                public static Result.Base BufferAllocationFailed => new Result.Base(ModuleFs, 6705);
                /// <summary>Error code: 2002-6706; Inner value: 0x346402</summary>
                public static Result.Base MappingTableFull => new Result.Base(ModuleFs, 6706);
                /// <summary>Error code: 2002-6707; Inner value: 0x346602</summary>
                public static Result.Base AllocationTableFull => new Result.Base(ModuleFs, 6707);
                /// <summary>Error code: 2002-6709; Inner value: 0x346a02</summary>
                public static Result.Base OpenCountLimit => new Result.Base(ModuleFs, 6709);
                /// <summary>The maximum number of file systems have been added to the multi-commit manager.<br/>Error code: 2002-6710; Inner value: 0x346c02</summary>
                public static Result.Base MultiCommitFileSystemLimit => new Result.Base(ModuleFs, 6710);

            /// <summary>Error code: 2002-6800; Range: 6800-6899; Inner value: 0x352002</summary>
            public static Result.Base MappingFailed { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6800, 6899); }
                /// <summary>Error code: 2002-6811; Inner value: 0x353602</summary>
                public static Result.Base MapFull => new Result.Base(ModuleFs, 6811);

            /// <summary>Error code: 2002-6900; Range: 6900-6999; Inner value: 0x35e802</summary>
            public static Result.Base BadState { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6900, 6999); }
                /// <summary>Error code: 2002-6902; Inner value: 0x35ec02</summary>
                public static Result.Base NotInitialized => new Result.Base(ModuleFs, 6902);
                /// <summary>Error code: 2002-6903; Inner value: 0x35ee02</summary>
                public static Result.Base BisProxyInvalidated => new Result.Base(ModuleFs, 6903);
                /// <summary>Error code: 2002-6904; Inner value: 0x35f002</summary>
                public static Result.Base NcaDigestInconsistent => new Result.Base(ModuleFs, 6904);
                /// <summary>Error code: 2002-6905; Inner value: 0x35f202</summary>
                public static Result.Base NotMounted => new Result.Base(ModuleFs, 6905);
                /// <summary>Error code: 2002-6906; Inner value: 0x35f402</summary>
                public static Result.Base SaveDataExtending => new Result.Base(ModuleFs, 6906);
                /// <summary>Error code: 2002-6907; Inner value: 0x35f602</summary>
                public static Result.Base SaveDataToExpandIsProvisionallyCommitted => new Result.Base(ModuleFs, 6907);

            /// <summary>Error code: 2002-7002; Inner value: 0x36b402</summary>
            public static Result.Base Result7002 => new Result.Base(ModuleFs, 7002);
            /// <summary>Error code: 2002-7003; Inner value: 0x36b602</summary>
            public static Result.Base Result7003 => new Result.Base(ModuleFs, 7003);
            /// <summary>Error code: 2002-7004; Inner value: 0x36b802</summary>
            public static Result.Base Result7004 => new Result.Base(ModuleFs, 7004);
            /// <summary>Error code: 2002-7005; Inner value: 0x36ba02</summary>
            public static Result.Base Result7005 => new Result.Base(ModuleFs, 7005);
            /// <summary>Error code: 2002-7006; Inner value: 0x36bc02</summary>
            public static Result.Base Result7006 => new Result.Base(ModuleFs, 7006);
            /// <summary>Error code: 2002-7009; Inner value: 0x36c202</summary>
            public static Result.Base Result7009 => new Result.Base(ModuleFs, 7009);
            /// <summary>Error code: 2002-7010; Inner value: 0x36c402</summary>
            public static Result.Base Result7010 => new Result.Base(ModuleFs, 7010);
            /// <summary>Error code: 2002-7011; Inner value: 0x36c602</summary>
            public static Result.Base Result7011 => new Result.Base(ModuleFs, 7011);
            /// <summary>Error code: 2002-7031; Inner value: 0x36ee02</summary>
            public static Result.Base SaveDataPorterInvalidated => new Result.Base(ModuleFs, 7031);
            /// <summary>Error code: 2002-7032; Inner value: 0x36f002</summary>
            public static Result.Base Result7032 => new Result.Base(ModuleFs, 7032);
            /// <summary>Error code: 2002-7033; Inner value: 0x36f202</summary>
            public static Result.Base Result7033 => new Result.Base(ModuleFs, 7033);
            /// <summary>Error code: 2002-7034; Inner value: 0x36f402</summary>
            public static Result.Base Result7034 => new Result.Base(ModuleFs, 7034);
            /// <summary>Error code: 2002-7035; Inner value: 0x36f602</summary>
            public static Result.Base Result7035 => new Result.Base(ModuleFs, 7035);
            /// <summary>Error code: 2002-7036; Inner value: 0x36f802</summary>
            public static Result.Base Result7036 => new Result.Base(ModuleFs, 7036);
            /// <summary>Error code: 2002-7037; Inner value: 0x36fa02</summary>
            public static Result.Base Result7037 => new Result.Base(ModuleFs, 7037);
            /// <summary>Error code: 2002-7038; Inner value: 0x36fc02</summary>
            public static Result.Base Result7038 => new Result.Base(ModuleFs, 7038);
            /// <summary>Error code: 2002-7062; Inner value: 0x372c02</summary>
            public static Result.Base InvalidKeyPackageMac => new Result.Base(ModuleFs, 7062);
            /// <summary>Error code: 2002-7063; Inner value: 0x372e02</summary>
            public static Result.Base KeyPackageSignatureVerificationFailed => new Result.Base(ModuleFs, 7063);
            /// <summary>Error code: 2002-7064; Inner value: 0x373002</summary>
            public static Result.Base InvalidKeyPackageChallengeData => new Result.Base(ModuleFs, 7064);
            /// <summary>Error code: 2002-7065; Inner value: 0x373202</summary>
            public static Result.Base UnsupportedKeyPackageKeyGeneration => new Result.Base(ModuleFs, 7065);
            /// <summary>Error code: 2002-7066; Inner value: 0x373402</summary>
            public static Result.Base InvalidSaveDataRepairInitialDataContentGcmMac => new Result.Base(ModuleFs, 7066);
            /// <summary>Error code: 2002-7069; Inner value: 0x373a02</summary>
            public static Result.Base InvalidSaveDataRepairInitialDataCmac => new Result.Base(ModuleFs, 7069);
            /// <summary>The before and after initial data have different AAD.<br/>Error code: 2002-7070; Inner value: 0x373c02</summary>
            public static Result.Base SaveDataRepairInitialDataAadMismatch => new Result.Base(ModuleFs, 7070);
            /// <summary>The before and after initial data refer to different saves.<br/>Error code: 2002-7071; Inner value: 0x373e02</summary>
            public static Result.Base SaveDataRepairInitialDataSaveMismatch => new Result.Base(ModuleFs, 7071);

            /// <summary>Error code: 2002-7100; Range: 7100-7139; Inner value: 0x377802</summary>
            public static Result.Base RamDiskCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 7100, 7139); }
                /// <summary>Error code: 2002-7101; Inner value: 0x377a02</summary>
                public static Result.Base Result7101 => new Result.Base(ModuleFs, 7101);

                /// <summary>Error code: 2002-7111; Range: 7111-7119; Inner value: 0x378e02</summary>
                public static Result.Base RamDiskSaveDataCoreFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 7111, 7119); }
                    /// <summary>Error code: 2002-7112; Inner value: 0x379002</summary>
                    public static Result.Base IncorrectRamDiskSaveDataControlAreaMagic => new Result.Base(ModuleFs, 7112);
                    /// <summary>Error code: 2002-7113; Inner value: 0x379202</summary>
                    public static Result.Base InvalidRamDiskSaveDataFileReadOffset => new Result.Base(ModuleFs, 7113);
                    /// <summary>Error code: 2002-7114; Inner value: 0x379402</summary>
                    public static Result.Base InvalidRamDiskSaveDataCoreDataStorageSize => new Result.Base(ModuleFs, 7114);

                /// <summary>Error code: 2002-7121; Range: 7121-7139; Inner value: 0x37a202</summary>
                public static Result.Base RamDiskDatabaseCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 7121, 7139); }
                    /// <summary>Error code: 2002-7122; Inner value: 0x37a402</summary>
                    public static Result.Base InvalidRamDiskAllocationTableBlock => new Result.Base(ModuleFs, 7122);
                    /// <summary>Error code: 2002-7123; Inner value: 0x37a602</summary>
                    public static Result.Base InvalidRamDiskKeyValueListElementIndex => new Result.Base(ModuleFs, 7123);
                    /// <summary>Error code: 2002-7124; Inner value: 0x37a802</summary>
                    public static Result.Base RamDiskAllocationTableIteratedRangeEntry => new Result.Base(ModuleFs, 7124);
                    /// <summary>Error code: 2002-7125; Inner value: 0x37aa02</summary>
                    public static Result.Base InvalidRamDiskAllocationTableOffset => new Result.Base(ModuleFs, 7125);
                    /// <summary>Error code: 2002-7126; Inner value: 0x37ac02</summary>
                    public static Result.Base InvalidRamDiskAllocationTableBlockCount => new Result.Base(ModuleFs, 7126);
                    /// <summary>Error code: 2002-7127; Inner value: 0x37ae02</summary>
                    public static Result.Base InvalidRamDiskKeyValueListEntryIndex => new Result.Base(ModuleFs, 7127);

            /// <summary>Error code: 2002-7142; Inner value: 0x37cc02</summary>
            public static Result.Base Result7142 => new Result.Base(ModuleFs, 7142);
            /// <summary>Error code: 2002-7900; Inner value: 0x3db802</summary>
            public static Result.Base Unknown => new Result.Base(ModuleFs, 7900);

            /// <summary>Error code: 2002-7901; Range: 7901-7904; Inner value: 0x3dba02</summary>
            public static Result.Base DbmNotFound { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 7901, 7904); }
                /// <summary>Error code: 2002-7902; Inner value: 0x3dbc02</summary>
                public static Result.Base DbmKeyNotFound => new Result.Base(ModuleFs, 7902);
                /// <summary>Error code: 2002-7903; Inner value: 0x3dbe02</summary>
                public static Result.Base DbmFileNotFound => new Result.Base(ModuleFs, 7903);
                /// <summary>Error code: 2002-7904; Inner value: 0x3dc002</summary>
                public static Result.Base DbmDirectoryNotFound => new Result.Base(ModuleFs, 7904);

            /// <summary>Error code: 2002-7906; Inner value: 0x3dc402</summary>
            public static Result.Base DbmAlreadyExists => new Result.Base(ModuleFs, 7906);
            /// <summary>Error code: 2002-7907; Inner value: 0x3dc602</summary>
            public static Result.Base DbmKeyFull => new Result.Base(ModuleFs, 7907);
            /// <summary>Error code: 2002-7908; Inner value: 0x3dc802</summary>
            public static Result.Base DbmDirectoryEntryFull => new Result.Base(ModuleFs, 7908);
            /// <summary>Error code: 2002-7909; Inner value: 0x3dca02</summary>
            public static Result.Base DbmFileEntryFull => new Result.Base(ModuleFs, 7909);

            /// <summary>Error code: 2002-7910; Range: 7910-7912; Inner value: 0x3dcc02</summary>
            public static Result.Base DbmFindFinished { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 7910, 7912); }
                /// <summary>Error code: 2002-7911; Inner value: 0x3dce02</summary>
                public static Result.Base DbmFindKeyFinished => new Result.Base(ModuleFs, 7911);
                /// <summary>Error code: 2002-7912; Inner value: 0x3dd002</summary>
                public static Result.Base DbmIterationFinished => new Result.Base(ModuleFs, 7912);

            /// <summary>Error code: 2002-7914; Inner value: 0x3dd402</summary>
            public static Result.Base DbmInvalidOperation => new Result.Base(ModuleFs, 7914);
            /// <summary>Error code: 2002-7915; Inner value: 0x3dd602</summary>
            public static Result.Base DbmInvalidPathFormat => new Result.Base(ModuleFs, 7915);
            /// <summary>Error code: 2002-7916; Inner value: 0x3dd802</summary>
            public static Result.Base DbmDirectoryNameTooLong => new Result.Base(ModuleFs, 7916);
            /// <summary>Error code: 2002-7917; Inner value: 0x3dda02</summary>
            public static Result.Base DbmFileNameTooLong => new Result.Base(ModuleFs, 7917);
    }
}
