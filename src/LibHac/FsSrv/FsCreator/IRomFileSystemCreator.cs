using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator;

public interface IRomFileSystemCreator : IDisposable
{
    Result Create(ref SharedRef<IFileSystem> outFileSystem, ref readonly SharedRef<IStorage> romFsStorage);
}