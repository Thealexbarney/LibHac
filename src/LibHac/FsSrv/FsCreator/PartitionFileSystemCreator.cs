using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.FsCreator;

/// <inheritdoc cref="IPartitionFileSystemCreator"/>
public class PartitionFileSystemCreator : IPartitionFileSystemCreator
{
    public Result Create(ref SharedRef<IFileSystem> outFileSystem, ref readonly SharedRef<IStorage> baseStorage)
    {
        using var partitionFs = new SharedRef<PartitionFileSystem>(new PartitionFileSystem());
        if (!partitionFs.HasValue)
            return ResultFs.AllocationMemoryFailedInPartitionFileSystemCreatorA.Log();

        Result res = partitionFs.Get.Initialize(in baseStorage);
        if (res.IsFailure()) return res.Miss();

        outFileSystem.SetByMove(ref partitionFs.Ref);
        return Result.Success;
    }
}