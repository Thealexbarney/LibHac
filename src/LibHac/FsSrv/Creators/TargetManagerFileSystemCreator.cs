using System;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Creators
{
    public class TargetManagerFileSystemCreator : ITargetManagerFileSystemCreator
    {
        public Result Create(out IFileSystem fileSystem, bool openCaseSensitive)
        {
            throw new NotImplementedException();
        }

        public Result GetCaseSensitivePath(out bool isSuccess, Span<byte> path)
        {
            throw new NotImplementedException();
        }
    }
}
