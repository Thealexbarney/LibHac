using System;
using System.Diagnostics;
using System.IO;
using LibHac.Common;
using LibHac.FsSystem;

namespace LibHac.Fs
{
    /// <summary>
    /// A filesystem stored in-memory. Mainly used for testing.
    /// </summary>
    public class InMemoryFileSystem : AttributeFileSystemBase
    {
        private FileTable FsTable { get; }

        public InMemoryFileSystem()
        {
            FsTable = new FileTable();
        }

        protected override Result CreateDirectoryImpl(U8Span path)
        {
            return FsTable.AddDirectory(new U8Span(path));
        }

        protected override Result CreateDirectoryImpl(U8Span path, NxFileAttributes archiveAttribute)
        {
            Result rc = FsTable.AddDirectory(path);
            if (rc.IsFailure()) return rc;

            rc = FsTable.GetDirectory(path, out DirectoryNode dir);
            if (rc.IsFailure()) return rc;

            dir.Attributes = archiveAttribute;
            return Result.Success;
        }

        protected override Result CreateFileImpl(U8Span path, long size, CreateFileOptions options)
        {
            Result rc = FsTable.AddFile(path);
            if (rc.IsFailure()) return rc;

            rc = FsTable.GetFile(path, out FileNode file);
            if (rc.IsFailure()) return rc;

            return file.File.SetSize(size);
        }

        protected override Result DeleteDirectoryImpl(U8Span path)
        {
            return FsTable.DeleteDirectory(new U8Span(path), false);
        }

        protected override Result DeleteDirectoryRecursivelyImpl(U8Span path)
        {
            return FsTable.DeleteDirectory(new U8Span(path), true);
        }

        protected override Result CleanDirectoryRecursivelyImpl(U8Span path)
        {
            return FsTable.CleanDirectory(new U8Span(path));
        }

        protected override Result DeleteFileImpl(U8Span path)
        {
            return FsTable.DeleteFile(new U8Span(path));
        }

        protected override Result OpenDirectoryImpl(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            directory = default;

            Result rs = FsTable.GetDirectory(new U8Span(path), out DirectoryNode dirNode);
            if (rs.IsFailure()) return rs;

            directory = new MemoryDirectory(dirNode, mode);
            return Result.Success;
        }

        protected override Result OpenFileImpl(out IFile file, U8Span path, OpenMode mode)
        {
            file = default;

            Result rc = FsTable.GetFile(path, out FileNode fileNode);
            if (rc.IsFailure()) return rc;

            file = new MemoryFile(mode, fileNode.File);

            return Result.Success;
        }

        protected override Result RenameDirectoryImpl(U8Span oldPath, U8Span newPath)
        {
            return FsTable.RenameDirectory(new U8Span(oldPath), new U8Span(newPath));
        }

        protected override Result RenameFileImpl(U8Span oldPath, U8Span newPath)
        {
            return FsTable.RenameFile(new U8Span(oldPath), new U8Span(newPath));
        }

        protected override Result GetEntryTypeImpl(out DirectoryEntryType entryType, U8Span path)
        {
            if (FsTable.GetFile(path, out _).IsSuccess())
            {
                entryType = DirectoryEntryType.File;
                return Result.Success;
            }

            if (FsTable.GetDirectory(path, out _).IsSuccess())
            {
                entryType = DirectoryEntryType.Directory;
                return Result.Success;
            }

            entryType = default;
            return ResultFs.PathNotFound.Log();
        }

        protected override Result CommitImpl()
        {
            return Result.Success;
        }

        protected override Result GetFileAttributesImpl(out NxFileAttributes attributes, U8Span path)
        {
            if (FsTable.GetFile(path, out FileNode file).IsSuccess())
            {
                attributes = file.Attributes;
                return Result.Success;
            }

            if (FsTable.GetDirectory(path, out DirectoryNode dir).IsSuccess())
            {
                attributes = dir.Attributes;
                return Result.Success;
            }

            attributes = default;
            return ResultFs.PathNotFound.Log();
        }

        protected override Result SetFileAttributesImpl(U8Span path, NxFileAttributes attributes)
        {
            if (FsTable.GetFile(path, out FileNode file).IsSuccess())
            {
                file.Attributes = attributes;
                return Result.Success;
            }

            if (FsTable.GetDirectory(path, out DirectoryNode dir).IsSuccess())
            {
                dir.Attributes = attributes;
                return Result.Success;
            }

            return ResultFs.PathNotFound.Log();
        }

        protected override Result GetFileSizeImpl(out long fileSize, U8Span path)
        {
            if (FsTable.GetFile(path, out FileNode file).IsSuccess())
            {
                return file.File.GetSize(out fileSize);
            }

            fileSize = default;
            return ResultFs.PathNotFound.Log();
        }

