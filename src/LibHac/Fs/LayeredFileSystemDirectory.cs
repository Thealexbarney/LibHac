using System.Collections.Generic;
using System.Linq;

namespace LibHac.Fs
{
    public class LayeredFileSystemDirectory : IDirectory
    {
        public IFileSystem ParentFileSystem { get; }

        public string FullPath { get; }
        public OpenDirectoryMode Mode { get; }

        private List<IDirectory> Sources { get; }

        public LayeredFileSystemDirectory(IFileSystem fs, List<IDirectory> sources, string path, OpenDirectoryMode mode)
        {
            ParentFileSystem = fs;
            Sources = sources;
            FullPath = path;
            Mode = mode;
        }

        public IEnumerable<DirectoryEntry> Read()
        {
            var returnedFiles = new HashSet<string>();

            foreach (IDirectory source in Sources)
            {
                foreach (DirectoryEntry entry in source.Read())
                {
                    if (returnedFiles.Contains(entry.FullPath)) continue;

                    returnedFiles.Add(entry.FullPath);
                    yield return entry;
                }
            }
        }

        public int GetEntryCount()
        {
            return Read().Count();
        }
    }
}
