using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LibHac.Streams;

namespace LibHac.Savefile
{
    public class Savefile
    {
        public Header Header { get; }
        private RemapStream FileRemap { get; }
        public SharedStreamSource FileRemapSource { get; }
        private RemapStream MetaRemap { get; }
        public SharedStreamSource MetaRemapSource { get; }
        private JournalStream JournalStream { get; }
        public SharedStreamSource JournalStreamSource { get; }

        public Stream DuplexL1A { get; }
        public Stream DuplexL1B { get; }
        public Stream DuplexDataA { get; }
        public Stream DuplexDataB { get; }
        public Stream JournalData { get; }

        public Stream JournalTable { get; }
        public Stream JournalBitmapUpdatedPhysical { get; }
        public Stream JournalBitmapUpdatedVirtual { get; }
        public Stream JournalBitmapUnassigned { get; }
        public Stream JournalLayer1Hash { get; }
        public Stream JournalLayer2Hash { get; }
        public Stream JournalLayer3Hash { get; }
        public Stream JournalFat { get; }

        public FileEntry[] Files { get; private set; }
        private Dictionary<string, FileEntry> FileDict { get; }

        public Savefile(Stream file, IProgressReport logger = null)
        {
            using (var reader = new BinaryReader(file, Encoding.Default, true))
            {
                Header = new Header(reader, logger);
                var layout = Header.Layout;
                FileRemap = new RemapStream(
                    new SubStream(file, layout.FileMapDataOffset, layout.FileMapDataSize),
                    Header.FileMapEntries, Header.FileRemap.MapSegmentCount);

                FileRemapSource = new SharedStreamSource(FileRemap);

                DuplexL1A = FileRemapSource.CreateStream(layout.DuplexL1OffsetA, layout.DuplexL1Size);
                DuplexL1B = FileRemapSource.CreateStream(layout.DuplexL1OffsetB, layout.DuplexL1Size);
                DuplexDataA = FileRemapSource.CreateStream(layout.DuplexDataOffsetA, layout.DuplexDataSize);
                DuplexDataB = FileRemapSource.CreateStream(layout.DuplexDataOffsetB, layout.DuplexDataSize);
                JournalData = FileRemapSource.CreateStream(layout.JournalDataOffset, layout.JournalDataSizeB + layout.SizeReservedArea);

                var duplexData = layout.DuplexIndex == 0 ? DuplexDataA : DuplexDataB;
                MetaRemap = new RemapStream(duplexData, Header.MetaMapEntries, Header.MetaRemap.MapSegmentCount);
                MetaRemapSource = new SharedStreamSource(MetaRemap);

                JournalTable = MetaRemapSource.CreateStream(layout.JournalTableOffset, layout.JournalTableSize);
                JournalBitmapUpdatedPhysical = MetaRemapSource.CreateStream(layout.JournalBitmapUpdatedPhysicalOffset, layout.JournalBitmapUpdatedPhysicalSize);
                JournalBitmapUpdatedVirtual = MetaRemapSource.CreateStream(layout.JournalBitmapUpdatedVirtualOffset, layout.JournalBitmapUpdatedVirtualSize);
                JournalBitmapUnassigned = MetaRemapSource.CreateStream(layout.JournalBitmapUnassignedOffset, layout.JournalBitmapUnassignedSize);
                JournalLayer1Hash = MetaRemapSource.CreateStream(layout.Layer1HashOffset, layout.Layer1HashSize);
                JournalLayer2Hash = MetaRemapSource.CreateStream(layout.Layer2HashOffset, layout.Layer2HashSize);
                JournalLayer3Hash = MetaRemapSource.CreateStream(layout.Layer3HashOffset, layout.Layer3HashSize);
                JournalFat = MetaRemapSource.CreateStream(layout.Field148, layout.Field150);

                var journalMap = JournalStream.ReadMappingEntries(JournalTable, Header.Journal.MappingEntryCount);

                var journalData = FileRemapSource.CreateStream(layout.JournalDataOffset,
                    layout.JournalDataSizeB + layout.SizeReservedArea);
                JournalStream = new JournalStream(journalData, journalMap, (int)Header.Journal.BlockSize);
                JournalStreamSource = new SharedStreamSource(JournalStream);
                ReadFileInfo();
                Dictionary<string, FileEntry> dictionary = new Dictionary<string, FileEntry>();
                foreach (FileEntry entry in Files)
                {
                    dictionary[entry.FullPath] = entry;
                }

                FileDict = dictionary;
            }
        }

        public Stream OpenFile(string filename)
        {
            if (!FileDict.TryGetValue(filename, out FileEntry file))
            {
                throw new FileNotFoundException();
            }

            return OpenFile(file);
        }

        public Stream OpenFile(FileEntry file)
        {
            return JournalStreamSource.CreateStream(file.Offset, file.Size);
        }

        public bool FileExists(string filename) => FileDict.ContainsKey(filename);

        private void ReadFileInfo()
        {
            var blockSize = Header.Save.BlockSize;
            var dirOffset = Header.Save.DirectoryTableBlock * blockSize;
            var fileOffset = Header.Save.FileTableBlock * blockSize;

            FileEntry[] dirEntries;
            FileEntry[] fileEntries;
            using (var reader = new BinaryReader(JournalStreamSource.CreateStream(), Encoding.Default, true))
            {
                reader.BaseStream.Position = dirOffset;
                dirEntries = ReadFileEntries(reader);

                reader.BaseStream.Position = fileOffset;
                fileEntries = ReadFileEntries(reader);
            }

            foreach (var dir in dirEntries)
            {
                if (dir.NextIndex != 0) dir.Next = dirEntries[dir.NextIndex];
                if (dir.ParentDirIndex != 0 && dir.ParentDirIndex < dirEntries.Length)
                    dir.ParentDir = dirEntries[dir.ParentDirIndex];
            }

            foreach (var file in fileEntries)
            {
                if (file.NextIndex != 0) file.Next = fileEntries[file.NextIndex];
                if (file.ParentDirIndex != 0 && file.ParentDirIndex < dirEntries.Length)
                    file.ParentDir = dirEntries[file.ParentDirIndex];
                file.Offset = file.BlockIndex < 0 ? 0 : file.BlockIndex * blockSize;
            }

            Files = new FileEntry[fileEntries.Length - 2];
            Array.Copy(fileEntries, 2, Files, 0, Files.Length);

            FileEntry.ResolveFilenames(Files);
        }

        private FileEntry[] ReadFileEntries(BinaryReader reader)
        {
            var count = reader.ReadInt32();
            reader.BaseStream.Position -= 4;

            var entries = new FileEntry[count];
            for (int i = 0; i < count; i++)
            {
                entries[i] = new FileEntry(reader);
            }

            return entries;
        }
    }

    public static class SavefileExtensions
    {
        public static void Extract(this Savefile save, string outDir, IProgressReport logger = null)
        {
            foreach (var file in save.Files)
            {
                var stream = save.OpenFile(file);
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
