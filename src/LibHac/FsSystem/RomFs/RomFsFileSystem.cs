using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem.RomFs
{
    public class RomFsFileSystem : IFileSystem
    {
        public RomfsHeader Header { get; }

        public HierarchicalRomFileTable<RomFileInfo> FileTable { get; }
        private IStorage BaseStorage { get; }

        public RomFsFileSystem(IStorage storage)
        {
            BaseStorage = storage;
            Header = new RomfsHeader(storage.AsFile(OpenMode.Read));

            IStorage dirHashTable = storage.Slice(Header.DirHashTableOffset, Header.DirHashTableSize);
            IStorage dirEntryTable = storage.Slice(Header.DirMetaTableOffset, Header.DirMetaTableSize);
            IStorage fileHashTable = storage.Slice(Header.FileHashTableOffset, Header.FileHashTableSize);
            IStorage fileEntryTable = storage.Slice(Header.FileMetaTableOffset, Header.FileMetaTableSize);

            FileTable = new HierarchicalRomFileTable<RomFileInfo>(dirHashTable, dirEntryTable, fileHashTable, fileEntryTable);
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out entryType);

            if (FileTable.TryOpenFile(path.ToString(), out RomFileInfo _))
            {
                entryType = DirectoryEntryType.File;
                return Result.Success;
            }

            if (FileTable.TryOpenDirectory(path.ToString(), out FindPosition _))
            {
                entryType = DirectoryEntryType.Directory;
                return Result.Success;
            }

            return ResultFs.PathNotFound.Log();
        }

        protected override Result DoCommit()
        {
            return Result.Success;
        }

        protected override Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            UnsafeHelpers.SkipParamInit(out directory);

            if (!FileTable.TryOpenDirectory(path.ToString(), out FindPosition position))
            {
                return ResultFs.PathNotFound.Log();
            }

            directory = new RomFsDirectory(this, position, mode);
            return Result.Success;
        }

        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            UnsafeHelpers.SkipParamInit(out file);

            if (!FileTable.TryOpenFile(path.ToString(), out RomFileInfo info))
            {
                return ResultFs.PathNotFound.Log();
            }

            if (mode != OpenMode.Read)
            {
                // RomFs files must be opened read-only.
                return ResultFs.InvalidArgument.Log();
            }

            file = new RomFsFile(BaseStorage, Header.DataOffset + info.Offset, info.Length);
            return Result.Success;
        }

        public IStorage GetBaseStorage()
        {
            return BaseStorage;
        }

        protected override Result DoCreateDirectory(U8Span path) => ResultFs.UnsupportedWriteForRomFsFileSystem.Log();
        protected override Result DoCreateFile(U8Span path, long size, CreateFileOptions options) => ResultFs.UnsupportedWriteForRomFsFileSystem.Log();
        protected override Result DoDeleteDirectory(U8Span path) => ResultFs.UnsupportedWriteForRomFsFileSystem.Log();
        protected override Result DoDeleteDirectoryRecursively(U8Span path) => ResultFs.UnsupportedWriteForRomFsFileSystem.Log();
        protected override Result DoCleanDirectoryRecursively(U8Span path) => ResultFs.UnsupportedWriteForRomFsFileSystem.Log();
        protected override Result DoDeleteFile(U8Span path) => ResultFs.UnsupportedWriteForRomFsFileSystem.Log();
        protected override Result DoRenameDirectory(U8Span oldPath, U8Span newPath) => ResultFs.UnsupportedWriteForRomFsFileSystem.Log();
        protected override Result DoRenameFile(U8Span oldPath, U8Span newPath) => ResultFs.UnsupportedWriteForRomFsFileSystem.Log();
        protected override Result DoCommitProvisionally(long counter) => ResultFs.UnsupportedCommitProvisionallyForRomFsFileSystem.Log();

        protected override Result DoGetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            freeSpace = 0;
            return Result.Success;
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out totalSpace);
            return ResultFs.UnsupportedGetTotalSpaceSizeForRomFsFileSystem.Log();
        }

        internal static Result ConvertRomFsDriverPrivateResult(Result result)
        {
            if (result.IsSuccess())
                return Result.Success;

            if (ResultFs.UnsupportedVersion.Includes(result))
                return ResultFs.UnsupportedRomVersion.LogConverted(result);

            if (ResultFs.NcaCorrupted.Includes(result) ||
                ResultFs.IntegrityVerificationStorageCorrupted.Includes(result) ||
                ResultFs.BuiltInStorageCorrupted.Includes(result) ||
                ResultFs.PartitionFileSystemCorrupted.Includes(result) ||
                ResultFs.HostFileSystemCorrupted.Includes(result))
            {
                return ConvertCorruptedResult(result);
            }

            if (ResultFs.FatFileSystemCorrupted.Includes(result))
                return result;

            if (ResultFs.NotFound.Includes(result))
                return ResultFs.PathNotFound.LogConverted(result);

            if (ResultFs.InvalidOffset.Includes(result))
                return ResultFs.OutOfRange.LogConverted(result);

            if (ResultFs.FileNotFound.Includes(result) ||
                ResultFs.IncompatiblePath.Includes(result))
            {
                return ResultFs.PathNotFound.LogConverted(result);
            }

            return result;
        }

        private static Result ConvertCorruptedResult(Result result)
        {
            if (ResultFs.NcaCorrupted.Includes(result))
            {
                if (ResultFs.InvalidNcaFileSystemType.Includes(result))
                    return ResultFs.InvalidRomNcaFileSystemType.LogConverted(result);

                if (ResultFs.InvalidNcaSignature.Includes(result))
                    return ResultFs.InvalidRomNcaSignature.LogConverted(result);

                if (ResultFs.NcaHeaderSignature1VerificationFailed.Includes(result))
                    return ResultFs.RomNcaHeaderSignature1VerificationFailed.LogConverted(result);

                if (ResultFs.NcaFsHeaderHashVerificationFailed.Includes(result))
                    return ResultFs.RomNcaFsHeaderHashVerificationFailed.LogConverted(result);

                if (ResultFs.InvalidNcaKeyIndex.Includes(result))
                    return ResultFs.InvalidRomNcaKeyIndex.LogConverted(result);

                if (ResultFs.InvalidNcaFsHeaderHashType.Includes(result))
                    return ResultFs.InvalidRomNcaFsHeaderHashType.LogConverted(result);

                if (ResultFs.InvalidNcaFsHeaderEncryptionType.Includes(result))
                    return ResultFs.InvalidRomNcaFsHeaderEncryptionType.LogConverted(result);

                if (ResultFs.InvalidNcaPatchInfoIndirectSize.Includes(result))
                    return ResultFs.InvalidRomNcaPatchInfoIndirectSize.LogConverted(result);

                if (ResultFs.InvalidNcaPatchInfoAesCtrExSize.Includes(result))
                    return ResultFs.InvalidRomNcaPatchInfoAesCtrExSize.LogConverted(result);

                if (ResultFs.InvalidNcaPatchInfoAesCtrExOffset.Includes(result))
                    return ResultFs.InvalidRomNcaPatchInfoAesCtrExOffset.LogConverted(result);

                if (ResultFs.InvalidNcaId.Includes(result))
                    return ResultFs.InvalidRomNcaId.LogConverted(result);

                if (ResultFs.InvalidNcaHeader.Includes(result))
                    return ResultFs.InvalidRomNcaHeader.LogConverted(result);

                if (ResultFs.InvalidNcaFsHeader.Includes(result))
                    return ResultFs.InvalidRomNcaFsHeader.LogConverted(result);

                if (ResultFs.InvalidNcaPatchInfoIndirectOffset.Includes(result))
                    return ResultFs.InvalidRomNcaPatchInfoIndirectOffset.LogConverted(result);

                if (ResultFs.InvalidHierarchicalSha256BlockSize.Includes(result))
                    return ResultFs.InvalidRomHierarchicalSha256BlockSize.LogConverted(result);

                if (ResultFs.InvalidHierarchicalSha256LayerCount.Includes(result))
                    return ResultFs.InvalidRomHierarchicalSha256LayerCount.LogConverted(result);

                if (ResultFs.HierarchicalSha256BaseStorageTooLarge.Includes(result))
                    return ResultFs.RomHierarchicalSha256BaseStorageTooLarge.LogConverted(result);

                if (ResultFs.HierarchicalSha256HashVerificationFailed.Includes(result))
                    return ResultFs.RomHierarchicalSha256HashVerificationFailed.LogConverted(result);

                if (ResultFs.InvalidHierarchicalIntegrityVerificationLayerCount.Includes(result))
                    return ResultFs.InvalidRomHierarchicalIntegrityVerificationLayerCount.LogConverted(result);

                if (ResultFs.NcaIndirectStorageOutOfRange.Includes(result))
                    return ResultFs.RomNcaIndirectStorageOutOfRange.LogConverted(result);
            }

            if (ResultFs.IntegrityVerificationStorageCorrupted.Includes(result))
            {
                if (ResultFs.IncorrectIntegrityVerificationMagic.Includes(result))
                    return ResultFs.IncorrectRomIntegrityVerificationMagic.LogConverted(result);

                if (ResultFs.InvalidZeroHash.Includes(result))
                    return ResultFs.InvalidRomZeroSignature.LogConverted(result);

                if (ResultFs.NonRealDataVerificationFailed.Includes(result))
                    return ResultFs.RomNonRealDataVerificationFailed.LogConverted(result);

                if (ResultFs.ClearedRealDataVerificationFailed.Includes(result))
                    return ResultFs.ClearedRomRealDataVerificationFailed.LogConverted(result);

                if (ResultFs.UnclearedRealDataVerificationFailed.Includes(result))
                    return ResultFs.UnclearedRomRealDataVerificationFailed.LogConverted(result);
            }

            if (ResultFs.PartitionFileSystemCorrupted.Includes(result))
            {
                if (ResultFs.InvalidSha256PartitionHashTarget.Includes(result))
                    return ResultFs.InvalidRomSha256PartitionHashTarget.LogConverted(result);

                if (ResultFs.Sha256PartitionHashVerificationFailed.Includes(result))
                    return ResultFs.RomSha256PartitionHashVerificationFailed.LogConverted(result);

                if (ResultFs.PartitionSignatureVerificationFailed.Includes(result))
                    return ResultFs.RomPartitionSignatureVerificationFailed.LogConverted(result);

                if (ResultFs.Sha256PartitionSignatureVerificationFailed.Includes(result))
                    return ResultFs.RomSha256PartitionSignatureVerificationFailed.LogConverted(result);

                if (ResultFs.InvalidPartitionEntryOffset.Includes(result))
                    return ResultFs.InvalidRomPartitionEntryOffset.LogConverted(result);

                if (ResultFs.InvalidSha256PartitionMetaDataSize.Includes(result))
                    return ResultFs.InvalidRomSha256PartitionMetaDataSize.LogConverted(result);
            }

            if (ResultFs.HostFileSystemCorrupted.Includes(result))
            {
                if (ResultFs.HostEntryCorrupted.Includes(result))
                    return ResultFs.RomHostEntryCorrupted.LogConverted(result);

                if (ResultFs.HostFileDataCorrupted.Includes(result))
                    return ResultFs.RomHostFileDataCorrupted.LogConverted(result);

                if (ResultFs.HostFileCorrupted.Includes(result))
                    return ResultFs.RomHostFileCorrupted.LogConverted(result);

                if (ResultFs.InvalidHostHandle.Includes(result))
                    return ResultFs.InvalidRomHostHandle.LogConverted(result);
            }

            return result;
        }
    }

    public class RomfsHeader
    {
        public long HeaderSize { get; }
        public long DirHashTableOffset { get; }
        public long DirHashTableSize { get; }
        public long DirMetaTableOffset { get; }
        public long DirMetaTableSize { get; }
        public long FileHashTableOffset { get; }
        public long FileHashTableSize { get; }
        public long FileMetaTableOffset { get; }
        public long FileMetaTableSize { get; }
        public long DataOffset { get; }

        public RomfsHeader(IFile file)
        {
            var reader = new FileReader(file);

            HeaderSize = reader.ReadInt32();

            Func<long> func;

            // Old pre-release romfs is exactly the same except the fields in the header are 32-bit instead of 64-bit
            if (HeaderSize == 0x28)
            {
                func = () => reader.ReadInt32();
            }
            else
            {
                func = reader.ReadInt64;
                reader.Position += 4;
            }

            DirHashTableOffset = func();
            DirHashTableSize = func();
            DirMetaTableOffset = func();
            DirMetaTableSize = func();
            FileHashTableOffset = func();
            FileHashTableSize = func();
            FileMetaTableOffset = func();
            FileMetaTableSize = func();
            DataOffset = func();
        }
    }
}
