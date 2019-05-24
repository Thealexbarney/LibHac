using System.Collections.Generic;

namespace LibHac.Fs
{
    public class ReadOnlyDirectory : IDirectory
    {
        private IDirectory BaseDir { get; }
        public IFileSystem ParentFileSystem { get; }

        public string FullPath => BaseDir.FullPath;
        public OpenDirectoryMode Mode => BaseDir.Mode;

        public ReadOnlyDirectory(IFileSystem parentFileSystem, IDirectory baseDirectory)
        {
            ParentFileSystem = parentFileSystem;
            BaseDir = baseDirectory;
        }

        public IEnumerable<DirectoryEntry> Read() => BaseDir.Read();
        public int GetEntryCount() => BaseDir.GetEntryCount();
    }
}
