using System;
using LibHac.Fs;
using LibHac.Sf;
using IFileSf = LibHac.FsSrv.Sf.IFile;
using IDirectorySf = LibHac.FsSrv.Sf.IDirectory;

namespace LibHac.FsSrv.Sf
{
    public interface IFileSystem : IDisposable
    {
        Result GetImpl(out ReferenceCountedDisposable<Fs.Fsa.IFileSystem> fileSystem);
        Result CreateFile(in Path path, long size, int option);
        Result DeleteFile(in Path path);
        Result CreateDirectory(in Path path);
        Result DeleteDirectory(in Path path);
        Result DeleteDirectoryRecursively(in Path path);
        Result RenameFile(in Path currentPath, in Path newPath);
        Result RenameDirectory(in Path currentPath, in Path newPath);
        Result GetEntryType(out uint entryType, in Path path);
        Result OpenFile(out ReferenceCountedDisposable<IFileSf> outFile, in Path path, uint mode);
        Result OpenDirectory(out ReferenceCountedDisposable<IDirectorySf> outDirectory, in Path path, uint mode);
        Result Commit();
        Result GetFreeSpaceSize(out long freeSpace, in Path path);
        Result GetTotalSpaceSize(out long totalSpace, in Path path);
        Result CleanDirectoryRecursively(in Path path);
        Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path);
        Result QueryEntry(OutBuffer outBuffer, InBuffer inBuffer, int queryId, in Path path);
    }
}
