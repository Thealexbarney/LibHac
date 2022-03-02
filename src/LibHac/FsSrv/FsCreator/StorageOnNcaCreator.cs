using System;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.FsSystem.Impl;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NcaFsHeader = LibHac.Tools.FsSystem.NcaUtils.NcaFsHeader;

namespace LibHac.FsSrv.FsCreator;

public class StorageOnNcaCreator : IStorageOnNcaCreator
{
    // ReSharper disable once UnusedMember.Local
    private bool IsEnabledProgramVerification { get; set; }
    private KeySet KeySet { get; }

    public StorageOnNcaCreator(KeySet keySet)
    {
        KeySet = keySet;
    }

    // todo: Implement NcaReader and other Nca classes
    public Result Create(ref SharedRef<IStorage> outStorage, out NcaFsHeader fsHeader, Nca nca,
        int fsIndex, bool isCodeFs)
    {
        UnsafeHelpers.SkipParamInit(out fsHeader);

        Result rc = OpenStorage(out IStorage storageTemp, nca, fsIndex);
        if (rc.IsFailure()) return rc;

        if (isCodeFs)
        {
            using (var codeFs = new PartitionFileSystemCore<StandardEntry>())
            {
                rc = codeFs.Initialize(storageTemp);
                if (rc.IsFailure()) return rc;

                rc = VerifyAcidSignature(codeFs, nca);
                if (rc.IsFailure()) return rc;
            }
        }

        outStorage.Reset(storageTemp);
        fsHeader = nca.GetFsHeader(fsIndex);

        return Result.Success;
    }

    public Result CreateWithPatch(ref SharedRef<IStorage> outStorage, out NcaFsHeader fsHeader,
        Nca baseNca, Nca patchNca, int fsIndex, bool isCodeFs)
    {
        throw new NotImplementedException();
    }

    public Result OpenNca(out Nca nca, IStorage ncaStorage)
    {
        nca = new Nca(KeySet, ncaStorage);
        return Result.Success;
    }

    public Result VerifyAcidSignature(IFileSystem codeFileSystem, Nca nca)
    {
        // todo
        return Result.Success;
    }

    private Result OpenStorage(out IStorage storage, Nca nca, int fsIndex)
    {
        UnsafeHelpers.SkipParamInit(out storage);

        if (!nca.SectionExists(fsIndex))
            return ResultFs.PartitionNotFound.Log();

        storage = nca.OpenStorage(fsIndex, IntegrityCheckLevel.ErrorOnInvalid);
        return Result.Success;
    }
}