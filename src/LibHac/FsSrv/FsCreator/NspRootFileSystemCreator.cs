using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.FsCreator;

/// <inheritdoc cref="INspRootFileSystemCreator"/>
public class NspRootFileSystemCreator : INspRootFileSystemCreator
{
    public Result Create(ref SharedRef<IFileSystem> outFileSystem, ref readonly SharedRef<IStorage> baseStorage)
    {
        using var nspFs = new SharedRef<NintendoSubmissionPackageRootFileSystem>(new NintendoSubmissionPackageRootFileSystem());
        if (!nspFs.HasValue)
            return ResultFs.AllocationMemoryFailedInPartitionFileSystemCreatorA.Log();

        Result res = nspFs.Get.Initialize(in baseStorage);
        if (res.IsFailure()) return res.Miss();

        outFileSystem.SetByMove(ref nspFs.Ref);
        return Result.Success;
    }
}