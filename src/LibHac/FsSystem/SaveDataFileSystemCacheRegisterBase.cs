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

        public override void Dispose()
        {
            if (_baseFileSystem is not null)
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

            base.Dispose();
        }

        protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
        {
            return _baseFileSystem.Target.OpenFile(ref outFile, path, mode);
        }

        protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
            OpenDirectoryMode mode)
        {
            return _baseFileSystem.Target.OpenDirectory(ref outDirectory, path, mode);
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
        {
            return _baseFileSystem.Target.GetEntryType(out entryType, path);
        }

        protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
        {
            return _baseFileSystem.Target.CreateFile(path, size, option);
        }

        protected override Result DoDeleteFile(in Path path)
        {
            return _baseFileSystem.Target.DeleteFile(path);
        }

        protected override Result DoCreateDirectory(in Path path)
        {
            return _baseFileSystem.Target.CreateDirectory(path);
        }

        protected override Result DoDeleteDirectory(in Path path)
        {
            return _baseFileSystem.Target.DeleteDirectory(path);
        }

        protected override Result DoDeleteDirectoryRecursively(in Path path)
        {
            return _baseFileSystem.Target.DeleteDirectoryRecursively(path);
        }

        protected override Result DoCleanDirectoryRecursively(in Path path)
        {
            return _baseFileSystem.Target.CleanDirectoryRecursively(path);
        }

        protected override Result DoRenameFile(in Path currentPath, in Path newPath)
        {
            return _baseFileSystem.Target.RenameFile(currentPath, newPath);
        }

        protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
        {
            return _baseFileSystem.Target.RenameDirectory(currentPath, newPath);
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

        protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
        {
            return _baseFileSystem.Target.GetFreeSpaceSize(out freeSpace, path);
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
        {
            return _baseFileSystem.Target.GetTotalSpaceSize(out totalSpace, path);
        }
    }
}