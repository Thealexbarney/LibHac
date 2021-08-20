using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem.NcaUtils;

namespace LibHac.FsSrv.FsCreator
{
    public interface IStorageOnNcaCreator
    {
        Result Create(ref SharedRef<IStorage> outStorage, out NcaFsHeader fsHeader, Nca nca, int fsIndex, bool isCodeFs);
        Result CreateWithPatch(ref SharedRef<IStorage> outStorage, out NcaFsHeader fsHeader, Nca baseNca, Nca patchNca, int fsIndex, bool isCodeFs);
        Result OpenNca(out Nca nca, IStorage ncaStorage);
        Result VerifyAcidSignature(IFileSystem codeFileSystem, Nca nca);
    }
}
