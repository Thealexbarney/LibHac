using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;

namespace LibHac.FsSrv.FsCreator;

public interface IStorageOnNcaCreator
{
    Result Create(ref SharedRef<IStorage> outStorage, out LibHac.Tools.FsSystem.NcaUtils.NcaFsHeader fsHeader, LibHac.Tools.FsSystem.NcaUtils.Nca nca, int fsIndex, bool isCodeFs);
    Result OpenNca(out LibHac.Tools.FsSystem.NcaUtils.Nca nca, IStorage ncaStorage);
}

public interface IStorageOnNcaCreator17
{
    Result Create(ref SharedRef<IStorage> outStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter, out NcaFsHeaderReader17 outHeaderReader,
        ref readonly SharedRef<NcaReader17> ncaReader, int fsIndex);

    Result CreateWithPatch(ref SharedRef<IStorage> outStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter, out NcaFsHeaderReader17 outHeaderReader,
        ref readonly SharedRef<NcaReader17> originalNcaReader, ref readonly SharedRef<NcaReader17> currentNcaReader,
        int fsIndex);

    Result CreateNcaReader(ref SharedRef<NcaReader17> outReader, ref readonly SharedRef<IStorage> baseStorage,
        ContentAttributes contentAttributes);
}