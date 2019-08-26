using LibHac.Fs;

namespace LibHac.FsService.Creators
{
    public class SubDirectoryFileSystemCreator : ISubDirectoryFileSystemCreator
    {
        public Result Create(out IFileSystem subDirFileSystem, IFileSystem baseFileSystem, string path)
        {
            try
            {
                baseFileSystem.OpenDirectory(path, OpenDirectoryMode.Directories);
            }
            catch (HorizonResultException ex)
            {
                subDirFileSystem = default;

                return ex.ResultValue;
            }

            subDirFileSystem = new SubdirectoryFileSystem(baseFileSystem, path);

            return Result.Success;
        }
    }
}
