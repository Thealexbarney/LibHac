using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Impl;

internal class OpenCountFileSystem : ForwardingFileSystem
{
    private SharedRef<IEntryOpenCountSemaphoreManager> _entryCountSemaphore;
    private UniqueRef<IUniqueLock> _mountCountSemaphore;

    public OpenCountFileSystem(ref readonly SharedRef<IFileSystem> baseFileSystem,
        ref readonly SharedRef<IEntryOpenCountSemaphoreManager> entryCountSemaphore) : base(in baseFileSystem)
    {
        _entryCountSemaphore = SharedRef<IEntryOpenCountSemaphoreManager>.CreateCopy(in entryCountSemaphore);
    }

    public OpenCountFileSystem(ref readonly SharedRef<IFileSystem> baseFileSystem,
        ref readonly SharedRef<IEntryOpenCountSemaphoreManager> entryCountSemaphore,
        ref UniqueRef<IUniqueLock> mountCountSemaphore) : base(in baseFileSystem)
    {
        _entryCountSemaphore = SharedRef<IEntryOpenCountSemaphoreManager>.CreateCopy(in entryCountSemaphore);
        _mountCountSemaphore = new UniqueRef<IUniqueLock>(ref mountCountSemaphore);
    }

    public static SharedRef<IFileSystem> CreateShared(
        ref readonly SharedRef<IFileSystem> baseFileSystem,
        ref readonly SharedRef<IEntryOpenCountSemaphoreManager> entryCountSemaphore,
        ref UniqueRef<IUniqueLock> mountCountSemaphore)
    {
        var filesystem = new OpenCountFileSystem(in baseFileSystem, in entryCountSemaphore, ref mountCountSemaphore);

        return new SharedRef<IFileSystem>(filesystem);
    }

    public static SharedRef<IFileSystem> CreateShared(
        ref readonly SharedRef<IFileSystem> baseFileSystem,
        ref readonly SharedRef<IEntryOpenCountSemaphoreManager> entryCountSemaphore)
    {
        var filesystem = new OpenCountFileSystem(in baseFileSystem, in entryCountSemaphore);

        return new SharedRef<IFileSystem>(filesystem);
    }

    // ReSharper disable once RedundantOverriddenMember
    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, ref readonly Path path, OpenMode mode)
    {
        // Todo: Implement
        return base.DoOpenFile(ref outFile, in path, mode);
    }

    // ReSharper disable once RedundantOverriddenMember
    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, ref readonly Path path,
        OpenDirectoryMode mode)
    {
        // Todo: Implement
        return base.DoOpenDirectory(ref outDirectory, in path, mode);
    }

    public override void Dispose()
    {
        _entryCountSemaphore.Destroy();
        _mountCountSemaphore.Destroy();
        base.Dispose();
    }
}