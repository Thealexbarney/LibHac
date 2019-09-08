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
                if (path == null) return ResultFs.NullArgument.Log();

                Result rc = baseFileSystem.GetEntryType(out DirectoryEntryType entryType, path);

                if (rc.IsFailure() || entryType != DirectoryEntryType.Directory)
                {
                    return ResultFs.PathNotFound.Log();
                }
            }

            baseFileSystem.EnsureDirectoryExists(path);

            return CreateSubFileSystemImpl(out subFileSystem, baseFileSystem, path);
        }

        public static Result CreateSubFileSystemImpl(out IFileSystem subFileSystem, IFileSystem baseFileSystem, string path)
        {
            subFileSystem = default;

            if (path == null) return ResultFs.NullArgument.Log();

            Result rc = baseFileSystem.OpenDirectory(out IDirectory _, path, OpenDirectoryMode.Directory);
            if (rc.IsFailure()) return rc;

            subFileSystem = new SubdirectoryFileSystem(baseFileSystem, path);

            return Result.Success;
        }

        public static bool UseDeviceUniqueSaveMac(SaveDataSpaceId spaceId)
        {
            return spaceId == SaveDataSpaceId.System ||
                   spaceId == SaveDataSpaceId.User ||
                   spaceId == SaveDataSpaceId.TemporaryStorage ||
                   spaceId == SaveDataSpaceId.ProperSystem ||
                   spaceId == SaveDataSpaceId.Safe;
        }
    }
}
