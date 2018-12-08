using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        public override IFileSytemEntry[] GetFileSystemEntries(IDirectory path, string searchPattern, SearchOption searchOption)
        {
            var result = new List<IFileSytemEntry>();

            DirectoryInfo root = new DirectoryInfo(GetFullPath(path));
            foreach(FileSystemInfo info in root.EnumerateFileSystemInfos(searchPattern, searchOption))
            {
                string relativePath = Util.GetRelativePath(info.FullName, Root);
                if (info.Attributes.HasFlag(FileAttributes.Directory))
                    result.Add(new IDirectory(this, relativePath));
                else
                    result.Add(new LocalFile(this, relativePath));
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

        public override IFileSytemEntry[] GetEntries(IDirectory directory)
        {
            List<IFileSytemEntry> list = new List<IFileSytemEntry>();
            list.AddRange(GetDirectories(directory));
            list.AddRange(GetFiles(directory));
            return list.ToArray();
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
