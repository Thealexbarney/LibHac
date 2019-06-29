namespace LibHac.Fs
{
    public class ResultFs
    {
        public const int ModuleFs = 2;

        public static Result PathNotFound => new Result(ModuleFs, 1);
        public static Result MountNameAlreadyExists => new Result(ModuleFs, 60);

        public static Result NotImplemented => new Result(ModuleFs, 3001);
        public static Result ValueOutOfRange => new Result(ModuleFs, 3005);

        public static Result AesXtsFileFileStorageAllocationError => new Result(ModuleFs, 3312);
        public static Result AesXtsFileXtsStorageAllocationError => new Result(ModuleFs, 3313);
        public static Result AesXtsFileAlignmentStorageAllocationError => new Result(ModuleFs, 3314);
        public static Result AesXtsFileStorageFileAllocationError => new Result(ModuleFs, 3315);
        public static Result AesXtsFileSubStorageAllocationError => new Result(ModuleFs, 3383);

        public static Result InvalidIndirectStorageSource => new Result(ModuleFs, 4023);

        public static Result InvalidSaveDataHeader => new Result(ModuleFs, 4315);

        public static Result InvalidHashInIvfc => new Result(ModuleFs, 4604);
        public static Result IvfcHashIsEmpty => new Result(ModuleFs, 4612);
        public static Result InvalidHashInIvfcTopLayer => new Result(ModuleFs, 4613);
        public static Result InvalidPartitionFileSystemMagic => new Result(ModuleFs, 4644);
        public static Result InvalidHashedPartitionFileSystemMagic => new Result(ModuleFs, 4645);


        public static Result AesXtsFileHeaderTooShort => new Result(ModuleFs, 4742);
        public static Result AesXtsFileHeaderInvalidKeys => new Result(ModuleFs, 4743);
        public static Result AesXtsFileHeaderInvalidMagic => new Result(ModuleFs, 4744);
        public static Result AesXtsFileTooShort => new Result(ModuleFs, 4745);
        public static Result AesXtsFileHeaderTooShortInSetSize => new Result(ModuleFs, 4746);
        public static Result AesXtsFileHeaderInvalidKeysInRenameFile => new Result(ModuleFs, 4747);
        public static Result AesXtsFileHeaderInvalidKeysInSetSize => new Result(ModuleFs, 4748);

        public static Result InvalidInput => new Result(ModuleFs, 6001);
        public static Result DifferentDestFileSystem => new Result(ModuleFs, 6034);
        public static Result InvalidOffset => new Result(ModuleFs, 6061);
        public static Result InvalidSize => new Result(ModuleFs, 6062);
        public static Result NullArgument => new Result(ModuleFs, 6063);
        public static Result InvalidMountName => new Result(ModuleFs, 6065);

        public static Result InvalidOpenModeOperation => new Result(ModuleFs, 6200);
        public static Result AllowAppendRequiredForImplicitExtension => new Result(ModuleFs, 6201);

        public static Result UnsupportedOperation => new Result(ModuleFs, 6300);
        public static Result UnsupportedOperationInMemoryStorageSetSize => new Result(ModuleFs, 6316);
        public static Result UnsupportedOperationInHierarchicalIvfcStorageSetSize => new Result(ModuleFs, 6304);
        public static Result UnsupportedOperationInIndirectStorageWrite => new Result(ModuleFs, 6324);
        public static Result UnsupportedOperationInIndirectStorageSetSize => new Result(ModuleFs, 6325);
        public static Result UnsupportedOperationInConcatFsQueryEntry => new Result(ModuleFs, 6359);
        public static Result UnsupportedOperationModifyRomFsFileSystem => new Result(ModuleFs, 6364);
        public static Result UnsupportedOperationRomFsFileSystemGetSpace => new Result(ModuleFs, 6366);
        public static Result UnsupportedOperationModifyRomFsFile => new Result(ModuleFs, 6367);
        public static Result UnsupportedOperationModifyReadOnlyFileSystem => new Result(ModuleFs, 6369);
        public static Result UnsupportedOperationReadOnlyFileSystemGetSpace => new Result(ModuleFs, 6371);
        public static Result UnsupportedOperationModifyReadOnlyFile => new Result(ModuleFs, 6372);
        public static Result UnsupportedOperationModifyPartitionFileSystem => new Result(ModuleFs, 6374);
        public static Result UnsupportedOperationInPartitionFileSetSize => new Result(ModuleFs, 6376);

        public static Result WriteStateUnflushed => new Result(ModuleFs, 6454);
        public static Result WritableFileOpen => new Result(ModuleFs, 6457);
        

        public static Result AllocationTableInsufficientFreeBlocks => new Result(ModuleFs, 6707);

        public static Result MountNameNotFound => new Result(ModuleFs, 6905);
    }
}
