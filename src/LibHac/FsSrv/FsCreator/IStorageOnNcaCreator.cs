using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem.NcaUtils;

namespace LibHac.FsSrv.FsCreator
{
    public interface IStorageOnNcaCreator
    {
        Result Create(out ReferenceCountedDisposable<IStorage> storage, out NcaFsHeader fsHeader, Nca nca, int fsIndex, bool isCodeFs);
        Result CreateWithPatch(out ReferenceCountedDisposable<IStorage> storage, out NcaFsHeader fsHeader, Nca baseNca, Nca patchNca, int fsIndex, bool isCodeFs);
        Result OpenNca(out Nca nca, IStorage ncaStorage);
        Result VerifyAcidSignature(IFileSystem codeFileSystem, Nca nca);
    }
}
