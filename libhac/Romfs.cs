using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using libhac.Streams;

namespace libhac
{
    public class Romfs
    {
        internal const int IvfcMaxLevel = 6;
        public RomfsHeader Header { get; }
        public List<RomfsDir> Directories { get; } = new List<RomfsDir>();
        public List<RomfsFile> Files { get; } = new List<RomfsFile>();
        public RomfsDir RootDir { get; }

        private Dictionary<string, RomfsFile> FileDict { get; }
        private SharedStreamSource StreamSource { get; }

        public Romfs(Stream stream)
        {
            StreamSource = new SharedStreamSource(stream);

            byte[] dirMetaTable;
            byte[] fileMetaTable;
            using (var reader = new BinaryReader(StreamSource.CreateStream(), Encoding.Default, true))
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
                    if (dir.ParentOffset == position) RootDir = dir;
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
            ResolveFilenames();
            FileDict = Files.ToDictionary(x => x.FullPath, x => x);
        }

        public Stream OpenFile(string filename)
        {
            if (!FileDict.TryGetValue(filename, out RomfsFile file))
            {
                throw new FileNotFoundException();
            }

            return OpenFile(file);
        }

        public Stream OpenFile(RomfsFile file)
        {
            return StreamSource.CreateStream(Header.DataOffset + file.DataOffset, file.DataLength);
        }

        public byte[] GetFile(string filename)
        {
            var stream = OpenFile(filename);
            var file = new byte[stream.Length];
            using (var ms = new MemoryStream(file))
            {
                stream.CopyTo(ms);
            }

            return file;
        }

        public bool FileExists(string filename) => FileDict.ContainsKey(filename);

        public Stream OpenRawStream() => StreamSource.CreateStream();

        private void SetReferences()
        {
            var dirDict = Directories.ToDictionary(x => x.Offset, x => x);
            var fileDict = Files.ToDictionary(x => x.Offset, x => x);

            foreach (var dir in Directories)
            {
                if (dir.ParentOffset >= 0 && dir.ParentOffset != dir.Offset) dir.Parent = dirDict[dir.ParentOffset];
                if (dir.NextSiblingOffset >= 0) dir.NextSibling = dirDict[dir.NextSiblingOffset];
                if (dir.FirstChildOffset >= 0) dir.FirstChild = dirDict[dir.FirstChildOffset];
                if (dir.FirstFileOffset >= 0) dir.FirstFile = fileDict[dir.FirstFileOffset];
                if (dir.NextDirHashOffset >= 0) dir.NextDirHash = dirDict[dir.NextDirHashOffset];
            }

            foreach (var file in Files)
            {
                if (file.ParentDirOffset >= 0) file.ParentDir = dirDict[file.ParentDirOffset];
                if (file.NextSiblingOffset >= 0) file.NextSibling = fileDict[file.NextSiblingOffset];
                if (file.NextFileHashOffset >= 0) file.NextFileHash = fileDict[file.NextFileHashOffset];
            }
        }

        private void ResolveFilenames()
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            var delimiter = "/";
            foreach (var file in Files)
            {
                list.Add(file.Name);
                var dir = file.ParentDir;
                while (dir != null)
                {
                    list.Add(delimiter);
                    list.Add(dir.Name);
                    dir = dir.Parent;
                }

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    sb.Append(list[i]);
                }

                file.FullPath = sb.ToString();
                list.Clear();
                sb.Clear();
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

    [DebuggerDisplay("{" + nameof(Name) + "}")]
    public class RomfsDir
    {
        public int Offset { get; set; }
        public int ParentOffset { get; }
        public int NextSiblingOffset { get; }
        public int FirstChildOffset { get; }
        public int FirstFileOffset { get; }
        public int NextDirHashOffset { get; }
        public int NameLength { get; }
        public string Name { get; }

        public RomfsDir Parent { get; internal set; }
        public RomfsDir NextSibling { get; internal set; }
        public RomfsDir FirstChild { get; internal set; }
        public RomfsFile FirstFile { get; internal set; }
        public RomfsDir NextDirHash { get; internal set; }

        public RomfsDir(BinaryReader reader)
        {
            ParentOffset = reader.ReadInt32();
            NextSiblingOffset = reader.ReadInt32();
            FirstChildOffset = reader.ReadInt32();
            FirstFileOffset = reader.ReadInt32();
            NextDirHashOffset = reader.ReadInt32();
            NameLength = reader.ReadInt32();
            Name = reader.ReadUtf8(NameLength);
            reader.BaseStream.Position = Util.GetNextMultiple(reader.BaseStream.Position, 4);
        }
    }

    [DebuggerDisplay("{" + nameof(Name) + "}")]
    public class RomfsFile
    {
        public int Offset { get; set; }
        public int ParentDirOffset { get; }
        public int NextSiblingOffset { get; }
        public long DataOffset { get; }
        public long DataLength { get; }
        public int NextFileHashOffset { get; }
        public int NameLength { get; }
        public string Name { get; }

        public RomfsDir ParentDir { get; internal set; }
        public RomfsFile NextSibling { get; internal set; }
        public RomfsFile NextFileHash { get; internal set; }
        public string FullPath { get; set; }

        public RomfsFile(BinaryReader reader)
        {
            ParentDirOffset = reader.ReadInt32();
            NextSiblingOffset = reader.ReadInt32();
            DataOffset = reader.ReadInt64();
            DataLength = reader.ReadInt64();
            NextFileHashOffset = reader.ReadInt32();
            NameLength = reader.ReadInt32();
            Name = reader.ReadUtf8(NameLength);
            reader.BaseStream.Position = Util.GetNextMultiple(reader.BaseStream.Position, 4);
        }
    }

    public class IvfcLevel
    {
        public long DataOffset { get; set; }
        public long DataSize { get; set; }
        public long HashOffset { get; set; }
        public long HashSize { get; set; }
        public long HashBlockSize { get; set; }
        public long HashBlockCount { get; set; }
        public Validity HashValidity { get; set; }
    }

    public static class RomfsExtensions
    {
        public static void Extract(this Romfs romfs, string outDir, IProgressReport logger = null)
        {
            foreach (var file in romfs.Files)
            {
                var stream = romfs.OpenFile(file);
                var outName = outDir + file.FullPath;
                var dir = Path.GetDirectoryName(outName);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

                using (var outFile = new FileStream(outName, FileMode.Create, FileAccess.ReadWrite))
                {
                    logger?.LogMessage(file.FullPath);
                    stream.CopyStream(outFile, stream.Length, logger);
                }
            }
        }
    }
}
