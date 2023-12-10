using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Sf;
using IFileSf = LibHac.FsSrv.Sf.IFile;
using IDirectorySf = LibHac.FsSrv.Sf.IDirectory;

namespace LibHac.FsSrv.Sf;

public interface IFileSystem : IDisposable
{
    Result GetImpl(ref SharedRef<Fs.Fsa.IFileSystem> fileSystem);
    Result CreateFile(ref readonly Path path, long size, int option);
    Result DeleteFile(ref readonly Path path);
    Result CreateDirectory(ref readonly Path path);
    Result DeleteDirectory(ref readonly Path path);
    Result DeleteDirectoryRecursively(ref readonly Path path);
    Result RenameFile(ref readonly Path currentPath, ref readonly Path newPath);
    Result RenameDirectory(ref readonly Path currentPath, ref readonly Path newPath);
    Result GetEntryType(out uint entryType, ref readonly Path path);
    Result OpenFile(ref SharedRef<IFileSf> outFile, ref readonly Path path, uint mode);
    Result OpenDirectory(ref SharedRef<IDirectorySf> outDirectory, ref readonly Path path, uint mode);
    Result Commit();
    Result GetFreeSpaceSize(out long freeSpace, ref readonly Path path);
    Result GetTotalSpaceSize(out long totalSpace, ref readonly Path path);
    Result CleanDirectoryRecursively(ref readonly Path path);
    Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, ref readonly Path path);
    Result QueryEntry(OutBuffer outBuffer, InBuffer inBuffer, int queryId, ref readonly Path path);
    Result GetFileSystemAttribute(out FileSystemAttribute outAttribute);
}