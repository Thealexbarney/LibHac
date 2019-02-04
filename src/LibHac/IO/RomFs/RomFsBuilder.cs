using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LibHac.IO.RomFs
{
    public class RomFsBuilder
    {
        private const int FileAlignment = 0x10;
        private const int HeaderSize = 0x50;
        private const int HeaderWithPaddingSize = 0x200;

        public List<IStorage> Sources { get; } = new List<IStorage>();
        public HierarchicalRomFileTable FileTable { get; } = new HierarchicalRomFileTable();
        private long CurrentOffset { get; set; }

        public RomFsBuilder() { }

        public RomFsBuilder(IFileSystem input)
        {
            foreach (DirectoryEntry file in input.EnumerateEntries().Where(x => x.Type == DirectoryEntryType.File)
                .OrderBy(x => x.FullPath, StringComparer.Ordinal))
            {
                AddFile(file.FullPath, input.OpenFile(file.FullPath, OpenMode.Read));
            }
        }

        public void AddFile(string path, IFile file)
        {
            var fileInfo = new RomFileInfo();
            long fileSize = file.GetSize();

            fileInfo.Offset = CurrentOffset;
            fileInfo.Length = fileSize;

            IStorage fileStorage = file.AsStorage();
            Sources.Add(fileStorage);

            long newOffset = CurrentOffset + fileSize;
            CurrentOffset = Util.AlignUp(newOffset, FileAlignment);

            var padding = new NullStorage(CurrentOffset - newOffset);
            Sources.Add(padding);

            FileTable.CreateFile(path, ref fileInfo);
        }

        public IStorage Build()
        {
            FileTable.TrimExcess();

            var header = new byte[HeaderWithPaddingSize];
            var headerWriter = new BinaryWriter(new MemoryStream(header));

            var sources = new List<IStorage>();
            sources.Add(new MemoryStorage(header));
            sources.AddRange(Sources);

            long fileLength = sources.Sum(x => x.Length);

            headerWriter.Write((long)HeaderSize);

            AddTable(FileTable.GetDirectoryBuckets());
            AddTable(FileTable.GetDirectoryEntries());
            AddTable(FileTable.GetFileBuckets());
            AddTable(FileTable.GetFileEntries());

            headerWriter.Write((long)HeaderWithPaddingSize);

            return new ConcatenationStorage(sources, true);

            void AddTable(byte[] table)
            {
                sources.Add(new MemoryStorage(table));
                headerWriter.Write(fileLength);
                headerWriter.Write((long)table.Length);
                fileLength += table.Length;
            }
        }
    }
}
