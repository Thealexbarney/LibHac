using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiscUtils.Fat;
using LibHac.IO;

namespace LibHac.Nand
{
    public class NandPartition : IFileSystem
    {
        public FatFileSystem Fs { get; }

        public override string PathSeperator => "\\";
        public override IDirectory RootDirectory => new IDirectory(this, "");

        public NandPartition(FatFileSystem fileSystem)
        {
            Fs = fileSystem;
        }

        public override bool FileExists(IFile file)
        {
            return Fs.FileExists(file.Path);
        }

        public override bool DirectoryExists(IDirectory directory)
        {
            return Fs.DirectoryExists(directory.Path);
        }

        public override long GetSize(IFile file)
        {
            return Fs.GetFileInfo(file.Path).Length;
        }

        public new virtual IStorage OpenFile(IFile file, FileMode mode)
        {
            return Fs.OpenFile(file.Path, mode).AsStorage();
        }

        public override IStorage OpenFile(IFile file, FileMode mode, FileAccess access)
        {
            return Fs.OpenFile(file.Path, mode, access).AsStorage();
        }

        public new virtual IFileSytemEntry[] GetFileSystemEntries(IDirectory directory, string searchPattern)
        {
            return GetFileSystemEntries(directory, searchPattern, SearchOption.TopDirectoryOnly);
        }

        public override IFileSytemEntry[] GetFileSystemEntries(IDirectory directory, string searchPattern, SearchOption searchOption)
        {
            string[] files = Fs.GetFiles(directory.Path, searchPattern, searchOption);
            string[] dirs = Fs.GetDirectories(directory.Path, searchPattern, searchOption);
            List<IFileSytemEntry> entries = new List<IFileSytemEntry>();
            for (int i = 0; i < dirs.Length; i++)
                entries.Add(GetDirectory(dirs[i]));
            for (int i = 0; i < files.Length; i++)
                entries.Add(GetFile(files[i]));
            return entries.ToArray();
        }

        protected override IDirectory GetPath(string path)
        {
            return new IDirectory(this, path);
        }

        protected override IFile GetFileImpl(string path)
        {
            return new IFile(this, path);
        }

        public override IFile[] GetFiles(IDirectory directory)
        {
            List<IFile> files = new List<IFile>();
            foreach (string file in Fs.GetFiles(directory.Path))
                files.Add(GetFile(file));
            return files.ToArray();
        }

        public override IDirectory[] GetDirectories(IDirectory directory)
        {
            List<IDirectory> directories = new List<IDirectory>();
            foreach (string dir in Fs.GetDirectories(directory.Path))
                directories.Add(GetDirectory(dir));
            return directories.ToArray();
        }

        public override IFileSytemEntry[] GetEntries(IDirectory directory)
        {
            List<IFileSytemEntry> list = new List<IFileSytemEntry>();
            list.AddRange(GetDirectories(directory));
            list.AddRange(GetFiles(directory));
            return list.ToArray();
        }
    }
}
