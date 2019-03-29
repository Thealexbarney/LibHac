using System;
using System.Runtime.InteropServices;

namespace LibHac.IO.Save
{
    public class HierarchicalSaveFileTable
    {
        private SaveFsList<FileSaveEntry> FileTable { get; }
        private SaveFsList<DirectorySaveEntry> DirectoryTable { get; }

        public HierarchicalSaveFileTable(IStorage dirTable, IStorage fileTable)
        {
            FileTable = new SaveFsList<FileSaveEntry>(fileTable);
            DirectoryTable = new SaveFsList<DirectorySaveEntry>(dirTable);
        }

        public bool TryOpenFile(string path, out SaveFileInfo fileInfo)
        {
            if (!FindPathRecursive(Util.GetUtf8Bytes(path), out SaveEntryKey key))
            {
                fileInfo = default;
                return false;
            }

            if (FileTable.TryGetValue(ref key, out FileSaveEntry value))
            {
                fileInfo = value.Info;
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

            bool success = FileTable.TryGetValue((int)position.NextFile, out FileSaveEntry entry, ref nameBytes);

            // todo error message
            if (!success)
            {
                info = default;
                name = default;
                return false;
            }

            position.NextFile = entry.NextSibling;
            info = entry.Info;

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

            Span<byte> nameBytes = stackalloc byte[FileTable.MaxNameLength];

            bool success = DirectoryTable.TryGetValue(position.NextDirectory, out DirectorySaveEntry entry, ref nameBytes);

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

            CreateFileRecursiveInternal(pathBytes, ref fileInfo);
        }

        private void CreateFileRecursiveInternal(ReadOnlySpan<byte> path, ref SaveFileInfo fileInfo)
        {
            var parser = new PathParser(path);
            var key = new SaveEntryKey(parser.GetCurrent(), 0);

            int prevIndex = 0;

            while (!parser.IsFinished())
            {
                int index = DirectoryTable.GetIndexFromKey(ref key).Index;

                if (index < 0)
                {
                    var newEntry = new DirectorySaveEntry();
                    index = DirectoryTable.Add(ref key, ref newEntry);

                    if (prevIndex > 0)
                    {
                        DirectoryTable.GetValue(prevIndex, out DirectorySaveEntry parentEntry);

                        newEntry.NextSibling = parentEntry.Pos.NextDirectory;
                        parentEntry.Pos.NextDirectory = index;

                        DirectoryTable.SetValue(prevIndex, ref parentEntry);
                        DirectoryTable.SetValue(index, ref newEntry);
                    }
                }

                prevIndex = index;
                key.Parent = index;
                parser.TryGetNext(out key.Name);
            }

            {
                int index = FileTable.GetIndexFromKey(ref key).Index;
                var fileEntry = new FileSaveEntry();

                if (index < 0)
                {
                    index = FileTable.Add(ref key, ref fileEntry);

                    DirectoryTable.GetValue(prevIndex, out DirectorySaveEntry parentEntry);

                    fileEntry.NextSibling = (int)parentEntry.Pos.NextFile;
                    parentEntry.Pos.NextFile = index;

                    DirectoryTable.SetValue(prevIndex, ref parentEntry);
                }

                fileEntry.Info = fileInfo;
                FileTable.SetValue(index, ref fileEntry);
            }
        }

        public bool TryOpenDirectory(string path, out SaveFindPosition position)
        {
            if (!FindPathRecursive(Util.GetUtf8Bytes(path), out SaveEntryKey key))
            {
                position = default;
                return false;
            }

            if (DirectoryTable.TryGetValue(ref key, out DirectorySaveEntry value))
            {
                position = value.Pos;
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

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct DirectorySaveEntry
        {
            public int NextSibling;
            public SaveFindPosition Pos;
            public long Field10;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FileSaveEntry
        {
            public int NextSibling;
            public SaveFileInfo Info;
            public long Field10;
        }
    }
}
