using System;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Creators
{
    public interface ITargetManagerFileSystemCreator
    {
        // Todo: Remove raw IFilesystem function
        Result Create(out IFileSystem fileSystem, bool openCaseSensitive);
        Result Create(out ReferenceCountedDisposable<IFileSystem> fileSystem, bool openCaseSensitive);
        Result NormalizeCaseOfPath(out bool isSupported, Span<byte> path);
    }
}