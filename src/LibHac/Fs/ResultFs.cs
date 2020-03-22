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
            /// <summary>Resource already in use (file already opened<br/>Error code: 2002-0007; Inner value: 0xe02</summary>
            public static Result.Base TargetLocked => new Result.Base(ModuleFs, 7);
            /// <summary>Specified directory is not empty when trying to delete it<br/>Error code: 2002-0008; Inner value: 0x1002</summary>
            public static Result.Base DirectoryNotEmpty => new Result.Base(ModuleFs, 8);
            /// <summary>Error code: 2002-0013; Inner value: 0x1a02</summary>
            public static Result.Base DirectoryStatusChanged => new Result.Base(ModuleFs, 13);

            /// <summary>Error code: 2002-0030; Range: 30-45; Inner value: 0x3c02</summary>
            public static Result.Base InsufficientFreeSpace => new Result.Base(ModuleFs, 30, 45);
                /// <summary>Error code: 2002-0031; Inner value: 0x3e02</summary>
                public static Result.Base UsableSpaceNotEnoughForSaveData => new Result.Base(ModuleFs, 31);

                /// <summary>Error code: 2002-0034; Range: 34-38; Inner value: 0x4402</summary>
                public static Result.Base InsufficientFreeSpaceBis => new Result.Base(ModuleFs, 34, 38);
                    /// <summary>Error code: 2002-0035; Inner value: 0x4602</summary>
                    public static Result.Base InsufficientFreeSpaceBisCalibration => new Result.Base(ModuleFs, 35);
                    /// <summary>Error code: 2002-0036; Inner value: 0x4802</summary>
                    public static Result.Base InsufficientFreeSpaceBisSafe => new Result.Base(ModuleFs, 36);
                    /// <summary>Error code: 2002-0037; Inner value: 0x4a02</summary>
                    public static Result.Base InsufficientFreeSpaceBisUser => new Result.Base(ModuleFs, 37);
                    /// <summary>Error code: 2002-0038; Inner value: 0x4c02</summary>
                    public static Result.Base InsufficientFreeSpaceBisSystem => new Result.Base(ModuleFs, 38);

                /// <summary>Error code: 2002-0039; Inner value: 0x4e02</summary>
                public static Result.Base InsufficientFreeSpaceSdCard => new Result.Base(ModuleFs, 39);

            /// <summary>Error code: 2002-0050; Inner value: 0x6402</summary>
            public static Result.Base UnsupportedSdkVersion => new Result.Base(ModuleFs, 50);
            /// <summary>Error code: 2002-0060; Inner value: 0x7802</summary>
            public static Result.Base MountNameAlreadyExists => new Result.Base(ModuleFs, 60);

        /// <summary>Error code: 2002-1001; Inner value: 0x7d202</summary>
        public static Result.Base PartitionNotFound => new Result.Base(ModuleFs, 1001);
        /// <summary>Error code: 2002-1002; Inner value: 0x7d402</summary>
        public static Result.Base TargetNotFound => new Result.Base(ModuleFs, 1002);
        /// <summary>The requested external key was not found<br/>Error code: 2002-1004; Inner value: 0x7d802</summary>
        public static Result.Base ExternalKeyNotFound => new Result.Base(ModuleFs, 1004);

        /// <summary>Error code: 2002-2000; Range: 2000-2499; Inner value: 0xfa002</summary>
        public static Result.Base SdCardAccessFailed { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 2000, 2499); }
            /// <summary>Error code: 2002-2001; Inner value: 0xfa202</summary>
            public static Result.Base SdCardNotFound => new Result.Base(ModuleFs, 2001);
            /// <summary>Error code: 2002-2004; Inner value: 0xfa802</summary>
            public static Result.Base SdCardAsleep => new Result.Base(ModuleFs, 2004);

        /// <summary>Error code: 2002-2500; Range: 2500-2999; Inner value: 0x138802</summary>
        public static Result.Base GameCardAccessFailed { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 2500, 2999); }
            /// <summary>Error code: 2002-2503; Inner value: 0x138e02</summary>
            public static Result.Base InvalidBufferForGameCard => new Result.Base(ModuleFs, 2503);
            /// <summary>Error code: 2002-2520; Inner value: 0x13b002</summary>
            public static Result.Base GameCardNotInserted => new Result.Base(ModuleFs, 2520);
            /// <summary>Error code: 2002-2951; Inner value: 0x170e02</summary>
            public static Result.Base GameCardNotInsertedOnGetHandle => new Result.Base(ModuleFs, 2951);
            /// <summary>Error code: 2002-2952; Inner value: 0x171002</summary>
            public static Result.Base InvalidGameCardHandleOnRead => new Result.Base(ModuleFs, 2952);
            /// <summary>Error code: 2002-2954; Inner value: 0x171402</summary>
            public static Result.Base InvalidGameCardHandleOnGetCardInfo => new Result.Base(ModuleFs, 2954);
            /// <summary>Error code: 2002-2960; Inner value: 0x172002</summary>
            public static Result.Base InvalidGameCardHandleOnOpenNormalPartition => new Result.Base(ModuleFs, 2960);
            /// <summary>Error code: 2002-2961; Inner value: 0x172202</summary>
            public static Result.Base InvalidGameCardHandleOnOpenSecurePartition => new Result.Base(ModuleFs, 2961);

        /// <summary>Error code: 2002-3001; Inner value: 0x177202</summary>
        public static Result.Base NotImplemented => new Result.Base(ModuleFs, 3001);
        /// <summary>Error code: 2002-3002; Inner value: 0x177402</summary>
        public static Result.Base UnsupportedVersion => new Result.Base(ModuleFs, 3002);
        /// <summary>Error code: 2002-3003; Inner value: 0x177602</summary>
        public static Result.Base SaveDataPathAlreadyExists => new Result.Base(ModuleFs, 3003);
        /// <summary>Error code: 2002-3005; Inner value: 0x177a02</summary>
        public static Result.Base OutOfRange => new Result.Base(ModuleFs, 3005);

        /// <summary>Error code: 2002-3200; Range: 3200-3499; Inner value: 0x190002</summary>
        public static Result.Base AllocationMemoryFailed { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 3200, 3499); }
            /// <summary>Error code: 2002-3312; Inner value: 0x19e002</summary>
            public static Result.Base AesXtsFileFileStorageAllocationError => new Result.Base(ModuleFs, 3312);
            /// <summary>Error code: 2002-3313; Inner value: 0x19e202</summary>
            public static Result.Base AesXtsFileXtsStorageAllocationError => new Result.Base(ModuleFs, 3313);
            /// <summary>Error code: 2002-3314; Inner value: 0x19e402</summary>
            public static Result.Base AesXtsFileAlignmentStorageAllocationError => new Result.Base(ModuleFs, 3314);
            /// <summary>Error code: 2002-3315; Inner value: 0x19e602</summary>
            public static Result.Base AesXtsFileStorageFileAllocationError => new Result.Base(ModuleFs, 3315);
            /// <summary>Error code: 2002-3383; Inner value: 0x1a6e02</summary>
            public static Result.Base AesXtsFileSubStorageAllocationError => new Result.Base(ModuleFs, 3383);

        /// <summary>Error code: 2002-3500; Range: 3500-3999; Inner value: 0x1b5802</summary>
        public static Result.Base MmcAccessFailed { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 3500, 3999); }

        /// <summary>Error code: 2002-4000; Range: 4000-4999; Inner value: 0x1f4002</summary>
        public static Result.Base DataCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4000, 4999); }
            /// <summary>Error code: 2002-4001; Range: 4001-4299; Inner value: 0x1f4202</summary>
            public static Result.Base RomCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4001, 4299); }
                /// <summary>Error code: 2002-4023; Inner value: 0x1f6e02</summary>
                public static Result.Base InvalidIndirectStorageSource => new Result.Base(ModuleFs, 4023);

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

            /// <summary>Error code: 2002-4301; Range: 4301-4499; Inner value: 0x219a02</summary>
            public static Result.Base SaveDataCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4301, 4499); }
                /// <summary>Error code: 2002-4302; Inner value: 0x219c02</summary>
                public static Result.Base UnsupportedSaveVersion => new Result.Base(ModuleFs, 4302);
                /// <summary>Error code: 2002-4303; Inner value: 0x219e02</summary>
                public static Result.Base InvalidSaveDataEntryType => new Result.Base(ModuleFs, 4303);
                /// <summary>Error code: 2002-4315; Inner value: 0x21b602</summary>
                public static Result.Base InvalidSaveDataHeader => new Result.Base(ModuleFs, 4315);
                /// <summary>Error code: 2002-4362; Inner value: 0x221402</summary>
                public static Result.Base InvalidSaveDataIvfcMagic => new Result.Base(ModuleFs, 4362);
                /// <summary>Error code: 2002-4363; Inner value: 0x221602</summary>
                public static Result.Base InvalidSaveDataIvfcHashValidationBit => new Result.Base(ModuleFs, 4363);
                /// <summary>Error code: 2002-4364; Inner value: 0x221802</summary>
                public static Result.Base InvalidSaveDataIvfcHash => new Result.Base(ModuleFs, 4364);
                /// <summary>Error code: 2002-4372; Inner value: 0x222802</summary>
                public static Result.Base EmptySaveDataIvfcHash => new Result.Base(ModuleFs, 4372);
                /// <summary>Error code: 2002-4373; Inner value: 0x222a02</summary>
                public static Result.Base InvalidSaveDataHashInIvfcTopLayer => new Result.Base(ModuleFs, 4373);
                /// <summary>Error code: 2002-4402; Inner value: 0x226402</summary>
                public static Result.Base SaveDataInvalidGptPartitionSignature => new Result.Base(ModuleFs, 4402);
                /// <summary>Error code: 2002-4427; Inner value: 0x229602</summary>
                public static Result.Base IncompleteBlockInZeroBitmapHashStorageFileSaveData => new Result.Base(ModuleFs, 4427);

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

                /// <summary>Error code: 2002-4462; Inner value: 0x22dc02</summary>
                public static Result.Base SaveDataAllocationTableCorrupted => new Result.Base(ModuleFs, 4462);
                /// <summary>Error code: 2002-4463; Inner value: 0x22de02</summary>
                public static Result.Base SaveDataFileTableCorrupted => new Result.Base(ModuleFs, 4463);
                /// <summary>Error code: 2002-4464; Inner value: 0x22e002</summary>
                public static Result.Base AllocationTableIteratedRangeEntry => new Result.Base(ModuleFs, 4464);

            /// <summary>Error code: 2002-4501; Range: 4501-4599; Inner value: 0x232a02</summary>
            public static Result.Base NcaCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4501, 4599); }
                /// <summary>Error code: 2002-4512; Inner value: 0x234002</summary>
                public static Result.Base InvalidNcaFsType => new Result.Base(ModuleFs, 4512);
                /// <summary>Error code: 2002-4527; Inner value: 0x235e02</summary>
                public static Result.Base InvalidNcaProgramId => new Result.Base(ModuleFs, 4527);

            /// <summary>Error code: 2002-4601; Range: 4601-4639; Inner value: 0x23f202</summary>
            public static Result.Base IntegrityVerificationStorageCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4601, 4639); }
                /// <summary>Error code: 2002-4602; Inner value: 0x23f402</summary>
                public static Result.Base InvalidIvfcMagic => new Result.Base(ModuleFs, 4602);
                /// <summary>Error code: 2002-4603; Inner value: 0x23f602</summary>
                public static Result.Base InvalidIvfcHashValidationBit => new Result.Base(ModuleFs, 4603);
                /// <summary>Error code: 2002-4604; Inner value: 0x23f802</summary>
                public static Result.Base InvalidIvfcHash => new Result.Base(ModuleFs, 4604);
                /// <summary>Error code: 2002-4612; Inner value: 0x240802</summary>
                public static Result.Base EmptyIvfcHash => new Result.Base(ModuleFs, 4612);
                /// <summary>Error code: 2002-4613; Inner value: 0x240a02</summary>
                public static Result.Base InvalidHashInIvfcTopLayer => new Result.Base(ModuleFs, 4613);

            /// <summary>Error code: 2002-4641; Range: 4641-4659; Inner value: 0x244202</summary>
            public static Result.Base PartitionFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4641, 4659); }
                /// <summary>Error code: 2002-4642; Inner value: 0x244402</summary>
                public static Result.Base InvalidPartitionFileSystemHashOffset => new Result.Base(ModuleFs, 4642);
                /// <summary>Error code: 2002-4643; Inner value: 0x244602</summary>
                public static Result.Base InvalidPartitionFileSystemHash => new Result.Base(ModuleFs, 4643);
                /// <summary>Error code: 2002-4644; Inner value: 0x244802</summary>
                public static Result.Base InvalidPartitionFileSystemMagic => new Result.Base(ModuleFs, 4644);
                /// <summary>Error code: 2002-4645; Inner value: 0x244a02</summary>
                public static Result.Base InvalidHashedPartitionFileSystemMagic => new Result.Base(ModuleFs, 4645);
                /// <summary>Error code: 2002-4646; Inner value: 0x244c02</summary>
                public static Result.Base InvalidPartitionFileSystemEntryNameOffset => new Result.Base(ModuleFs, 4646);

            /// <summary>Error code: 2002-4661; Range: 4661-4679; Inner value: 0x246a02</summary>
            public static Result.Base BuiltInStorageCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4661, 4679); }
                /// <summary>Error code: 2002-4662; Inner value: 0x246c02</summary>
                public static Result.Base InvalidGptPartitionSignature => new Result.Base(ModuleFs, 4662);

            /// <summary>Error code: 2002-4681; Range: 4681-4699; Inner value: 0x249202</summary>
            public static Result.Base FatFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4681, 4699); }

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
                public static Result.Base SaveDataAllocationTableCorruptedInternal => new Result.Base(ModuleFs, 4722);
                /// <summary>Error code: 2002-4723; Inner value: 0x24e602</summary>
                public static Result.Base SaveDataFileTableCorruptedInternal => new Result.Base(ModuleFs, 4723);
                /// <summary>Error code: 2002-4724; Inner value: 0x24e802</summary>
                public static Result.Base AllocationTableIteratedRangeEntryInternal => new Result.Base(ModuleFs, 4724);

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

            /// <summary>Error code: 2002-4771; Range: 4771-4779; Inner value: 0x254602</summary>
            public static Result.Base SignedSystemPartitionDataCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4771, 4779); }

            /// <summary>Error code: 2002-4781; Inner value: 0x255a02</summary>
            public static Result.Base GameCardLogoDataCorrupted => new Result.Base(ModuleFs, 4781);

            /// <summary>Error code: 2002-4791; Range: 4791-4799; Inner value: 0x256e02</summary>
            public static Result.Base MultiCommitContextCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4791, 4799); }
                /// <summary>The version of the multi-commit context file is to high for the current MultiCommitManager implementation.<br/>Error code: 2002-4791; Inner value: 0x256e02</summary>
                public static Result.Base InvalidMultiCommitContextVersion => new Result.Base(ModuleFs, 4791);
                /// <summary>The multi-commit has not been provisionally committed.<br/>Error code: 2002-4792; Inner value: 0x257002</summary>
                public static Result.Base InvalidMultiCommitContextState => new Result.Base(ModuleFs, 4792);

            /// <summary>Error code: 2002-4811; Range: 4811-4819; Inner value: 0x259602</summary>
            public static Result.Base ZeroBitmapFileCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4811, 4819); }
                /// <summary>Error code: 2002-4812; Inner value: 0x259802</summary>
                public static Result.Base IncompleteBlockInZeroBitmapHashStorageFile => new Result.Base(ModuleFs, 4812);

        /// <summary>Error code: 2002-5000; Range: 5000-5999; Inner value: 0x271002</summary>
        public static Result.Base Unexpected { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 5000, 5999); }
            /// <summary>Error code: 2002-5307; Inner value: 0x297602</summary>
            public static Result.Base UnexpectedErrorInHostFileFlush => new Result.Base(ModuleFs, 5307);
            /// <summary>Error code: 2002-5308; Inner value: 0x297802</summary>
            public static Result.Base UnexpectedErrorInHostFileGetSize => new Result.Base(ModuleFs, 5308);
            /// <summary>Error code: 2002-5309; Inner value: 0x297a02</summary>
            public static Result.Base UnknownHostFileSystemError => new Result.Base(ModuleFs, 5309);
            /// <summary>Error code: 2002-5320; Inner value: 0x299002</summary>
            public static Result.Base InvalidNcaMountPoint => new Result.Base(ModuleFs, 5320);

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
                    public static Result.Base DestinationIsSubPathOfSource => new Result.Base(ModuleFs, 6032);
                    /// <summary>Error code: 2002-6033; Inner value: 0x2f2202</summary>
                    public static Result.Base PathNotFoundInSaveDataFileTable => new Result.Base(ModuleFs, 6033);
                    /// <summary>Error code: 2002-6034; Inner value: 0x2f2402</summary>
                    public static Result.Base DifferentDestFileSystem => new Result.Base(ModuleFs, 6034);

                /// <summary>Error code: 2002-6061; Inner value: 0x2f5a02</summary>
                public static Result.Base InvalidOffset => new Result.Base(ModuleFs, 6061);
                /// <summary>Error code: 2002-6062; Inner value: 0x2f5c02</summary>
                public static Result.Base InvalidSize => new Result.Base(ModuleFs, 6062);
                /// <summary>Error code: 2002-6063; Inner value: 0x2f5e02</summary>
                public static Result.Base NullptrArgument => new Result.Base(ModuleFs, 6063);
                /// <summary>Error code: 2002-6065; Inner value: 0x2f6202</summary>
                public static Result.Base InvalidMountName => new Result.Base(ModuleFs, 6065);
                /// <summary>Error code: 2002-6066; Inner value: 0x2f6402</summary>
                public static Result.Base ExtensionSizeTooLarge => new Result.Base(ModuleFs, 6066);
                /// <summary>Error code: 2002-6067; Inner value: 0x2f6602</summary>
                public static Result.Base ExtensionSizeInvalid => new Result.Base(ModuleFs, 6067);
                /// <summary>Error code: 2002-6068; Inner value: 0x2f6802</summary>
                public static Result.Base ReadOldSaveDataInfoReader => new Result.Base(ModuleFs, 6068);

                /// <summary>Error code: 2002-6080; Range: 6080-6099; Inner value: 0x2f8002</summary>
                public static Result.Base InvalidEnumValue { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6080, 6099); }
                    /// <summary>Error code: 2002-6081; Inner value: 0x2f8202</summary>
                    public static Result.Base InvalidSaveDataState => new Result.Base(ModuleFs, 6081);
                    /// <summary>Error code: 2002-6082; Inner value: 0x2f8402</summary>
                    public static Result.Base InvalidSaveDataSpaceId => new Result.Base(ModuleFs, 6082);

            /// <summary>Error code: 2002-6200; Range: 6200-6299; Inner value: 0x307002</summary>
            public static Result.Base InvalidOperationForOpenMode { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6200, 6299); }
                /// <summary>Error code: 2002-6201; Inner value: 0x307202</summary>
                public static Result.Base FileExtensionWithoutOpenModeAllowAppend => new Result.Base(ModuleFs, 6201);
                /// <summary>Error code: 2002-6202; Inner value: 0x307402</summary>
                public static Result.Base InvalidOpenModeForRead => new Result.Base(ModuleFs, 6202);
                /// <summary>Error code: 2002-6203; Inner value: 0x307602</summary>
                public static Result.Base InvalidOpenModeForWrite => new Result.Base(ModuleFs, 6203);

            /// <summary>Error code: 2002-6300; Range: 6300-6399; Inner value: 0x313802</summary>
            public static Result.Base UnsupportedOperation { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6300, 6399); }
                /// <summary>Error code: 2002-6302; Inner value: 0x313c02</summary>
                public static Result.Base SubStorageNotResizable => new Result.Base(ModuleFs, 6302);
                /// <summary>Error code: 2002-6303; Inner value: 0x313e02</summary>
                public static Result.Base SubStorageNotResizableMiddleOfFile => new Result.Base(ModuleFs, 6303);
                /// <summary>Error code: 2002-6304; Inner value: 0x314002</summary>
                public static Result.Base UnsupportedOperationInMemoryStorageSetSize => new Result.Base(ModuleFs, 6304);
                /// <summary>Error code: 2002-6306; Inner value: 0x314402</summary>
                public static Result.Base UnsupportedOperationInFileStorageOperateRange => new Result.Base(ModuleFs, 6306);
                /// <summary>Error code: 2002-6310; Inner value: 0x314c02</summary>
                public static Result.Base UnsupportedOperationInAesCtrExStorageWrite => new Result.Base(ModuleFs, 6310);
                /// <summary>Error code: 2002-6316; Inner value: 0x315802</summary>
                public static Result.Base UnsupportedOperationInHierarchicalIvfcStorageSetSize => new Result.Base(ModuleFs, 6316);
                /// <summary>Error code: 2002-6324; Inner value: 0x316802</summary>
                public static Result.Base UnsupportedOperationInIndirectStorageWrite => new Result.Base(ModuleFs, 6324);
                /// <summary>Error code: 2002-6325; Inner value: 0x316a02</summary>
                public static Result.Base UnsupportedOperationInIndirectStorageSetSize => new Result.Base(ModuleFs, 6325);
                /// <summary>Error code: 2002-6350; Inner value: 0x319c02</summary>
                public static Result.Base UnsupportedOperationInRoGameCardStorageWrite => new Result.Base(ModuleFs, 6350);
                /// <summary>Error code: 2002-6351; Inner value: 0x319e02</summary>
                public static Result.Base UnsupportedOperationInRoGameCardStorageSetSize => new Result.Base(ModuleFs, 6351);
                /// <summary>Error code: 2002-6359; Inner value: 0x31ae02</summary>
                public static Result.Base UnsupportedOperationInConcatFsQueryEntry => new Result.Base(ModuleFs, 6359);
                /// <summary>Error code: 2002-6364; Inner value: 0x31b802</summary>
                public static Result.Base UnsupportedOperationModifyRomFsFileSystem => new Result.Base(ModuleFs, 6364);
                /// <summary>Called RomFsFileSystem::CommitProvisionally.<br/>Error code: 2002-6365; Inner value: 0x31ba02</summary>
                public static Result.Base UnsupportedOperationInRomFsFileSystem => new Result.Base(ModuleFs, 6365);
                /// <summary>Error code: 2002-6366; Inner value: 0x31bc02</summary>
                public static Result.Base UnsupportedOperationRomFsFileSystemGetSpace => new Result.Base(ModuleFs, 6366);
                /// <summary>Error code: 2002-6367; Inner value: 0x31be02</summary>
                public static Result.Base UnsupportedOperationModifyRomFsFile => new Result.Base(ModuleFs, 6367);
                /// <summary>Error code: 2002-6369; Inner value: 0x31c202</summary>
                public static Result.Base UnsupportedOperationModifyReadOnlyFileSystem => new Result.Base(ModuleFs, 6369);
                /// <summary>Error code: 2002-6371; Inner value: 0x31c602</summary>
                public static Result.Base UnsupportedOperationReadOnlyFileSystemGetSpace => new Result.Base(ModuleFs, 6371);
                /// <summary>Error code: 2002-6372; Inner value: 0x31c802</summary>
                public static Result.Base UnsupportedOperationModifyReadOnlyFile => new Result.Base(ModuleFs, 6372);
                /// <summary>Error code: 2002-6374; Inner value: 0x31cc02</summary>
                public static Result.Base UnsupportedOperationModifyPartitionFileSystem => new Result.Base(ModuleFs, 6374);
                /// <summary>Called PartitionFileSystemCore::CommitProvisionally.<br/>Error code: 2002-6375; Inner value: 0x31ce02</summary>
                public static Result.Base UnsupportedOperationInPartitionFileSystem => new Result.Base(ModuleFs, 6375);
                /// <summary>Error code: 2002-6376; Inner value: 0x31d002</summary>
                public static Result.Base UnsupportedOperationInPartitionFileSetSize => new Result.Base(ModuleFs, 6376);
                /// <summary>Error code: 2002-6377; Inner value: 0x31d202</summary>
                public static Result.Base UnsupportedOperationIdInPartitionFileSystem => new Result.Base(ModuleFs, 6377);
                /// <summary>Called DirectorySaveDataFileSystem::CommitProvisionally on a non-user savedata.<br/>Error code: 2002-6384; Inner value: 0x31e002</summary>
                public static Result.Base UnsupportedOperationInDirectorySaveDataFileSystem => new Result.Base(ModuleFs, 6384);

            /// <summary>Error code: 2002-6400; Range: 6400-6449; Inner value: 0x320002</summary>
            public static Result.Base PermissionDenied { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6400, 6449); }

            /// <summary>Error code: 2002-6452; Inner value: 0x326802</summary>
            public static Result.Base ExternalKeyAlreadyRegistered => new Result.Base(ModuleFs, 6452);
            /// <summary>Error code: 2002-6454; Inner value: 0x326c02</summary>
            public static Result.Base WriteStateUnflushed => new Result.Base(ModuleFs, 6454);
            /// <summary>Error code: 2002-6457; Inner value: 0x327202</summary>
            public static Result.Base WriteModeFileNotClosed => new Result.Base(ModuleFs, 6457);
            /// <summary>Error code: 2002-6461; Inner value: 0x327a02</summary>
            public static Result.Base AllocatorAlignmentViolation => new Result.Base(ModuleFs, 6461);
            /// <summary>The provided file system has already been added to the multi-commit manager.<br/>Error code: 2002-6463; Inner value: 0x327e02</summary>
            public static Result.Base MultiCommitFileSystemAlreadyAdded => new Result.Base(ModuleFs, 6463);
            /// <summary>Error code: 2002-6465; Inner value: 0x328202</summary>
            public static Result.Base UserNotExist => new Result.Base(ModuleFs, 6465);

        /// <summary>Error code: 2002-6600; Range: 6600-6699; Inner value: 0x339002</summary>
        public static Result.Base EntryNotFound { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6600, 6699); }
            /// <summary>Specified program index is not found<br/>Error code: 2002-6606; Inner value: 0x339c02</summary>
            public static Result.Base TargetProgramIndexNotFound => new Result.Base(ModuleFs, 6606);

        /// <summary>Error code: 2002-6700; Range: 6700-6799; Inner value: 0x345802</summary>
        public static Result.Base OutOfResource { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6700, 6799); }
            /// <summary>Error code: 2002-6706; Inner value: 0x346402</summary>
            public static Result.Base MappingTableFull => new Result.Base(ModuleFs, 6706);
            /// <summary>Error code: 2002-6707; Inner value: 0x346602</summary>
            public static Result.Base AllocationTableInsufficientFreeBlocks => new Result.Base(ModuleFs, 6707);
            /// <summary>Error code: 2002-6709; Inner value: 0x346a02</summary>
            public static Result.Base OpenCountLimit => new Result.Base(ModuleFs, 6709);
            /// <summary>The maximum number of file systems have been added to the multi-commit manager.<br/>Error code: 2002-6710; Inner value: 0x346c02</summary>
            public static Result.Base MultiCommitFileSystemLimit => new Result.Base(ModuleFs, 6710);

        /// <summary>Error code: 2002-6800; Range: 6800-6899; Inner value: 0x352002</summary>
        public static Result.Base MappingFailed { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6800, 6899); }
            /// <summary>Error code: 2002-6811; Inner value: 0x353602</summary>
            public static Result.Base RemapStorageMapFull => new Result.Base(ModuleFs, 6811);

        /// <summary>Error code: 2002-6900; Range: 6900-6999; Inner value: 0x35e802</summary>
        public static Result.Base BadState { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6900, 6999); }
            /// <summary>Error code: 2002-6902; Inner value: 0x35ec02</summary>
            public static Result.Base SubStorageNotInitialized => new Result.Base(ModuleFs, 6902);
            /// <summary>Error code: 2002-6905; Inner value: 0x35f202</summary>
            public static Result.Base NotMounted => new Result.Base(ModuleFs, 6905);
            /// <summary>Error code: 2002-6906; Inner value: 0x35f402</summary>
            public static Result.Base SaveDataIsExtending => new Result.Base(ModuleFs, 6906);
    }
}
