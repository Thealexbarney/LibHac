using System;
using System.IO;
using LibHac.Common.Keys;
using LibHac.Crypto;
using LibHac.Fs;

namespace LibHac.FsSystem.Save
{
    public class Header
    {
        public IStorage MainStorage { get; }
        public IStorage MainHeader { get; }
        public IStorage DuplexHeader { get; }
        public IStorage DataIvfcHeader { get; }
        public IStorage JournalHeader { get; }
        public IStorage SaveHeader { get; }
        public IStorage MainRemapHeader { get; }
        public IStorage MetaDataRemapHeader { get; }
        public IStorage ExtraDataStorage { get; }
        public IStorage FatIvfcHeader { get; }
        public IStorage DuplexMasterBitmapA { get; }
        public IStorage DuplexMasterBitmapB { get; }
        public IStorage DataIvfcMaster { get; }
        public IStorage FatIvfcMaster { get; }

        public byte[] Cmac { get; set; }
        public FsLayout Layout { get; set; }
        public DuplexHeader Duplex { get; set; }
        public IvfcHeader Ivfc { get; set; }
        public IvfcHeader FatIvfc { get; set; }

        public ExtraData ExtraData { get; set; }

        public IStorage MasterHash { get; }

        public Validity SignatureValidity { get; }
        public Validity HeaderHashValidity { get; }

        public byte[] Data { get; }

        public Header(IStorage storage, KeySet keySet)
        {
            MainStorage = storage;
            MainHeader = MainStorage.Slice(0x100, 0x200);
            DuplexHeader = MainStorage.Slice(0x300, 0x44);
            DataIvfcHeader = MainStorage.Slice(0x344, 0xC0);
            JournalHeader = MainStorage.Slice(0x408, 0x200);
            SaveHeader = MainStorage.Slice(0x608, 0x48);
            MainRemapHeader = MainStorage.Slice(0x650, 0x40);
            MetaDataRemapHeader = MainStorage.Slice(0x690, 0x40);
            ExtraDataStorage = MainStorage.Slice(0x6D8, 0x400);
            FatIvfcHeader = MainStorage.Slice(0xAD8, 0xC0);

            Layout = new FsLayout(MainHeader);

            DuplexMasterBitmapA = MainStorage.Slice(Layout.DuplexMasterOffsetA, Layout.DuplexMasterSize);
            DuplexMasterBitmapB = MainStorage.Slice(Layout.DuplexMasterOffsetB, Layout.DuplexMasterSize);
            DataIvfcMaster = MainStorage.Slice(Layout.IvfcMasterHashOffsetA, Layout.IvfcMasterHashSize);
            FatIvfcMaster = MainStorage.Slice(Layout.FatIvfcMasterHashA, Layout.IvfcMasterHashSize);

            var reader = new BinaryReader(storage.AsStream());

            reader.BaseStream.Position = 0;
            Data = reader.ReadBytes(0x4000);
            reader.BaseStream.Position = 0;

            Cmac = reader.ReadBytes(0x10);

            reader.BaseStream.Position = 0x100;

            reader.BaseStream.Position = 0x300;
            Duplex = new DuplexHeader(reader);

            reader.BaseStream.Position = 0x6D8;
            ExtraData = new ExtraData(reader);

            Ivfc = new IvfcHeader(DataIvfcHeader) { NumLevels = 5 };

            if (Layout.Version >= 0x50000)
            {
                FatIvfc = new IvfcHeader(FatIvfcHeader) { NumLevels = 4 };
            }

            MasterHash = storage.Slice(Layout.IvfcMasterHashOffsetA, Layout.IvfcMasterHashSize);

            Span<byte> actualHeaderHash = stackalloc byte[Sha256.DigestSize];
            Sha256.GenerateSha256Hash(Data.AsSpan(0x300, 0x3d00), actualHeaderHash);

            HeaderHashValidity = Utilities.SpansEqual(Layout.Hash, actualHeaderHash) ? Validity.Valid : Validity.Invalid;
            SignatureValidity = ValidateSignature(keySet);
        }

        private Validity ValidateSignature(KeySet keySet)
        {
            Span<byte> calculatedCmac = stackalloc byte[0x10];

            Aes.CalculateCmac(calculatedCmac, Data.AsSpan(0x100, 0x200), keySet.DeviceUniqueSaveMacKeys[0]);

            return CryptoUtil.IsSameBytes(calculatedCmac, Cmac, Aes.BlockSize) ? Validity.Valid : Validity.Invalid;
        }
    }

