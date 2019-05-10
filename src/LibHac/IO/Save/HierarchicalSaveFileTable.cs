using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LibHac.IO.Save
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

            SaveFindPosition emptyDir = default;
            CreateDirectoryRecursive(pathBytes, ref emptyDir);
        }

        private void CreateFileRecursive(ReadOnlySpan<byte> path, ref SaveFileInfo fileInfo)
        {
            var parser = new PathParser(path);
            var key = new SaveEntryKey(parser.GetCurrent(), 0);

            int parentIndex = CreateParentDirectoryRecursive(ref parser, ref key);

            int index = FileTable.GetIndexFromKey(ref key).Index;
            var fileEntry = new TableEntry<SaveFileInfo>();

            if (index < 0)
            {
                index = FileTable.Add(ref key, ref fileEntry);

                DirectoryTable.GetValue(parentIndex, out TableEntry<SaveFindPosition> parentEntry);

                fileEntry.NextSibling = parentEntry.Value.NextFile;
                parentEntry.Value.NextFile = index;

                DirectoryTable.SetValue(parentIndex, ref parentEntry);
            }

            fileEntry.Value = fileInfo;
            FileTable.SetValue(index, ref fileEntry);
        }

        private void CreateDirectoryRecursive(ReadOnlySpan<byte> path, ref SaveFindPosition dirInfo)
        {
            var parser = new PathParser(path);
            var key = new SaveEntryKey(parser.GetCurrent(), 0);

            int parentIndex = CreateParentDirectoryRecursive(ref parser, ref key);

            int index = DirectoryTable.GetIndexFromKey(ref key).Index;
            var dirEntry = new TableEntry<SaveFindPosition>();

            if (index < 0)
            {
                index = DirectoryTable.Add(ref key, ref dirEntry);

                DirectoryTable.GetValue(parentIndex, out TableEntry<SaveFindPosition> parentEntry);

                dirEntry.NextSibling = parentEntry.Value.NextDirectory;
                parentEntry.Value.NextDirectory = index;

                DirectoryTable.SetValue(parentIndex, ref parentEntry);
            }

            dirEntry.Value = dirInfo;
            DirectoryTable.SetValue(index, ref dirEntry);
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
                        DirectoryTable.GetValue(prevIndex, out TableEntry<SaveFindPosition> parentEntry);

                        newEntry.NextSibling = parentEntry.Value.NextDirectory;
                        parentEntry.Value.NextDirectory = index;

                        DirectoryTable.SetValue(prevIndex, ref parentEntry);
                        DirectoryTable.SetValue(index, ref newEntry);
                    }
                }

                prevIndex = index;
                key.Parent = index;
                parser.TryGetNext(out key.Name);
            }

            return prevIndex;
        }

        public void DeleteFile(string path)
        {
            path = PathTools.Normalize(path);
            ReadOnlySpan<byte> pathBytes = Util.GetUtf8Bytes(path);

            FindPathRecursive(pathBytes, out SaveEntryKey key);
            int parentIndex = key.Parent;

            DirectoryTable.GetValue(parentIndex, out TableEntry<SaveFindPosition> parentEntry);

            int toDeleteIndex = FileTable.GetIndexFromKey(ref key).Index;
            if (toDeleteIndex < 0) throw new FileNotFoundException();

            FileTable.GetValue(toDeleteIndex, out TableEntry<SaveFileInfo> toDeleteEntry);

            if (parentEntry.Value.NextFile == toDeleteIndex)
            {
                parentEntry.Value.NextFile = toDeleteEntry.NextSibling;
                DirectoryTable.SetValue(parentIndex, ref parentEntry);
                FileTable.Remove(ref key);
                return;
            }

            int prevIndex = parentEntry.Value.NextFile;
            FileTable.GetValue(prevIndex, out TableEntry<SaveFileInfo> prevEntry);
            int curIndex = prevEntry.NextSibling;

            while (curIndex != 0)
            {
                FileTable.GetValue(curIndex, out TableEntry<SaveFileInfo> curEntry);

                if (curIndex == toDeleteIndex)
                {
                    prevEntry.NextSibling = curEntry.NextSibling;
                    FileTable.SetValue(prevIndex, ref prevEntry);

                    FileTable.Remove(ref key);
                    return;
                }

                prevIndex = curIndex;
                prevEntry = curEntry;
                curIndex = prevEntry.NextSibling;
            }

            throw new FileNotFoundException();
        }

        public void DeleteDirectory(string path)
        {
            path = PathTools.Normalize(path);
            ReadOnlySpan<byte> pathBytes = Util.GetUtf8Bytes(path);

            FindPathRecursive(pathBytes, out SaveEntryKey key);
            int parentIndex = key.Parent;

            DirectoryTable.GetValue(parentIndex, out TableEntry<SaveFindPosition> parentEntry);

            int toDeleteIndex = DirectoryTable.GetIndexFromKey(ref key).Index;
            if (toDeleteIndex < 0) throw new DirectoryNotFoundException();

            DirectoryTable.GetValue(toDeleteIndex, out TableEntry<SaveFindPosition> toDeleteEntry);

            if (toDeleteEntry.Value.NextDirectory != 0 || toDeleteEntry.Value.NextFile != 0)
            {
                throw new IOException("Directory is not empty.");
            }

            if (parentEntry.Value.NextDirectory == toDeleteIndex)
            {
                parentEntry.Value.NextDirectory = toDeleteEntry.NextSibling;
                DirectoryTable.SetValue(parentIndex, ref parentEntry);
                DirectoryTable.Remove(ref key);
                return;
            }

            int prevIndex = parentEntry.Value.NextDirectory;
            DirectoryTable.GetValue(prevIndex, out TableEntry<SaveFindPosition> prevEntry);
            int curIndex = prevEntry.NextSibling;

            while (curIndex != 0)
            {
                DirectoryTable.GetValue(curIndex, out TableEntry<SaveFindPosition> curEntry);

                if (curIndex == toDeleteIndex)
                {
                    prevEntry.NextSibling = curEntry.NextSibling;
                    DirectoryTable.SetValue(prevIndex, ref prevEntry);

                    DirectoryTable.Remove(ref key);
                    return;
                }

                prevIndex = curIndex;
                prevEntry = curEntry;
                curIndex = prevEntry.NextSibling;
            }

            throw new DirectoryNotFoundException();
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
        private struct TableEntry<T> where T : struct
        {
            public int NextSibling;
            public T Value;
        }
    }
}
