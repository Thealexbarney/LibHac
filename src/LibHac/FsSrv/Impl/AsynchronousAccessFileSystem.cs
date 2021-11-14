using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Impl;

public class AsynchronousAccessFileSystem : ForwardingFileSystem
{
    public AsynchronousAccessFileSystem(ref SharedRef<IFileSystem> baseFileSystem) : base(
        ref baseFileSystem)
    { }

    // ReSharper disable once RedundantOverriddenMember
    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        // Todo: Implement
        return base.DoOpenFile(ref outFile, path, mode);
    }
}
