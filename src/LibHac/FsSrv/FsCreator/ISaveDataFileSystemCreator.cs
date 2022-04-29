using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.FsCreator;

public interface ISaveDataFileSystemCreator
{
    Result CreateFile(out IFile file, IFileSystem sourceFileSystem, ulong saveDataId, OpenMode openMode);

    Result Create(ref SharedRef<ISaveDataFileSystem> outFileSystem, ref SharedRef<IFileSystem> baseFileSystem,
        SaveDataSpaceId spaceId, ulong saveDataId, bool allowDirectorySaveData, bool isDeviceUniqueMac,
        bool isJournalingSupported, bool isMultiCommitSupported, bool openReadOnly, bool openShared,
        ISaveDataCommitTimeStampGetter timeStampGetter, bool isReconstructible);

    Result CreateExtraDataAccessor(ref SharedRef<ISaveDataExtraDataAccessor> outExtraDataAccessor,
        ref SharedRef<IFileSystem> baseFileSystem);

    Result IsDataEncrypted(out bool isEncrypted, ref SharedRef<IFileSystem> baseFileSystem, ulong saveDataId,
        IBufferManager bufferManager, bool isDeviceUniqueMac, bool isReconstructible);

    void SetMacGenerationSeed(ReadOnlySpan<byte> seed);
}