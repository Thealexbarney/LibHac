using LibHac.IO;
using System.IO;

namespace LibHac
{
    public abstract class IFileSystem
    {
        public abstract IDirectory RootDirectory { get; }

        public abstract string PathSeperator { get; }

        public abstract bool FileExists(IFile path);
        public abstract bool DirectoryExists(IDirectory path);
        public abstract long GetSize(IFile file);

        public IStorage OpenFile(IFile file, FileMode mode)
        {
            return OpenFile(file, mode, FileAccess.ReadWrite);
        }
        public abstract IStorage OpenFile(IFile file, FileMode mode, FileAccess access);

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

    public abstract class IFileSytemEntry
    {
        public abstract IFileSystem FileSystem { get; protected set; }
        public abstract string Path { get; protected set; }
        public virtual string Name => System.IO.Path.GetFileName(Path);
        public virtual bool Exists { get; protected set; }

        public IDirectory Parent
        {
            get
            {
                int index = Path.LastIndexOf(FileSystem.PathSeperator);
                if(index > 0)
                    return FileSystem.GetDirectory(Path.Substring(0, index));
                return null;
            }
        }

    }

    public abstract class IDirectory : IFileSytemEntry
    {
        public override IFileSystem FileSystem { get; protected set; }
        public override string Path { get; protected set; }
        public override bool Exists => FileSystem.DirectoryExists(this);

        public IFile[] Files => FileSystem.GetFiles(this);
        public IDirectory[] Directories => FileSystem.GetDirectories(this);

        public IDirectory(IFileSystem filesystem)
        {
            FileSystem = filesystem;
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

    public class Directory : IDirectory
    {
        public Directory(IFileSystem fileSystem, string path) : base(fileSystem)
        {
            Path = path;
        }
    }

    public class IFile : IFileSytemEntry
    {
        public override IFileSystem FileSystem { get; protected set; }
        public override string Path { get; protected set; }
        public override bool Exists => FileSystem.FileExists(this);

        public string Extension => System.IO.Path.GetExtension(Path);
        public string FileName => System.IO.Path.GetFileName(Path);
        public long Length => FileSystem.GetSize(this);

        public IFile(IFileSystem filesystem)
        {
            FileSystem = filesystem;
        }

        public IStorage Open(FileMode mode)
        {
            return FileSystem.OpenFile(this, mode);
        }

        public IStorage Open(FileMode mode, FileAccess access)
        {
            return FileSystem.OpenFile(this, mode, access);
        }

        public override bool Equals(object obj)
        {
            IFile other = (IFile) obj;
            return other.FileSystem == FileSystem && other.Path == Path;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public class File : IFile
        {
            public File(IFileSystem fileSystem, string path) : base(fileSystem)
            {
                Path = path;
            }
        }
    }
}
