using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LibHac.Streams;

namespace LibHac.Savefile
{
    public class Savefile
    {
        public Header Header { get; }
        private RemapStream FileRemap { get; }
        public SharedStreamSource SavefileSource { get; }
        public SharedStreamSource FileRemapSource { get; }
        private RemapStream MetaRemap { get; }
        public SharedStreamSource MetaRemapSource { get; }
        private JournalStream JournalStream { get; }
        public SharedStreamSource JournalStreamSource { get; }
        private HierarchicalIntegrityVerificationStream IvfcStream { get; }
        public SharedStreamSource IvfcStreamSource { get; }
        private AllocationTable AllocationTable { get; }

        public Stream DuplexL1A { get; }
        public Stream DuplexL1B { get; }
        public Stream DuplexDataA { get; }
        public Stream DuplexDataB { get; }
        public LayeredDuplexFs DuplexData { get; }
        public Stream JournalData { get; }

        public Stream JournalTable { get; }
        public Stream JournalBitmapUpdatedPhysical { get; }
        public Stream JournalBitmapUpdatedVirtual { get; }
        public Stream JournalBitmapUnassigned { get; }
        public Stream JournalLayer1Hash { get; }
        public Stream JournalLayer2Hash { get; }
        public Stream JournalLayer3Hash { get; }
        public Stream JournalFat { get; }

        public DirectoryEntry RootDirectory { get; private set; }
        public FileEntry[] Files { get; private set; }
        public DirectoryEntry[] Directories { get; private set; }
        private Dictionary<string, FileEntry> FileDict { get; }

        public Savefile(Keyset keyset, Stream file, IntegrityCheckLevel integrityCheckLevel)
        {
            SavefileSource = new SharedStreamSource(file);

            using (var reader = new BinaryReader(SavefileSource.CreateStream(), Encoding.Default, true))
            {
                Header = new Header(keyset, reader);
                FsLayout layout = Header.Layout;

                FileRemap = new RemapStream(
                    SavefileSource.CreateStream(layout.FileMapDataOffset, layout.FileMapDataSize),
                    Header.FileMapEntries, Header.FileRemap.MapSegmentCount);

                FileRemapSource = new SharedStreamSource(FileRemap);

                var duplexLayers = new DuplexFsLayerInfo[3];

                duplexLayers[0] = new DuplexFsLayerInfo
                {
                    DataA = new MemoryStream(Header.DuplexMasterA),
                    DataB = new MemoryStream(Header.DuplexMasterB),
                    Info = Header.Duplex.Layers[0]
                };

                duplexLayers[1] = new DuplexFsLayerInfo
                {
                    DataA = FileRemapSource.CreateStream(layout.DuplexL1OffsetA, layout.DuplexL1Size),
                    DataB = FileRemapSource.CreateStream(layout.DuplexL1OffsetB, layout.DuplexL1Size),
                    Info = Header.Duplex.Layers[1]
                };

                duplexLayers[2] = new DuplexFsLayerInfo
                {
                    DataA = FileRemapSource.CreateStream(layout.DuplexDataOffsetA, layout.DuplexDataSize),
                    DataB = FileRemapSource.CreateStream(layout.DuplexDataOffsetB, layout.DuplexDataSize),
                    Info = Header.Duplex.Layers[2]
                };

                DuplexL1A = FileRemapSource.CreateStream(layout.DuplexL1OffsetA, layout.DuplexL1Size);
                DuplexL1B = FileRemapSource.CreateStream(layout.DuplexL1OffsetB, layout.DuplexL1Size);
                DuplexDataA = FileRemapSource.CreateStream(layout.DuplexDataOffsetA, layout.DuplexDataSize);
                DuplexDataB = FileRemapSource.CreateStream(layout.DuplexDataOffsetB, layout.DuplexDataSize);
                JournalData = FileRemapSource.CreateStream(layout.JournalDataOffset, layout.JournalDataSizeB + layout.SizeReservedArea);

                DuplexData = new LayeredDuplexFs(duplexLayers, Header.Layout.DuplexIndex == 1);
                MetaRemap = new RemapStream(DuplexData, Header.MetaMapEntries, Header.MetaRemap.MapSegmentCount);
                MetaRemapSource = new SharedStreamSource(MetaRemap);

                JournalTable = MetaRemapSource.CreateStream(layout.JournalTableOffset, layout.JournalTableSize);
                JournalBitmapUpdatedPhysical = MetaRemapSource.CreateStream(layout.JournalBitmapUpdatedPhysicalOffset, layout.JournalBitmapUpdatedPhysicalSize);
                JournalBitmapUpdatedVirtual = MetaRemapSource.CreateStream(layout.JournalBitmapUpdatedVirtualOffset, layout.JournalBitmapUpdatedVirtualSize);
                JournalBitmapUnassigned = MetaRemapSource.CreateStream(layout.JournalBitmapUnassignedOffset, layout.JournalBitmapUnassignedSize);
                JournalLayer1Hash = MetaRemapSource.CreateStream(layout.IvfcL1Offset, layout.IvfcL1Size);
                JournalLayer2Hash = MetaRemapSource.CreateStream(layout.IvfcL2Offset, layout.IvfcL2Size);
                JournalLayer3Hash = MetaRemapSource.CreateStream(layout.IvfcL3Offset, layout.IvfcL3Size);
                JournalFat = MetaRemapSource.CreateStream(layout.FatOffset, layout.FatSize);
                AllocationTable = new AllocationTable(JournalFat);

                MappingEntry[] journalMap = JournalStream.ReadMappingEntries(JournalTable, Header.Journal.MainDataBlockCount);

                SharedStream journalData = FileRemapSource.CreateStream(layout.JournalDataOffset,
                    layout.JournalDataSizeB + layout.SizeReservedArea);
                JournalStream = new JournalStream(journalData, journalMap, (int)Header.Journal.BlockSize);
                JournalStreamSource = new SharedStreamSource(JournalStream);

                IvfcStream = InitIvfcStream(integrityCheckLevel);
                IvfcStreamSource = new SharedStreamSource(IvfcStream);

                ReadFileInfo();
                var dictionary = new Dictionary<string, FileEntry>();
                foreach (FileEntry entry in Files)
                {
                    dictionary[entry.FullPath] = entry;
                }

                FileDict = dictionary;
            }
        }

