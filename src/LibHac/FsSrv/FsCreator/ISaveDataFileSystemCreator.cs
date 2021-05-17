using System;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.FsCreator
{
    public interface ISaveDataFileSystemCreator
    {
        Result CreateFile(out IFile file, IFileSystem sourceFileSystem, ulong saveDataId, OpenMode openMode);

        Result Create(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            out ReferenceCountedDisposable<ISaveDataExtraDataAccessor> extraDataAccessor,
            ISaveDataFileSystemCacheManager cacheManager, ref ReferenceCountedDisposable<IFileSystem> baseFileSystem,
            SaveDataSpaceId spaceId, ulong saveDataId, bool allowDirectorySaveData, bool useDeviceUniqueMac,
            bool isJournalingSupported, bool isMultiCommitSupported, bool openReadOnly, bool openShared,
            ISaveDataCommitTimeStampGetter timeStampGetter);

        Result CreateExtraDataAccessor(out ReferenceCountedDisposable<ISaveDataExtraDataAccessor> extraDataAccessor,
            ReferenceCountedDisposable<IFileSystem> sourceFileSystem);

        void SetSdCardEncryptionSeed(ReadOnlySpan<byte> seed);
    }
}