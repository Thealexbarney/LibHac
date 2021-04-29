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
            ReferenceCountedDisposable<IFileSystem> sourceFileSystem, ulong saveDataId, bool allowDirectorySaveData,
            bool useDeviceUniqueMac, SaveDataType type, ISaveDataCommitTimeStampGetter timeStampGetter);

        void SetSdCardEncryptionSeed(ReadOnlySpan<byte> seed);
    }
}