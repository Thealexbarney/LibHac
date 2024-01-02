// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Fs.Dbm.Impl;

namespace LibHac.Fs.Dbm;

public class DirectoryObjectTemplate<TDirName, TFileName>
    where TDirName : unmanaged
    where TFileName : unmanaged
{
    private FileSystemObjectTemplate<TDirName, TFileName> _fileSystem;
    private DirectoryInfo _directoryInfo;
    private HierarchicalFileTableTemplate<TDirName, TFileName, DirectoryInfo, FileInfo>.DirectoryKey _directoryKey;
    private HierarchicalFileTableTemplate<TDirName, TFileName, DirectoryInfo, FileInfo>.FindIndex _findIndex;

    public DirectoryObjectTemplate()
    {
        throw new NotImplementedException();
    }

    public Result CountFile(out uint outCount)
    {
        throw new NotImplementedException();
    }

    public Result CountDirectory(out uint outCount)
    {
        throw new NotImplementedException();
    }

    public Result FindNextDirectory(out TDirName outDirectoryName, out bool outIsFinished)
    {
        throw new NotImplementedException();
    }

    public Result FindNextFile(out long outFileSize, out TFileName outFileName, out bool outIsFinished)
    {
        throw new NotImplementedException();
    }

    public Result FindOpen()
    {
        throw new NotImplementedException();
    }

    internal ref DirectoryInfo GetDirectoryInfo()
    {
        throw new NotImplementedException();
    }

    public Result Notify(out bool outIsFinished, long id, bool isFile)
    {
        throw new NotImplementedException();
    }

    internal void Initialize(FileSystemObjectTemplate<TDirName, TFileName> fileSystem,
        in HierarchicalFileTableTemplate<TDirName, TFileName, DirectoryInfo, FileInfo>.DirectoryKey directoryKey)
    {
        throw new NotImplementedException();
    }
}