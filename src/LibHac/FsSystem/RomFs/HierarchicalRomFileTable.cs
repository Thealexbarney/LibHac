using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSystem.RomFs
{
    /// <summary>
    /// Represents the file table used by the RomFS format.
    /// </summary>
    /// <typeparam name="T">The type of the value to be stored for each file entry.</typeparam>
    /// <remarks>
    /// This file table stores the structure of the file tree in a RomFS.
    /// Each file or directory entry is stored in the table using its full path as a key.
    /// Once added, a file or directory is assigned an ID that can also be used to retrieve it.
    /// Each file entry contains a structure of type <typeparamref name="T"/>.
    /// In a standard RomFS, this includes the size of the file and its offset in the RomFS.
    /// Each directory entry contains the IDs for its first child file and first child directory.
    ///
    /// The table is represented by four byte arrays. Two of the arrays contain the hash buckets and
    /// entries for the files, and the other two for the directories.
    /// 
    /// Once all files have been added to the table, <see cref="TrimExcess"/> should be called
    /// to optimize the size of the table.
    /// </remarks>
    public class HierarchicalRomFileTable<T> where T : unmanaged
    {
        private RomFsDictionary<FileRomEntry> FileTable { get; }
        private RomFsDictionary<DirectoryRomEntry> DirectoryTable { get; }

        /// <summary>
        /// Initializes a <see cref="HierarchicalRomFileTable{T}"/> from an existing table.
        /// </summary>
        /// <param name="dirHashTable"></param>
        /// <param name="dirEntryTable"></param>
        /// <param name="fileHashTable"></param>
        /// <param name="fileEntryTable"></param>
        public HierarchicalRomFileTable(IStorage dirHashTable, IStorage dirEntryTable, IStorage fileHashTable,
            IStorage fileEntryTable)
        {
            FileTable = new RomFsDictionary<FileRomEntry>(fileHashTable, fileEntryTable);
            DirectoryTable = new RomFsDictionary<DirectoryRomEntry>(dirHashTable, dirEntryTable);
        }

        /// <summary>
        /// Initializes a new <see cref="HierarchicalRomFileTable{T}"/> that has the default initial capacity.
        /// </summary>
        public HierarchicalRomFileTable() : this(0, 0) { }

        /// <summary>
        /// Initializes a new <see cref="HierarchicalRomFileTable{T}"/> that has the specified initial capacity.
        /// </summary>
        /// <param name="directoryCapacity">The initial number of directories that the
        /// <see cref="HierarchicalRomFileTable{T}"/> can contain.</param>
        /// <param name="fileCapacity">The initial number of files that the
        /// <see cref="HierarchicalRomFileTable{T}"/> can contain.</param>
        public HierarchicalRomFileTable(int directoryCapacity, int fileCapacity)
        {
            FileTable = new RomFsDictionary<FileRomEntry>(fileCapacity);
            DirectoryTable = new RomFsDictionary<DirectoryRomEntry>(directoryCapacity);

            CreateRootDirectory();
        }

        public byte[] GetDirectoryBuckets()
        {
            return MemoryMarshal.Cast<int, byte>(DirectoryTable.GetBucketData()).ToArray();
        }

        public byte[] GetDirectoryEntries()
        {
            return DirectoryTable.GetEntryData().ToArray();
        }

        public byte[] GetFileBuckets()
        {
            return MemoryMarshal.Cast<int, byte>(FileTable.GetBucketData()).ToArray();
        }

        public byte[] GetFileEntries()
        {
            return FileTable.GetEntryData().ToArray();
        }

        public bool TryOpenFile(string path, out T fileInfo)
        {
            FindPathRecursive(StringUtils.StringToUtf8(path), out RomEntryKey key);

            if (FileTable.TryGetValue(ref key, out RomKeyValuePair<FileRomEntry> keyValuePair))
            {
                fileInfo = keyValuePair.Value.Info;
                return true;
            }

            UnsafeHelpers.SkipParamInit(out fileInfo);
            return false;
        }

        public bool TryOpenFile(int fileId, out T fileInfo)
        {
            if (FileTable.TryGetValue(fileId, out RomKeyValuePair<FileRomEntry> keyValuePair))
            {
                fileInfo = keyValuePair.Value.Info;
                return true;
            }

            UnsafeHelpers.SkipParamInit(out fileInfo);
            return false;
        }

        /// <summary>
        /// Opens a directory for enumeration.
        /// </summary>
        /// <param name="path">The full path of the directory to open.</param>
        /// <param name="position">The initial position of the directory enumerator.</param>
        /// <returns><see langword="true"/> if the table contains a directory with the specified path;
        /// otherwise, <see langword="false"/>.</returns>
        public bool TryOpenDirectory(string path, out FindPosition position)
        {
            FindPathRecursive(StringUtils.StringToUtf8(path), out RomEntryKey key);

            if (DirectoryTable.TryGetValue(ref key, out RomKeyValuePair<DirectoryRomEntry> keyValuePair))
            {
                position = keyValuePair.Value.Pos;
                return true;
            }

            UnsafeHelpers.SkipParamInit(out position);
            return false;
        }

        /// <summary>
        /// Opens a directory for enumeration.
        /// </summary>
        /// <param name="directoryId">The ID of the directory to open.</param>
        /// <param name="position">When this method returns, contains the initial position of the directory enumerator.</param>
        /// <returns><see langword="true"/> if the table contains a directory with the specified path;
        /// otherwise, <see langword="false"/>.</returns>
        public bool TryOpenDirectory(int directoryId, out FindPosition position)
        {
            if (DirectoryTable.TryGetValue(directoryId, out RomKeyValuePair<DirectoryRomEntry> keyValuePair))
            {
                position = keyValuePair.Value.Pos;
                return true;
            }

            UnsafeHelpers.SkipParamInit(out position);
            return false;
        }

        /// <summary>
        /// Returns the next file in a directory and updates the enumerator's position.
        /// </summary>
        /// <param name="position">The current position of the directory enumerator.
        /// This position will be updated when the method returns.</param>
        /// <param name="info">When this method returns, contains the file's metadata.</param>
        /// <param name="name">When this method returns, contains the file's name (Not the full path).</param>
        /// <returns><see langword="true"/> if the next file was successfully returned.
        /// <see langword="false"/> if there are no more files to enumerate.</returns>
        public bool FindNextFile(ref FindPosition position, out T info, out string name)
        {
            if (position.NextFile == -1)
            {
                UnsafeHelpers.SkipParamInit(out info, out name);
                return false;
            }

            ref FileRomEntry entry = ref FileTable.GetValueReference(position.NextFile, out Span<byte> nameBytes);
            position.NextFile = entry.NextSibling;
            info = entry.Info;

            name = StringUtils.Utf8ToString(nameBytes);

            return true;
        }

        /// <summary>
        /// Returns the next child directory in a directory and updates the enumerator's position.
        /// </summary>
        /// <param name="position">The current position of the directory enumerator.
        /// This position will be updated when the method returns.</param>
        /// <param name="name">When this method returns, contains the directory's name (Not the full path).</param>
        /// <returns><see langword="true"/> if the next directory was successfully returned.
        /// <see langword="false"/> if there are no more directories to enumerate.</returns>
        public bool FindNextDirectory(ref FindPosition position, out string name)
        {
            if (position.NextDirectory == -1)
            {
                UnsafeHelpers.SkipParamInit(out name);
                return false;
            }

            ref DirectoryRomEntry entry = ref DirectoryTable.GetValueReference(position.NextDirectory, out Span<byte> nameBytes);
            position.NextDirectory = entry.NextSibling;

            name = StringUtils.Utf8ToString(nameBytes);

            return true;
        }

        /// <summary>
        /// Adds a file to the file table. If the file already exists
        /// its <see cref="RomFileInfo"/> will be updated.
        /// </summary>
        /// <param name="path">The full path of the file to be added.</param>
        /// <param name="fileInfo">The file information to be stored.</param>
        public void AddFile(string path, ref T fileInfo)
        {
            path = PathTools.Normalize(path);
            ReadOnlySpan<byte> pathBytes = StringUtils.StringToUtf8(path);

            if (path == "/") throw new ArgumentException("Path cannot be empty");

            CreateFileRecursiveInternal(pathBytes, ref fileInfo);
        }

        /// <summary>
        /// Adds a directory to the file table. If the directory already exists,
        /// no action is performed.
        /// </summary>
        /// <param name="path">The full path of the directory to be added.</param>
        public void AddDirectory(string path)
        {
            path = PathTools.Normalize(path);

            CreateDirectoryRecursive(StringUtils.StringToUtf8(path));
        }

        /// <summary>
        /// Sets the capacity of this dictionary to what it would be if
        /// it had been originally initialized with all its entries.
        /// 
        /// This method can be used to minimize the memory overhead 
        /// once it is known that no new elements will be added.
        /// </summary>
        public void TrimExcess()
        {
            DirectoryTable.TrimExcess();
            FileTable.TrimExcess();
        }

        private void CreateRootDirectory()
        {
            var key = new RomEntryKey(ReadOnlySpan<byte>.Empty, 0);
            var entry = new DirectoryRomEntry();
            entry.NextSibling = -1;
            entry.Pos.NextDirectory = -1;
            entry.Pos.NextFile = -1;

            DirectoryTable.Add(ref key, ref entry);
        }

        private void CreateDirectoryRecursive(ReadOnlySpan<byte> path)
        {
            var parser = new PathParser(path);
            var key = new RomEntryKey();

            int prevOffset = 0;

            while (parser.TryGetNext(out key.Name))
            {
                int offset = DirectoryTable.GetOffsetFromKey(ref key);
                if (offset < 0)
                {
                    ref DirectoryRomEntry entry = ref DirectoryTable.AddOrGet(ref key, out offset, out _, out _);
                    entry.NextSibling = -1;
                    entry.Pos.NextDirectory = -1;
                    entry.Pos.NextFile = -1;

                    ref DirectoryRomEntry parent = ref DirectoryTable.GetValueReference(prevOffset);

                    if (parent.Pos.NextDirectory == -1)
                    {
                        parent.Pos.NextDirectory = offset;
                    }
                    else
                    {
                        ref DirectoryRomEntry chain = ref DirectoryTable.GetValueReference(parent.Pos.NextDirectory);

                        while (chain.NextSibling != -1)
                        {
                            chain = ref DirectoryTable.GetValueReference(chain.NextSibling);
                        }

                        chain.NextSibling = offset;
                    }
                }

                prevOffset = offset;
                key.Parent = offset;
            }
        }

        private void CreateFileRecursiveInternal(ReadOnlySpan<byte> path, ref T fileInfo)
        {
            var parser = new PathParser(path);
            var key = new RomEntryKey();

            parser.TryGetNext(out key.Name);
            int prevOffset = 0;

            while (!parser.IsFinished())
            {
                int offset = DirectoryTable.GetOffsetFromKey(ref key);
                if (offset < 0)
                {
                    ref DirectoryRomEntry entry = ref DirectoryTable.AddOrGet(ref key, out offset, out _, out _);
                    entry.NextSibling = -1;
                    entry.Pos.NextDirectory = -1;
                    entry.Pos.NextFile = -1;

                    ref DirectoryRomEntry parent = ref DirectoryTable.GetValueReference(prevOffset);

                    if (parent.Pos.NextDirectory == -1)
                    {
                        parent.Pos.NextDirectory = offset;
                    }
                    else
                    {
                        ref DirectoryRomEntry chain = ref DirectoryTable.GetValueReference(parent.Pos.NextDirectory);

                        while (chain.NextSibling != -1)
                        {
                            chain = ref DirectoryTable.GetValueReference(chain.NextSibling);
                        }

                        chain.NextSibling = offset;
                    }
                }

                prevOffset = offset;
                key.Parent = offset;
                parser.TryGetNext(out key.Name);
            }

            {
                ref FileRomEntry entry = ref FileTable.AddOrGet(ref key, out int offset, out bool alreadyExists, out _);
                entry.Info = fileInfo;
                if (!alreadyExists) entry.NextSibling = -1;

                ref DirectoryRomEntry parent = ref DirectoryTable.GetValueReference(prevOffset);

                if (parent.Pos.NextFile == -1)
                {
                    parent.Pos.NextFile = offset;
                }
                else
                {
                    ref FileRomEntry chain = ref FileTable.GetValueReference(parent.Pos.NextFile);

                    while (chain.NextSibling != -1)
                    {
                        chain = ref FileTable.GetValueReference(chain.NextSibling);
                    }

                    chain.NextSibling = offset;
                }
            }
        }

        private void FindPathRecursive(ReadOnlySpan<byte> path, out RomEntryKey key)
        {
            var parser = new PathParser(path);
            key = new RomEntryKey(parser.GetCurrent(), 0);

            while (!parser.IsFinished())
            {
                key.Parent = DirectoryTable.GetOffsetFromKey(ref key);
                parser.TryGetNext(out key.Name);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct DirectoryRomEntry
        {
            public int NextSibling;
            public FindPosition Pos;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FileRomEntry
        {
            public int NextSibling;
            public T Info;
        }
    }
}
