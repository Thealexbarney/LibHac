using System.IO;
using System.Security.Cryptography;
using System.Linq;

namespace LibHac.Savefile
{
    public class Header
    {
        public byte[] Cmac { get; set; }
        public FsLayout Layout { get; set; }
        public JournalHeader Journal { get; set; }
        public DuplexHeader Duplex { get; set; }
        public IvfcHeader Ivfc { get; set; }
        public SaveHeader Save { get; set; }

        public RemapHeader FileRemap { get; set; }
        public RemapHeader MetaRemap { get; set; }

        public MapEntry[] FileMapEntries { get; set; }
        public MapEntry[] MetaMapEntries { get; set; }

        public byte[] MasterHashA { get; }
        public byte[] MasterHashB { get; }
        public byte[] DuplexMasterA { get; }
        public byte[] DuplexMasterB { get; }

        public Validity SignatureValidity { get; }
        public Validity HeaderHashValidity { get; }

        public byte[] Data { get; }

        public Header(Keyset keyset, BinaryReader reader, IProgressReport logger = null)
        {
            reader.BaseStream.Position = 0;
            Data = reader.ReadBytes(0x4000);
            reader.BaseStream.Position = 0;

            Cmac = reader.ReadBytes(0x10);

            reader.BaseStream.Position = 0x100;
            Layout = new FsLayout(reader);

            reader.BaseStream.Position = 0x300;
            Duplex = new DuplexHeader(reader);

            reader.BaseStream.Position = 0x344;
            Ivfc = new IvfcHeader(reader);

            reader.BaseStream.Position = 0x408;
            Journal = new JournalHeader(reader);

            reader.BaseStream.Position = 0x608;
            Save = new SaveHeader(reader);

            reader.BaseStream.Position = 0x650;
            FileRemap = new RemapHeader(reader);
            reader.BaseStream.Position = 0x690;
            MetaRemap = new RemapHeader(reader);

            reader.BaseStream.Position = Layout.MasterHashOffset0;
            MasterHashA = reader.ReadBytes((int)Layout.MasterHashSize);
            reader.BaseStream.Position = Layout.MasterHashOffset1;
            MasterHashB = reader.ReadBytes((int)Layout.MasterHashSize);

            reader.BaseStream.Position = Layout.L1BitmapOffset0;
            DuplexMasterA = reader.ReadBytes((int)Layout.L1BitmapSize);
            reader.BaseStream.Position = Layout.L1BitmapOffset1;
            DuplexMasterB = reader.ReadBytes((int)Layout.L1BitmapSize);

            reader.BaseStream.Position = Layout.FileMapEntryOffset;
            FileMapEntries = new MapEntry[FileRemap.MapEntryCount];
            for (int i = 0; i < FileRemap.MapEntryCount; i++)
            {
                FileMapEntries[i] = new MapEntry(reader);
            }

            reader.BaseStream.Position = Layout.MetaMapEntryOffset;
            MetaMapEntries = new MapEntry[MetaRemap.MapEntryCount];
            for (int i = 0; i < MetaRemap.MapEntryCount; i++)
            {
                MetaMapEntries[i] = new MapEntry(reader);
            }

            HeaderHashValidity = ValidateHeaderHash();
            SignatureValidity = ValidateSignature(keyset);

            logger?.LogMessage($"Header hash is {HeaderHashValidity}");
        }

