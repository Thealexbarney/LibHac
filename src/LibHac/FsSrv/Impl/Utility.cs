﻿using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSystem;
using LibHac.Util;

namespace LibHac.FsSrv.Impl;

internal static class Utility
{
    public static bool IsHostFsMountName(ReadOnlySpan<byte> name)
    {
        return StringUtils.Compare(name, CommonMountNames.HostRootFileSystemMountName) == 0;
    }

    public static Result CreateSubDirectoryFileSystem(ref SharedRef<IFileSystem> outSubDirFileSystem,
        ref SharedRef<IFileSystem> baseFileSystem, in Path rootPath)
    {
        if (rootPath.IsEmpty())
        {
            outSubDirFileSystem.SetByMove(ref baseFileSystem);
            return Result.Success;
        }

        // Check if the directory exists
        using var dir = new UniqueRef<IDirectory>();
        Result rc = baseFileSystem.Get.OpenDirectory(ref dir.Ref(), rootPath, OpenDirectoryMode.Directory);
        if (rc.IsFailure()) return rc;

        dir.Reset();

        using var fs = new SharedRef<SubdirectoryFileSystem>(new SubdirectoryFileSystem(ref baseFileSystem));
        if (!fs.HasValue)
            return ResultFs.AllocationMemoryFailedInSubDirectoryFileSystemCreatorA.Log();

        rc = fs.Get.Initialize(in rootPath);
        if (rc.IsFailure()) return rc;

        outSubDirFileSystem.SetByMove(ref fs.Ref());

        return Result.Success;
    }

    public static Result WrapSubDirectory(ref SharedRef<IFileSystem> outFileSystem,
        ref SharedRef<IFileSystem> baseFileSystem, in Path rootPath, bool createIfMissing)
    {
        // The path must already exist if we're not automatically creating it
        if (!createIfMissing)
        {
            Result result = baseFileSystem.Get.GetEntryType(out _, in rootPath);
            if (result.IsFailure()) return result;
        }

        // Ensure the path exists or check if it's a directory
        Result rc = FsSystem.Utility.EnsureDirectory(baseFileSystem.Get, in rootPath);
        if (rc.IsFailure()) return rc;

        return CreateSubDirectoryFileSystem(ref outFileSystem, ref baseFileSystem, rootPath);
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
