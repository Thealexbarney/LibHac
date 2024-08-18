﻿using LibHac.Common;
using LibHac.Fat;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator;

public interface IFatFileSystemCreator
{
    Result Create(ref SharedRef<IFileSystem> outFileSystem, ref readonly SharedRef<IStorage> baseStorage,
        FatAttribute attribute, int driveId, Result invalidFatFormatResult, Result usableSpaceNotEnoughResult);

    Result Format(ref readonly SharedRef<IStorage> partitionStorage, FatAttribute attribute, FatFormatParam formatParam,
        int driveId, Result invalidFatFormatResult, Result usableSpaceNotEnoughResult);
}