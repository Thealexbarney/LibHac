using System;
using LibHac.Fs;

namespace LibHac.FsService.Creators
{
    public class TargetManagerFileSystemCreator : ITargetManagerFileSystemCreator
    {
        public Result Create(out IFileSystem fileSystem, bool openCaseSensitive)
        {
            throw new NotImplementedException();
        }

        public Result GetCaseSensitivePath(out bool isSuccess, ref string path)
        {
            throw new NotImplementedException();
        }
    }
}
