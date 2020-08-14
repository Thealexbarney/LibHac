using System;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.FsSystem.Detail;
using LibHac.FsSystem.NcaUtils;

namespace LibHac.FsSrv.Creators
{
    public class StorageOnNcaCreator : IStorageOnNcaCreator
    {
        // ReSharper disable once UnusedMember.Local
        private bool IsEnabledProgramVerification { get; set; }
        private Keyset Keyset { get; }

        public StorageOnNcaCreator(Keyset keyset)
        {
            Keyset = keyset;
        }

        // todo: Implement NcaReader and other Nca classes
        public Result Create(out IStorage storage, out NcaFsHeader fsHeader, Nca nca, int fsIndex, bool isCodeFs)
        {
            storage = default;
            fsHeader = default;

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

            storage = storageTemp;
            fsHeader = nca.GetFsHeader(fsIndex);

            return Result.Success;
        }

        public Result CreateWithPatch(out IStorage storage, out NcaFsHeader fsHeader, Nca baseNca, Nca patchNca, int fsIndex, bool isCodeFs)
        {
            throw new NotImplementedException();
        }

        public Result OpenNca(out Nca nca, IStorage ncaStorage)
        {
            nca = new Nca(Keyset, ncaStorage);
            return Result.Success;
        }

        public Result VerifyAcidSignature(IFileSystem codeFileSystem, Nca nca)
        {
            // todo
            return Result.Success;
        }

        private Result OpenStorage(out IStorage storage, Nca nca, int fsIndex)
        {
            storage = default;

            if (!nca.SectionExists(fsIndex))
                return ResultFs.PartitionNotFound.Log();

            storage = nca.OpenStorage(fsIndex, IntegrityCheckLevel.ErrorOnInvalid);
            return Result.Success;
        }
    }
}
