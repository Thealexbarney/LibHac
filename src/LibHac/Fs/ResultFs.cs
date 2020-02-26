using System.Runtime.CompilerServices;

namespace LibHac.Fs
{
    public static class ResultFs
    {
        public const int ModuleFs = 2;

        public static Result.Base HandledByAllProcess => new Result.Base(ModuleFs, 0, 999);
            public static Result.Base PathNotFound => new Result.Base(ModuleFs, 1);
            public static Result.Base PathAlreadyExists => new Result.Base(ModuleFs, 2);
            public static Result.Base TargetLocked => new Result.Base(ModuleFs, 7);
            public static Result.Base DirectoryNotEmpty => new Result.Base(ModuleFs, 8);
            public static Result.Base DirectoryStatusChanged => new Result.Base(ModuleFs, 13);

            public static Result.Base InsufficientFreeSpace => new Result.Base(ModuleFs, 30, 45);
                public static Result.Base UsableSpaceNotEnoughForSaveData => new Result.Base(ModuleFs, 31);

                public static Result.Base InsufficientFreeSpaceBis => new Result.Base(ModuleFs, 34, 38);
                    public static Result.Base InsufficientFreeSpaceBisCalibration => new Result.Base(ModuleFs, 35);
                    public static Result.Base InsufficientFreeSpaceBisSafe => new Result.Base(ModuleFs, 36);
                    public static Result.Base InsufficientFreeSpaceBisUser => new Result.Base(ModuleFs, 37);
                    public static Result.Base InsufficientFreeSpaceBisSystem => new Result.Base(ModuleFs, 38);

                public static Result.Base InsufficientFreeSpaceSdCard => new Result.Base(ModuleFs, 39);

            public static Result.Base UnsupportedSdkVersion => new Result.Base(ModuleFs, 50);
            public static Result.Base MountNameAlreadyExists => new Result.Base(ModuleFs, 60);

        public static Result.Base PartitionNotFound => new Result.Base(ModuleFs, 1001);
        public static Result.Base TargetNotFound => new Result.Base(ModuleFs, 1002);
        public static Result.Base ExternalKeyNotFound => new Result.Base(ModuleFs, 1004);

        public static Result.Base SdCardAccessFailed { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 2000, 2499); }
            public static Result.Base SdCardNotFound => new Result.Base(ModuleFs, 2001);
            public static Result.Base SdCardAsleep => new Result.Base(ModuleFs, 2004);

