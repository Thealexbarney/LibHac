using LibHac.Fs;

namespace LibHac.FsSrv.Creators
{
    public interface ISdStorageCreator
    {
        Result Create(out IStorage storage);
    }
}
