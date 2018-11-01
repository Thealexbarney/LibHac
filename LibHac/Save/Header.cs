using System;
using System.IO;
using LibHac.IO.Save;

namespace LibHac.Save
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
        public ExtraData ExtraData { get; set; }

        public MapEntry[] FileMapEntries { get; set; }
        public MapEntry[] MetaMapEntries { get; set; }

        public byte[] MasterHashA { get; }
        public byte[] MasterHashB { get; }
        public byte[] DuplexMasterA { get; }
        public byte[] DuplexMasterB { get; }

        public Storage MasterHash { get; }

        public Validity SignatureValidity { get; }
        public Validity HeaderHashValidity { get; }

        public byte[] Data { get; }

        public Header(Keyset keyset, Storage storage)
        {

            var reader = new BinaryReader(storage.AsStream());

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

            reader.BaseStream.Position = 0x6D8;
            ExtraData = new ExtraData(reader);

            reader.BaseStream.Position = Layout.IvfcMasterHashOffsetA;
            MasterHashA = reader.ReadBytes((int)Layout.IvfcMasterHashSize);
            reader.BaseStream.Position = Layout.IvfcMasterHashOffsetB;
            MasterHashB = reader.ReadBytes((int)Layout.IvfcMasterHashSize);

            MasterHash = storage.Slice(Layout.IvfcMasterHashOffsetA, Layout.IvfcMasterHashSize);

            reader.BaseStream.Position = Layout.DuplexMasterOffsetA;
            DuplexMasterA = reader.ReadBytes((int)Layout.DuplexMasterSize);
            reader.BaseStream.Position = Layout.DuplexMasterOffsetB;
            DuplexMasterB = reader.ReadBytes((int)Layout.DuplexMasterSize);

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

            HeaderHashValidity = Crypto.CheckMemoryHashTable(Data, Layout.Hash, 0x300, 0x3d00);
            SignatureValidity = ValidateSignature(keyset);
        }

        private Validity ValidateSignature(Keyset keyset)
        {
            var calculatedCmac = new byte[0x10];

            Crypto.CalculateAesCmac(keyset.SaveMacKey, Data, 0x100, calculatedCmac, 0, 0x200);

            return Util.ArraysEqual(calculatedCmac, Cmac) ? Validity.Valid : Validity.Invalid;
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
        public long DuplexMasterOffsetA { get; set; }
        public long DuplexMasterOffsetB { get; set; }
        public long DuplexMasterSize { get; set; }
        public long IvfcMasterHashOffsetA { get; set; }
        public long IvfcMasterHashOffsetB { get; set; }
        public long IvfcMasterHashSize { get; set; }
        public long JournalTableOffset { get; set; }
        public long JournalTableSize { get; set; }
        public long JournalBitmapUpdatedPhysicalOffset { get; set; }
        public long JournalBitmapUpdatedPhysicalSize { get; set; }
        public long JournalBitmapUpdatedVirtualOffset { get; set; }
        public long JournalBitmapUpdatedVirtualSize { get; set; }
        public long JournalBitmapUnassignedOffset { get; set; }
        public long JournalBitmapUnassignedSize { get; set; }
        public long IvfcL1Offset { get; set; }
        public long IvfcL1Size { get; set; }
        public long IvfcL2Offset { get; set; }
        public long IvfcL2Size { get; set; }
        public long IvfcL3Offset { get; set; }
        public long IvfcL3Size { get; set; }
        public long FatOffset { get; set; }
        public long FatSize { get; set; }
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
            DuplexMasterOffsetA = reader.ReadInt64();
            DuplexMasterOffsetB = reader.ReadInt64();
            DuplexMasterSize = reader.ReadInt64();
            IvfcMasterHashOffsetA = reader.ReadInt64();
            IvfcMasterHashOffsetB = reader.ReadInt64();
            IvfcMasterHashSize = reader.ReadInt64();
            JournalTableOffset = reader.ReadInt64();
            JournalTableSize = reader.ReadInt64();
            JournalBitmapUpdatedPhysicalOffset = reader.ReadInt64();
            JournalBitmapUpdatedPhysicalSize = reader.ReadInt64();
            JournalBitmapUpdatedVirtualOffset = reader.ReadInt64();
            JournalBitmapUpdatedVirtualSize = reader.ReadInt64();
            JournalBitmapUnassignedOffset = reader.ReadInt64();
            JournalBitmapUnassignedSize = reader.ReadInt64();
            IvfcL1Offset = reader.ReadInt64();
            IvfcL1Size = reader.ReadInt64();
            IvfcL2Offset = reader.ReadInt64();
            IvfcL2Size = reader.ReadInt64();
            IvfcL3Offset = reader.ReadInt64();
            IvfcL3Size = reader.ReadInt64();
            FatOffset = reader.ReadInt64();
            FatSize = reader.ReadInt64();
            DuplexIndex = reader.ReadByte();
        }
    }

    public class RemapHeader
    {
        public string Magic { get; }
        public uint MagicNum { get; }
        public int MapEntryCount { get; }
        public int MapSegmentCount { get; }
        public int SegmentBits { get; }

        public RemapHeader(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            MagicNum = reader.ReadUInt32();
            MapEntryCount = reader.ReadInt32();
            MapSegmentCount = reader.ReadInt32();
            SegmentBits = reader.ReadInt32();
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
        public long TotalSize { get; }
        public long JournalSize { get; }
        public long BlockSize { get; }
        public int Field20 { get; }
        public int MainDataBlockCount { get; }
        public int JournalBlockCount { get; }
        public int Field2C { get; }

        public JournalHeader(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            MagicNum = reader.ReadUInt32();
            TotalSize = reader.ReadInt64();
            JournalSize = reader.ReadInt64();
            BlockSize = reader.ReadInt64();
            Field20 = reader.ReadInt32();
            MainDataBlockCount = reader.ReadInt32();
            JournalBlockCount = reader.ReadInt32();
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

    public class ExtraData
    {
        public ulong TitleId { get; }
        public Guid UserId { get; }
        public ulong SaveId { get; }
        public SaveDataType Type { get; }

        public ulong SaveOwnerId { get; }
        public long Timestamp { get; }
        public long Field50 { get; }
        public uint Field54 { get; }
        public long DataSize { get; }
        public long JournalSize { get; }

        public ExtraData(BinaryReader reader)
        {
            TitleId = reader.ReadUInt64();
            UserId = ToGuid(reader.ReadBytes(0x10));
            SaveId = reader.ReadUInt64();
            Type = (SaveDataType)reader.ReadByte();
            reader.BaseStream.Position += 0x1f;

            SaveOwnerId = reader.ReadUInt64();
            Timestamp = reader.ReadInt64();
            Field50 = reader.ReadUInt32();
            Field54 = reader.ReadUInt32();
            DataSize = reader.ReadInt64();
            JournalSize = reader.ReadInt64();
        }

        private static Guid ToGuid(byte[] bytes)
        {
            var b = new byte[0x10];
            Array.Copy(bytes, b, 0x10);

            // The Guid constructor uses a weird, mixed-endian format
            Array.Reverse(b, 10, 6);

            return new Guid(b);
        }
    }

    public enum SaveDataType
    {
        SystemSaveData,
        SaveData,
        BcatDeliveryCacheStorage,
        DeviceSaveData,
        TemporaryStorage,
        CacheStorage
    }
}
