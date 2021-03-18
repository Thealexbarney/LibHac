using System;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.FsSystem.Impl;
using LibHac.FsSystem.NcaUtils;

namespace LibHac.FsSrv.FsCreator
{
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
        public Result Create(out ReferenceCountedDisposable<IStorage> storage, out NcaFsHeader fsHeader, Nca nca,
            int fsIndex, bool isCodeFs)
        {
            UnsafeHelpers.SkipParamInit(out storage, out fsHeader);

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

            storage = new ReferenceCountedDisposable<IStorage>(storageTemp);
            fsHeader = nca.GetFsHeader(fsIndex);

            return Result.Success;
        }

        public Result CreateWithPatch(out ReferenceCountedDisposable<IStorage> storage, out NcaFsHeader fsHeader,
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
}
