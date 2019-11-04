using System;
using LibHac.Fs;
using LibHac.FsSystem.NcaUtils;

namespace LibHac.FsService.Creators
{
    public class StorageOnNcaCreator : IStorageOnNcaCreator
    {
        public Result Create(out IStorage storage, out NcaFsHeader fsHeader, Nca nca, int fsIndex, bool isCodeFs)
        {
            throw new NotImplementedException();
        }

        public Result CreateWithPatch(out IStorage storage, out NcaFsHeader fsHeader, Nca baseNca, Nca patchNca, int fsIndex, bool isCodeFs)
        {
            throw new NotImplementedException();
        }

        public Result OpenNca(out Nca nca, IStorage ncaStorage)
        {
            throw new NotImplementedException();
        }

        public Result VerifyAcidSignature(IFileSystem codeFileSystem, Nca nca)
        {
            throw new NotImplementedException();
        }
    }
}
