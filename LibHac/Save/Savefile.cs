using System.IO;
using System.Security.Cryptography;
using System.Text;
using LibHac.IO;
using LibHac.IO.Save;

namespace LibHac.Save
{
    public class Savefile
    {
        public Header Header { get; }
        public Storage BaseStorage { get; }

        public HierarchicalIntegrityVerificationStorage IvfcStorage { get; }
        public SaveFs SaveFs { get; }

        public RemapStorage DataRemapStorage { get; }
        public RemapStorage MetaRemapStorage { get; }

        public HierarchicalDuplexStorage DuplexStorage { get; }
        public JournalStorage JournalStorage { get; }

        public DirectoryEntry RootDirectory => SaveFs.RootDirectory;
        public FileEntry[] Files => SaveFs.Files;
        public DirectoryEntry[] Directories => SaveFs.Directories;

        public Savefile(Keyset keyset, Storage storage, IntegrityCheckLevel integrityCheckLevel)
        {
            BaseStorage = storage;

            Header = new Header(keyset, BaseStorage);
            FsLayout layout = Header.Layout;

            DataRemapStorage = new RemapStorage(BaseStorage.Slice(layout.FileMapDataOffset, layout.FileMapDataSize),
                    Header.FileRemap, Header.FileMapEntries);

            DuplexStorage = InitDuplexStorage(DataRemapStorage, Header);

            MetaRemapStorage = new RemapStorage(DuplexStorage, Header.MetaRemap, Header.MetaMapEntries);

            Storage journalTable = MetaRemapStorage.Slice(layout.JournalTableOffset, layout.JournalTableSize);

            JournalMapEntry[] journalMap = JournalStorage.ReadMapEntries(journalTable, Header.Journal.MainDataBlockCount);

            Storage journalData = DataRemapStorage.Slice(layout.JournalDataOffset,
                layout.JournalDataSizeB + layout.SizeReservedArea);

            JournalStorage = new JournalStorage(journalData, journalMap, (int)Header.Journal.BlockSize);

            IvfcStorage = InitIvfcStorage(integrityCheckLevel);

            SaveFs = new SaveFs(IvfcStorage, MetaRemapStorage.Slice(layout.FatOffset, layout.FatSize), Header.Save);
        }

        private static HierarchicalDuplexStorage InitDuplexStorage(Storage baseStorage, Header header)
        {
            FsLayout layout = header.Layout;
            var duplexLayers = new DuplexFsLayerInfo2[3];

            duplexLayers[0] = new DuplexFsLayerInfo2
            {
                DataA = new MemoryStorage(header.DuplexMasterA),
                DataB = new MemoryStorage(header.DuplexMasterB),
                Info = header.Duplex.Layers[0]
            };

            duplexLayers[1] = new DuplexFsLayerInfo2
            {
                DataA = baseStorage.Slice(layout.DuplexL1OffsetA, layout.DuplexL1Size),
                DataB = baseStorage.Slice(layout.DuplexL1OffsetB, layout.DuplexL1Size),
                Info = header.Duplex.Layers[1]
            };

            duplexLayers[2] = new DuplexFsLayerInfo2
            {
                DataA = baseStorage.Slice(layout.DuplexDataOffsetA, layout.DuplexDataSize),
                DataB = baseStorage.Slice(layout.DuplexDataOffsetB, layout.DuplexDataSize),
                Info = header.Duplex.Layers[2]
            };

            return new HierarchicalDuplexStorage(duplexLayers, layout.DuplexIndex == 1);
        }

        private HierarchicalIntegrityVerificationStorage InitIvfcStorage(IntegrityCheckLevel integrityCheckLevel)
        {
            IvfcHeader ivfc = Header.Ivfc;

            const int ivfcLevels = 5;
            var initInfo = new IntegrityVerificationInfoStorage[ivfcLevels];

            initInfo[0] = new IntegrityVerificationInfoStorage
            {
                Data = Header.MasterHash,
                BlockSize = 0,
                Type = IntegrityStorageType.Save
            };

            for (int i = 1; i < ivfcLevels; i++)
            {
                IvfcLevelHeader level = ivfc.LevelHeaders[i - 1];

                Storage data = i == ivfcLevels - 1
                    ? (Storage)JournalStorage
                    : MetaRemapStorage.Slice(level.LogicalOffset, level.HashDataSize);

                initInfo[i] = new IntegrityVerificationInfoStorage
                {
                    Data = data,
                    BlockSize = 1 << level.BlockSizePower,
                    Salt = new HMACSHA256(Encoding.ASCII.GetBytes(SaltSources[i - 1])).ComputeHash(ivfc.SaltSource),
                    Type = IntegrityStorageType.Save
                };
            }

            return new HierarchicalIntegrityVerificationStorage(initInfo, integrityCheckLevel);
        }

        public Storage OpenFile(string filename)
        {
            return SaveFs.OpenFile(filename);
        }

        public Storage OpenFile(FileEntry file)
        {
            return SaveFs.OpenFile(file);
        }

        public bool FileExists(string filename) => SaveFs.FileExists(filename);

        public bool CommitHeader(Keyset keyset)
        {
            // todo
            Stream headerStream = BaseStorage.AsStream();

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
            Validity validity = IvfcStorage.Validate(true, logger);
            IvfcStorage.SetLevelValidities(Header.Ivfc);

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
                Storage storage = save.OpenFile(file);
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
