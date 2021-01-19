using System;
using System.IO;
using LibHac.Common.Keys;
using LibHac.Util;

namespace LibHac
{
    public class Ticket
    {
        public TicketSigType SignatureType { get; set; }
        public byte[] Signature { get; set; }
        public string Issuer { get; set; }
        public byte[] TitleKeyBlock { get; set; }
        public byte FormatVersion { get; set; }
        public TitleKeyType TitleKeyType { get; set; }
        public LicenseType LicenseType { get; set; }
        public ushort TicketVersion { get; set; }
        public byte CryptoType { get; set; }
        public PropertyFlags PropertyMask { get; set; }
        public ulong TicketId { get; set; }
        public ulong DeviceId { get; set; }
        public byte[] RightsId { get; set; }
        public uint AccountId { get; set; }
        public int SectTotalSize { get; set; }
        public int SectHeaderOffset { get; set; }
        public short SectNum { get; set; }
        public short SectEntrySize { get; set; }

        public byte[] File { get; }

        internal static readonly byte[] LabelHash =
        {
            0xE3, 0xB0, 0xC4, 0x42, 0x98, 0xFC, 0x1C, 0x14, 0x9A, 0xFB, 0xF4, 0xC8, 0x99, 0x6F, 0xB9, 0x24,
            0x27, 0xAE, 0x41, 0xE4, 0x64, 0x9B, 0x93, 0x4C, 0xA4, 0x95, 0x99, 0x1B, 0x78, 0x52, 0xB8, 0x55
        };

        public Ticket() { }

        public Ticket(Stream stream) : this(new BinaryReader(stream)) { }

        public Ticket(BinaryReader reader)
        {
            long fileStart = reader.BaseStream.Position;
            SignatureType = (TicketSigType)reader.ReadUInt32();

            switch (SignatureType)
            {
                case TicketSigType.Rsa4096Sha1:
                case TicketSigType.Rsa4096Sha256:
                    Signature = reader.ReadBytes(0x200);
                    reader.BaseStream.Position += 0x3c;
                    break;
                case TicketSigType.Rsa2048Sha1:
                case TicketSigType.Rsa2048Sha256:
                    Signature = reader.ReadBytes(0x100);
                    reader.BaseStream.Position += 0x3c;
                    break;
                case TicketSigType.EcdsaSha1:
                case TicketSigType.EcdsaSha256:
                    Signature = reader.ReadBytes(0x3c);
                    reader.BaseStream.Position += 0x40;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            long dataStart = reader.BaseStream.Position;

            Issuer = reader.ReadUtf8Z(0x40);
            reader.BaseStream.Position = dataStart + 0x40;
            TitleKeyBlock = reader.ReadBytes(0x100);
            FormatVersion = reader.ReadByte();
            TitleKeyType = (TitleKeyType)reader.ReadByte();
            TicketVersion = reader.ReadUInt16();
            LicenseType = (LicenseType)reader.ReadByte();
            CryptoType = reader.ReadByte();
            PropertyMask = (PropertyFlags)reader.ReadUInt32();
            reader.BaseStream.Position = dataStart + 0x150;
            TicketId = reader.ReadUInt64();
            DeviceId = reader.ReadUInt64();
            RightsId = reader.ReadBytes(0x10);
            AccountId = reader.ReadUInt32();
            SectTotalSize = reader.ReadInt32();
            SectHeaderOffset = reader.ReadInt32();
            SectNum = reader.ReadInt16();
            SectEntrySize = reader.ReadInt16();

            reader.BaseStream.Position = fileStart;
            File = reader.ReadBytes(SectHeaderOffset);
        }

        public byte[] GetBytes()
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            int sigLength;

            switch (SignatureType)
            {
                case TicketSigType.Rsa4096Sha1:
                case TicketSigType.Rsa4096Sha256:
                    sigLength = 0x200;
                    break;
                case TicketSigType.Rsa2048Sha1:
                case TicketSigType.Rsa2048Sha256:
                    sigLength = 0x100;
                    break;
                case TicketSigType.EcdsaSha1:
                case TicketSigType.EcdsaSha256:
                    sigLength = 0x3c;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            long bodyStart = Alignment.AlignUp(4 + sigLength, 0x40);

            writer.Write((int)SignatureType);

            if (Signature?.Length == sigLength)
            {
                writer.Write(Signature);
            }

            stream.Position = bodyStart;
            if (Issuer != null) writer.WriteUTF8(Issuer);
            stream.Position = bodyStart + 0x40;
            if (TitleKeyBlock?.Length <= 0x100) writer.Write(TitleKeyBlock);
            stream.Position = bodyStart + 0x140;
            writer.Write(FormatVersion);
            writer.Write((byte)TitleKeyType);
            writer.Write(TicketVersion);
            writer.Write((byte)LicenseType);
            writer.Write(CryptoType);
            writer.Write((uint)PropertyMask);
            stream.Position = bodyStart + 0x150;
            writer.Write(TicketId);
            writer.Write(DeviceId);
            if (RightsId?.Length <= 0x10) writer.Write(RightsId);
            writer.Write(AccountId);
            writer.Write(SectTotalSize);
            writer.Write(SectHeaderOffset);
            writer.Write(SectNum);
            writer.Write(SectEntrySize);

            return stream.ToArray();
        }

        public byte[] GetTitleKey(KeySet keySet)
        {
            if (TitleKeyType == TitleKeyType.Common)
            {
                byte[] commonKey = new byte[0x10];
                Array.Copy(TitleKeyBlock, commonKey, commonKey.Length);
                return commonKey;
            }

            return CryptoOld.DecryptRsaOaep(TitleKeyBlock, keySet.ETicketExtKeyRsa);
        }
    }

    public enum TicketSigType
    {
        Rsa4096Sha1 = 0x10000,
        Rsa2048Sha1,
        EcdsaSha1,
        Rsa4096Sha256,
        Rsa2048Sha256,
        EcdsaSha256
    }

    public enum TitleKeyType
    {
        Common,
        Personalized
    }

    public enum LicenseType
    {
        Permanent,
        Demo,
        Trial,
        Rental,
        Subscription,
        Service
    }

    [Flags]
    public enum PropertyFlags
    {
        PreInstall = 1 << 0,
        SharedTitle = 1 << 1,
        AllowAllContent = 1 << 2
    }
}
