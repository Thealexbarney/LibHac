using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Impl;

public class DeepRetryFileSystem : ForwardingFileSystem
{
    // ReSharper disable once NotAccessedField.Local
    private WeakRef<DeepRetryFileSystem> _selfReference;
    private SharedRef<IRomFileSystemAccessFailureManager> _accessFailureManager;

    protected DeepRetryFileSystem(ref SharedRef<IFileSystem> baseFileSystem,
        ref SharedRef<IRomFileSystemAccessFailureManager> accessFailureManager) : base(ref baseFileSystem)
    {
        _accessFailureManager = SharedRef<IRomFileSystemAccessFailureManager>.CreateMove(ref accessFailureManager);
    }

    public static SharedRef<IFileSystem> CreateShared(ref SharedRef<IFileSystem> baseFileSystem,
        ref SharedRef<IRomFileSystemAccessFailureManager> accessFailureManager)
    {
        using var retryFileSystem = new SharedRef<DeepRetryFileSystem>(
            new DeepRetryFileSystem(ref baseFileSystem, ref accessFailureManager));

        retryFileSystem.Get._selfReference.Set(in retryFileSystem.Ref);

        return SharedRef<IFileSystem>.CreateMove(ref retryFileSystem.Ref);
    }

    public override void Dispose()
    {
        _accessFailureManager.Destroy();
        base.Dispose();
    }

    // ReSharper disable once RedundantOverriddenMember
    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        // Todo: Implement
        return base.DoOpenFile(ref outFile, path, mode);
    }
}