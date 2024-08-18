using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator;

public interface IPartitionFileSystemCreator
{
    Result Create(ref SharedRef<IFileSystem> outFileSystem, ref readonly SharedRef<IStorage> baseStorage);
}