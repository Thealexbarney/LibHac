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
        private KeySet KeySet { get; }

        public SaveDataFileSystemCreator(KeySet keySet)
        {
            KeySet = keySet;
        }

        public Result CreateFile(out IFile file, IFileSystem sourceFileSystem, ulong saveDataId, OpenMode openMode)
        {
            throw new NotImplementedException();
        }

        public Result Create(out IFileSystem fileSystem,
            out ReferenceCountedDisposable<ISaveDataExtraDataAccessor> extraDataAccessor, IFileSystem sourceFileSystem,
            ulong saveDataId, bool allowDirectorySaveData, bool useDeviceUniqueMac, SaveDataType type,
            ITimeStampGenerator timeStampGenerator)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem, out extraDataAccessor);

            var saveDataPath = $"/{saveDataId:x16}".ToU8String();

            Result rc = sourceFileSystem.GetEntryType(out DirectoryEntryType entryType, saveDataPath);
            if (rc.IsFailure())
            {
                return ResultFs.PathNotFound.Includes(rc) ? ResultFs.TargetNotFound.LogConverted(rc) : rc;
            }

            switch (entryType)
            {
                case DirectoryEntryType.Directory:
                    if (!allowDirectorySaveData) return ResultFs.InvalidSaveDataEntryType.Log();

                    rc = SubdirectoryFileSystem.CreateNew(out SubdirectoryFileSystem subDirFs, sourceFileSystem,
                        saveDataPath);
                    if (rc.IsFailure()) return rc;

                    bool isPersistentSaveData = type != SaveDataType.Temporary;
                    bool isUserSaveData = type == SaveDataType.Account || type == SaveDataType.Device;

                    rc = DirectorySaveDataFileSystem.CreateNew(out DirectorySaveDataFileSystem saveFs, subDirFs,
                        isPersistentSaveData, isUserSaveData, true);
                    if (rc.IsFailure()) return rc;

                    fileSystem = saveFs;

                    // Todo: Dummy ISaveDataExtraDataAccessor

                    return Result.Success;

                case DirectoryEntryType.File:
                    rc = sourceFileSystem.OpenFile(out IFile saveDataFile, saveDataPath, OpenMode.ReadWrite);
                    if (rc.IsFailure()) return rc;

                    var saveDataStorage = new DisposingFileStorage(saveDataFile);
                    fileSystem = new SaveDataFileSystem(KeySet, saveDataStorage, IntegrityCheckLevel.ErrorOnInvalid,
                        false);

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
