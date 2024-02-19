using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.Impl;
using LibHac.FsSystem;
using LibHac.FsSystem.Save;
using LibHac.Util;
using OpenType = LibHac.FsSrv.SaveDataOpenTypeSetFileStorage.OpenType;

namespace LibHac.FsSrv.FsCreator;

public class SaveDataFileSystemCreator : ISaveDataFileSystemCreator
{
    // Option to disable some restrictions enforced in actual FS.
    private static readonly bool EnforceSaveTypeRestrictions = false;

    // ReSharper disable once NotAccessedField.Local
    private IBufferManager _bufferManager;
    private RandomDataGenerator _randomGenerator;

    // LibHac Additions
    // ReSharper disable once NotAccessedField.Local
    private KeySet _keySet;
    private FileSystemServer _fsServer;

    public SaveDataFileSystemCreator(FileSystemServer fsServer, KeySet keySet, IBufferManager bufferManager,
        RandomDataGenerator randomGenerator)
    {
        _bufferManager = bufferManager;
        _randomGenerator = randomGenerator;
        _fsServer = fsServer;
        _keySet = keySet;
    }

    public Result Format(in ValueSubStorage saveImageStorage, long blockSize, int countExpandMax, uint blockCount,
        uint journalBlockCount, IBufferManager bufferManager, bool isDeviceUniqueMac, in HashSalt hashSalt,
        RandomDataGenerator encryptionKeyGenerator, bool isReconstructible, uint version)
    {
        throw new NotImplementedException();
    }

    public Result FormatAsIntegritySaveData(in ValueSubStorage saveImageStorage, long blockSize, uint blockCount,
        IBufferManager bufferManager, bool isDeviceUniqueMac, RandomDataGenerator encryptionKeyGenerator,
        bool isReconstructible, uint version)
    {
        throw new NotImplementedException();
    }

    public Result ExtractSaveDataParameters(out JournalIntegritySaveDataParameters outParams, IStorage saveFileStorage,
        bool isDeviceUniqueMac, bool isReconstructible)
    {
        throw new NotImplementedException();
    }

    public Result ExtendSaveData(SaveDataExtender extender, in ValueSubStorage baseStorage,
        in ValueSubStorage logStorage, bool isDeviceUniqueMac, bool isReconstructible)
    {
        throw new NotImplementedException();
    }

    public void SetMacGenerationSeed(ReadOnlySpan<byte> seed)
    {
        throw new NotImplementedException();
    }

    public Result CreateRaw(ref SharedRef<IFile> outFile, in SharedRef<IFileSystem> fileSystem, ulong saveDataId, OpenMode openMode)
    {
        throw new NotImplementedException();
    }

    public Result Create(ref SharedRef<ISaveDataFileSystem> outFileSystem, ref SharedRef<IFileSystem> baseFileSystem,
        SaveDataSpaceId spaceId, ulong saveDataId, bool allowDirectorySaveData, bool isDeviceUniqueMac,
        bool isJournalingSupported, bool isMultiCommitSupported, bool openReadOnly, bool openShared,
        ISaveDataCommitTimeStampGetter timeStampGetter, bool isReconstructible)
    {
        Unsafe.SkipInit(out Array18<byte> saveImageNameBuffer);

        using scoped var saveImageName = new Path();
        Result res = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer, saveDataId);
        if (res.IsFailure()) return res.Miss();

        res = baseFileSystem.Get.GetEntryType(out DirectoryEntryType type, in saveImageName);

        if (res.IsFailure())
        {
            return ResultFs.PathNotFound.Includes(res) ? ResultFs.TargetNotFound.LogConverted(res) : res.Miss();
        }

        using var saveDataFs = new SharedRef<ISaveDataFileSystem>();

