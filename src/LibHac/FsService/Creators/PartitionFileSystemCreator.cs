using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.FsSystem.Detail;

namespace LibHac.FsService.Creators
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
    }
}
