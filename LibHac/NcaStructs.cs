using System.IO;
using System.Linq;

namespace LibHac
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
        public ulong NcaSize; // Entire archive size.
        public ulong TitleId;
        public TitleVersion SdkVersion; // What SDK was this built with?
        public byte CryptoType2; // Which keyblob (field 2)
        public byte[] RightsId;
        public string Name;

        public NcaSectionEntry[] SectionEntries = new NcaSectionEntry[4];
        public byte[][] SectionHashes = new byte[4][];
        public byte[][] EncryptedKeys = new byte[4][];

        public NcaFsHeader[] FsHeaders = new NcaFsHeader[4];

        public NcaHeader(BinaryReader reader)
        {
            Signature1 = reader.ReadBytes(0x100);
            Signature2 = reader.ReadBytes(0x100);
            Magic = reader.ReadAscii(4);
            if (Magic != "NCA3") throw new InvalidDataException("Not an NCA3 file");
            Distribution = (DistributionType)reader.ReadByte();
            ContentType = (ContentType)reader.ReadByte();
            CryptoType = reader.ReadByte();
            KaekInd = reader.ReadByte();
            NcaSize = reader.ReadUInt64();
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

    public class NcaFsHeader
    {
        public short Version;
        public NcaFormatType FormatType;
        public NcaHashType HashType;
        public NcaEncryptionType EncryptionType;
        public SectionType Type;

        public IvfcHeader IvfcInfo;
        public Sha256Info Sha256Info;
        public BktrPatchInfo BktrInfo;

        public byte[] Ctr;

        public NcaFsHeader(BinaryReader reader)
        {
            long start = reader.BaseStream.Position;
            Version = reader.ReadInt16();
            FormatType = (NcaFormatType)reader.ReadByte();
            HashType = (NcaHashType)reader.ReadByte();
            EncryptionType = (NcaEncryptionType)reader.ReadByte();
            reader.BaseStream.Position += 3;

            switch (HashType)
            {
                case NcaHashType.Sha256:
                    Sha256Info = new Sha256Info(reader);
                    break;
                case NcaHashType.Ivfc:
                    IvfcInfo = new IvfcHeader(reader);
                    break;
            }

            if (EncryptionType == NcaEncryptionType.AesCtrEx)
            {
                BktrInfo = new BktrPatchInfo();

                reader.BaseStream.Position = start + 0x100;

                BktrInfo.RelocationHeader = new BktrHeader(reader);
                BktrInfo.EncryptionHeader = new BktrHeader(reader);
            }

            if (FormatType == NcaFormatType.Pfs0)
            {
                Type = SectionType.Pfs0;
            }
            else if (FormatType == NcaFormatType.Romfs)
            {
                if (EncryptionType == NcaEncryptionType.AesCtrEx)
                {
                    Type = SectionType.Bktr;
                }
                else
                {
                    Type = SectionType.Romfs;
                }
            }

            reader.BaseStream.Position = start + 0x140;
            Ctr = reader.ReadBytes(8).Reverse().ToArray();

            reader.BaseStream.Position = start + 512;
        }
    }

    public class RomfsSuperblock
    {
        public IvfcHeader IvfcHeader;

        public RomfsSuperblock(BinaryReader reader)
        {
            IvfcHeader = new IvfcHeader(reader);
            reader.BaseStream.Position += 0x58;
        }
    }

    public class BktrSuperblock
    {
        public IvfcHeader IvfcHeader;
        public BktrHeader RelocationHeader;
        public BktrHeader SubsectionHeader;

        public BktrSuperblock(BinaryReader reader)
        {
            IvfcHeader = new IvfcHeader(reader);
            reader.BaseStream.Position += 0x18;
            RelocationHeader = new BktrHeader(reader);
            SubsectionHeader = new BktrHeader(reader);
        }
    }

    public class BktrPatchInfo
    {
        public BktrHeader RelocationHeader;
        public BktrHeader EncryptionHeader;
    }

    public class IvfcHeader
    {
        public string Magic;
        public int Version;
        public uint MasterHashSize;
        public uint NumLevels;
        public IvfcLevelHeader[] LevelHeaders = new IvfcLevelHeader[6];
        public byte[] SaltSource;
        public byte[] MasterHash;

        public IvfcHeader(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            Version = reader.ReadInt16();
            reader.BaseStream.Position += 2;
            MasterHashSize = reader.ReadUInt32();
            NumLevels = reader.ReadUInt32();

            for (int i = 0; i < LevelHeaders.Length; i++)
            {
                LevelHeaders[i] = new IvfcLevelHeader(reader);
            }

            SaltSource = reader.ReadBytes(0x20);
            MasterHash = reader.ReadBytes(0x20);
        }
    }

    public class IvfcLevelHeader
    {
        public long LogicalOffset;
        public long HashDataSize;
        public int BlockSizePower;
        public uint Reserved;

        public Validity HashValidity = Validity.Unchecked;

        public IvfcLevelHeader(BinaryReader reader)
        {
            LogicalOffset = reader.ReadInt64();
            HashDataSize = reader.ReadInt64();
            BlockSizePower = reader.ReadInt32();
            Reserved = reader.ReadUInt32();
        }
    }

    public class Sha256Info
    {
        public byte[] MasterHash;
        public int BlockSize; // In bytes
        public uint Always2;
        public long HashTableOffset;
        public long HashTableSize;
        public long DataOffset;
        public long DataSize;

        public Validity HashValidity = Validity.Unchecked;
        
        public Sha256Info(BinaryReader reader)
        {
            MasterHash = reader.ReadBytes(0x20);
            BlockSize = reader.ReadInt32();
            Always2 = reader.ReadUInt32();
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

        public BktrHeader(BinaryReader reader)
        {
            Offset = reader.ReadInt64();
            Size = reader.ReadInt64();
            Magic = reader.ReadUInt32();
            Version = reader.ReadUInt32();
            NumEntries = reader.ReadUInt32();
            Field1C = reader.ReadUInt32();
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

    public class Pfs0Section
    {
        public PfsSuperblock Superblock { get; set; }
        public Validity Validity { get; set; }
    }

    public class RomfsSection
    {
        public RomfsSuperblock Superblock { get; set; }
        public IvfcLevel[] IvfcLevels { get; set; } = new IvfcLevel[Romfs.IvfcMaxLevel];
    }

    public enum ProgramPartitionType
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
        AocData
    }

    public enum DistributionType
    {
        Download,
        Gamecard
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

    public enum Validity : byte
    {
        Unchecked,
        Invalid,
        Valid,
        MissingKey
    }
}
