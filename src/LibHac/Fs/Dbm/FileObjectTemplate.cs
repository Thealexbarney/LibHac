// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Fs.Dbm.Impl;
using IndexType = uint;

namespace LibHac.Fs.Dbm;

public class FileObjectTemplate<TDirName, TFileName>
    where TDirName : unmanaged
    where TFileName : unmanaged
{
    private FileSystemObjectTemplate<TDirName, TFileName> _fileSystem;
    private uint _blockCount;
    private FileInfo _fileInfo;
    private HierarchicalFileTableTemplate<TDirName, TFileName, DirectoryInfo, FileInfo>.FileKey _fileKey;
    private IndexType _previousIndex;
    private IndexType _currentIndex;

    public FileObjectTemplate()
    {
        throw new NotImplementedException();
    }

    internal ref FileInfo GetFileInfo()
    {
        throw new NotImplementedException();
    }

    public long GetSize()
    {
        throw new NotImplementedException();
    }

    public Result Resize(long newSize)
    {
        throw new NotImplementedException();
    }

    public Result IterateBegin(out IndexType outIndex, out bool outFinished, IndexType startIndex)
    {
        throw new NotImplementedException();
    }

    internal void Initialize(FileSystemObjectTemplate<TDirName, TFileName> fileSystem,
        in HierarchicalFileTableTemplate<TDirName, TFileName, DirectoryInfo, FileInfo>.FileKey key)
    {
        throw new NotImplementedException();
    }
}