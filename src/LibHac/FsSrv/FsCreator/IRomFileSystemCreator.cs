using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator
{
    public interface IRomFileSystemCreator
    {
        Result Create(ref SharedRef<IFileSystem> outFileSystem, ref SharedRef<IStorage> romFsStorage);
    }
}
