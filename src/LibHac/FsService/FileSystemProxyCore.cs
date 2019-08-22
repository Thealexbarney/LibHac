using System;
using LibHac.Fs;
using LibHac.FsService.Creators;

namespace LibHac.FsService
{
    public class FileSystemProxyCore
    {
        private FileSystemCreators FsCreators { get; }
        private byte[] SdEncryptionSeed { get; } = new byte[0x10];

        private const string NintendoDirectoryName = "Nintendo";
        private const string ContentDirectoryName = "Contents";

        public FileSystemProxyCore(FileSystemCreators fsCreators)
        {
            FsCreators = fsCreators;
        }

        public Result OpenBisFileSystem(out IFileSystem fileSystem, string rootPath, BisPartitionId partitionId)
        {
            return FsCreators.BuiltInStorageFileSystemCreator.Create(out fileSystem, rootPath, partitionId);
        }

        public Result OpenSdCardFileSystem(out IFileSystem fileSystem)
        {
            return FsCreators.SdFileSystemCreator.Create(out fileSystem);
        }

        public Result OpenContentStorageFileSystem(out IFileSystem fileSystem, ContentStorageId storageId)
        {
            fileSystem = default;

            string contentDirPath = default;
            IFileSystem baseFileSystem = default;
            bool isEncrypted = false;
            Result baseFsResult;

            switch (storageId)
            {
                case ContentStorageId.System:
                    baseFsResult = OpenBisFileSystem(out baseFileSystem, string.Empty, BisPartitionId.System);
                    contentDirPath = $"/{ContentDirectoryName}";
                    break;
                case ContentStorageId.User:
                    baseFsResult = OpenBisFileSystem(out baseFileSystem, string.Empty, BisPartitionId.User);
                    contentDirPath = $"/{ContentDirectoryName}";
                    break;
                case ContentStorageId.SdCard:
                    baseFsResult = OpenSdCardFileSystem(out baseFileSystem);
                    contentDirPath = $"/{NintendoDirectoryName}/{ContentDirectoryName}";
                    isEncrypted = true;
                    break;
                default:
                    baseFsResult = ResultFs.InvalidArgument;
                    break;
            }

            if (baseFsResult.IsFailure()) return baseFsResult;

            baseFileSystem.EnsureDirectoryExists(contentDirPath);

            Result subFsResult = FsCreators.SubDirectoryFileSystemCreator.Create(out IFileSystem subDirFileSystem,
                baseFileSystem, contentDirPath);
            if (subFsResult.IsFailure()) return subFsResult;

            if (!isEncrypted)
            {
                fileSystem = subDirFileSystem;
                return Result.Success;
            }

            return FsCreators.EncryptedFileSystemCreator.Create(out fileSystem, subDirFileSystem,
                EncryptedFsKeyId.Content, SdEncryptionSeed);
        }

        public Result SetSdCardEncryptionSeed(ReadOnlySpan<byte> seed)
        {
            seed.CopyTo(SdEncryptionSeed);
            FsCreators.SaveDataFileSystemCreator.SetSdCardEncryptionSeed(seed);

            return Result.Success;
        }
    }
}