        private Validity ValidateHeaderHash()
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Data, 0x300, 0x3d00);
                return hash.SequenceEqual(Layout.Hash) ? Validity.Valid : Validity.Invalid;
            }
        }

        private Validity ValidateSignature(Keyset keyset)
        {
            var calculatedCmac = new byte[0x10];

            Crypto.CalculateAesCmac(keyset.SaveMacKey, Data, 0x100, calculatedCmac, 0, 0x200);

            return calculatedCmac.SequenceEqual(Cmac) ? Validity.Valid : Validity.Invalid;
        }
    }

    public class FsLayout
    {
        public string Magic { get; set; }
        public uint MagicNum { get; set; }
        public byte[] Hash { get; set; }
        public long FileMapEntryOffset { get; set; }
        public long FileMapEntrySize { get; set; }
        public long MetaMapEntryOffset { get; set; }
        public long MetaMapEntrySize { get; set; }
        public long FileMapDataOffset { get; set; }
        public long FileMapDataSize { get; set; }
        public long DuplexL1OffsetA { get; set; }
        public long DuplexL1OffsetB { get; set; }
        public long DuplexL1Size { get; set; }
        public long DuplexDataOffsetA { get; set; }
        public long DuplexDataOffsetB { get; set; }
        public long DuplexDataSize { get; set; }
        public long JournalDataOffset { get; set; }
        public long JournalDataSizeA { get; set; }
        public long JournalDataSizeB { get; set; }
        public long SizeReservedArea { get; set; }
        public long L1BitmapOffset0 { get; set; }
        public long L1BitmapOffset1 { get; set; }
        public long L1BitmapSize { get; set; }
        public long MasterHashOffset0 { get; set; }
        public long MasterHashOffset1 { get; set; }
        public long MasterHashSize { get; set; }
        public long JournalTableOffset { get; set; }
        public long JournalTableSize { get; set; }
        public long JournalBitmapUpdatedPhysicalOffset { get; set; }
        public long JournalBitmapUpdatedPhysicalSize { get; set; }
        public long JournalBitmapUpdatedVirtualOffset { get; set; }
        public long JournalBitmapUpdatedVirtualSize { get; set; }
        public long JournalBitmapUnassignedOffset { get; set; }
        public long JournalBitmapUnassignedSize { get; set; }
        public long Layer1HashOffset { get; set; }
        public long Layer1HashSize { get; set; }
        public long Layer2HashOffset { get; set; }
        public long Layer2HashSize { get; set; }
        public long Layer3HashOffset { get; set; }
        public long Layer3HashSize { get; set; }
        public long Field148 { get; set; }
        public long Field150 { get; set; }
        public long DuplexIndex { get; set; }

        public FsLayout(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            MagicNum = reader.ReadUInt32();
            Hash = reader.ReadBytes(0x20);
            FileMapEntryOffset = reader.ReadInt64();
            FileMapEntrySize = reader.ReadInt64();
            MetaMapEntryOffset = reader.ReadInt64();
            MetaMapEntrySize = reader.ReadInt64();
            FileMapDataOffset = reader.ReadInt64();
            FileMapDataSize = reader.ReadInt64();
            DuplexL1OffsetA = reader.ReadInt64();
            DuplexL1OffsetB = reader.ReadInt64();
            DuplexL1Size = reader.ReadInt64();
            DuplexDataOffsetA = reader.ReadInt64();
            DuplexDataOffsetB = reader.ReadInt64();
            DuplexDataSize = reader.ReadInt64();
            JournalDataOffset = reader.ReadInt64();
            JournalDataSizeA = reader.ReadInt64();
            JournalDataSizeB = reader.ReadInt64();
            SizeReservedArea = reader.ReadInt64();
            L1BitmapOffset0 = reader.ReadInt64();
            L1BitmapOffset1 = reader.ReadInt64();
            L1BitmapSize = reader.ReadInt64();
            MasterHashOffset0 = reader.ReadInt64();
            MasterHashOffset1 = reader.ReadInt64();
            MasterHashSize = reader.ReadInt64();
            JournalTableOffset = reader.ReadInt64();
            JournalTableSize = reader.ReadInt64();
            JournalBitmapUpdatedPhysicalOffset = reader.ReadInt64();
            JournalBitmapUpdatedPhysicalSize = reader.ReadInt64();
            JournalBitmapUpdatedVirtualOffset = reader.ReadInt64();
            JournalBitmapUpdatedVirtualSize = reader.ReadInt64();
            JournalBitmapUnassignedOffset = reader.ReadInt64();
            JournalBitmapUnassignedSize = reader.ReadInt64();
            Layer1HashOffset = reader.ReadInt64();
            Layer1HashSize = reader.ReadInt64();
            Layer2HashOffset = reader.ReadInt64();
            Layer2HashSize = reader.ReadInt64();
            Layer3HashOffset = reader.ReadInt64();
            Layer3HashSize = reader.ReadInt64();
            Field148 = reader.ReadInt64();
            Field150 = reader.ReadInt64();
            DuplexIndex = reader.ReadInt64();
        }
    }

    public class RemapHeader
    {
        public string Magic { get; set; }
        public uint MagicNum { get; set; }
        public int MapEntryCount { get; set; }
        public int MapSegmentCount { get; set; }
        public int Field10 { get; set; }

        public RemapHeader(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            MagicNum = reader.ReadUInt32();
            MapEntryCount = reader.ReadInt32();
            MapSegmentCount = reader.ReadInt32();
            Field10 = reader.ReadInt32();
        }
    }

    public class DuplexHeader
    {
        public string Magic { get; }
        public uint MagicNum { get; }
        public DuplexInfo[] Layers { get; } = new DuplexInfo[3];

        public DuplexHeader(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            MagicNum = reader.ReadUInt32();

            for (int i = 0; i < Layers.Length; i++)
            {
                Layers[i] = new DuplexInfo(reader);
            }
        }
    }

    public class DuplexInfo
    {
        public long Offset { get; }
        public long Length { get; set; }
        public int BlockSizePower { get; set; }
        public int BlockSize { get; set; }

        public DuplexInfo() { }

        public DuplexInfo(BinaryReader reader)
        {
            Offset = reader.ReadInt64();
            Length = reader.ReadInt64();
            BlockSizePower = reader.ReadInt32();
            BlockSize = 1 << BlockSizePower;
        }
    }

    public class JournalHeader
    {
        public string Magic { get; }
        public uint MagicNum { get; }
        public long Field8 { get; }
        public long Field10 { get; }
        public long BlockSize { get; }
        public int Field20 { get; }
        public int MappingEntryCount { get; }
        public int Field28 { get; }
        public int Field2C { get; }

        public JournalHeader(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            MagicNum = reader.ReadUInt32();
            Field8 = reader.ReadInt64();
            Field10 = reader.ReadInt64();
            BlockSize = reader.ReadInt64();
            Field20 = reader.ReadInt32();
            MappingEntryCount = reader.ReadInt32();
            Field28 = reader.ReadInt32();
            Field2C = reader.ReadInt32();
        }
    }

    public class SaveHeader
    {
        public string Magic { get; }
        public uint MagicNum { get; }
        public int Field8 { get; }
        public int FieldC { get; }
        public int Field10 { get; }
        public int Field14 { get; }
        public long BlockSize { get; }
        public StorageInfo AllocationTableInfo { get; }
        public StorageInfo DataInfo { get; }
        public int DirectoryTableBlock { get; }
        public int FileTableBlock { get; }

        public SaveHeader(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            MagicNum = reader.ReadUInt32();
            Field8 = reader.ReadInt32();
            FieldC = reader.ReadInt32();
            Field10 = reader.ReadInt32();
            Field14 = reader.ReadInt32();
            BlockSize = reader.ReadInt64();
            AllocationTableInfo = new StorageInfo(reader);
            DataInfo = new StorageInfo(reader);
            DirectoryTableBlock = reader.ReadInt32();
            FileTableBlock = reader.ReadInt32();
        }
    }

    public class StorageInfo
    {
        public long Offset { get; }
        public int Size { get; }
        public int FieldC { get; }

        public StorageInfo(BinaryReader reader)
        {
            Offset = reader.ReadInt64();
            Size = reader.ReadInt32();
            FieldC = reader.ReadInt32();
        }
    }

    public class MapEntry
    {
        public long VirtualOffset { get; }
        public long PhysicalOffset { get; }
        public long Size { get; }
        public int Alignment { get; }
        public int StorageType { get; }
        public long VirtualOffsetEnd => VirtualOffset + Size;
        public long PhysicalOffsetEnd => PhysicalOffset + Size;
        internal RemapSegment Segment { get; set; }
        internal MapEntry Next { get; set; }

        public MapEntry(BinaryReader reader)
        {
            VirtualOffset = reader.ReadInt64();
            PhysicalOffset = reader.ReadInt64();
            Size = reader.ReadInt64();
            Alignment = reader.ReadInt32();
            StorageType = reader.ReadInt32();
        }
    }
}
