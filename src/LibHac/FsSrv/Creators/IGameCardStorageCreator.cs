using LibHac.Fs;

namespace LibHac.FsSrv.Creators
{
    public interface IGameCardStorageCreator
    {
        Result CreateNormal(GameCardHandle handle, out IStorage storage);
        Result CreateSecure(GameCardHandle handle, out IStorage storage);
        Result CreateWritable(GameCardHandle handle, out IStorage storage);
    }
}