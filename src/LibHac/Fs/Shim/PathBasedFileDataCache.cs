using System;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;

namespace LibHac.Fs.Shim;

public static class PathBasedFileDataCacheShim
{
    internal static UniqueLock LockPathBasedFileDataCacheEntries(this FileSystemClientImpl fs)
    {
        throw new NotImplementedException();
    }

    internal static void InvalidatePathBasedFileDataCacheEntries(this FileSystemClientImpl fs,
        FileSystemAccessor fsAccessor)
    {
        throw new NotImplementedException();
    }

    internal static void InvalidatePathBasedFileDataCacheEntry(this FileSystemClientImpl fs,
        FileSystemAccessor fsAccessor, in Path path)
    {
        throw new NotImplementedException();
    }

    internal static void InvalidatePathBasedFileDataCacheEntry(this FileSystemClientImpl fs,
        FileSystemAccessor fsAccessor, in FilePathHash hash, int hashIndex)
    {
        throw new NotImplementedException();
    }

    internal static bool FindPathBasedFileDataCacheEntry(this FileSystemClientImpl fs, out FilePathHash outHash,
        out int outHashIndex, FileSystemAccessor fsAccessor, in Path path)
    {
        throw new NotImplementedException();
    }

    internal static Result ReadViaPathBasedFileDataCache(this FileSystemClientImpl fs, IFile file, int openMode,
        FileSystemAccessor fileSystem, in FilePathHash hash, int hashIndex, out long bytesRead, long offset,
        Span<byte> buffer, in ReadOption option, ref FileDataCacheAccessResult outCacheAccessResult)
    {
        throw new NotImplementedException();
    }

    internal static Result WriteViaPathBasedFileDataCache(this FileSystemClientImpl fs, IFile file, int openMode,
        FileSystemAccessor fileSystem, in FilePathHash hash, int hashIndex, long offset, ReadOnlySpan<byte> buffer,
        in WriteOption option)
    {
        throw new NotImplementedException();
    }

    public static Result EnableIndividualFileDataCache(this FileSystemClient fs, U8Span path, Memory<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public static void DisableIndividualFileDataCache(this FileSystemClient fs, U8Span path)
    {
        throw new NotImplementedException();
    }
}