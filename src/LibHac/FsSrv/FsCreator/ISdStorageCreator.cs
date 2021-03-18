using LibHac.Fs;

namespace LibHac.FsSrv.FsCreator
{
    public interface ISdStorageCreator
    {
        Result Create(out IStorage storage);
    }
}
