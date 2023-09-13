using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Storage;
using LibHac.GcSrv;

namespace LibHac.FsSrv.FsCreator;

/// <summary>
/// Creates <see cref="IStorage"/>s to the currently mounted game card.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
public class GameCardStorageCreator : IGameCardStorageCreator
{
    // LibHac addition so we can access fssrv::storage functions
    private readonly FileSystemServer _fsServer;

    public GameCardStorageCreator(FileSystemServer fsServer)
    {
        _fsServer = fsServer;
    }

    public void Dispose() { }

    public Result CreateReadOnly(GameCardHandle handle, ref SharedRef<IStorage> outStorage)
    {
        return _fsServer.Storage.OpenGameCardStorage(ref outStorage, OpenGameCardAttribute.ReadOnly, handle).Ret();
    }

    public Result CreateSecureReadOnly(GameCardHandle handle, ref SharedRef<IStorage> outStorage)
    {
        return _fsServer.Storage.OpenGameCardStorage(ref outStorage, OpenGameCardAttribute.SecureReadOnly, handle).Ret();
    }

    public Result CreateWriteOnly(GameCardHandle handle, ref SharedRef<IStorage> outStorage)
    {
        return _fsServer.Storage.OpenGameCardStorage(ref outStorage, OpenGameCardAttribute.WriteOnly, handle).Ret();
    }
}