using System;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Sf
{
    public interface IFileSystemSf : IDisposable
    {
        Result GetImpl(out ReferenceCountedDisposable<IFileSystem> fileSystem);
        Result CreateFile(in Path path, long size, int option);
        Result DeleteFile(in Path path);
        Result CreateDirectory(in Path path);
        Result DeleteDirectory(in Path path);
        Result DeleteDirectoryRecursively(in Path path);
        Result RenameFile(in Path oldPath, in Path newPath);
        Result RenameDirectory(in Path oldPath, in Path newPath);
        Result GetEntryType(out uint entryType, in Path path);
        Result OpenFile(out ReferenceCountedDisposable<IFileSf> file, in Path path, uint mode);
        Result OpenDirectory(out ReferenceCountedDisposable<IDirectorySf> directory, in Path path, uint mode);
        Result Commit();
        Result GetFreeSpaceSize(out long freeSpace, in Path path);
        Result GetTotalSpaceSize(out long totalSpace, in Path path);
        Result CleanDirectoryRecursively(in Path path);
        Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path);
        Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, int queryId, in Path path);
    }
}
