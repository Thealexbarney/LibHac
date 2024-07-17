using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator;

/// <summary>
/// Creates an <see cref="IFileSystem"/> from an <see cref="IStorage"/> containing a partition filesystem of version 0 or version 1.
/// </summary>
/// <remarks>Based on nnSdk 18.3.0 (FS 18.0.0)</remarks>
public interface INspRootFileSystemCreator
{
    Result Create(ref SharedRef<IFileSystem> outFileSystem, ref readonly SharedRef<IStorage> baseStorage);
}