using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Impl;

public class AsynchronousAccessFileSystem : ForwardingFileSystem
{
    public AsynchronousAccessFileSystem(ref readonly SharedRef<IFileSystem> baseFileSystem) : base(
        in baseFileSystem)
    { }

    // ReSharper disable once RedundantOverriddenMember
    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, ref readonly Path path, OpenMode mode)
    {
        // Todo: Implement
        return base.DoOpenFile(ref outFile, in path, mode);
    }
}