﻿using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Impl
{
    public class AsynchronousAccessFileSystem : ForwardingFileSystem
    {
        protected AsynchronousAccessFileSystem(ref ReferenceCountedDisposable<IFileSystem> baseFileSystem) : base(
            ref baseFileSystem)
        { }

        public static ReferenceCountedDisposable<IFileSystem> CreateShared(
            ref ReferenceCountedDisposable<IFileSystem> fileSystem)
        {
            return new ReferenceCountedDisposable<IFileSystem>(new AsynchronousAccessFileSystem(ref fileSystem));
        }

        // ReSharper disable once RedundantOverriddenMember
        protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
        {
            // Todo: Implement
            return base.DoOpenFile(ref outFile, path, mode);
        }
    }
}
