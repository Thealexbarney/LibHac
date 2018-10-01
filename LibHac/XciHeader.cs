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

        private readonly byte[] _xciHeaderPubk =
        {
            0x98, 0xC7, 0x26, 0xB6, 0x0D, 0x0A, 0x50, 0xA7, 0x39, 0x21, 0x0A, 0xE3, 0x2F, 0xE4, 0x3E, 0x2E,
            0x5B, 0xA2, 0x86, 0x75, 0xAA, 0x5C, 0xEE, 0x34, 0xF1, 0xA3, 0x3A, 0x7E, 0xBD, 0x90, 0x4E, 0xF7,
            0x8D, 0xFA, 0x17, 0xAA, 0x6B, 0xC6, 0x36, 0x6D, 0x4C, 0x9A, 0x6D, 0x57, 0x2F, 0x80, 0xA2, 0xBC,
            0x38, 0x4D, 0xDA, 0x99, 0xA1, 0xD8, 0xC3, 0xE2, 0x99, 0x79, 0x36, 0x71, 0x90, 0x20, 0x25, 0x9D,
            0x4D, 0x11, 0xB8, 0x2E, 0x63, 0x6B, 0x5A, 0xFA, 0x1E, 0x9C, 0x04, 0xD1, 0xC5, 0xF0, 0x9C, 0xB1,
            0x0F, 0xB8, 0xC1, 0x7B, 0xBF, 0xE8, 0xB0, 0xD2, 0x2B, 0x47, 0x01, 0x22, 0x6B, 0x23, 0xC9, 0xD0,
            0xBC, 0xEB, 0x75, 0x6E, 0x41, 0x7D, 0x4C, 0x26, 0xA4, 0x73, 0x21, 0xB4, 0xF0, 0x14, 0xE5, 0xD9,
            0x8D, 0xB3, 0x64, 0xEE, 0xA8, 0xFA, 0x84, 0x1B, 0xB8, 0xB8, 0x7C, 0x88, 0x6B, 0xEF, 0xCC, 0x97,
            0x04, 0x04, 0x9A, 0x67, 0x2F, 0xDF, 0xEC, 0x0D, 0xB2, 0x5F, 0xB5, 0xB2, 0xBD, 0xB5, 0x4B, 0xDE,
            0x0E, 0x88, 0xA3, 0xBA, 0xD1, 0xB4, 0xE0, 0x91, 0x81, 0xA7, 0x84, 0xEB, 0x77, 0x85, 0x8B, 0xEF,
            0xA5, 0xE3, 0x27, 0xB2, 0xF2, 0x82, 0x2B, 0x29, 0xF1, 0x75, 0x2D, 0xCE, 0xCC, 0xAE, 0x9B, 0x8D,
            0xED, 0x5C, 0xF1, 0x8E, 0xDB, 0x9A, 0xD7, 0xAF, 0x42, 0x14, 0x52, 0xCD, 0xE3, 0xC5, 0xDD, 0xCE,
            0x08, 0x12, 0x17, 0xD0, 0x7F, 0x1A, 0xAA, 0x1F, 0x7D, 0xE0, 0x93, 0x54, 0xC8, 0xBC, 0x73, 0x8A,
            0xCB, 0xAD, 0x6E, 0x93, 0xE2, 0x19, 0x72, 0x6B, 0xD3, 0x45, 0xF8, 0x73, 0x3D, 0x2B, 0x6A, 0x55,
            0xD2, 0x3A, 0x8B, 0xB0, 0x8A, 0x42, 0xE3, 0x3D, 0xF1, 0x92, 0x23, 0x42, 0x2E, 0xBA, 0xCC, 0x9C,
            0x9A, 0xC1, 0xDD, 0x62, 0x86, 0x9C, 0x2E, 0xE1, 0x2D, 0x6F, 0x62, 0x67, 0x51, 0x08, 0x0E, 0xCF
        };

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

        public Validity SignatureValidity { get; set; }

        public Validity PartitionFsHeaderValidity { get; set; }

        public XciHeader(Keyset keyset, Stream stream)
        {

            using (var reader = new BinaryReader(stream, Encoding.Default, true)) {

                Signature = reader.ReadBytes(SignatureSize);
                Magic = reader.ReadAscii(4);
                if (Magic != HeaderMagic)
                {
                    throw new InvalidDataException("Invalid XCI file: Header magic invalid.");
                }

                reader.BaseStream.Position = SignatureSize;
                byte[] sigData = reader.ReadBytes(SignatureSize);
                reader.BaseStream.Position = SignatureSize + 4;

                if (Crypto.Rsa2048Pkcs1Verify(sigData, Signature, _xciHeaderPubk))
                {
                    SignatureValidity = Validity.Valid;
                }
                else
                {
                    SignatureValidity = Validity.Invalid;
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

                if (!keyset.XciHeaderKey.IsEmpty()) {

                    var encHeader = reader.ReadBytes(EncryptedHeaderSize);
                    var decHeader = new byte[EncryptedHeaderSize];
                    Crypto.DecryptCbc(keyset.XciHeaderKey, AesCbcIv, encHeader, decHeader, EncryptedHeaderSize);

                    using (var decreader = new BinaryReader(new MemoryStream(decHeader))) {
                        FwVersion = decreader.ReadUInt64();
                        AccCtrl1 = (CardClockRate)decreader.ReadInt32();
                        Wait1TimeRead = decreader.ReadInt32();
                        Wait2TimeRead = decreader.ReadInt32();
                        Wait1TimeWrite = decreader.ReadInt32();
                        Wait2TimeWrite = decreader.ReadInt32();
                        FwMode = decreader.ReadInt32();
                        UppVersion = decreader.ReadInt32();
                        decreader.BaseStream.Position += 4;
                        UppHash = decreader.ReadBytes(8);
                        UppId = decreader.ReadUInt64();
                    }
                }

                reader.BaseStream.Position = PartitionFsHeaderAddress;

                if (Crypto.CheckMemoryHashTable(reader.ReadBytes((int)PartitionFsHeaderSize), PartitionFsHeaderHash)) {
                    PartitionFsHeaderValidity = Validity.Valid;
                }
                else
                {
                    PartitionFsHeaderValidity = Validity.Invalid;
                }

            }


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
