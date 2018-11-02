using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LibHac.IO;

namespace LibHac
{
    public class Romfs
    {
        public const int IvfcMaxLevel = 6;
        public RomfsHeader Header { get; }
        public List<RomfsDir> Directories { get; } = new List<RomfsDir>();
        public List<RomfsFile> Files { get; } = new List<RomfsFile>();
        public RomfsDir RootDir { get; }

        public Dictionary<string, RomfsFile> FileDict { get; }
        private Storage BaseStorage { get; }

        public Romfs(Storage storage)
        {
            BaseStorage = storage;

            byte[] dirMetaTable;
            byte[] fileMetaTable;
            using (var reader = new BinaryReader(BaseStorage.AsStream(), Encoding.Default, true))
            {
                Header = new RomfsHeader(reader);
                reader.BaseStream.Position = Header.DirMetaTableOffset;
                dirMetaTable = reader.ReadBytes((int)Header.DirMetaTableSize);
                reader.BaseStream.Position = Header.FileMetaTableOffset;
                fileMetaTable = reader.ReadBytes((int)Header.FileMetaTableSize);
            }

            using (var reader = new BinaryReader(new MemoryStream(dirMetaTable)))
            {
                int position = 0;
                while (position + 20 < Header.DirMetaTableSize)
                {
                    var dir = new RomfsDir(reader) { Offset = position };
                    Directories.Add(dir);
                    if (dir.ParentDirOffset == position) RootDir = dir;
                    position = (int)reader.BaseStream.Position;
                }
            }

            using (var reader = new BinaryReader(new MemoryStream(fileMetaTable)))
            {
                int position = 0;
                while (position + 20 < Header.FileMetaTableSize)
                {
                    var file = new RomfsFile(reader) { Offset = position };
                    Files.Add(file);
                    position = (int)reader.BaseStream.Position;
                }
            }

            SetReferences();
            RomfsEntry.ResolveFilenames(Files);
            RomfsEntry.ResolveFilenames(Directories);
            FileDict = Files.ToDictionary(x => x.FullPath, x => x);
        }

        public Storage OpenFile(string filename)
        {
            if (!FileDict.TryGetValue(filename, out RomfsFile file))
            {
                throw new FileNotFoundException();
            }

            return OpenFile(file);
        }

        public Storage OpenFile(RomfsFile file)
        {
            return BaseStorage.Slice(Header.DataOffset + file.DataOffset, file.DataLength);
        }

        public byte[] GetFile(string filename)
        {
            Storage storage = OpenFile(filename);
            var file = new byte[storage.Length];

            storage.Read(file, 0);

            return file;
        }

        public bool FileExists(string filename) => FileDict.ContainsKey(filename);

        public Storage OpenRawStream() => BaseStorage.Slice(0);

        private void SetReferences()
        {
            Dictionary<int, RomfsDir> dirDict = Directories.ToDictionary(x => x.Offset, x => x);
            Dictionary<int, RomfsFile> fileDict = Files.ToDictionary(x => x.Offset, x => x);

            foreach (RomfsDir dir in Directories)
            {
                if (dir.ParentDirOffset >= 0 && dir.ParentDirOffset != dir.Offset) dir.ParentDir = dirDict[dir.ParentDirOffset];
                if (dir.NextSiblingOffset >= 0) dir.NextSibling = dirDict[dir.NextSiblingOffset];
                if (dir.FirstChildOffset >= 0) dir.FirstChild = dirDict[dir.FirstChildOffset];
                if (dir.FirstFileOffset >= 0) dir.FirstFile = fileDict[dir.FirstFileOffset];
                if (dir.NextDirHashOffset >= 0) dir.NextDirHash = dirDict[dir.NextDirHashOffset];
            }

            foreach (RomfsFile file in Files)
            {
                if (file.ParentDirOffset >= 0) file.ParentDir = dirDict[file.ParentDirOffset];
                if (file.NextSiblingOffset >= 0) file.NextSibling = fileDict[file.NextSiblingOffset];
                if (file.NextFileHashOffset >= 0) file.NextFileHash = fileDict[file.NextFileHashOffset];
            }
        }
    }

    public class RomfsHeader
    {
        public long HeaderSize { get; }
        public long DirHashTableOffset { get; }
        public long DirHashTableSize { get; }
        public long DirMetaTableOffset { get; }
        public long DirMetaTableSize { get; }
        public long FileHashTableOffset { get; }
        public long FileHashTableSize { get; }
        public long FileMetaTableOffset { get; }
        public long FileMetaTableSize { get; }
        public long DataOffset { get; }

        public RomfsHeader(BinaryReader reader)
        {
            HeaderSize = reader.ReadInt64();
            DirHashTableOffset = reader.ReadInt64();
            DirHashTableSize = reader.ReadInt64();
            DirMetaTableOffset = reader.ReadInt64();
            DirMetaTableSize = reader.ReadInt64();
            FileHashTableOffset = reader.ReadInt64();
            FileHashTableSize = reader.ReadInt64();
            FileMetaTableOffset = reader.ReadInt64();
            FileMetaTableSize = reader.ReadInt64();
            DataOffset = reader.ReadInt64();
        }
    }

    public static class RomfsExtensions
    {
        public static void Extract(this Romfs romfs, string outDir, IProgressReport logger = null)
        {
            foreach (RomfsFile file in romfs.Files)
            {
                Storage storage = romfs.OpenFile(file);
                string outName = outDir + file.FullPath;
                string dir = Path.GetDirectoryName(outName);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

                using (var outFile = new FileStream(outName, FileMode.Create, FileAccess.ReadWrite))
                {
                    logger?.LogMessage(file.FullPath);
                    storage.CopyToStream(outFile, storage.Length, logger);
                }
            }
        }
    }
}
