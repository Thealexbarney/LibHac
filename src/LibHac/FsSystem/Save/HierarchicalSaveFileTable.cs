using System;
using System.IO;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSystem.Save
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

        public bool TryOpenFile(U8Span path, out SaveFileInfo fileInfo)
        {
            if (!FindPathRecursive(path, out SaveEntryKey key))
            {
                UnsafeHelpers.SkipParamInit(out fileInfo);
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
                UnsafeHelpers.SkipParamInit(out info, out name);
                return false;
            }

            Span<byte> nameBytes = stackalloc byte[FileTable.MaxNameLength];

            bool success = FileTable.TryGetValue(position.NextFile, out TableEntry<SaveFileInfo> entry, ref nameBytes);

            // todo error message
            if (!success)
            {
                UnsafeHelpers.SkipParamInit(out info, out name);
                return false;
            }

            position.NextFile = entry.NextSibling;
            info = entry.Value;

            name = StringUtils.NullTerminatedUtf8ToString(nameBytes);

            return true;
        }

        public bool FindNextDirectory(ref SaveFindPosition position, out string name)
        {
            if (position.NextDirectory == 0)
            {
                UnsafeHelpers.SkipParamInit(out name);
                return false;
            }

            Span<byte> nameBytes = stackalloc byte[DirectoryTable.MaxNameLength];

            bool success = DirectoryTable.TryGetValue(position.NextDirectory, out TableEntry<SaveFindPosition> entry, ref nameBytes);

            // todo error message
            if (!success)
            {
                UnsafeHelpers.SkipParamInit(out name);
                return false;
            }

            position.NextDirectory = entry.NextSibling;

            name = StringUtils.NullTerminatedUtf8ToString(nameBytes);

            return true;
        }

        public void AddFile(U8Span path, ref SaveFileInfo fileInfo)
        {
            if (path.Length == 1 && path[0] == '/') throw new ArgumentException("Path cannot be empty");

            CreateFileRecursive(path, ref fileInfo);
        }

        public void AddDirectory(U8Span path)
        {
            if (path.Length == 1 && path[0] == '/') throw new ArgumentException("Path cannot be empty");

            CreateDirectoryRecursive(path);
        }

        private void CreateFileRecursive(ReadOnlySpan<byte> path, ref SaveFileInfo fileInfo)
        {
            var parser = new PathParser(path);
            var key = new SaveEntryKey(parser.GetCurrent(), 0);

            int parentIndex = CreateParentDirectoryRecursive(ref parser, ref key);

            int index = FileTable.GetIndexFromKey(ref key).Index;
            var fileEntry = new TableEntry<SaveFileInfo>();

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
            var dirEntry = new TableEntry<SaveFindPosition>();

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

        public void DeleteFile(U8Span path)
        {
            FindPathRecursive(path, out SaveEntryKey key);
            int parentIndex = key.Parent;

            int toDeleteIndex = FileTable.GetIndexFromKey(ref key).Index;
            if (toDeleteIndex < 0) throw new FileNotFoundException();

            UnlinkFileFromParent(parentIndex, toDeleteIndex);

            FileTable.Remove(ref key);
        }

        public void DeleteDirectory(U8Span path)
        {
            FindPathRecursive(path, out SaveEntryKey key);
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

        public void RenameFile(U8Span srcPath, U8Span dstPath)
        {
            if (srcPath.Value == dstPath.Value || TryOpenFile(dstPath, out _) || TryOpenDirectory(dstPath, out _))
            {
                throw new IOException("Destination path already exists.");
            }

            if (!FindPathRecursive(srcPath, out SaveEntryKey oldKey))
            {
                throw new FileNotFoundException();
            }

            int fileIndex = FileTable.GetIndexFromKey(ref oldKey).Index;

            if (!FindPathRecursive(dstPath, out SaveEntryKey newKey))
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

        public Result RenameDirectory(U8Span srcPath, U8Span dstPath)
        {
            if (srcPath.Value == dstPath.Value || TryOpenFile(dstPath, out _) || TryOpenDirectory(dstPath, out _))
            {
                return ResultFs.PathAlreadyExists.Log();
            }

            if (!FindPathRecursive(srcPath, out SaveEntryKey oldKey))
            {
                return ResultFs.PathNotFound.Log();
            }

            int dirIndex = DirectoryTable.GetIndexFromKey(ref oldKey).Index;

            if (!FindPathRecursive(dstPath, out SaveEntryKey newKey))
            {
                return ResultFs.PathNotFound.Log();
            }

            if (PathTools.IsSubPath(srcPath, dstPath))
            {
                return ResultFs.DirectoryNotRenamable.Log();
            }

            if (oldKey.Parent != newKey.Parent)
            {
                UnlinkDirectoryFromParent(oldKey.Parent, dirIndex);
                LinkDirectoryToParent(newKey.Parent, dirIndex);
            }

            DirectoryTable.ChangeKey(ref oldKey, ref newKey);

            return Result.Success;
        }

        public bool TryOpenDirectory(U8Span path, out SaveFindPosition position)
        {
            UnsafeHelpers.SkipParamInit(out position);

            if (!FindPathRecursive(path, out SaveEntryKey key))
            {
                return false;
            }

            if (DirectoryTable.TryGetValue(ref key, out TableEntry<SaveFindPosition> entry))
            {
                position = entry.Value;
                return true;
            }

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
