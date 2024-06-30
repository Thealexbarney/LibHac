﻿using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator;

public interface IBuiltInStorageFileSystemCreator
{
    Result Create(ref SharedRef<IFileSystem> outFileSystem, BisPartitionId partitionId, bool caseSensitive);

    public Result Create(ref SharedRef<IFileSystem> outFileSystem, BisPartitionId partitionId)
    {
        return Create(ref outFileSystem, partitionId, caseSensitive: false).Ret();
    }
}