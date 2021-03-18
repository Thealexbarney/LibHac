﻿using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.FsCreator
{
    public interface ISaveDataFileSystemCreator
    {
        Result CreateFile(out IFile file, IFileSystem sourceFileSystem, ulong saveDataId, OpenMode openMode);

        Result Create(out IFileSystem fileSystem,
            out ReferenceCountedDisposable<ISaveDataExtraDataAccessor> extraDataAccessor, IFileSystem sourceFileSystem,
            ulong saveDataId, bool allowDirectorySaveData, bool useDeviceUniqueMac, SaveDataType type,
            ITimeStampGenerator timeStampGenerator);

        void SetSdCardEncryptionSeed(ReadOnlySpan<byte> seed);
    }
}
