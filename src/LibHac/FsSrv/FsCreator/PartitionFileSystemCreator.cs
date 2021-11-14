using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.FsSystem.Impl;

namespace LibHac.FsSrv.FsCreator;

public class PartitionFileSystemCreator : IPartitionFileSystemCreator
{
    public Result Create(ref SharedRef<IFileSystem> outFileSystem, ref SharedRef<IStorage> baseStorage)
    {
        using var partitionFs =
            new SharedRef<PartitionFileSystemCore<StandardEntry>>(new PartitionFileSystemCore<StandardEntry>());

        Result rc = partitionFs.Get.Initialize(ref baseStorage);
        if (rc.IsFailure()) return rc.Miss();

        outFileSystem.SetByMove(ref partitionFs.Ref());
        return Result.Success;
    }
}
