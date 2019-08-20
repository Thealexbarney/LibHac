using LibHac.Fs;
using LibHac.Fs.NcaUtils;

namespace LibHac.FsService.Creators
{
    public interface IStorageOnNcaCreator
    {
        Result Create(out IStorage storage, out NcaFsHeader fsHeader, Nca nca, int fsIndex, bool isCodeFs);
        Result CreateWithPatch(out IStorage storage, out NcaFsHeader fsHeader, Nca baseNca, Nca patchNca, int fsIndex, bool isCodeFs);
        Result OpenNca(out Nca nca, IStorage ncaStorage);
        Result VerifyAcidSignature(IFileSystem codeFileSystem, Nca nca);
    }
}
