using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.FsSystem.Save;

namespace LibHac.FsService.Creators
{
    public class SaveDataFileSystemCreator : ISaveDataFileSystemCreator
    {
        private Keyset Keyset { get; }

        public SaveDataFileSystemCreator(Keyset keyset)
        {
            Keyset = keyset;
        }

        public Result CreateFile(out IFile file, IFileSystem sourceFileSystem, ulong saveDataId, OpenMode openMode)
        {
            throw new NotImplementedException();
        }

        public Result Create(out IFileSystem fileSystem, out ISaveDataExtraDataAccessor extraDataAccessor,
            IFileSystem sourceFileSystem, ulong saveDataId, bool allowDirectorySaveData, bool useDeviceUniqueMac,
            SaveDataType type, ITimeStampGenerator timeStampGenerator)
        {
            fileSystem = default;
            extraDataAccessor = default;

            string saveDataPath = $"/{saveDataId:x16}";

            Result rc = sourceFileSystem.GetEntryType(out DirectoryEntryType entryType, saveDataPath);
            if (rc.IsFailure())
            {
                return ResultFs.PathNotFound.Includes(rc) ? ResultFs.TargetNotFound.LogConverted(rc) : rc;
            }

            switch (entryType)
            {
                case DirectoryEntryType.Directory:
                    // Actual FS does this check
                    // if (!allowDirectorySaveData) return ResultFs.InvalidSaveDataEntryType.Log();

                    var subDirFs = new SubdirectoryFileSystem(sourceFileSystem, saveDataPath);
                    bool isPersistentSaveData = type != SaveDataType.Temporary;
                    bool isUserSaveData = type == SaveDataType.Account || type == SaveDataType.Device;

                    rc = DirectorySaveDataFileSystem.CreateNew(out DirectorySaveDataFileSystem saveFs, subDirFs, isPersistentSaveData, isUserSaveData);
                    if (rc.IsFailure()) return rc;

                    fileSystem = saveFs;

                    // Todo: Dummy ISaveDataExtraDataAccessor

                    return Result.Success;

                case DirectoryEntryType.File:
                    rc = sourceFileSystem.OpenFile(out IFile saveDataFile, saveDataPath, OpenMode.ReadWrite);
                    if (rc.IsFailure()) return rc;

                    var saveDataStorage = new DisposingFileStorage(saveDataFile);
                    fileSystem = new SaveDataFileSystem(Keyset, saveDataStorage, IntegrityCheckLevel.ErrorOnInvalid, false);

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