        private HierarchicalIntegrityVerificationStream InitIvfcStream(IntegrityCheckLevel integrityCheckLevel)
        {
            IvfcHeader ivfc = Header.Ivfc;

            const int ivfcLevels = 5;
            var initInfo = new IntegrityVerificationInfo[ivfcLevels];

            initInfo[0] = new IntegrityVerificationInfo
            {
                Data = new MemoryStream(Header.MasterHashA),
                BlockSize = 0,
                Type = IntegrityStreamType.Save
            };

            for (int i = 1; i < ivfcLevels; i++)
            {
                IvfcLevelHeader level = ivfc.LevelHeaders[i - 1];

                Stream data = i == ivfcLevels - 1
                    ? (Stream)JournalStream
                    : MetaRemapSource.CreateStream(level.LogicalOffset, level.HashDataSize);

                initInfo[i] = new IntegrityVerificationInfo
                {
                    Data = data,
                    BlockSize = 1 << level.BlockSizePower,
                    Salt = new HMACSHA256(Encoding.ASCII.GetBytes(SaltSources[i - 1])).ComputeHash(ivfc.SaltSource),
                    Type = IntegrityStreamType.Save
                };
            }

            return new HierarchicalIntegrityVerificationStream(initInfo, integrityCheckLevel);
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
            if (file.BlockIndex < 0)
            {
                return Stream.Null;
            }

            return OpenFatBlock(file.BlockIndex, file.FileSize);
        }

        private AllocationTableStream OpenFatBlock(int blockIndex, long size)
        {
            return new AllocationTableStream(IvfcStreamSource.CreateStream(), AllocationTable, (int)Header.Save.BlockSize, blockIndex, size);
        }

        public bool FileExists(string filename) => FileDict.ContainsKey(filename);

