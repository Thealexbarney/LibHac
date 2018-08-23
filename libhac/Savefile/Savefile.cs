using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using libhac.Streams;

namespace libhac.Savefile
{
    public class Savefile
    {
        public Header Header { get; }
        public RemapStream FileRemap { get; }
        public RemapStream MetaRemap { get; }
        private Stream FileStream { get; }
        public JournalStream JournalStream { get; }

        public byte[] DuplexL1A { get; }
        public byte[] DuplexL1B { get; }
        public byte[] DuplexDataA { get; }
        public byte[] DuplexDataB { get; }

        public byte[] JournalTable { get; }
        public byte[] JournalBitmapUpdatedPhysical { get; }
        public byte[] JournalBitmapUpdatedVirtual { get; }
        public byte[] JournalBitmapUnassigned { get; }
        public byte[] JournalLayer1Hash { get; }
        public byte[] JournalLayer2Hash { get; }
        public byte[] JournalLayer3Hash { get; }
        public byte[] JournalFat { get; }

        public FileEntry[] Files { get; private set; }
        private Dictionary<string, FileEntry> FileDict { get; }

        public Savefile(Stream file, IProgressReport logger = null)
        {
            FileStream = file;
            using (var reader = new BinaryReader(file, Encoding.Default, true))
            {
                Header = new Header(reader, logger);
                var layout = Header.Layout;
                FileRemap = new RemapStream(
                    new SubStream(file, layout.FileMapDataOffset, layout.FileMapDataSize),
                    Header.FileMapEntries, Header.FileRemap.MapSegmentCount);

                DuplexL1A = new byte[layout.DuplexL1Size];
                DuplexL1B = new byte[layout.DuplexL1Size];
                DuplexDataA = new byte[layout.DuplexDataSize];
                DuplexDataB = new byte[layout.DuplexDataSize];

                FileRemap.Position = layout.DuplexL1OffsetA;
                FileRemap.Read(DuplexL1A, 0, DuplexL1A.Length);
                FileRemap.Position = layout.DuplexL1OffsetB;
                FileRemap.Read(DuplexL1B, 0, DuplexL1B.Length);
                FileRemap.Position = layout.DuplexDataOffsetA;
                FileRemap.Read(DuplexDataA, 0, DuplexDataA.Length);
                FileRemap.Position = layout.DuplexDataOffsetB;
                FileRemap.Read(DuplexDataB, 0, DuplexDataB.Length);

                var duplexDataOffset = layout.DuplexIndex == 0 ? layout.DuplexDataOffsetA : layout.DuplexDataOffsetB;
                var duplexData = new SubStream(FileRemap, duplexDataOffset, layout.DuplexDataSize);
                MetaRemap = new RemapStream(duplexData, Header.MetaMapEntries, Header.MetaRemap.MapSegmentCount);

                JournalTable = new byte[layout.JournalTableSize];
                JournalBitmapUpdatedPhysical = new byte[layout.JournalBitmapUpdatedPhysicalSize];
                JournalBitmapUpdatedVirtual = new byte[layout.JournalBitmapUpdatedVirtualSize];
                JournalBitmapUnassigned = new byte[layout.JournalBitmapUnassignedSize];
                JournalLayer1Hash = new byte[layout.Layer1HashSize];
                JournalLayer2Hash = new byte[layout.Layer2HashSize];
                JournalLayer3Hash = new byte[layout.Layer3HashSize];
                JournalFat = new byte[layout.Field150];

                MetaRemap.Position = layout.JournalTableOffset;
                MetaRemap.Read(JournalTable, 0, JournalTable.Length);
                MetaRemap.Position = layout.JournalBitmapUpdatedPhysicalOffset;
                MetaRemap.Read(JournalBitmapUpdatedPhysical, 0, JournalBitmapUpdatedPhysical.Length);
                MetaRemap.Position = layout.JournalBitmapUpdatedVirtualOffset;
                MetaRemap.Read(JournalBitmapUpdatedVirtual, 0, JournalBitmapUpdatedVirtual.Length);
                MetaRemap.Position = layout.JournalBitmapUnassignedOffset;
                MetaRemap.Read(JournalBitmapUnassigned, 0, JournalBitmapUnassigned.Length);
                MetaRemap.Position = layout.Layer1HashOffset;
                MetaRemap.Read(JournalLayer1Hash, 0, JournalLayer1Hash.Length);
                MetaRemap.Position = layout.Layer2HashOffset;
                MetaRemap.Read(JournalLayer2Hash, 0, JournalLayer2Hash.Length);
                MetaRemap.Position = layout.Layer3HashOffset;
                MetaRemap.Read(JournalLayer3Hash, 0, JournalLayer3Hash.Length);
                MetaRemap.Position = layout.Field148;
                MetaRemap.Read(JournalFat, 0, JournalFat.Length);

                var journalMap = JournalStream.ReadMappingEntries(JournalTable, JournalBitmapUpdatedPhysical,
                    JournalBitmapUpdatedVirtual, JournalBitmapUnassigned, Header.Journal.MappingEntryCount);

                var journalData = new SubStream(FileRemap, layout.JournalDataOffset,
                    layout.JournalDataSizeB + layout.SizeReservedArea);
                JournalStream = new JournalStream(journalData, journalMap, (int)Header.Journal.BlockSize);
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
            return new SubStream(JournalStream, file.Offset, file.Size);
        }

        public bool FileExists(string filename) => FileDict.ContainsKey(filename);

        private void ReadFileInfo()
        {
            var blockSize = Header.Save.BlockSize;
            var dirOffset = Header.Save.DirectoryTableBlock * blockSize;
            var fileOffset = Header.Save.FileTableBlock * blockSize;

            FileEntry[] dirEntries;
            FileEntry[] fileEntries;
            using (var reader = new BinaryReader(JournalStream, Encoding.Default, true))
            {
                JournalStream.Position = dirOffset;
                dirEntries = ReadFileEntries(reader);

                JournalStream.Position = fileOffset;
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
            JournalStream.Position -= 4;

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
