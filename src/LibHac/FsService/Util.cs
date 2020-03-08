using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;

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
                if (path == null) return ResultFs.NullptrArgument.Log();

                Result rc = baseFileSystem.GetEntryType(out DirectoryEntryType entryType, path.ToU8Span());

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

            if (path == null) return ResultFs.NullptrArgument.Log();

            Result rc = baseFileSystem.OpenDirectory(out IDirectory _, path.ToU8Span(), OpenDirectoryMode.Directory);
            if (rc.IsFailure()) return rc;

            rc = SubdirectoryFileSystem.CreateNew(out SubdirectoryFileSystem fs, baseFileSystem, path.ToU8String());
            subFileSystem = fs;
            return rc;
        }

        public static bool UseDeviceUniqueSaveMac(SaveDataSpaceId spaceId)
        {
            return spaceId == SaveDataSpaceId.System ||
                   spaceId == SaveDataSpaceId.User ||
                   spaceId == SaveDataSpaceId.Temporary ||
                   spaceId == SaveDataSpaceId.ProperSystem ||
                   spaceId == SaveDataSpaceId.SafeMode;
        }
    }
}