        private void ReadFileInfo()
        {
            // todo: Query the FAT for the file size when none is given
            AllocationTableStream dirTableStream = OpenFatBlock(Header.Save.DirectoryTableBlock, 1000000);
            AllocationTableStream fileTableStream = OpenFatBlock(Header.Save.FileTableBlock, 1000000);

            DirectoryEntry[] dirEntries = ReadDirEntries(dirTableStream);
            FileEntry[] fileEntries = ReadFileEntries(fileTableStream);

            foreach (DirectoryEntry dir in dirEntries)
            {
                if (dir.NextSiblingIndex != 0) dir.NextSibling = dirEntries[dir.NextSiblingIndex];
                if (dir.FirstChildIndex != 0) dir.FirstChild = dirEntries[dir.FirstChildIndex];
                if (dir.FirstFileIndex != 0) dir.FirstFile = fileEntries[dir.FirstFileIndex];
                if (dir.NextInChainIndex != 0) dir.NextInChain = dirEntries[dir.NextInChainIndex];
                if (dir.ParentDirIndex != 0 && dir.ParentDirIndex < dirEntries.Length)
                    dir.ParentDir = dirEntries[dir.ParentDirIndex];
            }

            foreach (FileEntry file in fileEntries)
            {
                if (file.NextSiblingIndex != 0) file.NextSibling = fileEntries[file.NextSiblingIndex];
                if (file.NextInChainIndex != 0) file.NextInChain = fileEntries[file.NextInChainIndex];
                if (file.ParentDirIndex != 0 && file.ParentDirIndex < dirEntries.Length)
                    file.ParentDir = dirEntries[file.ParentDirIndex];
            }

            RootDirectory = dirEntries[2];

            FileEntry fileChain = fileEntries[1].NextInChain;
            var files = new List<FileEntry>();
            while (fileChain != null)
            {
                files.Add(fileChain);
                fileChain = fileChain.NextInChain;
            }

            DirectoryEntry dirChain = dirEntries[1].NextInChain;
            var dirs = new List<DirectoryEntry>();
            while (dirChain != null)
            {
                dirs.Add(dirChain);
                dirChain = dirChain.NextInChain;
            }

            Files = files.ToArray();
            Directories = dirs.ToArray();

            FsEntry.ResolveFilenames(Files);
            FsEntry.ResolveFilenames(Directories);
        }

        private FileEntry[] ReadFileEntries(Stream stream)
        {
            var reader = new BinaryReader(stream);
            int count = reader.ReadInt32();

            reader.BaseStream.Position -= 4;

            var entries = new FileEntry[count];
            for (int i = 0; i < count; i++)
            {
                entries[i] = new FileEntry(reader);
            }

            return entries;
        }

        private DirectoryEntry[] ReadDirEntries(Stream stream)
        {
            var reader = new BinaryReader(stream);
            int count = reader.ReadInt32();

            reader.BaseStream.Position -= 4;

            var entries = new DirectoryEntry[count];
            for (int i = 0; i < count; i++)
            {
                entries[i] = new DirectoryEntry(reader);
            }

            return entries;
        }

        public bool SignHeader(Keyset keyset)
        {
            if (keyset.SaveMacKey.IsEmpty()) return false;

            var data = new byte[0x200];
            var cmac = new byte[0x10];

            SharedStream headerStream = SavefileSource.CreateStream();
            headerStream.Position = 0x100;
            headerStream.Read(data, 0, 0x200);

            Crypto.CalculateAesCmac(keyset.SaveMacKey, data, 0, cmac, 0, 0x200);

            headerStream.Position = 0;
            headerStream.Write(cmac, 0, 0x10);

            return true;
        }

        private string[] SaltSources =
        {
            "HierarchicalIntegrityVerificationStorage::Master",
            "HierarchicalIntegrityVerificationStorage::L1",
            "HierarchicalIntegrityVerificationStorage::L2",
            "HierarchicalIntegrityVerificationStorage::L3",
            "HierarchicalIntegrityVerificationStorage::L4",
            "HierarchicalIntegrityVerificationStorage::L5"
        };
    }

    public static class SavefileExtensions
    {
        public static void Extract(this Savefile save, string outDir, IProgressReport logger = null)
        {
            foreach (FileEntry file in save.Files)
            {
                Stream stream = save.OpenFile(file);
                string outName = outDir + file.FullPath;
                string dir = Path.GetDirectoryName(outName);
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