    public class FsLayout
    {
        public string Magic { get; set; }
        public uint Version { get; set; }
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
        public long JournalSize { get; set; }
        public long DuplexMasterOffsetA { get; set; }
        public long DuplexMasterOffsetB { get; set; }
        public long DuplexMasterSize { get; set; }
        public long IvfcMasterHashOffsetA { get; set; }
        public long IvfcMasterHashOffsetB { get; set; }
        public long IvfcMasterHashSize { get; set; }
        public long JournalMapTableOffset { get; set; }
        public long JournalMapTableSize { get; set; }
        public long JournalPhysicalBitmapOffset { get; set; }
        public long JournalPhysicalBitmapSize { get; set; }
        public long JournalVirtualBitmapOffset { get; set; }
        public long JournalVirtualBitmapSize { get; set; }
        public long JournalFreeBitmapOffset { get; set; }
        public long JournalFreeBitmapSize { get; set; }
        public long IvfcL1Offset { get; set; }
        public long IvfcL1Size { get; set; }
        public long IvfcL2Offset { get; set; }
        public long IvfcL2Size { get; set; }
        public long IvfcL3Offset { get; set; }
        public long IvfcL3Size { get; set; }
        public long FatOffset { get; set; }
        public long FatSize { get; set; }
        public long DuplexIndex { get; set; }
        public long FatIvfcMasterHashA { get; set; }
        public long FatIvfcMasterHashB { get; set; }
        public long FatIvfcL1Offset { get; set; }
        public long FatIvfcL1Size { get; set; }
        public long FatIvfcL2Offset { get; set; }
        public long FatIvfcL2Size { get; set; }

        public FsLayout(IStorage storage)
        {
            var reader = new BinaryReader(storage.AsStream());

            Magic = reader.ReadAscii(4);
            Version = reader.ReadUInt32();
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
            JournalSize = reader.ReadInt64();
            DuplexMasterOffsetA = reader.ReadInt64();
            DuplexMasterOffsetB = reader.ReadInt64();
            DuplexMasterSize = reader.ReadInt64();
            IvfcMasterHashOffsetA = reader.ReadInt64();
            IvfcMasterHashOffsetB = reader.ReadInt64();
            IvfcMasterHashSize = reader.ReadInt64();
            JournalMapTableOffset = reader.ReadInt64();
            JournalMapTableSize = reader.ReadInt64();
            JournalPhysicalBitmapOffset = reader.ReadInt64();
            JournalPhysicalBitmapSize = reader.ReadInt64();
            JournalVirtualBitmapOffset = reader.ReadInt64();
            JournalVirtualBitmapSize = reader.ReadInt64();
            JournalFreeBitmapOffset = reader.ReadInt64();
            JournalFreeBitmapSize = reader.ReadInt64();
            IvfcL1Offset = reader.ReadInt64();
            IvfcL1Size = reader.ReadInt64();
            IvfcL2Offset = reader.ReadInt64();
            IvfcL2Size = reader.ReadInt64();
            IvfcL3Offset = reader.ReadInt64();
            IvfcL3Size = reader.ReadInt64();
            FatOffset = reader.ReadInt64();
            FatSize = reader.ReadInt64();
            DuplexIndex = reader.ReadByte();

            reader.BaseStream.Position += 7;
            FatIvfcMasterHashA = reader.ReadInt64();
            FatIvfcMasterHashB = reader.ReadInt64();
            FatIvfcL1Offset = reader.ReadInt64();
            FatIvfcL1Size = reader.ReadInt64();
            FatIvfcL2Offset = reader.ReadInt64();
            FatIvfcL2Size = reader.ReadInt64();
        }
    }

    public class DuplexHeader
    {
        public string Magic { get; }
        public uint Version { get; }
        public DuplexInfo[] Layers { get; } = new DuplexInfo[3];

        public DuplexHeader(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            Version = reader.ReadUInt32();

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
            byte[] b = new byte[0x10];
            Array.Copy(bytes, b, 0x10);

            // The Guid constructor uses a weird, mixed-endian format
            Array.Reverse(b, 10, 6);

            return new Guid(b);
        }
    }
}
