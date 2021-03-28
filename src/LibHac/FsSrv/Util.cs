using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using PathNormalizer = LibHac.FsSrv.Impl.PathNormalizer;

namespace LibHac.FsSrv
{
    public static class Util
    {
        public static Result CreateSubFileSystem(out IFileSystem subFileSystem, IFileSystem baseFileSystem, string path,
            bool createPathIfMissing)
        {
            UnsafeHelpers.SkipParamInit(out subFileSystem);
            Result rc;

            if (!createPathIfMissing)
            {
                if (path == null) return ResultFs.NullptrArgument.Log();

                rc = baseFileSystem.GetEntryType(out DirectoryEntryType entryType, path.ToU8Span());

                if (rc.IsFailure() || entryType != DirectoryEntryType.Directory)
                {
                    return ResultFs.PathNotFound.Log();
                }
            }

            rc = baseFileSystem.EnsureDirectoryExists(path);
            if (rc.IsFailure()) return rc;

            return CreateSubFileSystemImpl(out subFileSystem, baseFileSystem, path);
        }

        public static Result CreateSubFileSystemImpl(out IFileSystem subFileSystem, IFileSystem baseFileSystem, string path)
        {
            UnsafeHelpers.SkipParamInit(out subFileSystem);

            if (path == null) return ResultFs.NullptrArgument.Log();

            Result rc = baseFileSystem.OpenDirectory(out IDirectory _, path.ToU8Span(), OpenDirectoryMode.Directory);
            if (rc.IsFailure()) return rc;

            rc = SubdirectoryFileSystem.CreateNew(out SubdirectoryFileSystem fs, baseFileSystem, path.ToU8String());
            subFileSystem = fs;
            return rc;
        }

        public static Result VerifyHostPath(U8Span path)
        {
            if (path.IsEmpty())
                return Result.Success;

            if (path[0] != StringTraits.DirectorySeparator)
                return ResultFs.InvalidPathFormat.Log();

            U8Span path2 = path.Slice(1);

            if (path2.IsEmpty())
                return Result.Success;

            int skipLength = WindowsPath.GetWindowsPathSkipLength(path2);
            int remainingLength = PathTools.MaxPathLength - skipLength;

            Result rc = PathUtility.VerifyPath(null, path2.Slice(skipLength), remainingLength, remainingLength);
            if (rc.IsFailure()) return rc;

            using var normalizer = new PathNormalizer(path, PathNormalizer.Option.PreserveUnc);
            return normalizer.Result;
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
