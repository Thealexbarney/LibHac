using System;
using System.Diagnostics;
using System.IO;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.FsSystem;
using LibHac.Util;
using Path = LibHac.Fs.Path;

namespace LibHac.Tools.Fs;

/// <summary>
/// A filesystem stored in-memory. Mainly used for testing.
/// </summary>
public class InMemoryFileSystem : IAttributeFileSystem
{
    private FileTable FsTable { get; }

    public InMemoryFileSystem()
    {
        FsTable = new FileTable();
    }

    protected override Result DoCreateDirectory(in Path path)
    {
        return FsTable.AddDirectory(new U8Span(path.GetString()));
    }

    protected override Result DoCreateDirectory(in Path path, NxFileAttributes archiveAttribute)
    {
        Result res = FsTable.AddDirectory(new U8Span(path.GetString()));
        if (res.IsFailure()) return res.Miss();

        res = FsTable.GetDirectory(new U8Span(path.GetString()), out DirectoryNode dir);
        if (res.IsFailure()) return res.Miss();

        dir.Attributes = archiveAttribute;
        return Result.Success;
    }

    protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
    {
        Result res = FsTable.AddFile(new U8Span(path.GetString()));
        if (res.IsFailure()) return res.Miss();

        res = FsTable.GetFile(new U8Span(path.GetString()), out FileNode file);
        if (res.IsFailure()) return res.Miss();

        return file.File.SetSize(size);
    }

    protected override Result DoDeleteDirectory(in Path path)
    {
        return FsTable.DeleteDirectory(new U8Span(path.GetString()), false);
    }

    protected override Result DoDeleteDirectoryRecursively(in Path path)
    {
        return FsTable.DeleteDirectory(new U8Span(path.GetString()), true);
    }

    protected override Result DoCleanDirectoryRecursively(in Path path)
    {
        return FsTable.CleanDirectory(new U8Span(path.GetString()));
    }

    protected override Result DoDeleteFile(in Path path)
    {
        return FsTable.DeleteFile(new U8Span(path.GetString()));
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
        OpenDirectoryMode mode)
    {
        Result res = FsTable.GetDirectory(new U8Span(path.GetString()), out DirectoryNode dirNode);
        if (res.IsFailure()) return res.Miss();

        outDirectory.Reset(new MemoryDirectory(dirNode, mode));
        return Result.Success;
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        Result res = FsTable.GetFile(new U8Span(path.GetString()), out FileNode fileNode);
        if (res.IsFailure()) return res.Miss();

        outFile.Reset(new MemoryFile(mode, fileNode.File));

        return Result.Success;
    }

    protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
    {
        return FsTable.RenameDirectory(new U8Span(currentPath.GetString()), new U8Span(newPath.GetString()));
    }

    protected override Result DoRenameFile(in Path currentPath, in Path newPath)
    {
        return FsTable.RenameFile(new U8Span(currentPath.GetString()), new U8Span(newPath.GetString()));
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out entryType);

        if (FsTable.GetFile(new U8Span(path.GetString()), out _).IsSuccess())
        {
            entryType = DirectoryEntryType.File;
            return Result.Success;
        }

        if (FsTable.GetDirectory(new U8Span(path.GetString()), out _).IsSuccess())
        {
            entryType = DirectoryEntryType.Directory;
            return Result.Success;
        }