        if (type == DirectoryEntryType.Directory)
        {
            if (EnforceSaveTypeRestrictions)
            {
                if (!allowDirectorySaveData)
                    return ResultFs.InvalidSaveDataEntryType.Log();
            }

            // Get a file system over the save directory
            using var baseFs = new UniqueRef<SubdirectoryFileSystem>(new SubdirectoryFileSystem(ref baseFileSystem));

            if (!baseFs.HasValue)
                return ResultFs.AllocationMemoryFailedInSaveDataFileSystemCreatorA.Log();

            res = baseFs.Get.Initialize(in saveImageName);
            if (res.IsFailure()) return res.Miss();

            // Create and initialize the directory save data FS
            using UniqueRef<IFileSystem> tempFs = UniqueRef<IFileSystem>.Create(ref baseFs.Ref);
            using var saveDirFs = new SharedRef<DirectorySaveDataFileSystem>(
                new DirectorySaveDataFileSystem(ref tempFs.Ref, _fsServer.Hos.Fs));

            if (!saveDirFs.HasValue)
                return ResultFs.AllocationMemoryFailedInSaveDataFileSystemCreatorB.Log();

            res = saveDirFs.Get.Initialize(isJournalingSupported, isMultiCommitSupported, !openReadOnly,
                timeStampGetter, _randomGenerator);
            if (res.IsFailure()) return res.Miss();

            saveDataFs.SetByMove(ref saveDirFs.Ref);
        }
        else
        {
            using var fileStorage = new SharedRef<IStorage>();

            Optional<OpenType> openType =
                openShared ? new Optional<OpenType>(OpenType.Normal) : new Optional<OpenType>();

            res = _fsServer.OpenSaveDataStorage(ref fileStorage.Ref, ref baseFileSystem, spaceId, saveDataId,
                OpenMode.ReadWrite, openType);
            if (res.IsFailure()) return res.Miss();

            throw new NotImplementedException();
        }

        // Wrap the save FS in a result convert FS and set it as the output FS
        using var resultConvertFs = new SharedRef<SaveDataResultConvertFileSystem>(
            new SaveDataResultConvertFileSystem(ref saveDataFs.Ref, isReconstructible));

        outFileSystem.SetByMove(ref resultConvertFs.Ref);

        return Result.Success;
    }

    public Result CreateExtraDataAccessor(ref SharedRef<ISaveDataExtraDataAccessor> outExtraDataAccessor,
        in SharedRef<IStorage> baseStorage, bool isDeviceUniqueMac, bool isIntegritySaveData, bool isReconstructible)
    {
        throw new NotImplementedException();
    }

    public Result CreateInternalStorage(ref SharedRef<IFileSystem> outFileSystem,
        in SharedRef<IFileSystem> baseFileSystem, SaveDataSpaceId spaceId, ulong saveDataId, bool isDeviceUniqueMac,
        bool useUniqueKey1, ISaveDataCommitTimeStampGetter timeStampGetter, bool isReconstructible)
    {
        throw new NotImplementedException();
    }

    public Result RecoverMasterHeader(in SharedRef<IFileSystem> baseFileSystem, ulong saveDataId,
        IBufferManager bufferManager, bool isDeviceUniqueMac, bool isReconstructible)
    {
        throw new NotImplementedException();
    }

    public Result UpdateMac(in SharedRef<IFileSystem> baseFileSystem, ulong saveDataId, bool isDeviceUniqueMac,
        bool isReconstructible)
    {
        throw new NotImplementedException();
    }

    public Result IsProvisionallyCommittedSaveData(out bool outIsProvisionallyCommitted,
        in SharedRef<IFileSystem> baseFileSystem, in SaveDataInfo info, bool isDeviceUniqueMac,
        ISaveDataCommitTimeStampGetter timeStampGetter, bool isReconstructible)
    {
        throw new NotImplementedException();
    }

    public IMacGenerator GetMacGenerator(bool isDeviceUniqueMac, bool isTemporaryTransferSave)
    {
        throw new NotImplementedException();
    }
}