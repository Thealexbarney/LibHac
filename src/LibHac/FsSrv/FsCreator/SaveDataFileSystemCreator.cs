using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
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

    public Result CreateFile(out IFile file, IFileSystem sourceFileSystem, ulong saveDataId, OpenMode openMode)
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
        Result res = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer.Items, saveDataId);
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
        ref SharedRef<IFileSystem> baseFileSystem)
    {
        throw new NotImplementedException();
    }

    public Result IsDataEncrypted(out bool isEncrypted, ref SharedRef<IFileSystem> baseFileSystem, ulong saveDataId,
        IBufferManager bufferManager, bool isDeviceUniqueMac, bool isReconstructible)
    {
        throw new NotImplementedException();
    }

    public void SetMacGenerationSeed(ReadOnlySpan<byte> seed)
    {
        throw new NotImplementedException();
    }
}