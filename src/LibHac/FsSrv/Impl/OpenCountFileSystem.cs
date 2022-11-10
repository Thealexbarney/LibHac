using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Impl;

internal class OpenCountFileSystem : ForwardingFileSystem
{
    private SharedRef<IEntryOpenCountSemaphoreManager> _entryCountSemaphore;
    private UniqueRef<IUniqueLock> _mountCountSemaphore;

    public OpenCountFileSystem(ref SharedRef<IFileSystem> baseFileSystem,
        ref SharedRef<IEntryOpenCountSemaphoreManager> entryCountSemaphore) : base(ref baseFileSystem)
    {
        _entryCountSemaphore = SharedRef<IEntryOpenCountSemaphoreManager>.CreateMove(ref entryCountSemaphore);
    }

    public OpenCountFileSystem(ref SharedRef<IFileSystem> baseFileSystem,
        ref SharedRef<IEntryOpenCountSemaphoreManager> entryCountSemaphore,
        ref UniqueRef<IUniqueLock> mountCountSemaphore) : base(ref baseFileSystem)
    {
        _entryCountSemaphore = SharedRef<IEntryOpenCountSemaphoreManager>.CreateMove(ref entryCountSemaphore);
        _mountCountSemaphore = new UniqueRef<IUniqueLock>(ref mountCountSemaphore);
    }

    public static SharedRef<IFileSystem> CreateShared(
        ref SharedRef<IFileSystem> baseFileSystem,
        ref SharedRef<IEntryOpenCountSemaphoreManager> entryCountSemaphore,
        ref UniqueRef<IUniqueLock> mountCountSemaphore)
    {
        var filesystem =
            new OpenCountFileSystem(ref baseFileSystem, ref entryCountSemaphore, ref mountCountSemaphore);

        return new SharedRef<IFileSystem>(filesystem);
    }

    public static SharedRef<IFileSystem> CreateShared(
        ref SharedRef<IFileSystem> baseFileSystem,
        ref SharedRef<IEntryOpenCountSemaphoreManager> entryCountSemaphore)
    {
        var filesystem =
            new OpenCountFileSystem(ref baseFileSystem, ref entryCountSemaphore);

        return new SharedRef<IFileSystem>(filesystem);
    }

    // ReSharper disable once RedundantOverriddenMember
    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        // Todo: Implement
        return base.DoOpenFile(ref outFile, path, mode);
    }

    // ReSharper disable once RedundantOverriddenMember
    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
        OpenDirectoryMode mode)
    {
        // Todo: Implement
        return base.DoOpenDirectory(ref outDirectory, path, mode);
    }

    public override void Dispose()
    {
        _entryCountSemaphore.Destroy();
        _mountCountSemaphore.Destroy();
        base.Dispose();
    }
}