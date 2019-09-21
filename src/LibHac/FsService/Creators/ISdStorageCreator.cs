using LibHac.FsSystem;

namespace LibHac.FsService.Creators
{
    public interface ISdStorageCreator
    {
        Result Create(out IStorage storage);
    }
}
