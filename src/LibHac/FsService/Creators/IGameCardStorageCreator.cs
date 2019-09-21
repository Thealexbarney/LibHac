using LibHac.Fs;

namespace LibHac.FsService.Creators
{
    public interface IGameCardStorageCreator
    {
        Result CreateNormal(GameCardHandle handle, out IStorage storage);
        Result CreateSecure(GameCardHandle handle, out IStorage storage);
        Result CreateWritable(GameCardHandle handle, out IStorage storage);
    }
}