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
        public HierarchicalRomFileTable FileTable { get; }

        public RomFsBuilder(IFileSystem input)
        {
            DirectoryEntry[] entries = input.EnumerateEntries().ToArray();
            int fileCount = entries.Count(x => x.Type == DirectoryEntryType.File);
            int dirCount = entries.Count(x => x.Type == DirectoryEntryType.Directory);

            FileTable = new HierarchicalRomFileTable(dirCount, fileCount);

            long offset = 0;

            foreach (DirectoryEntry file in entries.Where(x => x.Type == DirectoryEntryType.File).OrderBy(x => x.FullPath, StringComparer.Ordinal))
            {
                var fileInfo = new RomFileInfo();
                fileInfo.Offset = offset;
                fileInfo.Length = file.Size;

                IStorage fileStorage = input.OpenFile(file.FullPath, OpenMode.Read).AsStorage();
                Sources.Add(fileStorage);

                long newOffset = offset + file.Size;
                offset = Util.AlignUp(newOffset, FileAlignment);

                var padding = new NullStorage(offset - newOffset);
                Sources.Add(padding);

                FileTable.CreateFile(file.FullPath, ref fileInfo);
            }
        }

        public IStorage Build()
        {
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
