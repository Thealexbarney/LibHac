using System.IO;
using System.Security.Cryptography;
using System.Text;
using LibHac.Streams;

namespace LibHac.Save
{
    public class Savefile
    {
        public Header Header { get; }
        public SharedStreamSource SavefileSource { get; }

        public SharedStreamSource JournalStreamSource { get; }
        private HierarchicalIntegrityVerificationStream IvfcStream { get; }
        public SharedStreamSource IvfcStreamSource { get; }
        public SaveFs SaveFs { get; }

        public RemapStorage DataRemapStorage { get; }
        public RemapStorage MetaRemapStorage { get; }

        public LayeredDuplexFs DuplexData { get; }

        public DirectoryEntry RootDirectory => SaveFs.RootDirectory;
        public FileEntry[] Files => SaveFs.Files;
        public DirectoryEntry[] Directories => SaveFs.Directories;

        public Savefile(Keyset keyset, Stream file, IntegrityCheckLevel integrityCheckLevel)
        {
            SavefileSource = new SharedStreamSource(file);

            Header = new Header(keyset, SavefileSource);
            FsLayout layout = Header.Layout;

            DataRemapStorage = new RemapStorage(SavefileSource.CreateStream(layout.FileMapDataOffset, layout.FileMapDataSize),
                    Header.FileRemap, Header.FileMapEntries);

            DuplexData = InitDuplexStream(DataRemapStorage, Header);

            MetaRemapStorage = new RemapStorage(DuplexData, Header.MetaRemap, Header.MetaMapEntries);

            Stream journalTable = MetaRemapStorage.OpenStream(layout.JournalTableOffset, layout.JournalTableSize);

            MappingEntry[] journalMap = JournalStream.ReadMappingEntries(journalTable, Header.Journal.MainDataBlockCount);

            Stream journalData = DataRemapStorage.OpenStream(layout.JournalDataOffset,
                layout.JournalDataSizeB + layout.SizeReservedArea);
            var journalStream = new JournalStream(journalData, journalMap, (int)Header.Journal.BlockSize);
            JournalStreamSource = new SharedStreamSource(journalStream);

            IvfcStream = InitIvfcStream(integrityCheckLevel);

            SaveFs = new SaveFs(IvfcStream, MetaRemapStorage.OpenStream(layout.FatOffset, layout.FatSize), Header.Save);

            IvfcStreamSource = new SharedStreamSource(IvfcStream);
        }

        private static LayeredDuplexFs InitDuplexStream(RemapStorage baseStorage, Header header)
        {
            FsLayout layout = header.Layout;
            var duplexLayers = new DuplexFsLayerInfo[3];

            duplexLayers[0] = new DuplexFsLayerInfo
            {
                DataA = new MemoryStream(header.DuplexMasterA),
                DataB = new MemoryStream(header.DuplexMasterB),
                Info = header.Duplex.Layers[0]
            };

            duplexLayers[1] = new DuplexFsLayerInfo
            {
                DataA = baseStorage.OpenStream(layout.DuplexL1OffsetA, layout.DuplexL1Size),
                DataB = baseStorage.OpenStream(layout.DuplexL1OffsetB, layout.DuplexL1Size),
                Info = header.Duplex.Layers[1]
            };

            duplexLayers[2] = new DuplexFsLayerInfo
            {
                DataA = baseStorage.OpenStream(layout.DuplexDataOffsetA, layout.DuplexDataSize),
                DataB = baseStorage.OpenStream(layout.DuplexDataOffsetB, layout.DuplexDataSize),
                Info = header.Duplex.Layers[2]
            };

            return new LayeredDuplexFs(duplexLayers, layout.DuplexIndex == 1);
        }

        private HierarchicalIntegrityVerificationStream InitIvfcStream(IntegrityCheckLevel integrityCheckLevel)
        {
            IvfcHeader ivfc = Header.Ivfc;

            const int ivfcLevels = 5;
            var initInfo = new IntegrityVerificationInfo[ivfcLevels];

            initInfo[0] = new IntegrityVerificationInfo
            {
                Data = Header.MasterHash,
                BlockSize = 0,
                Type = IntegrityStreamType.Save
            };

            for (int i = 1; i < ivfcLevels; i++)
            {
                IvfcLevelHeader level = ivfc.LevelHeaders[i - 1];

                Stream data = i == ivfcLevels - 1
                    ? JournalStreamSource.CreateStream()
                    : MetaRemapStorage.OpenStream(level.LogicalOffset, level.HashDataSize);

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
            return SaveFs.OpenFile(filename);
        }

        public Stream OpenFile(FileEntry file)
        {
            return SaveFs.OpenFile(file);
        }

        public bool FileExists(string filename) => SaveFs.FileExists(filename);

        public bool CommitHeader(Keyset keyset)
        {
            SharedStream headerStream = SavefileSource.CreateStream();

            var hashData = new byte[0x3d00];

            headerStream.Position = 0x300;
            headerStream.Read(hashData, 0, hashData.Length);

            byte[] hash = Crypto.ComputeSha256(hashData, 0, hashData.Length);
            headerStream.Position = 0x108;
            headerStream.Write(hash, 0, hash.Length);

            if (keyset.SaveMacKey.IsEmpty()) return false;

            var cmacData = new byte[0x200];
            var cmac = new byte[0x10];

            headerStream.Position = 0x100;
            headerStream.Read(cmacData, 0, 0x200);

            Crypto.CalculateAesCmac(keyset.SaveMacKey, cmacData, 0, cmac, 0, 0x200);

            headerStream.Position = 0;
            headerStream.Write(cmac, 0, 0x10);
            headerStream.Flush();

            return true;
        }

        public Validity Verify(IProgressReport logger = null)
        {
            Validity validity = IvfcStream.Validate(true, logger);
            IvfcStream.SetLevelValidities(Header.Ivfc);

            return validity;
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
