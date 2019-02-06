using System.Linq;
using LibHac;
using LibHac.IO;

namespace Net
{
    public class Cache
    {
        private IFileSystem CacheFs { get; }

        public Cache(IFileSystem cacheFs)
        {
            CacheFs = cacheFs;
        }

        public bool TryOpenNca(ulong titleId, int version, byte[] ncaId, out IFile file)
        {
            file = default;

            string titleDir = GetTitleDir(titleId, version);
            if (!CacheFs.DirectoryExists(titleDir)) return false;

            string filePath = $"{titleDir}/{ncaId.ToHexString().ToLower()}.nca");

            if (CacheFs.FileExists(filePath))
            {
                file = CacheFs.OpenFile(filePath, OpenMode.Read);
                return true;
            }

            return false;
        }

        public bool TryOpenMetaNca(ulong titleId, int version, out IFile file)
        {
            file = default;
            string titleDir = GetTitleDir(titleId, version);
            if (!CacheFs.DirectoryExists(titleDir)) return false;

            IDirectory dir = CacheFs.OpenDirectory(titleDir, OpenDirectoryMode.All);

            DirectoryEntry[] metaFiles = dir.EnumerateEntries("*.cnmt.nca", SearchOptions.Default).ToArray();

            if (metaFiles.Length == 1)
            {
                file = CacheFs.OpenFile(metaFiles[0].FullPath, OpenMode.Read);
                return true;
            }

            if (metaFiles.Length > 1)
            {
                throw new System.IO.FileNotFoundException($"More than 1 cnmt file exists for {titleId:x16}v{version}");
            }

            return false;
        }

        private string GetTitleDir(ulong titleId, int version = -1)
        {
            if (version >= 0)
            {
                return $"/{titleId:x16}/{version}";
            }

            return $"/{titleId:x16}";
        }
    }
}
