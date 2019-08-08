﻿using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LibHac.Fs.Save
{
    public class HierarchicalSaveFileTable
    {
        private SaveFsList<TableEntry<SaveFileInfo>> FileTable { get; }
        private SaveFsList<TableEntry<SaveFindPosition>> DirectoryTable { get; }

        public HierarchicalSaveFileTable(IStorage dirTable, IStorage fileTable)
        {
            FileTable = new SaveFsList<TableEntry<SaveFileInfo>>(fileTable);
            DirectoryTable = new SaveFsList<TableEntry<SaveFindPosition>>(dirTable);
        }

        public bool TryOpenFile(string path, out SaveFileInfo fileInfo)
        {
            if (!FindPathRecursive(Util.GetUtf8Bytes(path), out SaveEntryKey key))
            {
                fileInfo = default;
                return false;
            }

            if (FileTable.TryGetValue(ref key, out TableEntry<SaveFileInfo> value))
            {
                fileInfo = value.Value;
                return true;
            }

            fileInfo = default;
            return false;
        }

        public bool FindNextFile(ref SaveFindPosition position, out SaveFileInfo info, out string name)
        {
            if (position.NextFile == 0)
            {
                info = default;
                name = default;
                return false;
            }

            Span<byte> nameBytes = stackalloc byte[FileTable.MaxNameLength];

            bool success = FileTable.TryGetValue(position.NextFile, out TableEntry<SaveFileInfo> entry, ref nameBytes);

            // todo error message
            if (!success)
            {
                info = default;
                name = default;
                return false;
            }

            position.NextFile = entry.NextSibling;
            info = entry.Value;

            name = Util.GetUtf8StringNullTerminated(nameBytes);

            return true;
        }

        public bool FindNextDirectory(ref SaveFindPosition position, out string name)
        {
            if (position.NextDirectory == 0)
            {
                name = default;
                return false;
            }

            Span<byte> nameBytes = stackalloc byte[DirectoryTable.MaxNameLength];

            bool success = DirectoryTable.TryGetValue(position.NextDirectory, out TableEntry<SaveFindPosition> entry, ref nameBytes);

            // todo error message
            if (!success)
            {
                name = default;
                return false;
            }

            position.NextDirectory = entry.NextSibling;

            name = Util.GetUtf8StringNullTerminated(nameBytes);

            return true;
        }

        public void AddFile(string path, ref SaveFileInfo fileInfo)
        {
            path = PathTools.Normalize(path);
            ReadOnlySpan<byte> pathBytes = Util.GetUtf8Bytes(path);

            if (path == "/") throw new ArgumentException("Path cannot be empty");

            CreateFileRecursive(pathBytes, ref fileInfo);
        }

        public void AddDirectory(string path)
        {
            path = PathTools.Normalize(path);
            ReadOnlySpan<byte> pathBytes = Util.GetUtf8Bytes(path);

            if (path == "/") throw new ArgumentException("Path cannot be empty");

            CreateDirectoryRecursive(pathBytes);
        }

        private void CreateFileRecursive(ReadOnlySpan<byte> path, ref SaveFileInfo fileInfo)
        {
            var parser = new PathParser(path);
            var key = new SaveEntryKey(parser.GetCurrent(), 0);

            int parentIndex = CreateParentDirectoryRecursive(ref parser, ref key);

            int index = FileTable.GetIndexFromKey(ref key).Index;
            TableEntry<SaveFileInfo> fileEntry = default;

            // File already exists. Update file info.
            if (index >= 0)
            {
                FileTable.GetValue(index, out fileEntry);
                fileEntry.Value = fileInfo;
                FileTable.SetValue(index, ref fileEntry);
                return;
            }

            fileEntry.Value = fileInfo;
            index = FileTable.Add(ref key, ref fileEntry);

            LinkFileToParent(parentIndex, index);
        }

        private void CreateDirectoryRecursive(ReadOnlySpan<byte> path)
        {
            var parser = new PathParser(path);
            var key = new SaveEntryKey(parser.GetCurrent(), 0);

            int parentIndex = CreateParentDirectoryRecursive(ref parser, ref key);

            int index = DirectoryTable.GetIndexFromKey(ref key).Index;
            TableEntry<SaveFindPosition> dirEntry = default;

            // Directory already exists. Do nothing.
            if (index >= 0) return;

            index = DirectoryTable.Add(ref key, ref dirEntry);

            LinkDirectoryToParent(parentIndex, index);
        }

        private int CreateParentDirectoryRecursive(ref PathParser parser, ref SaveEntryKey key)
        {
            int prevIndex = 0;

            while (!parser.IsFinished())
            {
                int index = DirectoryTable.GetIndexFromKey(ref key).Index;

                if (index < 0)
                {
                    var newEntry = new TableEntry<SaveFindPosition>();
                    index = DirectoryTable.Add(ref key, ref newEntry);

                    if (prevIndex > 0)
                    {
                        LinkDirectoryToParent(prevIndex, index);
                    }
                }

                prevIndex = index;
                key.Parent = index;
                parser.TryGetNext(out key.Name);
            }

            return prevIndex;
        }

        private void LinkFileToParent(int parentIndex, int fileIndex)
        {
            DirectoryTable.GetValue(parentIndex, out TableEntry<SaveFindPosition> parentEntry);
            FileTable.GetValue(fileIndex, out TableEntry<SaveFileInfo> fileEntry);

            fileEntry.NextSibling = parentEntry.Value.NextFile;
            parentEntry.Value.NextFile = fileIndex;

            DirectoryTable.SetValue(parentIndex, ref parentEntry);
            FileTable.SetValue(fileIndex, ref fileEntry);
        }

        private void LinkDirectoryToParent(int parentIndex, int dirIndex)
        {
            DirectoryTable.GetValue(parentIndex, out TableEntry<SaveFindPosition> parentEntry);
            DirectoryTable.GetValue(dirIndex, out TableEntry<SaveFindPosition> dirEntry);

            dirEntry.NextSibling = parentEntry.Value.NextDirectory;
            parentEntry.Value.NextDirectory = dirIndex;

            DirectoryTable.SetValue(parentIndex, ref parentEntry);
            DirectoryTable.SetValue(dirIndex, ref dirEntry);
        }

        private void UnlinkFileFromParent(int parentIndex, int fileIndex)
        {
            DirectoryTable.GetValue(parentIndex, out TableEntry<SaveFindPosition> parentEntry);
            FileTable.GetValue(fileIndex, out TableEntry<SaveFileInfo> fileEntry);

            if (parentEntry.Value.NextFile == fileIndex)
            {
                parentEntry.Value.NextFile = fileEntry.NextSibling;
                DirectoryTable.SetValue(parentIndex, ref parentEntry);
                return;
            }

            int prevIndex = parentEntry.Value.NextFile;
            FileTable.GetValue(prevIndex, out TableEntry<SaveFileInfo> prevEntry);
            int curIndex = prevEntry.NextSibling;

            while (curIndex != 0)
            {
                FileTable.GetValue(curIndex, out TableEntry<SaveFileInfo> curEntry);

                if (curIndex == fileIndex)
                {
                    prevEntry.NextSibling = curEntry.NextSibling;
                    FileTable.SetValue(prevIndex, ref prevEntry);
                    return;
                }

                prevIndex = curIndex;
                prevEntry = curEntry;
                curIndex = prevEntry.NextSibling;
            }
        }

        private void UnlinkDirectoryFromParent(int parentIndex, int dirIndex)
        {
            DirectoryTable.GetValue(parentIndex, out TableEntry<SaveFindPosition> parentEntry);
            DirectoryTable.GetValue(dirIndex, out TableEntry<SaveFindPosition> dirEntry);

            if (parentEntry.Value.NextDirectory == dirIndex)
            {
                parentEntry.Value.NextDirectory = dirEntry.NextSibling;
                DirectoryTable.SetValue(parentIndex, ref parentEntry);
                return;
            }

            int prevIndex = parentEntry.Value.NextDirectory;
            DirectoryTable.GetValue(prevIndex, out TableEntry<SaveFindPosition> prevEntry);
            int curIndex = prevEntry.NextSibling;

            while (curIndex != 0)
            {
                DirectoryTable.GetValue(curIndex, out TableEntry<SaveFindPosition> curEntry);

                if (curIndex == dirIndex)
                {
                    prevEntry.NextSibling = curEntry.NextSibling;
                    DirectoryTable.SetValue(prevIndex, ref prevEntry);
                    return;
                }

                prevIndex = curIndex;
                prevEntry = curEntry;
                curIndex = prevEntry.NextSibling;
            }
        }

        public void DeleteFile(string path)
        {
            path = PathTools.Normalize(path);
            ReadOnlySpan<byte> pathBytes = Util.GetUtf8Bytes(path);

            FindPathRecursive(pathBytes, out SaveEntryKey key);
            int parentIndex = key.Parent;

            int toDeleteIndex = FileTable.GetIndexFromKey(ref key).Index;
            if (toDeleteIndex < 0) throw new FileNotFoundException();

            UnlinkFileFromParent(parentIndex, toDeleteIndex);

            FileTable.Remove(ref key);
        }

        public void DeleteDirectory(string path)
        {
            path = PathTools.Normalize(path);
            ReadOnlySpan<byte> pathBytes = Util.GetUtf8Bytes(path);

            FindPathRecursive(pathBytes, out SaveEntryKey key);
            int parentIndex = key.Parent;

            int toDeleteIndex = DirectoryTable.GetIndexFromKey(ref key).Index;
            if (toDeleteIndex < 0) throw new DirectoryNotFoundException();

            DirectoryTable.GetValue(toDeleteIndex, out TableEntry<SaveFindPosition> toDeleteEntry);

            if (toDeleteEntry.Value.NextDirectory != 0 || toDeleteEntry.Value.NextFile != 0)
            {
                throw new IOException("Directory is not empty.");
            }

            UnlinkDirectoryFromParent(parentIndex, toDeleteIndex);

            DirectoryTable.Remove(ref key);
        }

        public void RenameFile(string srcPath, string dstPath)
        {
            if (srcPath == dstPath || TryOpenFile(dstPath, out _) || TryOpenDirectory(dstPath, out _))
            {
                throw new IOException("Destination path already exists.");
            }

            ReadOnlySpan<byte> oldPathBytes = Util.GetUtf8Bytes(srcPath);
            ReadOnlySpan<byte> newPathBytes = Util.GetUtf8Bytes(dstPath);

            if (!FindPathRecursive(oldPathBytes, out SaveEntryKey oldKey))
            {
                throw new FileNotFoundException();
            }

            int fileIndex = FileTable.GetIndexFromKey(ref oldKey).Index;

            if (!FindPathRecursive(newPathBytes, out SaveEntryKey newKey))
            {
                throw new FileNotFoundException();
            }

            if (oldKey.Parent != newKey.Parent)
            {
                UnlinkFileFromParent(oldKey.Parent, fileIndex);
                LinkFileToParent(newKey.Parent, fileIndex);
            }

            FileTable.ChangeKey(ref oldKey, ref newKey);
        }

        public void RenameDirectory(string srcPath, string dstPath)
        {
            if (srcPath == dstPath || TryOpenFile(dstPath, out _) || TryOpenDirectory(dstPath, out _))
            {
                throw new IOException(Messages.DestPathAlreadyExists);
            }

            ReadOnlySpan<byte> oldPathBytes = Util.GetUtf8Bytes(srcPath);
            ReadOnlySpan<byte> newPathBytes = Util.GetUtf8Bytes(dstPath);

            if (!FindPathRecursive(oldPathBytes, out SaveEntryKey oldKey))
            {
                throw new DirectoryNotFoundException();
            }

            int dirIndex = DirectoryTable.GetIndexFromKey(ref oldKey).Index;

            if (!FindPathRecursive(newPathBytes, out SaveEntryKey newKey))
            {
                throw new IOException(Messages.PartialPathNotFound);
            }

            if (PathTools.IsSubPath(oldPathBytes, newPathBytes))
            {
                ThrowHelper.ThrowResult(ResultFs.DestinationIsSubPathOfSource);
            }

            if (oldKey.Parent != newKey.Parent)
            {
                UnlinkDirectoryFromParent(oldKey.Parent, dirIndex);
                LinkDirectoryToParent(newKey.Parent, dirIndex);
            }

            DirectoryTable.ChangeKey(ref oldKey, ref newKey);
        }

        public bool TryOpenDirectory(string path, out SaveFindPosition position)
        {
            if (!FindPathRecursive(Util.GetUtf8Bytes(path), out SaveEntryKey key))
            {
                position = default;
                return false;
            }

            if (DirectoryTable.TryGetValue(ref key, out TableEntry<SaveFindPosition> entry))
            {
                position = entry.Value;
                return true;
            }

            position = default;
            return false;
        }

        private bool FindPathRecursive(ReadOnlySpan<byte> path, out SaveEntryKey key)
        {
            var parser = new PathParser(path);
            key = new SaveEntryKey(parser.GetCurrent(), 0);

            while (!parser.IsFinished())
            {
                key.Parent = DirectoryTable.GetIndexFromKey(ref key).Index;

                if (key.Parent < 0) return false;

                parser.TryGetNext(out key.Name);
            }

            return true;
        }

        public void TrimFreeEntries()
        {
            DirectoryTable.TrimFreeEntries();
            FileTable.TrimFreeEntries();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        // todo: Change constraint to "unmanaged" after updating to
        // a newer SDK https://github.com/dotnet/csharplang/issues/1937
        private struct TableEntry<T> where T : struct
        {
            public int NextSibling;
            public T Value;
        }
    }
}