        public static Result.Base GameCardAccessFailed { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 2500, 2999); }
            public static Result.Base InvalidBufferForGameCard => new Result.Base(ModuleFs, 2503);
            public static Result.Base GameCardNotInserted => new Result.Base(ModuleFs, 2520);
            public static Result.Base GameCardNotInsertedOnGetHandle => new Result.Base(ModuleFs, 2951);
            public static Result.Base InvalidGameCardHandleOnRead => new Result.Base(ModuleFs, 2952);
            public static Result.Base InvalidGameCardHandleOnGetCardInfo => new Result.Base(ModuleFs, 2954);
            public static Result.Base InvalidGameCardHandleOnOpenNormalPartition => new Result.Base(ModuleFs, 2960);
            public static Result.Base InvalidGameCardHandleOnOpenSecurePartition => new Result.Base(ModuleFs, 2961);

        public static Result.Base NotImplemented => new Result.Base(ModuleFs, 3001);
        public static Result.Base Result3002 => new Result.Base(ModuleFs, 3002);
        public static Result.Base SaveDataPathAlreadyExists => new Result.Base(ModuleFs, 3003);
        public static Result.Base OutOfRange => new Result.Base(ModuleFs, 3005);

        public static Result.Base AllocationMemoryFailed { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 3200, 3499); }
            public static Result.Base AesXtsFileFileStorageAllocationError => new Result.Base(ModuleFs, 3312);
            public static Result.Base AesXtsFileXtsStorageAllocationError => new Result.Base(ModuleFs, 3313);
            public static Result.Base AesXtsFileAlignmentStorageAllocationError => new Result.Base(ModuleFs, 3314);
            public static Result.Base AesXtsFileStorageFileAllocationError => new Result.Base(ModuleFs, 3315);
            public static Result.Base AesXtsFileSubStorageAllocationError => new Result.Base(ModuleFs, 3383);

        public static Result.Base MmcAccessFailed { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 3500, 3999); }

        public static Result.Base DataCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4000, 4999); }
            public static Result.Base RomCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4001, 4299); }
                public static Result.Base InvalidIndirectStorageSource => new Result.Base(ModuleFs, 4023);

                public static Result.Base RomHostFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4241, 4259); }
                    public static Result.Base RomHostEntryCorrupted => new Result.Base(ModuleFs, 4242);
                    public static Result.Base RomHostFileDataCorrupted => new Result.Base(ModuleFs, 4243);
                    public static Result.Base RomHostFileCorrupted => new Result.Base(ModuleFs, 4244);
                    public static Result.Base InvalidRomHostHandle => new Result.Base(ModuleFs, 4245);

            public static Result.Base SaveDataCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4301, 4499); }
                public static Result.Base Result4302 => new Result.Base(ModuleFs, 4302);
                public static Result.Base InvalidSaveDataEntryType => new Result.Base(ModuleFs, 4303);
                public static Result.Base InvalidSaveDataHeader => new Result.Base(ModuleFs, 4315);
                public static Result.Base Result4362 => new Result.Base(ModuleFs, 4362);
                public static Result.Base Result4363 => new Result.Base(ModuleFs, 4363);
                public static Result.Base InvalidHashInSaveIvfc => new Result.Base(ModuleFs, 4364);
                public static Result.Base SaveIvfcHashIsEmpty => new Result.Base(ModuleFs, 4372);
                public static Result.Base InvalidHashInSaveIvfcTopLayer => new Result.Base(ModuleFs, 4373);
                public static Result.Base Result4402 => new Result.Base(ModuleFs, 4402);
                public static Result.Base Result4427 => new Result.Base(ModuleFs, 4427);

                public static Result.Base SaveDataHostFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4441, 4459); }
                    public static Result.Base SaveDataHostEntryCorrupted => new Result.Base(ModuleFs, 4442);
                    public static Result.Base SaveDataHostFileDataCorrupted => new Result.Base(ModuleFs, 4443);
                    public static Result.Base SaveDataHostFileCorrupted => new Result.Base(ModuleFs, 4444);
                    public static Result.Base InvalidSaveDataHostHandle => new Result.Base(ModuleFs, 4445);

                public static Result.Base SaveDataAllocationTableCorrupted => new Result.Base(ModuleFs, 4462);
                public static Result.Base SaveDataFileTableCorrupted => new Result.Base(ModuleFs, 4463);
                public static Result.Base AllocationTableIteratedRangeEntry => new Result.Base(ModuleFs, 4464);

            public static Result.Base NcaCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4501, 4599); }

            public static Result.Base IntegrityVerificationStorageCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4601, 4639); }
                public static Result.Base Result4602 => new Result.Base(ModuleFs, 4602);
                public static Result.Base Result4603 => new Result.Base(ModuleFs, 4603);
                public static Result.Base InvalidHashInIvfc => new Result.Base(ModuleFs, 4604);
                public static Result.Base IvfcHashIsEmpty => new Result.Base(ModuleFs, 4612);
                public static Result.Base InvalidHashInIvfcTopLayer => new Result.Base(ModuleFs, 4613);

            public static Result.Base PartitionFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4641, 4659); }
                public static Result.Base InvalidPartitionFileSystemHashOffset => new Result.Base(ModuleFs, 4642);
                public static Result.Base InvalidPartitionFileSystemHash => new Result.Base(ModuleFs, 4643);
                public static Result.Base InvalidPartitionFileSystemMagic => new Result.Base(ModuleFs, 4644);
                public static Result.Base InvalidHashedPartitionFileSystemMagic => new Result.Base(ModuleFs, 4645);
                public static Result.Base InvalidPartitionFileSystemEntryNameOffset => new Result.Base(ModuleFs, 4646);

            public static Result.Base BuiltInStorageCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4661, 4679); }
                public static Result.Base Result4662 => new Result.Base(ModuleFs, 4662);

            public static Result.Base FatFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4681, 4699); }

            public static Result.Base HostFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4701, 4719); }
                public static Result.Base HostEntryCorrupted => new Result.Base(ModuleFs, 4702);
                public static Result.Base HostFileDataCorrupted => new Result.Base(ModuleFs, 4703);
                public static Result.Base HostFileCorrupted => new Result.Base(ModuleFs, 4704);
                public static Result.Base InvalidHostHandle => new Result.Base(ModuleFs, 4705);

            public static Result.Base DatabaseCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4721, 4739); }
                public static Result.Base SaveDataAllocationTableCorruptedInternal => new Result.Base(ModuleFs, 4722);
                public static Result.Base SaveDataFileTableCorruptedInternal => new Result.Base(ModuleFs, 4723);
                public static Result.Base AllocationTableIteratedRangeEntryInternal => new Result.Base(ModuleFs, 4724);

            public static Result.Base AesXtsFileSystemCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4741, 4759); }
                public static Result.Base AesXtsFileHeaderTooShort => new Result.Base(ModuleFs, 4742);
                public static Result.Base AesXtsFileHeaderInvalidKeys => new Result.Base(ModuleFs, 4743);
                public static Result.Base AesXtsFileHeaderInvalidMagic => new Result.Base(ModuleFs, 4744);
                public static Result.Base AesXtsFileTooShort => new Result.Base(ModuleFs, 4745);
                public static Result.Base AesXtsFileHeaderTooShortInSetSize => new Result.Base(ModuleFs, 4746);
                public static Result.Base AesXtsFileHeaderInvalidKeysInRenameFile => new Result.Base(ModuleFs, 4747);
                public static Result.Base AesXtsFileHeaderInvalidKeysInSetSize => new Result.Base(ModuleFs, 4748);

            public static Result.Base SaveDataTransferDataCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4761, 4769); }

            public static Result.Base SignedSystemPartitionDataCorrupted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4771, 4779); }

            public static Result.Base GameCardLogoDataCorrupted => new Result.Base(ModuleFs, 4781);

            public static Result.Base Range4811To4819 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 4811, 4819); }
                public static Result.Base Result4812 => new Result.Base(ModuleFs, 4812);

        public static Result.Base Unexpected { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 5000, 5999); }
            public static Result.Base UnexpectedErrorInHostFileFlush => new Result.Base(ModuleFs, 5307);
            public static Result.Base UnexpectedErrorInHostFileGetSize => new Result.Base(ModuleFs, 5308);
            public static Result.Base UnknownHostFileSystemError => new Result.Base(ModuleFs, 5309);
            public static Result.Base InvalidNcaMountPoint => new Result.Base(ModuleFs, 5320);

        public static Result.Base PreconditionViolation { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6000, 6499); }
            public static Result.Base InvalidArgument { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6001, 6199); }
                public static Result.Base InvalidPath { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6002, 6029); }
                    public static Result.Base TooLongPath => new Result.Base(ModuleFs, 6003);
                    public static Result.Base InvalidCharacter => new Result.Base(ModuleFs, 6004);
                    public static Result.Base InvalidPathFormat => new Result.Base(ModuleFs, 6005);
                    public static Result.Base DirectoryUnobtainable => new Result.Base(ModuleFs, 6006);
                    public static Result.Base NotNormalized => new Result.Base(ModuleFs, 6007);

                public static Result.Base InvalidPathForOperation { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6030, 6059); }
                    public static Result.Base DirectoryNotDeletable => new Result.Base(ModuleFs, 6031);
                    public static Result.Base DestinationIsSubPathOfSource => new Result.Base(ModuleFs, 6032);
                    public static Result.Base PathNotFoundInSaveDataFileTable => new Result.Base(ModuleFs, 6033);
                    public static Result.Base DifferentDestFileSystem => new Result.Base(ModuleFs, 6034);

                public static Result.Base InvalidOffset => new Result.Base(ModuleFs, 6061);
                public static Result.Base InvalidSize => new Result.Base(ModuleFs, 6062);
                public static Result.Base NullArgument => new Result.Base(ModuleFs, 6063);
                public static Result.Base InvalidMountName => new Result.Base(ModuleFs, 6065);
                public static Result.Base ExtensionSizeTooLarge => new Result.Base(ModuleFs, 6066);
                public static Result.Base ExtensionSizeInvalid => new Result.Base(ModuleFs, 6067);
                public static Result.Base ReadOldSaveDataInfoReader => new Result.Base(ModuleFs, 6068);

                public static Result.Base InvalidEnumValue { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6080, 6099); }
                    public static Result.Base InvalidSaveDataState => new Result.Base(ModuleFs, 6081);
                    public static Result.Base InvalidSaveDataSpaceId => new Result.Base(ModuleFs, 6082);

            public static Result.Base InvalidOperationForOpenMode { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6200, 6299); }
                public static Result.Base FileExtensionWithoutOpenModeAllowAppend => new Result.Base(ModuleFs, 6201);
                public static Result.Base InvalidOpenModeForRead => new Result.Base(ModuleFs, 6202);
                public static Result.Base InvalidOpenModeForWrite => new Result.Base(ModuleFs, 6203);

            public static Result.Base UnsupportedOperation { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6300, 6399); }
                public static Result.Base SubStorageNotResizable => new Result.Base(ModuleFs, 6302);
                public static Result.Base SubStorageNotResizableMiddleOfFile => new Result.Base(ModuleFs, 6303);
                public static Result.Base UnsupportedOperationInMemoryStorageSetSize => new Result.Base(ModuleFs, 6304);
                public static Result.Base UnsupportedOperationInFileStorageOperateRange => new Result.Base(ModuleFs, 6306);
                public static Result.Base UnsupportedOperationInAesCtrExStorageWrite => new Result.Base(ModuleFs, 6310);
                public static Result.Base UnsupportedOperationInHierarchicalIvfcStorageSetSize => new Result.Base(ModuleFs, 6316);
                public static Result.Base UnsupportedOperationInIndirectStorageWrite => new Result.Base(ModuleFs, 6324);
                public static Result.Base UnsupportedOperationInIndirectStorageSetSize => new Result.Base(ModuleFs, 6325);
                public static Result.Base UnsupportedOperationInRoGameCardStorageWrite => new Result.Base(ModuleFs, 6350);
                public static Result.Base UnsupportedOperationInRoGameCardStorageSetSize => new Result.Base(ModuleFs, 6351);
                public static Result.Base UnsupportedOperationInConcatFsQueryEntry => new Result.Base(ModuleFs, 6359);
                public static Result.Base UnsupportedOperationModifyRomFsFileSystem => new Result.Base(ModuleFs, 6364);
                public static Result.Base UnsupportedOperationRomFsFileSystemGetSpace => new Result.Base(ModuleFs, 6366);
                public static Result.Base UnsupportedOperationModifyRomFsFile => new Result.Base(ModuleFs, 6367);
                public static Result.Base UnsupportedOperationModifyReadOnlyFileSystem => new Result.Base(ModuleFs, 6369);
                public static Result.Base UnsupportedOperationReadOnlyFileSystemGetSpace => new Result.Base(ModuleFs, 6371);
                public static Result.Base UnsupportedOperationModifyReadOnlyFile => new Result.Base(ModuleFs, 6372);
                public static Result.Base UnsupportedOperationModifyPartitionFileSystem => new Result.Base(ModuleFs, 6374);
                public static Result.Base UnsupportedOperationInPartitionFileSetSize => new Result.Base(ModuleFs, 6376);
                public static Result.Base UnsupportedOperationIdInPartitionFileSystem => new Result.Base(ModuleFs, 6377);

            public static Result.Base PermissionDenied { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6400, 6449); }

            public static Result.Base ExternalKeyAlreadyRegistered => new Result.Base(ModuleFs, 6452);
            public static Result.Base WriteStateUnflushed => new Result.Base(ModuleFs, 6454);
            public static Result.Base WriteModeFileNotClosed => new Result.Base(ModuleFs, 6457);
            public static Result.Base AllocatorAlignmentViolation => new Result.Base(ModuleFs, 6461);
            public static Result.Base UserNotExist => new Result.Base(ModuleFs, 6465);

        public static Result.Base EntryNotFound { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6600, 6699); }

        public static Result.Base OutOfResource { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6700, 6799); }
            public static Result.Base MappingTableFull => new Result.Base(ModuleFs, 6706);
            public static Result.Base AllocationTableInsufficientFreeBlocks => new Result.Base(ModuleFs, 6707);
            public static Result.Base OpenCountLimit => new Result.Base(ModuleFs, 6709);

        public static Result.Base MappingFailed { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6800, 6899); }
            public static Result.Base RemapStorageMapFull => new Result.Base(ModuleFs, 6811);

        public static Result.Base BadState { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleFs, 6900, 6999); }
            public static Result.Base SubStorageNotInitialized => new Result.Base(ModuleFs, 6902);
            public static Result.Base NotMounted => new Result.Base(ModuleFs, 6905);
            public static Result.Base SaveDataIsExtending => new Result.Base(ModuleFs, 6906);
    }
}
