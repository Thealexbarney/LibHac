using System;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem.Save;

namespace LibHac.FsSystem
{
    public interface ISaveDataFileSystemCacheManager : IDisposable
    {
        bool GetCache(out ReferenceCountedDisposable<IFileSystem> fileSystem, SaveDataSpaceId spaceId, ulong saveDataId);
        void Register(ReferenceCountedDisposable<ApplicationTemporaryFileSystem> fileSystem);
        void Register(ReferenceCountedDisposable<SaveDataFileSystem> fileSystem);
        void Unregister(SaveDataSpaceId spaceId, ulong saveDataId);
        
        // LibHac addition
        void Register(ReferenceCountedDisposable<DirectorySaveDataFileSystem> fileSystem);
    }
}