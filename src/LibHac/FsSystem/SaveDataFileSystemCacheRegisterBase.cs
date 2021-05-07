using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem.Save;

namespace LibHac.FsSystem
{
    /// <summary>
    /// Wraps a save data <see cref="IFileSystem"/>.
    /// Upon disposal the base file system is returned to the provided <see cref="ISaveDataFileSystemCacheManager"/>.
    /// </summary>
    /// <typeparam name="T">The type of the base file system. Must be one of <see cref="SaveDataFileSystem"/>,
    /// <see cref="ApplicationTemporaryFileSystem"/> or <see cref="DirectorySaveDataFileSystem"/>.</typeparam>
    public class SaveDataFileSystemCacheRegisterBase<T> : IFileSystem where T : IFileSystem
    {
        private ReferenceCountedDisposable<T> _baseFileSystem;
        private ISaveDataFileSystemCacheManager _cacheManager;

        public SaveDataFileSystemCacheRegisterBase(ref ReferenceCountedDisposable<T> baseFileSystem,
            ISaveDataFileSystemCacheManager cacheManager)
        {
            Assert.SdkRequires(typeof(T) == typeof(SaveDataFileSystem) ||
                               typeof(T) == typeof(ApplicationTemporaryFileSystem) ||
                               typeof(T) == typeof(DirectorySaveDataFileSystem));

            _baseFileSystem = Shared.Move(ref baseFileSystem);
            _cacheManager = cacheManager;
        }


        public static ReferenceCountedDisposable<IFileSystem> CreateShared(
            ReferenceCountedDisposable<IFileSystem> baseFileSystem, ISaveDataFileSystemCacheManager cacheManager)
        {
            IFileSystem baseFsTarget = baseFileSystem.Target;

            switch (baseFsTarget)
            {
                case SaveDataFileSystem:
                {
                    ReferenceCountedDisposable<SaveDataFileSystem> castedFs =
                        baseFileSystem.AddReference<SaveDataFileSystem>();

                    return new ReferenceCountedDisposable<IFileSystem>(
                        new SaveDataFileSystemCacheRegisterBase<SaveDataFileSystem>(ref castedFs, cacheManager));
                }
                case ApplicationTemporaryFileSystem:
                {
                    ReferenceCountedDisposable<ApplicationTemporaryFileSystem> castedFs =
                        baseFileSystem.AddReference<ApplicationTemporaryFileSystem>();

                    return new ReferenceCountedDisposable<IFileSystem>(
                        new SaveDataFileSystemCacheRegisterBase<ApplicationTemporaryFileSystem>(ref castedFs,
                            cacheManager));
                }
                case DirectorySaveDataFileSystem:
                {
                    ReferenceCountedDisposable<DirectorySaveDataFileSystem> castedFs =
                        baseFileSystem.AddReference<DirectorySaveDataFileSystem>();

                    return new ReferenceCountedDisposable<IFileSystem>(
                        new SaveDataFileSystemCacheRegisterBase<DirectorySaveDataFileSystem>(ref castedFs,
                            cacheManager));
                }
                default:
                    Assert.SdkAssert(false, "Invalid save data file system type.");
                    return null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (_baseFileSystem is null)
                return;

            if (disposing)
            {
                if (typeof(T) == typeof(SaveDataFileSystem))
                {
                    _cacheManager.Register(
                        (ReferenceCountedDisposable<SaveDataFileSystem>)(object)_baseFileSystem);
                }
                else if (typeof(T) == typeof(ApplicationTemporaryFileSystem))
                {
                    _cacheManager.Register(
                        (ReferenceCountedDisposable<ApplicationTemporaryFileSystem>)(object)_baseFileSystem);
                }
                else if (typeof(T) == typeof(DirectorySaveDataFileSystem))
                {
                    _cacheManager.Register(
                        (ReferenceCountedDisposable<DirectorySaveDataFileSystem>)(object)_baseFileSystem);
                }

                _baseFileSystem.Dispose();
                _baseFileSystem = null;
            }

            base.Dispose(disposing);
        }

        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            return _baseFileSystem.Target.OpenFile(out file, path, mode);
        }

        protected override Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            return _baseFileSystem.Target.OpenDirectory(out directory, path, mode);
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            return _baseFileSystem.Target.GetEntryType(out entryType, path);
        }

        protected override Result DoCreateFile(U8Span path, long size, CreateFileOptions option)
        {
            return _baseFileSystem.Target.CreateFile(path, size, option);
        }

        protected override Result DoDeleteFile(U8Span path)
        {
            return _baseFileSystem.Target.DeleteFile(path);
        }

        protected override Result DoCreateDirectory(U8Span path)
        {
            return _baseFileSystem.Target.CreateDirectory(path);
        }

        protected override Result DoDeleteDirectory(U8Span path)
        {
            return _baseFileSystem.Target.DeleteDirectory(path);
        }

        protected override Result DoDeleteDirectoryRecursively(U8Span path)
        {
            return _baseFileSystem.Target.DeleteDirectoryRecursively(path);
        }

        protected override Result DoCleanDirectoryRecursively(U8Span path)
        {
            return _baseFileSystem.Target.CleanDirectoryRecursively(path);
        }

        protected override Result DoRenameFile(U8Span oldPath, U8Span newPath)
        {
            return _baseFileSystem.Target.RenameFile(oldPath, newPath);
        }

        protected override Result DoRenameDirectory(U8Span oldPath, U8Span newPath)
        {
            return _baseFileSystem.Target.RenameDirectory(oldPath, newPath);
        }

        protected override Result DoCommit()
        {
            return _baseFileSystem.Target.Commit();
        }

        protected override Result DoCommitProvisionally(long counter)
        {
            return _baseFileSystem.Target.CommitProvisionally(counter);
        }

        protected override Result DoRollback()
        {
            return _baseFileSystem.Target.Rollback();
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            return _baseFileSystem.Target.GetFreeSpaceSize(out freeSpace, path);
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            return _baseFileSystem.Target.GetTotalSpaceSize(out totalSpace, path);
        }
    }
}