using System;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSrv.FsCreator;

public interface ISdStorageCreator: IDisposable
{
    Result Create(ref SharedRef<IStorage> outStorage);
}