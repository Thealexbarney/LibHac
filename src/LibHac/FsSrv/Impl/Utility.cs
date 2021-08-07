using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSystem;
using LibHac.Util;

namespace LibHac.FsSrv.Impl
{
    internal static class Utility
    {
        public static bool IsHostFsMountName(ReadOnlySpan<byte> name)
        {
            return StringUtils.Compare(name, CommonMountNames.HostRootFileSystemMountName) == 0;
        }

        public static Result CreateSubDirectoryFileSystem(out ReferenceCountedDisposable<IFileSystem> subDirFileSystem,
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, in Path rootPath)
        {
            UnsafeHelpers.SkipParamInit(out subDirFileSystem);

            if (rootPath.IsEmpty())
            {
                subDirFileSystem = Shared.Move(ref baseFileSystem);
                return Result.Success;
            }

            // Check if the directory exists
            Result rc = baseFileSystem.Target.OpenDirectory(out IDirectory dir, rootPath, OpenDirectoryMode.Directory);
            if (rc.IsFailure()) return rc;

            dir.Dispose();

            var fs = new SubdirectoryFileSystem(ref baseFileSystem);
            using (var subDirFs = new ReferenceCountedDisposable<SubdirectoryFileSystem>(fs))
            {
                rc = subDirFs.Target.Initialize(in rootPath);
                if (rc.IsFailure()) return rc;

                subDirFileSystem = subDirFs.AddReference<IFileSystem>();
                return Result.Success;
            }
        }

        public static Result WrapSubDirectory(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, in Path rootPath, bool createIfMissing)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            // The path must already exist if we're not automatically creating it
            if (!createIfMissing)
            {
                Result result = baseFileSystem.Target.GetEntryType(out _, in rootPath);
                if (result.IsFailure()) return result;
            }

            // Ensure the path exists or check if it's a directory
            Result rc = Utility12.EnsureDirectory(baseFileSystem.Target, in rootPath);
            if (rc.IsFailure()) return rc;

            return CreateSubDirectoryFileSystem(out fileSystem, ref baseFileSystem, rootPath);
        }

        public static long ConvertZeroCommitId(in SaveDataExtraData extraData)
        {
            if (extraData.CommitId != 0)
                return extraData.CommitId;

            Span<byte> hash = stackalloc byte[Crypto.Sha256.DigestSize];

            Crypto.Sha256.GenerateSha256Hash(SpanHelpers.AsReadOnlyByteSpan(in extraData), hash);
            return BitConverter.ToInt64(hash);
        }
    }
}
