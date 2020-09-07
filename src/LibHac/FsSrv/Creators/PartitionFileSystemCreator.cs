using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.FsSystem.Detail;

namespace LibHac.FsSrv.Creators
{
    public class PartitionFileSystemCreator : IPartitionFileSystemCreator
    {
        public Result Create(out IFileSystem fileSystem, IStorage pFsStorage)
        {
            var partitionFs = new PartitionFileSystemCore<StandardEntry>();

            Result rc = partitionFs.Initialize(pFsStorage);
            if (rc.IsFailure())
            {
                fileSystem = default;
                return rc;
            }

            fileSystem = partitionFs;
            return Result.Success;
        }

        public Result Create(out ReferenceCountedDisposable<IFileSystem> fileSystem, ReferenceCountedDisposable<IStorage> pFsStorage)
        {
            var partitionFs = new PartitionFileSystemCore<StandardEntry>();

            Result rc = partitionFs.Initialize(pFsStorage);
            if (rc.IsFailure())
            {
                fileSystem = default;
                return rc;
            }

            fileSystem = new ReferenceCountedDisposable<IFileSystem>(partitionFs);
            return Result.Success;
        }
    }
}
