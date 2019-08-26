using LibHac.Fs;

namespace LibHac.FsService
{
    public static class Util
    {
        public static Result CreateSubFileSystem(out IFileSystem subFileSystem, IFileSystem baseFileSystem, string path,
            bool createPathIfMissing)
        {
            subFileSystem = default;

            if (!createPathIfMissing)
            {
                if (path == null) return ResultFs.NullArgument;

                if (baseFileSystem.GetEntryType(path) != DirectoryEntryType.Directory)
                {
                    return ResultFs.PathNotFound;
                }
            }

            baseFileSystem.EnsureDirectoryExists(path);

            return CreateSubFileSystemImpl(out subFileSystem, baseFileSystem, path);
        }

        public static Result CreateSubFileSystemImpl(out IFileSystem subFileSystem, IFileSystem baseFileSystem, string path)
        {
            subFileSystem = default;

            if (path == null) return ResultFs.NullArgument;

            try
            {
                baseFileSystem.OpenDirectory(path, OpenDirectoryMode.Directories);
            }
            catch (HorizonResultException ex)
            {
                return ex.ResultValue;
            }

            subFileSystem = new SubdirectoryFileSystem(baseFileSystem, path);

            return Result.Success;
        }
    }
}
