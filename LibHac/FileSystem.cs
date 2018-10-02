using System;
using System.Collections.Generic;
using System.IO;

namespace LibHac
{
    public class FileSystem : IFileSystem
    {
        public string Root { get; }

        public FileSystem(string rootDir)
        {
            Root = Path.GetFullPath(rootDir);
        }

        public bool FileExists(string path)
        {
            return File.Exists(Path.Combine(Root, path));
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(Path.Combine(Root, path));
        }

        public Stream OpenFile(string path, FileMode mode)
        {
            return new FileStream(Path.Combine(Root, path), mode);
        }

        public Stream OpenFile(string path, FileMode mode, FileAccess access)
        {
            return new FileStream(Path.Combine(Root, path), mode, access);
        }

        public string[] GetFileSystemEntries(string path, string searchPattern)
        {
            return Directory.GetFileSystemEntries(Path.Combine(Root, path), searchPattern);
        }

        public string[] GetFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
        {
            //return Directory.GetFileSystemEntries(Path.Combine(Root, path), searchPattern, searchOption);
            var result = new List<string>();

            try
            {
                result.AddRange(GetFileSystemEntries(Path.Combine(Root, path), searchPattern));
            }
            catch (UnauthorizedAccessException) { /* Skip this directory */ }

            if (searchOption == SearchOption.TopDirectoryOnly)
                return result.ToArray();

            string[] searchDirectories = Directory.GetDirectories(Path.Combine(Root, path));
            foreach (string search in searchDirectories)
            {
                try
                {
                    result.AddRange(GetFileSystemEntries(search, searchPattern, searchOption));
                }
                catch (UnauthorizedAccessException) { /* Skip this result */ }
            }

            return result.ToArray();
        }

        public string GetFullPath(string path)
        {
            return Path.Combine(Root, path);
        }
    }
}
