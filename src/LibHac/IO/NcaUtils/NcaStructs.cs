using System.IO;

namespace LibHac.IO.NcaUtils
{
    public class NcaHeader
    {
        public byte[] Signature1; // RSA-PSS signature over header with fixed key.
        public byte[] Signature2; // RSA-PSS signature over header with key in NPDM.
        public string Magic;
        public DistributionType Distribution; // System vs gamecard.
        public ContentType ContentType;
        public byte CryptoType; // Which keyblob (field 1)
        public byte KaekInd; // Which kaek index?
        public long NcaSize; // Entire archive size.
        public ulong TitleId;
        public TitleVersion SdkVersion; // What SDK was this built with?
        public byte CryptoType2; // Which keyblob (field 2)
        public byte[] RightsId;
        public string Name;

        public NcaSectionEntry[] SectionEntries = new NcaSectionEntry[4];
        public byte[][] SectionHashes = new byte[4][];
        public byte[][] EncryptedKeys = new byte[4][];

        public NcaFsHeader[] FsHeaders = new NcaFsHeader[4];

        private byte[] SignatureData { get; }
        public Validity FixedSigValidity { get; }
        public Validity NpdmSigValidity { get; private set; }

        public NcaHeader(BinaryReader reader, Keyset keyset)
        {
            Signature1 = reader.ReadBytes(0x100);
            Signature2 = reader.ReadBytes(0x100);
            Magic = reader.ReadAscii(4);
            if (Magic != "NCA3") throw new InvalidDataException("Not an NCA3 file");

            reader.BaseStream.Position -= 4;
            SignatureData = reader.ReadBytes(0x200);
            FixedSigValidity = Crypto.Rsa2048PssVerify(SignatureData, Signature1, keyset.NcaHdrFixedKeyModulus);

            reader.BaseStream.Position -= 0x200 - 4;
            Distribution = (DistributionType)reader.ReadByte();
            ContentType = (ContentType)reader.ReadByte();
            CryptoType = reader.ReadByte();
            KaekInd = reader.ReadByte();
            NcaSize = reader.ReadInt64();
            TitleId = reader.ReadUInt64();
            reader.BaseStream.Position += 4;

            SdkVersion = new TitleVersion(reader.ReadUInt32());
            CryptoType2 = reader.ReadByte();
            reader.BaseStream.Position += 0xF;

            RightsId = reader.ReadBytes(0x10);

            for (int i = 0; i < 4; i++)
            {
                SectionEntries[i] = new NcaSectionEntry(reader);
            }

            for (int i = 0; i < 4; i++)
            {
                SectionHashes[i] = reader.ReadBytes(0x20);
            }

            for (int i = 0; i < 4; i++)
            {
                EncryptedKeys[i] = reader.ReadBytes(0x10);
            }

            reader.BaseStream.Position += 0xC0;

            for (int i = 0; i < 4; i++)
            {
                FsHeaders[i] = new NcaFsHeader(reader);
            }
        }

        internal void ValidateNpdmSignature(byte[] modulus)
        {
            NpdmSigValidity = Crypto.Rsa2048PssVerify(SignatureData, Signature2, modulus);
        }
    }

    public class NcaSectionEntry
    {
        public uint MediaStartOffset;
        public uint MediaEndOffset;

        public NcaSectionEntry(BinaryReader reader)
        {
            MediaStartOffset = reader.ReadUInt32();
            MediaEndOffset = reader.ReadUInt32();
            reader.BaseStream.Position += 8;
        }
    }

    public class BktrPatchInfo
    {
        public BktrHeader RelocationHeader;
        public BktrHeader EncryptionHeader;
    }

    public class Sha256Info
    {
        public byte[] MasterHash;
        public int BlockSize; // In bytes
        public uint LevelCount;
        public long HashTableOffset;
        public long HashTableSize;
        public long DataOffset;
        public long DataSize;

        public Validity MasterHashValidity = Validity.Unchecked;
        public Validity HashValidity = Validity.Unchecked;

        public Sha256Info(BinaryReader reader)
        {
            MasterHash = reader.ReadBytes(0x20);
            BlockSize = reader.ReadInt32();
            LevelCount = reader.ReadUInt32();
            HashTableOffset = reader.ReadInt64();
            HashTableSize = reader.ReadInt64();
            DataOffset = reader.ReadInt64();
            DataSize = reader.ReadInt64();
        }
    }

    public class BktrHeader
    {
        public long Offset;
        public long Size;
        public uint Magic;
        public uint Version;
        public uint NumEntries;
        public uint Field1C;

        public byte[] Header;

        public BktrHeader(BinaryReader reader)
        {
            Offset = reader.ReadInt64();
            Size = reader.ReadInt64();
            Magic = reader.ReadUInt32();
            Version = reader.ReadUInt32();
            NumEntries = reader.ReadUInt32();
            Field1C = reader.ReadUInt32();

            reader.BaseStream.Position -= 0x10;
            Header = reader.ReadBytes(0x10);
        }
    }

    public class TitleVersion
    {
        public uint Version { get; }
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public int Revision { get; }

        public TitleVersion(uint version, bool isSystemTitle = false)
        {
            Version = version;

            if (isSystemTitle)
            {
                Revision = (int)(version & ((1 << 16) - 1));
                Patch = (int)((version >> 16) & ((1 << 4) - 1));
                Minor = (int)((version >> 20) & ((1 << 6) - 1));
                Major = (int)((version >> 26) & ((1 << 6) - 1));
            }
            else
            {
                Revision = (byte)version;
                Patch = (byte)(version >> 8);
                Minor = (byte)(version >> 16);
                Major = (byte)(version >> 24);
            }
        }

        public override string ToString()
        {
            return $"{Major}.{Minor}.{Patch}.{Revision}";
        }
    }

    public enum ProgramPartitionType
    {
        Code,
        Data,
        Logo
    };

    public enum NcaSectionType
    {
        Code,
        Data,
        Logo
    };

    public enum ContentType
    {
        Program,
        Meta,
        Control,
        Manual,
        Data,
        PublicData
    }

    public enum DistributionType
    {
        Download,
        GameCard
    }

    public enum NcaEncryptionType
    {
        Auto,
        None,
        XTS,
        AesCtr,
        AesCtrEx
    }

    public enum NcaHashType
    {
        Auto,
        None,
        Sha256,
        Ivfc
    }

    public enum NcaFormatType
    {
        Romfs,
        Pfs0
    }

    public enum SectionType
    {
        Invalid,
        Pfs0,
        Romfs,
        Bktr
    }
}
