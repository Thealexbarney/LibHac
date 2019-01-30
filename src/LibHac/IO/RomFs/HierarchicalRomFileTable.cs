using System;
using System.Runtime.InteropServices;

namespace LibHac.IO.RomFs
{
    public class HierarchicalRomFileTable
    {
        private IStorage DirHashTableStorage { get; }
        private IStorage DirEntryTableStorage { get; }
        private IStorage FileHashTableStorage { get; }
        private IStorage FileEntryTableStorage { get; }

        private RomFsDictionary<FileRomEntry> FileTable { get; }
        private RomFsDictionary<DirectoryRomEntry> DirectoryTable { get; }

        public HierarchicalRomFileTable(IStorage dirHashTable, IStorage dirEntryTable, IStorage fileHashTable,
            IStorage fileEntryTable)
        {
            DirHashTableStorage = dirHashTable;
            DirEntryTableStorage = dirEntryTable;
            FileHashTableStorage = fileHashTable;
            FileEntryTableStorage = fileEntryTable;

            FileTable = new RomFsDictionary<FileRomEntry>(FileHashTableStorage, FileEntryTableStorage);
            DirectoryTable = new RomFsDictionary<DirectoryRomEntry>(DirHashTableStorage, DirEntryTableStorage);
        }

        public bool OpenFile(string path, out RomFileInfo fileInfo)
        {
            FindFileRecursive(path.AsSpan(), out RomEntryKey key);

            if (FileTable.TryGetValue(ref key, out FileRomEntry entry, out int _))
            {
                fileInfo = entry.Info;
                return true;
            }

            fileInfo = default;
            return false;
        }

        public bool OpenFile(int offset, out RomFileInfo fileInfo)
        {
            if (FileTable.TryGetValue(offset, out FileRomEntry entry))
            {
                fileInfo = entry.Info;
                return true;
            }

            fileInfo = default;
            return false;
        }

        public bool OpenDirectory(string path, out FindPosition position)
        {
            FindDirectoryRecursive(path.AsSpan(), out RomEntryKey key);

            if (DirectoryTable.TryGetValue(ref key, out DirectoryRomEntry entry, out int _))
            {
                position = entry.Pos;
                return true;
            }

            position = default;
            return false;
        }

        public bool OpenDirectory(int offset, out FindPosition position)
        {
            if (DirectoryTable.TryGetValue(offset, out DirectoryRomEntry entry))
            {
                position = entry.Pos;
                return true;
            }

            position = default;
            return false;
        }

        public bool FindNextFile(ref FindPosition position, out RomFileInfo info, out string name)
        {
            if (FileTable.TryGetValue(position.NextFile, out FileRomEntry entry, out name))
            {
                position.NextFile = entry.NextSibling;
                info = entry.Info;
                return true;
            }

            info = default;
            return false;
        }

        public bool FindNextDirectory(ref FindPosition position, out string name)
        {
            if (DirectoryTable.TryGetValue(position.NextDirectory, out DirectoryRomEntry entry, out name))
            {
                position.NextDirectory = entry.NextSibling;
                return true;
            }

            return false;
        }

        private void FindFileRecursive(ReadOnlySpan<char> path, out RomEntryKey key)
        {
            var parser = new PathParser(path);
            FindParentDirectoryRecursive(ref parser, out DirectoryRomEntry _, out int parentOffset);

            key = new RomEntryKey(parser.GetCurrent(), parentOffset);
        }

        private void FindDirectoryRecursive(ReadOnlySpan<char> path, out RomEntryKey key)
        {
            var parser = new PathParser(path);
            FindParentDirectoryRecursive(ref parser, out DirectoryRomEntry _, out int parentOffset);

            ReadOnlySpan<char> name = parser.GetCurrent();
            if (name.Length == 0) parentOffset = 0;

            key = new RomEntryKey(name, parentOffset);
        }

        private void FindParentDirectoryRecursive(ref PathParser parser, out DirectoryRomEntry parentEntry, out int parentOffset)
        {
            parentEntry = default;
            parentOffset = default;
            RomEntryKey key = default;

            while (parser.TryGetNext(out key.Name) && !parser.IsFinished())
            {
                DirectoryTable.TryGetValue(ref key, out parentEntry, out parentOffset);
                key.Parent = parentOffset;
            }
        }
    }

    internal ref struct RomEntryKey
    {
        public ReadOnlySpan<char> Name;
        public int Parent;

        public RomEntryKey(ReadOnlySpan<char> name, int parent)
        {
            Name = name;
            Parent = parent;
        }

        public uint GetRomHashCode()
        {
            uint hash = 123456789 ^ (uint)Parent;

            foreach (char c in Name)
            {
                hash = c ^ ((hash << 27) | (hash >> 5));
            }

            return hash;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RomFsEntry<T> where T : unmanaged
    {
        public int Parent;
        public T Value;
        public int Next;
        public int KeyLength;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FileRomEntry
    {
        public int NextSibling;
        public RomFileInfo Info;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RomFileInfo
    {
        public long Offset;
        public long Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DirectoryRomEntry
    {
        public int NextSibling;
        public FindPosition Pos;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FindPosition
    {
        public int NextDirectory;
        public int NextFile;
    }
}
