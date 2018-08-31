using System;
using System.IO;
using System.Text;

namespace LibHac
{
    public class XciHeader
    {
        private const int SignatureSize = 0x100;
        private const string HeaderMagic = "HEAD";
        private const int EncryptedHeaderSize = 0x70;

        public byte[] Signature { get; set; }
        public string Magic { get; set; }
        public int RomAreaStartPage { get; set; }
        public int BackupAreaStartPage { get; set; }
        public byte KekIndex { get; set; }
        public byte TitleKeyDecIndex { get; set; }
        public RomSize RomSize { get; set; }
        public byte CardHeaderVersion { get; set; }
        public XciFlags Flags { get; set; }
        public ulong PackageId { get; set; }
        public long ValidDataEndPage { get; set; }
        public byte[] AesCbcIv { get; set; }
        public long PartitionFsHeaderAddress { get; set; }
        public long PartitionFsHeaderSize { get; set; }
        public byte[] PartitionFsHeaderHash { get; set; }
        public byte[] InitialDataHash { get; set; }
        public int SelSec { get; set; }
        public int SelT1Key { get; set; }
        public int SelKey { get; set; }
        public int LimAreaPage { get; set; }

        public ulong FwVersion { get; set; }
        public CardClockRate AccCtrl1 { get; set; }
        public int Wait1TimeRead { get; set; }
        public int Wait2TimeRead { get; set; }
        public int Wait1TimeWrite { get; set; }
        public int Wait2TimeWrite { get; set; }
        public int FwMode { get; set; }
        public int UppVersion { get; set; }
        public byte[] UppHash { get; set; }
        public ulong UppId { get; set; }

        public XciHeader(Keyset keyset, Stream stream)
        {
            var reader = new BinaryReader(stream, Encoding.Default, true);
            Signature = reader.ReadBytes(SignatureSize);
            Magic = reader.ReadAscii(4);
            if (Magic != HeaderMagic)
            {
                throw new InvalidDataException("Invalid XCI file: Header magic invalid.");
            }

            RomAreaStartPage = reader.ReadInt32();
            BackupAreaStartPage = reader.ReadInt32();
            byte keyIndex = reader.ReadByte();
            KekIndex = (byte)(keyIndex >> 4);
            TitleKeyDecIndex = (byte)(keyIndex & 7);
            RomSize = (RomSize)reader.ReadByte();
            CardHeaderVersion = reader.ReadByte();
            Flags = (XciFlags)reader.ReadByte();
            PackageId = reader.ReadUInt64();
            ValidDataEndPage = reader.ReadInt64();
            AesCbcIv = reader.ReadBytes(Crypto.Aes128Size);
            Array.Reverse(AesCbcIv);
            PartitionFsHeaderAddress = reader.ReadInt64();
            PartitionFsHeaderSize = reader.ReadInt64();
            PartitionFsHeaderHash = reader.ReadBytes(Crypto.Sha256DigestSize);
            InitialDataHash = reader.ReadBytes(Crypto.Sha256DigestSize);
            SelSec = reader.ReadInt32();
            SelT1Key = reader.ReadInt32();
            SelKey = reader.ReadInt32();
            LimAreaPage = reader.ReadInt32();

            if (keyset.xci_header_key.IsEmpty()) return;

            var encHeader = reader.ReadBytes(EncryptedHeaderSize);
            var decHeader = new byte[EncryptedHeaderSize];
            Crypto.DecryptCbc(keyset.xci_header_key, AesCbcIv, encHeader, decHeader, EncryptedHeaderSize);

            reader = new BinaryReader(new MemoryStream(decHeader));
            FwVersion = reader.ReadUInt64();
            AccCtrl1 = (CardClockRate)reader.ReadInt32();
            Wait1TimeRead = reader.ReadInt32();
            Wait2TimeRead = reader.ReadInt32();
            Wait1TimeWrite = reader.ReadInt32();
            Wait2TimeWrite = reader.ReadInt32();
            FwMode = reader.ReadInt32();
            UppVersion = reader.ReadInt32();
            reader.BaseStream.Position += 4;
            UppHash = reader.ReadBytes(8);
            UppId = reader.ReadUInt64();
        }
    }

    public enum RomSize
    {
        Size1Gb = 0xFA,
        Size2Gb = 0xF8,
        Size4Gb = 0xF0,
        Size8Gb = 0xE0,
        Size16Gb = 0xE1,
        Size32Gb = 0xE2
    }

    [Flags]
    public enum XciFlags
    {
        AutoBoot = 1 << 0,
        HistoryErase = 1 << 1,
        RepairTool = 1 << 2
    }

    public enum CardClockRate
    {
        ClockRate25 = 10551312,
        ClockRate50
    }
}
