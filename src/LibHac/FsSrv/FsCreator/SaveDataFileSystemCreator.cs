using System;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.FsSystem.Save;

namespace LibHac.FsSrv.FsCreator
{
    public class SaveDataFileSystemCreator : ISaveDataFileSystemCreator
    {
        private IBufferManager _bufferManager;
        private RandomDataGenerator _randomGenerator;

        private KeySet _keySet;

        public SaveDataFileSystemCreator(KeySet keySet, IBufferManager bufferManager,
            RandomDataGenerator randomGenerator)
        {
            _bufferManager = bufferManager;
            _randomGenerator = randomGenerator;
            _keySet = keySet;
        }

        public Result CreateFile(out IFile file, IFileSystem sourceFileSystem, ulong saveDataId, OpenMode openMode)
        {
            throw new NotImplementedException();
        }

        public Result Create(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            out ReferenceCountedDisposable<ISaveDataExtraDataAccessor> extraDataAccessor,
            ReferenceCountedDisposable<IFileSystem> sourceFileSystem, ulong saveDataId, bool allowDirectorySaveData,
            bool useDeviceUniqueMac, SaveDataType type, ISaveDataCommitTimeStampGetter timeStampGetter)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem, out extraDataAccessor);

            var saveDataPath = $"/{saveDataId:x16}".ToU8String();

            Result rc = sourceFileSystem.Target.GetEntryType(out DirectoryEntryType entryType, saveDataPath);
            if (rc.IsFailure())
            {
                return ResultFs.PathNotFound.Includes(rc) ? ResultFs.TargetNotFound.LogConverted(rc) : rc;
            }

            switch (entryType)
            {
                case DirectoryEntryType.Directory:
                    if (!allowDirectorySaveData) return ResultFs.InvalidSaveDataEntryType.Log();

                    var subDirFs = new SubdirectoryFileSystem(ref sourceFileSystem);

                    rc = subDirFs.Initialize(saveDataPath);
                    if (rc.IsFailure())
                    {
                        subDirFs.Dispose();
                        return rc;
                    }

                    bool isPersistentSaveData = type != SaveDataType.Temporary;
                    bool isUserSaveData = type == SaveDataType.Account || type == SaveDataType.Device;

                    rc = DirectorySaveDataFileSystem.CreateNew(out DirectorySaveDataFileSystem saveFs, subDirFs,
                        timeStampGetter, _randomGenerator, isPersistentSaveData, isUserSaveData, true, null);
                    if (rc.IsFailure()) return rc;

                    ReferenceCountedDisposable<DirectorySaveDataFileSystem> sharedSaveFs = null;
                    try
                    {
                        sharedSaveFs = new ReferenceCountedDisposable<DirectorySaveDataFileSystem>(saveFs);
                        fileSystem = sharedSaveFs.AddReference<IFileSystem>();
                        extraDataAccessor = sharedSaveFs.AddReference<ISaveDataExtraDataAccessor>();

                        return Result.Success;
                    }
                    finally
                    {
                        sharedSaveFs?.Dispose();
                    }

                case DirectoryEntryType.File:
                    rc = sourceFileSystem.Target.OpenFile(out IFile saveDataFile, saveDataPath, OpenMode.ReadWrite);
                    if (rc.IsFailure()) return rc;

                    var saveDataStorage = new DisposingFileStorage(saveDataFile);
                    fileSystem = new ReferenceCountedDisposable<IFileSystem>(new SaveDataFileSystem(_keySet,
                        saveDataStorage, IntegrityCheckLevel.ErrorOnInvalid, false));

                    // Todo: ISaveDataExtraDataAccessor

                    return Result.Success;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void SetSdCardEncryptionSeed(ReadOnlySpan<byte> seed)
        {
            throw new NotImplementedException();
        }
    }
}