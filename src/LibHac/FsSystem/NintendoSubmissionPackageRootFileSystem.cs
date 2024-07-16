using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem.Impl;
using LibHac.Util;

namespace LibHac.FsSystem;

using NspRootFileSystemCore =
    PartitionFileSystemCore<NintendoSubmissionPackageRootFileSystemMeta, NintendoSubmissionPackageRootFileSystemFormat,
        NintendoSubmissionPackageRootFileSystemFormat.PartitionFileSystemHeaderImpl,
        NintendoSubmissionPackageRootFileSystemFormat.PartitionEntry>;

/// <summary>
/// Reads a standard partition file system of version 0 or version 1. These files start with "PFS0" or "PFS1" respectively.
/// </summary>
/// <remarks>Based on nnSdk 18.3.0 (FS 18.0.0)</remarks>
public class NintendoSubmissionPackageRootFileSystem : IFileSystem
{
    private Optional<NspRootFileSystemCore> _nspRootFileSystem;
    private Optional<PartitionFileSystem> _partitionFileSystem;

    public NintendoSubmissionPackageRootFileSystem()
    {
        _nspRootFileSystem = new Optional<NspRootFileSystemCore>();
        _partitionFileSystem = new Optional<PartitionFileSystem>();
    }

    public override void Dispose()
    {
        if (_nspRootFileSystem.HasValue)
        {
            _nspRootFileSystem.Value.Dispose();
            _nspRootFileSystem.Clear();
        }

        if (_partitionFileSystem.HasValue)
        {
            _partitionFileSystem.Value.Dispose();
            _partitionFileSystem.Clear();
        }

        base.Dispose();
    }

    public Result Initialize(ref readonly SharedRef<IStorage> baseStorage)
    {
        return InitializeImpl(in baseStorage).Ret();
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, ref readonly Path path, OpenMode mode)
    {
        return GetImpl().OpenFile(ref outFile, in path, mode).Ret();
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, ref readonly Path path, OpenDirectoryMode mode)
    {
        return GetImpl().OpenDirectory(ref outDirectory, in path, mode).Ret();
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, ref readonly Path path)
    {
        return GetImpl().GetEntryType(out entryType, in path).Ret();
    }

    protected override Result DoCreateFile(ref readonly Path path, long size, CreateFileOptions option)
    {
        return GetImpl().CreateFile(in path, size, option).Ret();
    }

    protected override Result DoDeleteFile(ref readonly Path path)
    {
        return GetImpl().DeleteFile(in path).Ret();
    }

    protected override Result DoCreateDirectory(ref readonly Path path)
    {
        return GetImpl().CreateDirectory(in path).Ret();
    }

    protected override Result DoDeleteDirectory(ref readonly Path path)
    {
        return GetImpl().DeleteDirectory(in path).Ret();
    }

    protected override Result DoDeleteDirectoryRecursively(ref readonly Path path)
    {
        return GetImpl().DeleteDirectoryRecursively(in path).Ret();
    }

    protected override Result DoCleanDirectoryRecursively(ref readonly Path path)
    {
        return GetImpl().CleanDirectoryRecursively(in path).Ret();
    }

    protected override Result DoRenameFile(ref readonly Path currentPath, ref readonly Path newPath)
    {
        return GetImpl().RenameFile(in currentPath, in newPath).Ret();
    }

    protected override Result DoRenameDirectory(ref readonly Path currentPath, ref readonly Path newPath)
    {
        return GetImpl().RenameDirectory(in currentPath, in newPath).Ret();
    }

    protected override Result DoCommit()
    {
        return GetImpl().Commit().Ret();
    }

    protected override Result DoCommitProvisionally(long counter)
    {
        return GetImpl().CommitProvisionally(counter).Ret();
    }

    private Result InitializeImpl(ref readonly SharedRef<IStorage> baseStorage)
    {
        bool successNsp = false;
        try
        {
            if (_nspRootFileSystem.HasValue)
                _nspRootFileSystem.Value.Dispose();

            // First try to open the file system as an NspRootFileSystem
            _nspRootFileSystem.Set(new NspRootFileSystemCore());

            Result res = _nspRootFileSystem.Value.Initialize(in baseStorage);

            if (!res.IsSuccess())
            {
                if (ResultFs.PartitionSignatureVerificationFailed.Includes(res))
                {
                    // If that fails, try to open the file system as a PartitionFileSystem
                    bool successPfs = false;
                    try
                    {
                        if (_partitionFileSystem.HasValue)
                            _partitionFileSystem.Value.Dispose();

                        _partitionFileSystem.Set(new PartitionFileSystem());

                        res = _partitionFileSystem.Value.Initialize(in baseStorage);
                        if (res.IsFailure()) return res.Miss();

                        successPfs = true;
                        return Result.Success;
                    }
                    finally
                    {
                        if (!successPfs)
                        {
                            if (_partitionFileSystem.HasValue)
                            {
                                _partitionFileSystem.Value.Dispose();
                                _partitionFileSystem.Clear();
                            }
                        }
                    }
                }
                else
                {
                    return res.Miss();
                }
            }
            else
            {
                successNsp = true;
                return Result.Success;
            }
        }
        finally
        {
            if (!successNsp)
            {
                if (_nspRootFileSystem.HasValue)
                {
                    _nspRootFileSystem.Value.Dispose();
                    _nspRootFileSystem.Clear();
                }
            }
        }
    }

    private IFileSystem GetImpl()
    {
        if (_nspRootFileSystem.HasValue)
            return _nspRootFileSystem.Value;

        Assert.SdkAssert(_partitionFileSystem.HasValue);

        return _partitionFileSystem.Value;
    }
}