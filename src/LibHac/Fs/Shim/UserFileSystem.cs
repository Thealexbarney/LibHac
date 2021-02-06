using System;
using LibHac.Common;
using LibHac.Fs.Accessors;
using LibHac.FsSrv.Sf;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim
{
    public static class UserFileSystem
    {
        public static Result Commit(this FileSystemClient fs, ReadOnlySpan<U8String> mountNames)
        {
            // Todo: Add access log

            if (mountNames.Length > 10)
                return ResultFs.InvalidCommitNameCount.Log();

            if (mountNames.Length == 0)
                return Result.Success;

            ReferenceCountedDisposable<IMultiCommitManager> commitManager = null;
            ReferenceCountedDisposable<IFileSystemSf> fileSystem = null;
            try
            {
                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

                Result rc = fsProxy.Target.OpenMultiCommitManager(out commitManager);
                if (rc.IsFailure()) return rc;

                for (int i = 0; i < mountNames.Length; i++)
                {
                    rc = fs.MountTable.Find(mountNames[i].ToString(), out FileSystemAccessor accessor);
                    if (rc.IsFailure()) return rc;

                    fileSystem = accessor.GetMultiCommitTarget();
                    if (fileSystem is null)
                        return ResultFs.UnsupportedCommitTarget.Log();

                    rc = commitManager.Target.Add(fileSystem);
                    if (rc.IsFailure()) return rc;
                }

                rc = commitManager.Target.Commit();
                if (rc.IsFailure()) return rc;

                return Result.Success;
            }
            finally
            {
                commitManager?.Dispose();
                fileSystem?.Dispose();
            }
        }
    }
}
