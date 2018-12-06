using System;
using System.Collections.Generic;
using System.IO;

namespace LibHac
{
    public class LocalFileSystem : IFileSystem
    {
        public string Root { get; }
        public override string PathSeperator => new string(new char[] { Path.DirectorySeparatorChar });
        public override IDirectory RootDirectory => new IDirectory(this, "");

        public LocalFileSystem(string rootDir)
        {
            Root = rootDir;
        }

        public override bool FileExists(IFile file)
        {
            return File.Exists(GetFullPath(file));
        }

        public override bool DirectoryExists(IDirectory directory)
        {
            return Directory.Exists(Path.Combine(Root, directory.Path));
        }

        public override Stream OpenFile(IFile file, FileMode mode, FileAccess access)
        {
            return new FileStream(GetFullPath(file), mode, access);
        }

        public override IFile[] GetFileSystemEntries(IDirectory path, string searchPattern, SearchOption searchOption)
        {
            //return Directory.GetFileSystemEntries(Path.Combine(Root, path), searchPattern, searchOption);
            var result = new List<IFile>();

            try
            {
                result.AddRange(GetFileSystemEntries(path, searchPattern));
            }
            catch (UnauthorizedAccessException) { /* Skip this directory */ }

            if (searchOption == SearchOption.TopDirectoryOnly)
                return result.ToArray();

            string[] searchDirectories = Directory.GetDirectories(GetFullPath(path));
            foreach (string search in searchDirectories)
            {
                try
                {
                    result.AddRange(GetFileSystemEntries(FullPathToDirectory(search), searchPattern, searchOption));
                }
                catch (UnauthorizedAccessException) { /* Skip this result */ }
            }

            return result.ToArray();
        }


        public override IFile[] GetFiles(IDirectory directory)
        {
            FileInfo[] infos = new DirectoryInfo(GetFullPath(directory)).GetFiles();
            IFile[] files = new IFile[infos.Length];
            for (int i = 0; i < files.Length; i++)
                files[i] = FullPathToFile(infos[i].FullName);
            return files;
        }

        public override IDirectory[] GetDirectories(IDirectory directory)
        {
            DirectoryInfo[] infos = new DirectoryInfo(GetFullPath(directory)).GetDirectories();
            IDirectory[] directories = new IDirectory[infos.Length];
            for (int i = 0; i < directories.Length; i++)
                directories[i] = FullPathToDirectory(infos[i].FullName);
            return directories;
        }

        protected override IDirectory GetPath(string path)
        {
            return new IDirectory(this, path);
        }


        protected override IFile GetFileImpl(string path)
        {
            return new LocalFile(this, path);
        }

        private static string GetFullPath(IFile file)
        {
            return ((LocalFile)file).GetFullPath();
        }

        private static string GetFullPath(IDirectory directory)
        {
            return Path.Combine(((LocalFileSystem)directory.FileSystem).Root, directory.Path);
        }

        private IDirectory FullPathToDirectory(string path)
        {
            return new IDirectory(this, Util.GetRelativePath(path, Root));
        }

        private LocalFile FullPathToFile(string path)
        {
            return new LocalFile(this, Util.GetRelativePath(path, Root));
        }

        public class LocalFile : IFile
        {
            public LocalFile(LocalFileSystem localFileSystem, string path) : base(localFileSystem, path)
            {
            }

            public string GetFullPath()
            {
                return System.IO.Path.Combine(((LocalFileSystem)FileSystem).Root, Path);
            }
        }
    }
}
