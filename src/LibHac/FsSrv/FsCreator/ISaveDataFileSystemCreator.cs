using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.FsCreator;

public interface ISaveDataFileSystemCreator
{
    Result CreateFile(out IFile file, IFileSystem sourceFileSystem, ulong saveDataId, OpenMode openMode);

    Result Create(ref SharedRef<IFileSystem> outFileSystem,
        ref SharedRef<ISaveDataExtraDataAccessor> outExtraDataAccessor,
        ISaveDataFileSystemCacheManager cacheManager, ref SharedRef<IFileSystem> baseFileSystem,
        SaveDataSpaceId spaceId, ulong saveDataId, bool allowDirectorySaveData, bool useDeviceUniqueMac,
        bool isJournalingSupported, bool isMultiCommitSupported, bool openReadOnly, bool openShared,
        ISaveDataCommitTimeStampGetter timeStampGetter);

    Result CreateExtraDataAccessor(ref SharedRef<ISaveDataExtraDataAccessor> outExtraDataAccessor,
        ref SharedRef<IFileSystem> baseFileSystem);

    void SetSdCardEncryptionSeed(ReadOnlySpan<byte> seed);
}
