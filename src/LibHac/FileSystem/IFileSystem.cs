using System.IO;

namespace LibHac
{
    public abstract class IFileSystem
    {
        public abstract IDirectory RootDirectory { get; }

        public abstract string PathSeperator { get; }

        public abstract bool FileExists(IFile path);
        public abstract bool DirectoryExists(IDirectory path);

        public Stream OpenFile(IFile file, FileMode mode)
        {
            return OpenFile(file, mode, FileAccess.ReadWrite);
        }
        public abstract Stream OpenFile(IFile file, FileMode mode, FileAccess access);

        public IFileSytemEntry[] GetFileSystemEntries(IDirectory directory, string searchPattern)
        {
            return GetFileSystemEntries(directory, searchPattern, SearchOption.TopDirectoryOnly);
        }
        public abstract IFileSytemEntry[] GetFileSystemEntries(IDirectory path, string searchPattern, SearchOption searchOption);

        public IDirectory GetDirectory(string path)
        {
            if (path.StartsWith(PathSeperator))
                path = path.Substring(PathSeperator.Length);
            return GetPath(path);
        }
        protected abstract IDirectory GetPath(string path);

        public IFile GetFile(string path)
        {
            if (path.StartsWith(PathSeperator))
                path = path.Substring(PathSeperator.Length);
            return GetFileImpl(path);
        }
        protected abstract IFile GetFileImpl(string path);


        public abstract IFile[] GetFiles(IDirectory directory);
        public abstract IDirectory[] GetDirectories(IDirectory directory);
        public abstract IFileSytemEntry[] GetEntries(IDirectory directory);

    }

    public interface IFileSytemEntry
    {
        IFileSystem FileSystem { get; }
        string Path { get; }
        bool Exists { get; }
    }

    public class IDirectory : IFileSytemEntry
    {
        public IFileSystem FileSystem { get; }
        public string Path { get; }

        public IDirectory Parent
        {
            get
            {
                int index = Path.LastIndexOf(FileSystem.PathSeperator);
                return FileSystem.GetDirectory(Path.Substring(0, index));
            }
        }

        public IFile[] Files => FileSystem.GetFiles(this);
        public IDirectory[] Directories => FileSystem.GetDirectories(this);
        public bool Exists => FileSystem.DirectoryExists(this);

        public IDirectory(IFileSystem filesystem, string path)
        {
            FileSystem = filesystem;
            Path = path;
        }

        public IFile GetFile(string path)
        {
            return FileSystem.GetFile(Path + FileSystem.PathSeperator + path);
        }

        public IDirectory GetDirectory(string path)
        {
            return FileSystem.GetDirectory(Path + FileSystem.PathSeperator + path);
        }

        public IFileSytemEntry[] GetFileSystemEntries(string searchOption)
        {
            return FileSystem.GetFileSystemEntries(this, searchOption);
        }

        public IFileSytemEntry[] GetFileSystemEntries(string searchPattern, SearchOption searchOption)
        {
            return FileSystem.GetFileSystemEntries(this, searchPattern, searchOption);
        }
    }

    public class IFile : IFileSytemEntry
    {
        public IFileSystem FileSystem { get; }
        public string Path { get; }

        public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
        public string Extension => System.IO.Path.GetExtension(Path);
        public string FileName => System.IO.Path.GetFileName(Path);

        public bool Exists => FileSystem.FileExists(this);

        public IDirectory Parent {
            get
            {
                int index = Path.LastIndexOf(FileSystem.PathSeperator);
                return FileSystem.GetDirectory(Path.Substring(0, index));
            }
        }


        public IFile(IFileSystem filesystem, string path)
        {
            FileSystem = filesystem;
            Path = path;
        }

        public Stream Open(FileMode mode)
        {
            return FileSystem.OpenFile(this, mode);
        }

        public Stream Open(FileMode mode, FileAccess access)
        {
            return FileSystem.OpenFile(this, mode, access);
        }
    }
}
