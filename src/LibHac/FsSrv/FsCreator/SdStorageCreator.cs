using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Storage;

namespace LibHac.FsSrv.FsCreator;

/// <summary>
/// Creates <see cref="IStorage"/>s for accessing the inserted SD card's storage.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
public class SdStorageCreator : ISdStorageCreator
{
    // LibHac addition
    private readonly FileSystemServer _fsServer;

    public SdStorageCreator(FileSystemServer fsServer)
    {
        _fsServer = fsServer;
    }

    public void Dispose()
    {
        // ...
    }

    public Result Create(ref SharedRef<IStorage> outStorage)
    {
        return _fsServer.Storage.OpenSdStorage(ref outStorage).Ret();
    }
}