        // todo: Make a more generic MemoryFile-type class
        private class MemoryFile : FileBase
        {
            private OpenMode Mode { get; }
            private MemoryStreamAccessor BaseStream { get; }

            public MemoryFile(OpenMode mode, MemoryStreamAccessor buffer)
            {
                BaseStream = buffer;
                Mode = mode;
            }

            protected override Result ReadImpl(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
            {
                if (!Mode.HasFlag(OpenMode.Read))
                {
                    bytesRead = 0;
                    return ResultFs.InvalidOpenModeForRead.Log();
                }

                return BaseStream.Read(out bytesRead, offset, destination);
            }

            protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source, WriteOption options)
            {
                if (!Mode.HasFlag(OpenMode.Write))
                {
                    return ResultFs.InvalidOpenModeForWrite.Log();
                }

                return BaseStream.Write(offset, source, Mode.HasFlag(OpenMode.AllowAppend));
            }

            protected override Result FlushImpl()
            {
                return BaseStream.Flush();
            }

            protected override Result SetSizeImpl(long size)
            {
                return BaseStream.SetSize(size);
            }

            protected override Result GetSizeImpl(out long size)
            {
                return BaseStream.GetSize(out size);
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

            public Result Read(out long entriesRead, Span<DirectoryEntry> entryBuffer)
            {
                int i = 0;

                if (Mode.HasFlag(OpenDirectoryMode.Directory))
                {
                    while (CurrentDir != null && i < entryBuffer.Length)
                    {
                        ref DirectoryEntry entry = ref entryBuffer[i];

                        StringUtils.Copy(entry.Name, CurrentDir.Name);
                        entry.Name[PathTools.MaxPathLength] = 0;

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
                        entry.Name[PathTools.MaxPathLength] = 0;

                        entry.Type = DirectoryEntryType.File;
                        entry.Attributes = CurrentFile.Attributes;
                        CurrentFile.File.GetSize(out entry.Size);

                        i++;
                        CurrentFile = CurrentFile.Next;
                    }
                }

                entriesRead = i;

                return Result.Success;
            }

            public Result GetEntryCount(out long entryCount)
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
                        bytesRead = default;
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
            private DirectoryNode Root;

            public FileTable()
            {
                Root = new DirectoryNode();
                Root.Name = new U8String("");
            }

            public Result AddFile(U8Span path)
            {
                var parentPath = new U8Span(PathTools.GetParentDirectory(path));

                Result rc = FindDirectory(parentPath, out DirectoryNode parent);
                if (rc.IsFailure()) return rc;
                var fileName = new U8Span(PathTools.GetLastSegment(path));


                return AddFile(fileName, parent);
            }

            public Result AddDirectory(U8Span path)
            {
                var parentPath = new U8Span(PathTools.GetParentDirectory(path));

                Result rc = FindDirectory(parentPath, out DirectoryNode parent);
                if (rc.IsFailure()) return rc;

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
                Result rc = FindDirectory(oldPath, out DirectoryNode directory);
                if (rc.IsFailure()) return rc;

                var newParentPath = new U8Span(PathTools.GetParentDirectory(newPath));

                rc = FindDirectory(newParentPath, out DirectoryNode newParent);
                if (rc.IsFailure()) return rc;

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
                Result rc = FindFile(oldPath, out FileNode file);
                if (rc.IsFailure()) return rc;

                var newParentPath = new U8Span(PathTools.GetParentDirectory(newPath));

                rc = FindDirectory(newParentPath, out DirectoryNode newParent);
                if (rc.IsFailure()) return rc;

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
                Result rc = FindDirectory(path, out DirectoryNode directory);
                if (rc.IsFailure()) return rc;

                if (!recursive && (directory.ChildDirectory != null || directory.ChildFile != null))
                {
                    return ResultFs.DirectoryNotEmpty.Log();
                }

                UnlinkDirectory(directory);
                return Result.Success;
            }

            public Result DeleteFile(U8Span path)
            {
                Result rc = FindFile(path, out FileNode file);
                if (rc.IsFailure()) return rc;

                UnlinkFile(file);
                return Result.Success;
            }

            public Result CleanDirectory(U8Span path)
            {
                Result rc = FindDirectory(path, out DirectoryNode directory);
                if (rc.IsFailure()) return rc;

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

                Result rc = FindDirectory(parentPath, out DirectoryNode parentNode);
                if (rc.IsFailure())
                {
                    file = default;
                    return rc;
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
                DirectoryNode current = Root;

                while (parser.MoveNext())
                {
                    var currentDir = new U8Span(parser.GetCurrent());

                    // End if we've hit a trailing separator
                    if (currentDir.IsEmpty() && parser.IsFinished()) break;

                    if (!TryFindChildDirectory(currentDir, current, out DirectoryNode child))
                    {
                        directory = default;
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

                child = default;
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

                child = default;
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
}
