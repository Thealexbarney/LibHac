using System;
using System.Collections.Generic;

namespace LibHac.IO
{
    public class AesXtsDirectory : IDirectory
    {
        public IFileSystem ParentFileSystem { get; }
        public string FullPath { get; }
        public OpenDirectoryMode Mode { get; }

        private IFileSystem BaseFileSystem { get; }
        private IDirectory BaseDirectory { get; }

        public AesXtsDirectory(IFileSystem parentFs, IDirectory baseDir, OpenDirectoryMode mode)
        {
            ParentFileSystem = parentFs;
            BaseDirectory = baseDir;
            Mode = mode;
            BaseFileSystem = BaseDirectory.ParentFileSystem;
            FullPath = BaseDirectory.FullPath;
        }

        public IEnumerable<DirectoryEntry> Read()
        {
            foreach (DirectoryEntry entry in BaseDirectory.Read())
            {
                if (entry.Type == DirectoryEntryType.Directory)
                {
                    yield return entry;
                }
                else
                {
                    long size = GetAesXtsFileSize(entry.FullPath);
                    yield return new DirectoryEntry(entry.Name, entry.FullPath, entry.Type, size);
                }
            }
        }

        public int GetEntryCount()
        {
            return BaseDirectory.GetEntryCount();
        }

        private long GetAesXtsFileSize(string path)
        {
            using (IFile file = BaseFileSystem.OpenFile(path, OpenMode.Read))
            {
                var buffer = new byte[8];

                file.Read(buffer, 0);
                if (BitConverter.ToUInt32(buffer, 0) != 0x3058414E) return 0;

                file.Read(buffer, 0x48);
                return BitConverter.ToInt32(buffer, 0);
            }
        }
    }
}
