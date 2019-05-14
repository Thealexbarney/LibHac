﻿using System;
using System.Collections.Generic;

namespace LibHac.Fs
{
    public class AesXtsDirectory : IDirectory
    {
        IFileSystem IDirectory.ParentFileSystem => ParentFileSystem;
        public AesXtsFileSystem ParentFileSystem { get; }

        public string FullPath { get; }
        public OpenDirectoryMode Mode { get; }

        private IFileSystem BaseFileSystem { get; }
        private IDirectory BaseDirectory { get; }

        public AesXtsDirectory(AesXtsFileSystem parentFs, IDirectory baseDir, OpenDirectoryMode mode)
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
                    if (size == -1) continue;

                    yield return new DirectoryEntry(entry.Name, entry.FullPath, entry.Type, size);
                }
            }
        }

        public int GetEntryCount()
        {
            return BaseDirectory.GetEntryCount();
        }

        /// <summary>
        /// Reads the size of a NAX0 file from its header. Returns -1 on error.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private long GetAesXtsFileSize(string path)
        {
            try
            {
                using (IFile file = BaseFileSystem.OpenFile(path, OpenMode.Read))
                {
                    if (file.GetSize() < 0x50)
                    {
                        return -1;
                    }

                    var buffer = new byte[8];

                    file.Read(buffer, 0x20);
                    if (BitConverter.ToUInt32(buffer, 0) != 0x3058414E) return 0;

                    file.Read(buffer, 0x48);
                    return BitConverter.ToInt64(buffer, 0);
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                return -1;
            }
        }
    }
}
