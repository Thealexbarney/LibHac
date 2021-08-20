using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Sf;
using IFileSf = LibHac.FsSrv.Sf.IFile;
using IDirectorySf = LibHac.FsSrv.Sf.IDirectory;

namespace LibHac.FsSrv.Sf
{
    public interface IFileSystem : IDisposable
    {
        Result GetImpl(ref SharedRef<Fs.Fsa.IFileSystem> fileSystem);
        Result CreateFile(in Path path, long size, int option);
        Result DeleteFile(in Path path);
        Result CreateDirectory(in Path path);
        Result DeleteDirectory(in Path path);
        Result DeleteDirectoryRecursively(in Path path);
        Result RenameFile(in Path currentPath, in Path newPath);
        Result RenameDirectory(in Path currentPath, in Path newPath);
        Result GetEntryType(out uint entryType, in Path path);
        Result OpenFile(ref SharedRef<IFileSf> outFile, in Path path, uint mode);
        Result OpenDirectory(ref SharedRef<IDirectorySf> outDirectory, in Path path, uint mode);
        Result Commit();
        Result GetFreeSpaceSize(out long freeSpace, in Path path);
        Result GetTotalSpaceSize(out long totalSpace, in Path path);
        Result CleanDirectoryRecursively(in Path path);
        Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path);
        Result QueryEntry(OutBuffer outBuffer, InBuffer inBuffer, int queryId, in Path path);
    }
}
