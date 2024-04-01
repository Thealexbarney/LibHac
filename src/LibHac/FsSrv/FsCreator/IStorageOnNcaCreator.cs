using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;

namespace LibHac.FsSrv.FsCreator;

public interface IStorageOnNcaCreator
{
    Result Create(ref SharedRef<IStorage> outStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter, out NcaFsHeaderReader outHeaderReader,
        ref readonly SharedRef<NcaReader> ncaReader, int fsIndex);

    Result CreateWithPatch(ref SharedRef<IStorage> outStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter, out NcaFsHeaderReader outHeaderReader,
        ref readonly SharedRef<NcaReader> originalNcaReader, ref readonly SharedRef<NcaReader> currentNcaReader,
        int fsIndex);

    Result CreateNcaReader(ref SharedRef<NcaReader> outReader, ref readonly SharedRef<IStorage> baseStorage,
        ContentAttributes contentAttributes);
}