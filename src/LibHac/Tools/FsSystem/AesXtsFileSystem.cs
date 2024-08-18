﻿using System;
using System.Diagnostics;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.Fs;
using LibHac.Util;

namespace LibHac.Tools.FsSystem;

public class AesXtsFileSystem : IFileSystem
{
    public int BlockSize { get; }

    private IFileSystem _baseFileSystem;
    private SharedRef<IFileSystem> _sharedBaseFileSystem;
    private byte[] _kekSource;
    private byte[] _validationKey;

    public AesXtsFileSystem(ref readonly SharedRef<IFileSystem> fs, byte[] keys, int blockSize)
    {
        _sharedBaseFileSystem = SharedRef<IFileSystem>.CreateCopy(in fs);
        _baseFileSystem = _sharedBaseFileSystem.Get;
        _kekSource = keys.AsSpan(0, 0x10).ToArray();
        _validationKey = keys.AsSpan(0x10, 0x10).ToArray();
        BlockSize = blockSize;
    }

    public AesXtsFileSystem(IFileSystem fs, byte[] keys, int blockSize)
    {
        _baseFileSystem = fs;
        _kekSource = keys.AsSpan(0, 0x10).ToArray();
        _validationKey = keys.AsSpan(0x10, 0x10).ToArray();
        BlockSize = blockSize;
    }

    public override void Dispose()
    {
        _sharedBaseFileSystem.Destroy();
        base.Dispose();
    }

    protected override Result DoCreateDirectory(ref readonly Path path)
    {
        return _baseFileSystem.CreateDirectory(in path);
    }

    protected override Result DoCreateFile(ref readonly Path path, long size, CreateFileOptions option)
    {
        return CreateFile(in path, size, option, new byte[0x20]);
    }

    /// <summary>
    /// Creates a new <see cref="AesXtsFile"/> using the provided key.
    /// </summary>
    /// <param name="path">The full path of the file to create.</param>
    /// <param name="size">The initial size of the created file.</param>
    /// <param name="options">Flags to control how the file is created.
    /// Should usually be <see cref="CreateFileOptions.None"/></param>
    /// <param name="key">The 256-bit key containing a 128-bit data key followed by a 128-bit tweak key.</param>
    public Result CreateFile(ref readonly Path path, long size, CreateFileOptions options, byte[] key)
    {
        long containerSize = AesXtsFile.HeaderLength + Alignment.AlignUp(size, 0x10);

        Result res = _baseFileSystem.CreateFile(in path, containerSize, options);
        if (res.IsFailure()) return res.Miss();

        var header = new AesXtsFileHeader(key, size, path.ToString(), _kekSource, _validationKey);

        using var baseFile = new UniqueRef<IFile>();
        res = _baseFileSystem.OpenFile(ref baseFile.Ref, in path, OpenMode.Write);
        if (res.IsFailure()) return res.Miss();

        res = baseFile.Get.Write(0, header.ToBytes(false));
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoDeleteDirectory(ref readonly Path path)
    {
        return _baseFileSystem.DeleteDirectory(in path);
    }

    protected override Result DoDeleteDirectoryRecursively(ref readonly Path path)
    {
        return _baseFileSystem.DeleteDirectoryRecursively(in path);
    }

    protected override Result DoCleanDirectoryRecursively(ref readonly Path path)
    {
        return _baseFileSystem.CleanDirectoryRecursively(in path);
    }

    protected override Result DoDeleteFile(ref readonly Path path)
    {
        return _baseFileSystem.DeleteFile(in path);
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, ref readonly Path path,
        OpenDirectoryMode mode)
    {
        using var baseDir = new UniqueRef<IDirectory>();
        Result res = _baseFileSystem.OpenDirectory(ref baseDir.Ref, in path, mode);
        if (res.IsFailure()) return res.Miss();

        outDirectory.Reset(new AesXtsDirectory(_baseFileSystem, ref baseDir.Ref, new U8String(path.GetString().ToArray()), mode));
        return Result.Success;
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, ref readonly Path path, OpenMode mode)
    {
        using var baseFile = new UniqueRef<IFile>();
        Result res = _baseFileSystem.OpenFile(ref baseFile.Ref, in path, mode | OpenMode.Read);
        if (res.IsFailure()) return res.Miss();

        var xtsFile = new AesXtsFile(mode, ref baseFile.Ref, new U8String(path.GetString().ToArray()), _kekSource,
            _validationKey, BlockSize);

        outFile.Reset(xtsFile);
        return Result.Success;
    }

    protected override Result DoRenameDirectory(ref readonly Path currentPath, ref readonly Path newPath)
    {
        // todo: Return proper result codes

        // Official code procedure:
        // Make sure all file headers can be decrypted
        // Rename directory to the new path
        // Reencrypt file headers with new path
        // If no errors, return
        // Reencrypt any modified file headers with the old path
        // Rename directory to the old path

        Result res = _baseFileSystem.RenameDirectory(in currentPath, in newPath);
        if (res.IsFailure()) return res.Miss();

        try
        {
            RenameDirectoryImpl(currentPath.ToString(), newPath.ToString(), false);
        }
        catch (Exception)
        {
            RenameDirectoryImpl(currentPath.ToString(), newPath.ToString(), true);
            _baseFileSystem.RenameDirectory(in currentPath, in newPath);

            throw;
        }

        return Result.Success;
    }

    private void RenameDirectoryImpl(string srcDir, string dstDir, bool doRollback)
    {
        foreach (DirectoryEntryEx entry in this.EnumerateEntries(dstDir, "*"))
        {
            string subSrcPath = $"{srcDir}/{entry.Name}";
            string subDstPath = $"{dstDir}/{entry.Name}";

            if (entry.Type == DirectoryEntryType.Directory)
            {
                RenameDirectoryImpl(subSrcPath, subDstPath, doRollback);
            }

            if (entry.Type == DirectoryEntryType.File)
            {
                if (doRollback)
                {
                    if (TryReadXtsHeader(subDstPath, subDstPath, out AesXtsFileHeader header))
                    {
                        WriteXtsHeader(header, subDstPath, subSrcPath);
                    }
                }
                else
                {
                    AesXtsFileHeader header = ReadXtsHeader(subDstPath, subSrcPath);
                    WriteXtsHeader(header, subDstPath, subDstPath);
                }
            }
        }
    }

    protected override Result DoRenameFile(ref readonly Path currentPath, ref readonly Path newPath)
    {
        // todo: Return proper result codes

        AesXtsFileHeader header = ReadXtsHeader(currentPath.ToString(), currentPath.ToString());

        Result res = _baseFileSystem.RenameFile(in currentPath, in newPath);
        if (res.IsFailure()) return res.Miss();

        try
        {
            WriteXtsHeader(header, newPath.ToString(), newPath.ToString());
        }
        catch (Exception)
        {
            _baseFileSystem.RenameFile(in newPath, in currentPath);
            WriteXtsHeader(header, currentPath.ToString(), currentPath.ToString());

            throw;
        }

        return Result.Success;
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, ref readonly Path path)
    {
        return _baseFileSystem.GetEntryType(out entryType, in path);
    }

    protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, ref readonly Path path)
    {
        return _baseFileSystem.GetFileTimeStampRaw(out timeStamp, in path);
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, ref readonly Path path)
    {
        return _baseFileSystem.GetFreeSpaceSize(out freeSpace, in path);
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, ref readonly Path path)
    {
        return _baseFileSystem.GetTotalSpaceSize(out totalSpace, in path);
    }

