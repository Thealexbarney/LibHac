using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Util;

namespace LibHac.FsSrv.Impl
{
    internal static class Utility
    {
        public static Result EnsureDirectory(IFileSystem fileSystem, U8Span path)
        {
            FsPath.FromSpan(out FsPath pathBuffer, path.Value).IgnoreResult();

            int pathLength = StringUtils.GetLength(path);

            if (pathLength > 0)
            {
                // Remove any trailing directory separators
                while (pathLength > 0 && path[pathLength - 1] == StringTraits.DirectorySeparator)
                {
                    pathLength--;
                }

                // Copy the path to a mutable buffer
                path.Value.Slice(0, pathLength).CopyTo(pathBuffer.Str);
            }

            pathBuffer.Str[pathLength] = StringTraits.NullTerminator;

            return EnsureDirectoryImpl(fileSystem, pathBuffer.Str.Slice(0, pathLength));
        }

        private static Result EnsureDirectoryImpl(IFileSystem fileSystem, Span<byte> path)
        {
            // Double check the trailing separators have been trimmed
            Assert.SdkRequires(path.Length <= 1 || path[path.Length - 1] != StringTraits.DirectorySeparator);

            // Use the root path if the input path is empty
            var pathToCheck = new U8Span(path.IsEmpty ? FileSystemRootPath : path);

            // Check if the path exists
            Result rc = fileSystem.GetEntryType(out DirectoryEntryType entryType, pathToCheck);

            if (rc.IsFailure())
            {
                // Something went wrong if we get a result other than PathNotFound
                if (!ResultFs.PathNotFound.Includes(rc))
                    return rc;

                if (path.Length <= 0)
                {
                    // The file system either reported that the root directory doesn't exist,
                    // or the input path had a negative length
                    return ResultFs.PathNotFound.Log();
                }

                // The path does not exist. Ensure its parent directory exists
                rc = EnsureParentDirectoryImpl(fileSystem, path);
                if (rc.IsFailure()) return rc;

                // The parent directory exists, we can now create a directory at the input path
                rc = fileSystem.CreateDirectory(new U8Span(path));

                if (rc.IsSuccess())
                {
                    // The directory was successfully created
                    return Result.Success;
                }

                if (!ResultFs.PathAlreadyExists.Includes(rc))
                    return rc;

                // Someone else created a file system entry at the input path after we checked
                // if the path existed. Get the entry type to check if it's a directory.
                rc = fileSystem.GetEntryType(out entryType, new U8Span(path));
                if (rc.IsFailure()) return rc;
            }

            // We want the entry that exists at the input path to be a directory
            // Return PathAlreadyExists if it's a file
            if (entryType == DirectoryEntryType.File)
                return ResultFs.PathAlreadyExists.Log();

            // A directory exists at the input path. Success
            return Result.Success;
        }

        private static Result EnsureParentDirectoryImpl(IFileSystem fileSystem, Span<byte> path)
        {
            // The path should not be empty or have a trailing directory separator
            Assert.SdkRequiresLess(0, path.Length);
            Assert.SdkRequiresNotEqual(path[path.Length - 1], StringTraits.DirectorySeparator);

            // Make sure the path's not too long
            if (path.Length > PathTool.EntryNameLengthMax)
                return ResultFs.TooLongPath.Log();

            // Iterate until we run out of path or find the next separator
            int length = path.Length - 1;
            while (length > 0 && path[length] != StringTraits.DirectorySeparator)
            {
                length--;
            }

            if (length == 0)
            {
                // We hit the beginning of the path. Ensure the root directory exists and return
                return EnsureDirectoryImpl(fileSystem, Span<byte>.Empty);
            }

            // We found the length of the parent directory. Ensure it exists
            path[length] = StringTraits.NullTerminator;
            Result rc = EnsureDirectoryImpl(fileSystem, path.Slice(0, length));
            if (rc.IsFailure()) return rc;

            // Restore the separator
            path[length] = StringTraits.DirectorySeparator;
            return Result.Success;
        }

        public static Result CreateSubDirectoryFileSystem(out ReferenceCountedDisposable<IFileSystem> subDirFileSystem,
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, U8Span subPath, bool preserveUnc = false)
        {
            UnsafeHelpers.SkipParamInit(out subDirFileSystem);

            // Check if the directory exists
            Result rc = baseFileSystem.Target.OpenDirectory(out IDirectory dir, subPath, OpenDirectoryMode.Directory);
            if (rc.IsFailure()) return rc;

            dir.Dispose();

            var fs = new SubdirectoryFileSystem(ref baseFileSystem, preserveUnc);
            using (var subDirFs = new ReferenceCountedDisposable<SubdirectoryFileSystem>(fs))
            {
                rc = subDirFs.Target.Initialize(subPath);
                if (rc.IsFailure()) return rc;

                subDirFileSystem = subDirFs.AddReference<IFileSystem>();
                return Result.Success;
            }
        }

        public static Result WrapSubDirectory(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, U8Span path, bool createIfMissing)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            // The path must already exist if we're not automatically creating it
            if (!createIfMissing)
            {
                Result result = baseFileSystem.Target.GetEntryType(out _, path);
                if (result.IsFailure()) return result;
            }

            // Ensure the path exists or check if it's a directory
            Result rc = EnsureDirectory(baseFileSystem.Target, path);
            if (rc.IsFailure()) return rc;

            return CreateSubDirectoryFileSystem(out fileSystem, ref baseFileSystem, path);
        }

        public static long ConvertZeroCommitId(in SaveDataExtraData extraData)
        {
            if (extraData.CommitId != 0)
                return extraData.CommitId;

            Span<byte> hash = stackalloc byte[Crypto.Sha256.DigestSize];

            Crypto.Sha256.GenerateSha256Hash(SpanHelpers.AsReadOnlyByteSpan(in extraData), hash);
            return BitConverter.ToInt64(hash);
        }

        private static ReadOnlySpan<byte> FileSystemRootPath => // /
            new[]
            {
                (byte) '/'
            };
    }
}
