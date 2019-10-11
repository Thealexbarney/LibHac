namespace LibHac.Fs
{
    public class ResultFs
    {
        public const int ModuleFs = 2;

        public static Result PathNotFound => new Result(ModuleFs, 1);
        public static Result PathAlreadyExists => new Result(ModuleFs, 2);
        public static Result TargetLocked => new Result(ModuleFs, 7);
        public static Result DirectoryNotEmpty => new Result(ModuleFs, 8);
        public static Result InsufficientFreeSpace => new Result(ModuleFs, 30);
        public static Result MountNameAlreadyExists => new Result(ModuleFs, 60);

        public static Result PartitionNotFound => new Result(ModuleFs, 1001);
        public static Result TargetNotFound => new Result(ModuleFs, 1002);
        public static Result ExternalKeyNotFound => new Result(ModuleFs, 1004);

        public static Result InvalidBufferForGameCard => new Result(ModuleFs, 2503);
        public static Result GameCardNotInserted => new Result(ModuleFs, 2520);

        public static Result GameCardNotInsertedOnGetHandle => new Result(ModuleFs, 2951);
        public static Result InvalidGameCardHandleOnRead => new Result(ModuleFs, 2952);
        public static Result InvalidGameCardHandleOnGetCardInfo => new Result(ModuleFs, 2954);
        public static Result InvalidGameCardHandleOnOpenNormalPartition => new Result(ModuleFs, 2960);
        public static Result InvalidGameCardHandleOnOpenSecurePartition => new Result(ModuleFs, 2961);

        public static Result NotImplemented => new Result(ModuleFs, 3001);
        public static Result Result3002 => new Result(ModuleFs, 3002);
        public static Result SaveDataPathAlreadyExists => new Result(ModuleFs, 3003);
        public static Result ValueOutOfRange => new Result(ModuleFs, 3005);

        public static Result AesXtsFileFileStorageAllocationError => new Result(ModuleFs, 3312);
        public static Result AesXtsFileXtsStorageAllocationError => new Result(ModuleFs, 3313);
        public static Result AesXtsFileAlignmentStorageAllocationError => new Result(ModuleFs, 3314);
        public static Result AesXtsFileStorageFileAllocationError => new Result(ModuleFs, 3315);
        public static Result AesXtsFileSubStorageAllocationError => new Result(ModuleFs, 3383);

        public static Result InvalidIndirectStorageSource => new Result(ModuleFs, 4023);

        public static Result Result4302 => new Result(ModuleFs, 4302);
        public static Result InvalidSaveDataEntryType => new Result(ModuleFs, 4303);
        public static Result InvalidSaveDataHeader => new Result(ModuleFs, 4315);
        public static Result Result4362 => new Result(ModuleFs, 4362);
        public static Result Result4363 => new Result(ModuleFs, 4363);
        public static Result InvalidHashInSaveIvfc => new Result(ModuleFs, 4364);
        public static Result SaveIvfcHashIsEmpty => new Result(ModuleFs, 4372);
        public static Result InvalidHashInSaveIvfcTopLayer => new Result(ModuleFs, 4373);


        public static Result Result4402 => new Result(ModuleFs, 4402);
        public static Result Result4427 => new Result(ModuleFs, 4427);
        public static Result SaveDataAllocationTableCorrupted => new Result(ModuleFs, 4462);
        public static Result SaveDataFileTableCorrupted => new Result(ModuleFs, 4463);
        public static Result AllocationTableIteratedRangeEntry => new Result(ModuleFs, 4464);

        public static Result Result4602 => new Result(ModuleFs, 4602);
        public static Result Result4603 => new Result(ModuleFs, 4603);
        public static Result InvalidHashInIvfc => new Result(ModuleFs, 4604);
        public static Result IvfcHashIsEmpty => new Result(ModuleFs, 4612);
        public static Result InvalidHashInIvfcTopLayer => new Result(ModuleFs, 4613);
        public static Result InvalidPartitionFileSystemMagic => new Result(ModuleFs, 4644);
        public static Result InvalidHashedPartitionFileSystemMagic => new Result(ModuleFs, 4645);
        public static Result Result4662 => new Result(ModuleFs, 4662);

        public static Result SaveDataAllocationTableCorruptedInternal => new Result(ModuleFs, 4722);
        public static Result SaveDataFileTableCorruptedInternal => new Result(ModuleFs, 4723);
        public static Result AllocationTableIteratedRangeEntryInternal => new Result(ModuleFs, 4724);
        public static Result AesXtsFileHeaderTooShort => new Result(ModuleFs, 4742);
        public static Result AesXtsFileHeaderInvalidKeys => new Result(ModuleFs, 4743);
        public static Result AesXtsFileHeaderInvalidMagic => new Result(ModuleFs, 4744);
        public static Result AesXtsFileTooShort => new Result(ModuleFs, 4745);
        public static Result AesXtsFileHeaderTooShortInSetSize => new Result(ModuleFs, 4746);
        public static Result AesXtsFileHeaderInvalidKeysInRenameFile => new Result(ModuleFs, 4747);
        public static Result AesXtsFileHeaderInvalidKeysInSetSize => new Result(ModuleFs, 4748);

        public static Result Result4812 => new Result(ModuleFs, 4812);

        public static Result UnexpectedErrorInHostFileFlush => new Result(ModuleFs, 5307);
        public static Result UnexpectedErrorInHostFileGetSize => new Result(ModuleFs, 5308);
        public static Result UnknownHostFileSystemError => new Result(ModuleFs, 5309);

        public static Result PreconditionViolation => new Result(ModuleFs, 6000);
        public static Result InvalidArgument => new Result(ModuleFs, 6001);
        public static Result InvalidPath => new Result(ModuleFs, 6002);
        public static Result TooLongPath => new Result(ModuleFs, 6003);
        public static Result InvalidCharacter => new Result(ModuleFs, 6004);
        public static Result InvalidPathFormat => new Result(ModuleFs, 6005);
        public static Result DirectoryUnobtainable => new Result(ModuleFs, 6006);
        public static Result NotNormalized => new Result(ModuleFs, 6007);

        public static Result DestinationIsSubPathOfSource => new Result(ModuleFs, 6032);
        public static Result PathNotFoundInSaveDataFileTable => new Result(ModuleFs, 6033);
        public static Result DifferentDestFileSystem => new Result(ModuleFs, 6034);
        public static Result InvalidOffset => new Result(ModuleFs, 6061);
        public static Result InvalidSize => new Result(ModuleFs, 6062);
        public static Result NullArgument => new Result(ModuleFs, 6063);
        public static Result InvalidMountName => new Result(ModuleFs, 6065);
        public static Result ExtensionSizeTooLarge => new Result(ModuleFs, 6066);
        public static Result ExtensionSizeInvalid => new Result(ModuleFs, 6067);

        public static Result InvalidOpenModeOperation => new Result(ModuleFs, 6200);
        public static Result FileExtensionWithoutOpenModeAllowAppend => new Result(ModuleFs, 6201);
        public static Result InvalidOpenModeForRead => new Result(ModuleFs, 6202);
        public static Result InvalidOpenModeForWrite => new Result(ModuleFs, 6203);

        public static Result UnsupportedOperation => new Result(ModuleFs, 6300);
        public static Result SubStorageNotResizable => new Result(ModuleFs, 6302);
        public static Result SubStorageNotResizableMiddleOfFile => new Result(ModuleFs, 6302);
        public static Result UnsupportedOperationInMemoryStorageSetSize => new Result(ModuleFs, 6316);
        public static Result UnsupportedOperationInHierarchicalIvfcStorageSetSize => new Result(ModuleFs, 6304);
        public static Result UnsupportedOperationInAesCtrExStorageWrite => new Result(ModuleFs, 6310);
        public static Result UnsupportedOperationInIndirectStorageWrite => new Result(ModuleFs, 6324);
        public static Result UnsupportedOperationInIndirectStorageSetSize => new Result(ModuleFs, 6325);
        public static Result UnsupportedOperationInRoGameCardStorageWrite => new Result(ModuleFs, 6350);
        public static Result UnsupportedOperationInRoGameCardStorageSetSize => new Result(ModuleFs, 6351);
        public static Result UnsupportedOperationInConcatFsQueryEntry => new Result(ModuleFs, 6359);
        public static Result UnsupportedOperationModifyRomFsFileSystem => new Result(ModuleFs, 6364);
        public static Result UnsupportedOperationRomFsFileSystemGetSpace => new Result(ModuleFs, 6366);
        public static Result UnsupportedOperationModifyRomFsFile => new Result(ModuleFs, 6367);
        public static Result UnsupportedOperationModifyReadOnlyFileSystem => new Result(ModuleFs, 6369);
        public static Result UnsupportedOperationReadOnlyFileSystemGetSpace => new Result(ModuleFs, 6371);
        public static Result UnsupportedOperationModifyReadOnlyFile => new Result(ModuleFs, 6372);
        public static Result UnsupportedOperationModifyPartitionFileSystem => new Result(ModuleFs, 6374);
        public static Result UnsupportedOperationInPartitionFileSetSize => new Result(ModuleFs, 6376);

        public static Result PermissionDenied => new Result(ModuleFs, 6400);
        public static Result ExternalKeyAlreadyRegistered => new Result(ModuleFs, 6452);
        public static Result WriteStateUnflushed => new Result(ModuleFs, 6454);
        public static Result WritableFileOpen => new Result(ModuleFs, 6457);


        public static Result MappingTableFull => new Result(ModuleFs, 6706);
        public static Result AllocationTableInsufficientFreeBlocks => new Result(ModuleFs, 6707);
        public static Result OpenCountLimit => new Result(ModuleFs, 6709);

        public static Result RemapStorageMapFull => new Result(ModuleFs, 6811);

        public static Result SubStorageNotInitialized => new Result(ModuleFs, 6902);
        public static Result MountNameNotFound => new Result(ModuleFs, 6905);
    }
}
