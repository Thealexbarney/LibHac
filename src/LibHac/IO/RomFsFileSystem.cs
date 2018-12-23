using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LibHac.IO
{
    public class RomFsFileSystem : IFileSystem
    {
        public RomfsHeader Header { get; }
        public List<RomfsDir> Directories { get; } = new List<RomfsDir>();
        public List<RomfsFile> Files { get; } = new List<RomfsFile>();
        public RomfsDir RootDir { get; }

        public Dictionary<string, RomfsFile> FileDict { get; }
        public Dictionary<string, RomfsDir> DirectoryDict { get; }
        private IStorage BaseStorage { get; }

        // todo Don't parse entire table when opening
        public RomFsFileSystem(IStorage storage)
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
            DirectoryDict = Directories.ToDictionary(x => x.FullPath, x => x);
        }

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

        public void Commit()
        {
            throw new NotSupportedException();
        }

        public void CreateDirectory(string path)
        {
            throw new NotSupportedException();
        }

        public void CreateFile(string path, long size)
        {
            throw new NotSupportedException();
        }

        public void DeleteDirectory(string path)
        {
            throw new NotSupportedException();
        }

        public void DeleteFile(string path)
        {
            throw new NotSupportedException();
        }

        public IDirectory OpenDirectory(string path, OpenDirectoryMode mode)
        {
            return new RomFsDirectory(this, path, mode);
        }

        public IFile OpenFile(string path)
        {
            if (!FileDict.TryGetValue(path, out RomfsFile file))
            {
                throw new FileNotFoundException();
            }

            return OpenFile(file);
        }

        public IFile OpenFile(RomfsFile file)
        {
            return new RomFsFile(BaseStorage, Header.DataOffset + file.DataOffset, file.DataLength);
        }

        public void RenameDirectory(string srcPath, string dstPath)
        {
            throw new NotSupportedException();
        }

        public void RenameFile(string srcPath, string dstPath)
        {
            throw new NotSupportedException();
        }

        public bool DirectoryExists(string path)
        {
            return DirectoryDict.ContainsKey(path);
        }

        public bool FileExists(string path)
        {
            return FileDict.ContainsKey(path);
        }
    }
}
