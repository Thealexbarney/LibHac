using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Crypto;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Impl;

[InlineArray(0x20)]
public struct NpdmHash
{
    private byte _data;
}

/// <summary>
/// Wraps an <see cref="IFile"/>. When reading, calculates a hash of the entire file and compares it to the file's
/// original hash to ensure the file hasn't been modified.
/// </summary>
/// <remarks>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
public class NpdmVerificationFile : IFile
{
    private readonly NpdmHash _hash;
    private UniqueRef<IFile> _baseFile;

    public NpdmVerificationFile(ref UniqueRef<IFile> baseFile, NpdmHash hash)
    {
        _hash = hash;
        _baseFile = UniqueRef<IFile>.Create(ref baseFile);
    }

    public override void Dispose()
    {
        _baseFile.Destroy();
        base.Dispose();
    }

    protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
    {
        UnsafeHelpers.SkipParamInit(out bytesRead);

        Result res = DryRead(out long readableSize, offset, destination.Length, in option, OpenMode.Read);
        if (res.IsFailure()) return res.Miss();

        byte[] hashData;
        byte[] npdmData = null;

        res = ReadAndCalculateNpdmHash();
        if (res.IsFailure()) return res.Miss();

        if (!CryptoUtil.IsSameBytes(hashData.AsSpan(), _hash, Unsafe.SizeOf<NpdmHash>()))
            return ResultFs.InvalidNspdVerificationData.Log();

        npdmData.AsSpan((int)offset, (int)readableSize).CopyTo(destination);
        return Result.Success;

        Result ReadAndCalculateNpdmHash()
        {
            hashData = new byte[0x20];
            Result res2 = _baseFile.Get.GetSize(out long npdmDataSize);
            if (res2.IsFailure()) return res2.Miss();

            npdmData = new byte[npdmDataSize];
            res2 = _baseFile.Get.Read(out long npdmReadSize, 0, npdmData, ReadOption.None);
            if (res2.IsFailure()) return res2.Miss();

            Sha256.GenerateSha256Hash(npdmData.AsSpan(0, (int)npdmReadSize), hashData);
            return Result.Success;
        }
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option) =>
        _baseFile.Get.Write(offset, source, in option).Ret();

    protected override Result DoFlush() => _baseFile.Get.Flush().Ret();
    protected override Result DoSetSize(long size) => _baseFile.Get.SetSize(size).Ret();
    protected override Result DoGetSize(out long size) => _baseFile.Get.GetSize(out size).Ret();

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer) =>
        _baseFile.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer).Ret();
}

/// <summary>
/// Wraps a code <see cref="IFileSystem"/>. Contains the hash of the code filesystem's .npdm file. When opening the npdm
/// file, a <see cref="NpdmVerificationFile"/> is used to ensure that the npdm file hasn't been modified.
/// </summary>
/// <remarks>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
public class NpdmVerificationFileSystem : IFileSystem
{
    private static ReadOnlySpan<byte> NpdmFilePath => "/main.npdm"u8;

    private readonly NpdmHash _hash;
    private readonly ReadOnlyFileSystem _baseFileSystem;

    public NpdmVerificationFileSystem(ref readonly SharedRef<IFileSystem> baseFileSystem, NpdmHash hash)
    {
        _hash = hash;
        _baseFileSystem = new ReadOnlyFileSystem(in baseFileSystem);
    }

    public override void Dispose()
    {
        _baseFileSystem?.Dispose();
        base.Dispose();
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, ref readonly Path path, OpenMode mode)
    {
        using var file = new UniqueRef<IFile>();
        Result res = _baseFileSystem.OpenFile(ref file.Ref, in path, mode);
        if (res.IsFailure()) return res.Miss();

        if (path != NpdmFilePath)
        {
            outFile.Set(ref file.Ref);
            return Result.Success;
        }

        using var npdmFile = new UniqueRef<IFile>(new NpdmVerificationFile(ref file.Ref, _hash));

        outFile.Set(ref npdmFile.Ref);
        return Result.Success;
    }

    protected override Result DoCreateFile(ref readonly Path path, long size, CreateFileOptions option) =>
        _baseFileSystem.CreateFile(in path, size, option).Ret();

    protected override Result DoDeleteFile(ref readonly Path path) =>
        _baseFileSystem.DeleteFile(in path).Ret();

    protected override Result DoCreateDirectory(ref readonly Path path) =>
        _baseFileSystem.CreateDirectory(in path).Ret();

    protected override Result DoDeleteDirectory(ref readonly Path path) =>
        _baseFileSystem.DeleteDirectory(in path).Ret();

    protected override Result DoDeleteDirectoryRecursively(ref readonly Path path) =>
        _baseFileSystem.DeleteDirectoryRecursively(in path).Ret();

    protected override Result DoCleanDirectoryRecursively(ref readonly Path path) =>
        _baseFileSystem.CleanDirectoryRecursively(in path).Ret();

    protected override Result DoRenameFile(ref readonly Path currentPath, ref readonly Path newPath) =>
        _baseFileSystem.RenameFile(in currentPath, in newPath).Ret();

    protected override Result DoRenameDirectory(ref readonly Path currentPath, ref readonly Path newPath) =>
        _baseFileSystem.RenameDirectory(in currentPath, in newPath).Ret();

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, ref readonly Path path) =>
        _baseFileSystem.GetEntryType(out entryType, in path).Ret();

    protected override Result DoGetFreeSpaceSize(out long freeSpace, ref readonly Path path) =>
        _baseFileSystem.GetFreeSpaceSize(out freeSpace, in path).Ret();

    protected override Result DoGetTotalSpaceSize(out long totalSpace, ref readonly Path path) =>
        _baseFileSystem.GetFreeSpaceSize(out totalSpace, in path).Ret();

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, ref readonly Path path, OpenDirectoryMode mode) =>
        _baseFileSystem.OpenDirectory(ref outDirectory, in path, mode).Ret();

    protected override Result DoCommit() =>
        _baseFileSystem.Commit().Ret();

    protected override Result DoCommitProvisionally(long counter) =>
        _baseFileSystem.CommitProvisionally(counter).Ret();

    protected override Result DoRollback() =>
        _baseFileSystem.Rollback().Ret();

    protected override Result DoFlush() =>
        _baseFileSystem.Flush().Ret();

    protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, ref readonly Path path) =>
        _baseFileSystem.GetFileTimeStampRaw(out timeStamp, in path).Ret();

    protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, ref readonly Path path) =>
        _baseFileSystem.QueryEntry(outBuffer, inBuffer, queryId, in path).Ret();
}