using System;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Creators
{
    public interface ITargetManagerFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, bool openCaseSensitive);
        Result GetCaseSensitivePath(out bool isSuccess, Span<byte> path);
    }
}