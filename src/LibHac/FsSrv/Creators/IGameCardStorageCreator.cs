using LibHac.Fs;

namespace LibHac.FsSrv.Creators
{
    public interface IGameCardStorageCreator
    {
        Result CreateReadOnly(GameCardHandle handle, out ReferenceCountedDisposable<IStorage> storage);
        Result CreateSecureReadOnly(GameCardHandle handle, out ReferenceCountedDisposable<IStorage> storage);
        Result CreateWriteOnly(GameCardHandle handle, out ReferenceCountedDisposable<IStorage> storage);
    }
}