    protected override Result DoCommit()
    {
        return _baseFileSystem.Commit();
    }

    protected override Result DoCommitProvisionally(long counter)
    {
        return _baseFileSystem.CommitProvisionally(counter);
    }

    protected override Result DoRollback()
    {
        return _baseFileSystem.Rollback();
    }

    protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
        ref readonly Path path)
    {
        return _baseFileSystem.QueryEntry(outBuffer, inBuffer, queryId, in path);
    }

    private AesXtsFileHeader ReadXtsHeader(string filePath, string keyPath)
    {
        if (!TryReadXtsHeader(filePath, keyPath, out AesXtsFileHeader header))
        {
            ThrowHelper.ThrowResult(ResultFs.AesXtsFileSystemFileHeaderCorruptedOnRename.Value, "Could not decrypt AES-XTS keys");
        }

        return header;
    }

    private bool TryReadXtsHeader(string filePath, string keyPath, out AesXtsFileHeader header)
    {
        Debug.Assert(PathTools.IsNormalized(filePath.AsSpan()));
        Debug.Assert(PathTools.IsNormalized(keyPath.AsSpan()));

        header = null;

        using var file = new UniqueRef<IFile>();
        Result res = _baseFileSystem.OpenFile(ref file.Ref, filePath.ToU8Span(), OpenMode.Read);
        if (res.IsFailure()) return false;

        header = new AesXtsFileHeader(file.Get);

        return header.TryDecryptHeader(keyPath, _kekSource, _validationKey);
    }

    private void WriteXtsHeader(AesXtsFileHeader header, string filePath, string keyPath)
    {
        Debug.Assert(PathTools.IsNormalized(filePath.AsSpan()));
        Debug.Assert(PathTools.IsNormalized(keyPath.AsSpan()));

        header.EncryptHeader(keyPath, _kekSource, _validationKey);

        using var file = new UniqueRef<IFile>();
        _baseFileSystem.OpenFile(ref file.Ref, filePath.ToU8Span(), OpenMode.ReadWrite);

        file.Get.Write(0, header.ToBytes(false), WriteOption.Flush).ThrowIfFailure();
    }
}