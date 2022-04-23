using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Common.Keys;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.Save;
using LibHac.Util;

using OpenType = LibHac.FsSrv.SaveDataOpenTypeSetFileStorage.OpenType;

namespace LibHac.FsSrv.FsCreator;

public class SaveDataFileSystemCreator : ISaveDataFileSystemCreator
{
    private IBufferManager _bufferManager;
    private RandomDataGenerator _randomGenerator;

    // LibHac Additions
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

    public Result Create(ref SharedRef<IFileSystem> outFileSystem,
        ref SharedRef<ISaveDataExtraDataAccessor> outExtraDataAccessor,
        ISaveDataFileSystemCacheManager cacheManager, ref SharedRef<IFileSystem> baseFileSystem,
        SaveDataSpaceId spaceId, ulong saveDataId, bool allowDirectorySaveData, bool useDeviceUniqueMac,
        bool isJournalingSupported, bool isMultiCommitSupported, bool openReadOnly, bool openShared,
        ISaveDataCommitTimeStampGetter timeStampGetter)
    {
        Unsafe.SkipInit(out Array18<byte> saveImageNameBuffer);

        Assert.SdkRequiresNotNull(cacheManager);

        using var saveImageName = new Path();
        Result rc = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer.Items, saveDataId);
        if (rc.IsFailure()) return rc;

        rc = baseFileSystem.Get.GetEntryType(out DirectoryEntryType type, in saveImageName);

        if (rc.IsFailure())
        {
            return ResultFs.PathNotFound.Includes(rc) ? ResultFs.TargetNotFound.LogConverted(rc) : rc;
        }

        if (type == DirectoryEntryType.Directory)
        {
            if (!allowDirectorySaveData)
                return ResultFs.InvalidSaveDataEntryType.Log();

            using var baseFs =
                new UniqueRef<SubdirectoryFileSystem>(new SubdirectoryFileSystem(ref baseFileSystem));

            if (!baseFs.HasValue)
                return ResultFs.AllocationMemoryFailedInSaveDataFileSystemCreatorA.Log();

            rc = baseFs.Get.Initialize(in saveImageName);
            if (rc.IsFailure()) return rc;

            using UniqueRef<IFileSystem> tempFs = UniqueRef<IFileSystem>.Create(ref baseFs.Ref());
            using var saveDirFs = new SharedRef<DirectorySaveDataFileSystem>(
                new DirectorySaveDataFileSystem(ref tempFs.Ref(), _fsServer.Hos.Fs));

            rc = saveDirFs.Get.Initialize(isJournalingSupported, isMultiCommitSupported, !openReadOnly,
                timeStampGetter, _randomGenerator);
            if (rc.IsFailure()) return rc;

            outFileSystem.SetByCopy(in saveDirFs);
            outExtraDataAccessor.SetByCopy(in saveDirFs);

            return Result.Success;
        }
        else
        {
            using var fileStorage = new SharedRef<IStorage>();

            Optional<OpenType> openType =
                openShared ? new Optional<OpenType>(OpenType.Normal) : new Optional<OpenType>();

            rc = _fsServer.OpenSaveDataStorage(ref fileStorage.Ref(), ref baseFileSystem, spaceId, saveDataId,
                OpenMode.ReadWrite, openType);
            if (rc.IsFailure()) return rc;

            if (!isJournalingSupported)
            {
                throw new NotImplementedException();
            }

            using var saveFs = new SharedRef<SaveDataFileSystem>(new SaveDataFileSystem(_keySet, fileStorage.Get,
                IntegrityCheckLevel.ErrorOnInvalid, false));

            // Todo: ISaveDataExtraDataAccessor

            return Result.Success;
        }
    }

    public Result CreateExtraDataAccessor(ref SharedRef<ISaveDataExtraDataAccessor> outExtraDataAccessor,
        ref SharedRef<IFileSystem> baseFileSystem)
    {
        throw new NotImplementedException();
    }

    public void SetSdCardEncryptionSeed(ReadOnlySpan<byte> seed)
    {
        throw new NotImplementedException();
    }
}