        return ResultFs.PathNotFound.Log();
    }

    protected override Result DoCommit()
    {
        return Result.Success;
    }

    protected override Result DoGetFileAttributes(out NxFileAttributes attributes, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out attributes);

        if (FsTable.GetFile(new U8Span(path.GetString()), out FileNode file).IsSuccess())
        {
            attributes = file.Attributes;
            return Result.Success;
        }

        if (FsTable.GetDirectory(new U8Span(path.GetString()), out DirectoryNode dir).IsSuccess())
        {
            attributes = dir.Attributes;
            return Result.Success;
        }

        return ResultFs.PathNotFound.Log();
    }

    protected override Result DoSetFileAttributes(in Path path, NxFileAttributes attributes)
    {
        if (FsTable.GetFile(new U8Span(path.GetString()), out FileNode file).IsSuccess())
        {
            file.Attributes = attributes;
            return Result.Success;
        }

        if (FsTable.GetDirectory(new U8Span(path.GetString()), out DirectoryNode dir).IsSuccess())
        {
            dir.Attributes = attributes;
            return Result.Success;
        }

        return ResultFs.PathNotFound.Log();
    }

    protected override Result DoGetFileSize(out long fileSize, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out fileSize);

        if (FsTable.GetFile(new U8Span(path.GetString()), out FileNode file).IsSuccess())
        {
            return file.File.GetSize(out fileSize);
        }

        return ResultFs.PathNotFound.Log();
    }

    // todo: Make a more generic MemoryFile-type class
    private class MemoryFile : IFile
    {
        private OpenMode Mode { get; }
        private MemoryStreamAccessor BaseStream { get; }

        public MemoryFile(OpenMode mode, MemoryStreamAccessor buffer)
        {
            BaseStream = buffer;
            Mode = mode;
        }

        protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination,
            in ReadOption option)
        {
            if (!Mode.HasFlag(OpenMode.Read))
            {
                bytesRead = 0;
                return ResultFs.ReadUnpermitted.Log();
            }

            return BaseStream.Read(out bytesRead, offset, destination);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
        {
            if (!Mode.HasFlag(OpenMode.Write))
            {
                return ResultFs.WriteUnpermitted.Log();
            }

            return BaseStream.Write(offset, source, Mode.HasFlag(OpenMode.AllowAppend));
        }

        protected override Result DoFlush()
        {
            return BaseStream.Flush();
        }

        protected override Result DoSetSize(long size)
        {
            return BaseStream.SetSize(size);
        }

        protected override Result DoGetSize(out long size)
        {
            return BaseStream.GetSize(out size);
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
        {
            throw new NotImplementedException();
        }
    }

    private class MemoryDirectory : IDirectory
    {
        private OpenDirectoryMode Mode { get; }
        private DirectoryNode Directory { get; }
        private DirectoryNode CurrentDir { get; set; }
        private FileNode CurrentFile { get; set; }

        public MemoryDirectory(DirectoryNode directory, OpenDirectoryMode mode)
        {
            Mode = mode;
            Directory = directory;
            CurrentDir = directory.ChildDirectory;
            CurrentFile = directory.ChildFile;
        }

        protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            int i = 0;

            if (Mode.HasFlag(OpenDirectoryMode.Directory))
            {
                while (CurrentDir != null && i < entryBuffer.Length)
                {
                    ref DirectoryEntry entry = ref entryBuffer[i];

                    StringUtils.Copy(entry.Name, CurrentDir.Name);
                    entry.Name[PathTool.EntryNameLengthMax] = 0;

                    entry.Type = DirectoryEntryType.Directory;
                    entry.Attributes = CurrentDir.Attributes;
                    entry.Size = 0;

                    i++;
                    CurrentDir = CurrentDir.Next;
                }
            }

            if (Mode.HasFlag(OpenDirectoryMode.File))
            {
                while (CurrentFile != null && i < entryBuffer.Length)
                {
                    ref DirectoryEntry entry = ref entryBuffer[i];

                    StringUtils.Copy(entry.Name, CurrentFile.Name);
                    entry.Name[PathTool.EntryNameLengthMax] = 0;

                    entry.Type = DirectoryEntryType.File;
                    entry.Attributes = CurrentFile.Attributes;

                    Result res = CurrentFile.File.GetSize(out entry.Size);
                    if (res.IsFailure())
                    {
                        entriesRead = 0;
                        return res;
                    }

                    i++;
                    CurrentFile = CurrentFile.Next;
                }
            }

            entriesRead = i;

            return Result.Success;
        }

        protected override Result DoGetEntryCount(out long entryCount)
        {
            long count = 0;

            if (Mode.HasFlag(OpenDirectoryMode.Directory))
            {
                DirectoryNode current = Directory.ChildDirectory;

                while (current != null)
                {
                    count++;
                    current = current.Next;
                }
            }

            if (Mode.HasFlag(OpenDirectoryMode.File))
            {
                FileNode current = Directory.ChildFile;

                while (current != null)
                {
                    count++;
                    current = current.Next;
                }
            }

            entryCount = count;
            return Result.Success;
        }
    }

    // todo: Replace with a class that uses multiple byte arrays as backing memory
    // so resizing doesn't involve copies
    /// <summary>
    /// Provides exclusive access to a <see cref="MemoryStream"/> object.
    /// Used by <see cref="MemoryFile"/> to enable opening a file multiple times with differing permissions.
    /// </summary>
    private class MemoryStreamAccessor
    {
        private const int MemStreamMaxLength = int.MaxValue;

        private MemoryStream BaseStream { get; }
        private object Locker { get; } = new object();

        public MemoryStreamAccessor(MemoryStream stream)
        {
            BaseStream = stream;
        }

        public Result Read(out long bytesRead, long offset, Span<byte> destination)
        {
            lock (Locker)
            {
                if (offset > BaseStream.Length)
                {
                    bytesRead = 0;
                    return ResultFs.OutOfRange.Log();
                }

                BaseStream.Position = offset;

                bytesRead = BaseStream.Read(destination);
                return Result.Success;
            }
        }

        public Result Write(long offset, ReadOnlySpan<byte> source, bool allowAppend)
        {
            lock (Locker)
            {
                if (offset + source.Length > BaseStream.Length)
                {
                    if (!allowAppend)
                    {
                        return ResultFs.FileExtensionWithoutOpenModeAllowAppend.Log();
                    }

                    if (offset + source.Length > MemStreamMaxLength)
                        return ResultFs.OutOfRange.Log();
                }

                BaseStream.Position = offset;

                BaseStream.Write(source);
                return Result.Success;
            }
        }

        public Result Flush()
        {
            return Result.Success;
        }

        public Result SetSize(long size)
        {
            lock (Locker)
            {
                if (size > MemStreamMaxLength)
                    return ResultFs.OutOfRange.Log();

                BaseStream.SetLength(size);

                return Result.Success;
            }
        }

        public Result GetSize(out long size)
        {
            size = BaseStream.Length;
            return Result.Success;
        }
    }

    private class FileNode
    {
        public MemoryStreamAccessor File { get; set; }
        public DirectoryNode Parent { get; set; }
        public U8String Name { get; set; }
        public NxFileAttributes Attributes { get; set; }
        public FileNode Next { get; set; }
    }

    private class DirectoryNode
    {
        private NxFileAttributes _attributes = NxFileAttributes.Directory;

        public NxFileAttributes Attributes
        {
            get => _attributes;
            set => _attributes = value | NxFileAttributes.Directory;
        }

        public DirectoryNode Parent { get; set; }
        public U8String Name { get; set; }
        public DirectoryNode Next { get; set; }
        public DirectoryNode ChildDirectory { get; set; }
        public FileNode ChildFile { get; set; }
    }

    private class FileTable
    {
        private DirectoryNode _root;

        public FileTable()
        {
            _root = new DirectoryNode();
            _root.Name = new U8String("");
        }

        public Result AddFile(U8Span path)
        {
            var parentPath = new U8Span(PathTools.GetParentDirectory(path));

            Result res = FindDirectory(parentPath, out DirectoryNode parent);
            if (res.IsFailure()) return res.Miss();
            var fileName = new U8Span(PathTools.GetLastSegment(path));


            return AddFile(fileName, parent);
        }

        public Result AddDirectory(U8Span path)
        {
            var parentPath = new U8Span(PathTools.GetParentDirectory(path));

            Result res = FindDirectory(parentPath, out DirectoryNode parent);
            if (res.IsFailure()) return res.Miss();

            var dirName = new U8Span(PathTools.GetLastSegment(path));

            return AddDirectory(dirName, parent);
        }

        public Result GetFile(U8Span path, out FileNode file)
        {
            return FindFile(path, out file);
        }

        public Result GetDirectory(U8Span path, out DirectoryNode dir)
        {
            return FindDirectory(path, out dir);
        }

        public Result RenameDirectory(U8Span oldPath, U8Span newPath)
        {
            Result res = FindDirectory(oldPath, out DirectoryNode directory);
            if (res.IsFailure()) return res.Miss();

            var newParentPath = new U8Span(PathTools.GetParentDirectory(newPath));

            res = FindDirectory(newParentPath, out DirectoryNode newParent);
            if (res.IsFailure()) return res.Miss();

            var newName = new U8Span(PathTools.GetLastSegment(newPath));

            if (TryFindChildDirectory(newName, newParent, out _) || TryFindChildFile(newName, newParent, out _))
            {
                return ResultFs.PathAlreadyExists.Log();
            }

            if (directory.Parent != newParent)
            {
                if (!UnlinkDirectory(directory))
                {
                    return ResultFs.PreconditionViolation.Log();
                }

                LinkDirectory(directory, newParent);
            }

            if (StringUtils.Compare(directory.Name, newName) != 0)
            {
                directory.Name = newName.ToU8String();
            }

            return Result.Success;
        }

        public Result RenameFile(U8Span oldPath, U8Span newPath)
        {
            Result res = FindFile(oldPath, out FileNode file);
            if (res.IsFailure()) return res.Miss();

            var newParentPath = new U8Span(PathTools.GetParentDirectory(newPath));

            res = FindDirectory(newParentPath, out DirectoryNode newParent);
            if (res.IsFailure()) return res.Miss();

            var newName = new U8Span(PathTools.GetLastSegment(newPath));

            if (TryFindChildDirectory(newName, newParent, out _) || TryFindChildFile(newName, newParent, out _))
            {
                return ResultFs.PathAlreadyExists.Log();
            }

            if (file.Parent != newParent)
            {
                if (!UnlinkFile(file))
                {
                    return ResultFs.PreconditionViolation.Log();
                }

                LinkFile(file, newParent);
            }


            if (StringUtils.Compare(file.Name, newName) != 0)
            {
                file.Name = newName.ToU8String();
            }

            return Result.Success;
        }

        public Result DeleteDirectory(U8Span path, bool recursive)
        {
            Result res = FindDirectory(path, out DirectoryNode directory);
            if (res.IsFailure()) return res.Miss();

            if (!recursive && (directory.ChildDirectory != null || directory.ChildFile != null))
            {
                return ResultFs.DirectoryNotEmpty.Log();
            }

            UnlinkDirectory(directory);
            return Result.Success;
        }

        public Result DeleteFile(U8Span path)
        {
            Result res = FindFile(path, out FileNode file);
            if (res.IsFailure()) return res.Miss();

            UnlinkFile(file);
            return Result.Success;
        }

        public Result CleanDirectory(U8Span path)
        {
            Result res = FindDirectory(path, out DirectoryNode directory);
            if (res.IsFailure()) return res.Miss();

            directory.ChildDirectory = null;
            directory.ChildFile = null;

            return Result.Success;
        }

        private Result AddFile(U8Span name, DirectoryNode parent)
        {
            if (TryFindChildDirectory(name, parent, out _))
            {
                return ResultFs.PathAlreadyExists.Log();
            }

            if (TryFindChildFile(name, parent, out _))
            {
                return ResultFs.PathAlreadyExists.Log();
            }

            var newFileNode = new FileNode
            {
                Name = name.ToU8String(),
                File = new MemoryStreamAccessor(new MemoryStream())
            };

            LinkFile(newFileNode, parent);
            return Result.Success;
        }

        private Result AddDirectory(U8Span name, DirectoryNode parent)
        {
            if (TryFindChildDirectory(name, parent, out _))
            {
                return ResultFs.PathAlreadyExists.Log();
            }

            if (TryFindChildFile(name, parent, out _))
            {
                return ResultFs.PathAlreadyExists.Log();
            }

            var newDirNode = new DirectoryNode { Name = name.ToU8String() };

            LinkDirectory(newDirNode, parent);
            return Result.Success;
        }

        private Result FindFile(U8Span path, out FileNode file)
        {
            var parentPath = new U8Span(PathTools.GetParentDirectory(path));

            Result res = FindDirectory(parentPath, out DirectoryNode parentNode);
            if (res.IsFailure())
            {
                UnsafeHelpers.SkipParamInit(out file);
                return res;
            }

            var fileName = new U8Span(PathTools.GetLastSegment(path));

            if (TryFindChildFile(fileName, parentNode, out file))
            {
                return Result.Success;
            }

            return ResultFs.PathNotFound.Log();
        }

        private Result FindDirectory(U8Span path, out DirectoryNode directory)
        {
            var parser = new PathParser(path);
            DirectoryNode current = _root;

            while (parser.MoveNext())
            {
                var currentDir = new U8Span(parser.GetCurrent());

                // End if we've hit a trailing separator
                if (currentDir.IsEmpty() && parser.IsFinished()) break;

                if (!TryFindChildDirectory(currentDir, current, out DirectoryNode child))
                {
                    UnsafeHelpers.SkipParamInit(out directory);
                    return ResultFs.PathNotFound.Log();
                }

                current = child;
            }

            directory = current;
            return Result.Success;
        }

        private bool TryFindChildDirectory(U8Span name, DirectoryNode parent, out DirectoryNode child)
        {
            DirectoryNode currentChild = parent.ChildDirectory;

            while (currentChild != null)
            {
                if (StringUtils.Compare(name, currentChild.Name) == 0)
                {
                    child = currentChild;
                    return true;
                }

                currentChild = currentChild.Next;
            }

            UnsafeHelpers.SkipParamInit(out child);
            return false;
        }

        private bool TryFindChildFile(U8Span name, DirectoryNode parent, out FileNode child)
        {
            FileNode currentChild = parent.ChildFile;

            while (currentChild != null)
            {
                if (StringUtils.Compare(name, currentChild.Name) == 0)
                {
                    child = currentChild;
                    return true;
                }

                currentChild = currentChild.Next;
            }

            UnsafeHelpers.SkipParamInit(out child);
            return false;
        }

        private void LinkDirectory(DirectoryNode dir, DirectoryNode parentDir)
        {
            Debug.Assert(dir.Parent == null);
            Debug.Assert(dir.Next == null);

            dir.Next = parentDir.ChildDirectory;
            dir.Parent = parentDir;
            parentDir.ChildDirectory = dir;
        }

        private bool UnlinkDirectory(DirectoryNode dir)
        {
            Debug.Assert(dir.Parent != null);

            DirectoryNode parent = dir.Parent;

            if (parent.ChildDirectory == null)
                return false;

            if (parent.ChildDirectory == dir)
            {
                parent.ChildDirectory = dir.Next;
                dir.Parent = null;

                return true;
            }

            DirectoryNode current = parent.ChildDirectory;

            while (current != null)
            {
                if (current.Next == dir)
                {
                    current.Next = dir.Next;
                    dir.Parent = null;

                    return true;
                }

                current = current.Next;
            }

            return false;
        }

        private void LinkFile(FileNode file, DirectoryNode parentDir)
        {
            Debug.Assert(file.Parent == null);
            Debug.Assert(file.Next == null);

            file.Next = parentDir.ChildFile;
            file.Parent = parentDir;
            parentDir.ChildFile = file;
        }

        private bool UnlinkFile(FileNode file)
        {
            Debug.Assert(file.Parent != null);

            DirectoryNode parent = file.Parent;

            if (parent.ChildFile == null)
                return false;

            if (parent.ChildFile == file)
            {
                parent.ChildFile = file.Next;
                file.Parent = null;

                return true;
            }

            FileNode current = parent.ChildFile;

            while (current != null)
            {
                if (current.Next == file)
                {
                    current.Next = file.Next;
                    file.Parent = null;

                    return true;
                }

                current = current.Next;
            }

            return false;
        }
    }
}