using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.Impl;
using LibHac.FsSystem;
using LibHac.FsSystem.Save;

namespace LibHac.FsSrv.FsCreator;

public interface ISaveDataFileSystemCreator : IDisposable
{
    Result CreateRaw(ref SharedRef<IFile> outFile, ref readonly SharedRef<IFileSystem> fileSystem, ulong saveDataId, OpenMode openMode);

    Result Create(ref SharedRef<ISaveDataFileSystem> outFileSystem, ref SharedRef<IFileSystem> baseFileSystem,
        SaveDataSpaceId spaceId, ulong saveDataId, bool allowDirectorySaveData, bool isDeviceUniqueMac,
        bool isJournalingSupported, bool isMultiCommitSupported, bool openReadOnly, bool openShared,
        ISaveDataCommitTimeStampGetter timeStampGetter, bool isReconstructible);

    Result CreateExtraDataAccessor(ref SharedRef<ISaveDataExtraDataAccessor> outExtraDataAccessor,
        ref readonly SharedRef<IStorage> baseStorage, bool isDeviceUniqueMac, bool isIntegritySaveData,
        bool isReconstructible);

    Result CreateInternalStorage(ref SharedRef<IFileSystem> outFileSystem,
        ref readonly SharedRef<IFileSystem> baseFileSystem, SaveDataSpaceId spaceId, ulong saveDataId,
        bool isDeviceUniqueMac, bool useUniqueKey1, ISaveDataCommitTimeStampGetter timeStampGetter,
        bool isReconstructible);

    Result RecoverMasterHeader(ref readonly SharedRef<IFileSystem> baseFileSystem, ulong saveDataId,
        IBufferManager bufferManager, bool isDeviceUniqueMac, bool isReconstructible);

    Result UpdateMac(ref readonly SharedRef<IFileSystem> baseFileSystem, ulong saveDataId, bool isDeviceUniqueMac,
        bool isReconstructible);

    Result Format(in ValueSubStorage saveImageStorage, long blockSize, int countExpandMax, uint blockCount,
        uint journalBlockCount, IBufferManager bufferManager, bool isDeviceUniqueMac, in HashSalt hashSalt,
        RandomDataGenerator encryptionKeyGenerator, bool isReconstructible, uint version);

    Result FormatAsIntegritySaveData(in ValueSubStorage saveImageStorage, long blockSize, uint blockCount,
        IBufferManager bufferManager, bool isDeviceUniqueMac, RandomDataGenerator encryptionKeyGenerator,
        bool isReconstructible, uint version);

    Result ExtractSaveDataParameters(out JournalIntegritySaveDataParameters outParams, IStorage saveFileStorage,
        bool isDeviceUniqueMac, bool isReconstructible);

    Result ExtendSaveData(SaveDataExtender extender, in ValueSubStorage baseStorage, in ValueSubStorage logStorage,
        bool isDeviceUniqueMac, bool isReconstructible);

    void SetMacGenerationSeed(ReadOnlySpan<byte> seed);

    Result IsProvisionallyCommittedSaveData(out bool outIsProvisionallyCommitted,
        ref readonly SharedRef<IFileSystem> baseFileSystem, in SaveDataInfo info, bool isDeviceUniqueMac,
        ISaveDataCommitTimeStampGetter timeStampGetter, bool isReconstructible);

    IMacGenerator GetMacGenerator(bool isDeviceUniqueMac, bool isTemporaryTransferSave);
}