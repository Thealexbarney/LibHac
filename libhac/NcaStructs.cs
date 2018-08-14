using System.IO;
using System.Linq;

namespace libhac
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

        public static NcaHeader Read(BinaryReader reader)
        {
            var head = new NcaHeader();

            head.Signature1 = reader.ReadBytes(0x100);
            head.Signature2 = reader.ReadBytes(0x100);
            head.Magic = reader.ReadAscii(4);
            if (head.Magic != "NCA3") throw new InvalidDataException("Not an NCA3 file");
            head.Distribution = (DistributionType)reader.ReadByte();
            head.ContentType = (ContentType)reader.ReadByte();
            head.CryptoType = reader.ReadByte();
            head.KaekInd = reader.ReadByte();
            head.NcaSize = reader.ReadUInt64();
            head.TitleId = reader.ReadUInt64();
            reader.BaseStream.Position += 4;

            head.SdkVersion = new TitleVersion(reader.ReadUInt32());
            head.CryptoType2 = reader.ReadByte();
            reader.BaseStream.Position += 0xF;

            head.RightsId = reader.ReadBytes(0x10);

            for (int i = 0; i < 4; i++)
            {
                head.SectionEntries[i] = new NcaSectionEntry(reader);
            }

            for (int i = 0; i < 4; i++)
            {
                head.SectionHashes[i] = reader.ReadBytes(0x20);
            }

            for (int i = 0; i < 4; i++)
            {
                head.EncryptedKeys[i] = reader.ReadBytes(0x10);
            }

            reader.BaseStream.Position += 0xC0;

            for (int i = 0; i < 4; i++)
            {
                head.FsHeaders[i] = new NcaFsHeader(reader);
            }
            return head;
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
        public byte Field0;
        public byte Field1;
        public SectionPartitionType PartitionType;
        public SectionFsType FsType;
        public SectionCryptType CryptType;
        public SectionType Type;

        public PfsSuperblock Pfs;
        public RomfsSuperblock Romfs;
        public BktrSuperblock Bktr;
        public byte[] Ctr;

        public NcaFsHeader(BinaryReader reader)
        {
            var start = reader.BaseStream.Position;
            Field0 = reader.ReadByte();
            Field1 = reader.ReadByte();
            PartitionType = (SectionPartitionType)reader.ReadByte();
            FsType = (SectionFsType)reader.ReadByte();
            CryptType = (SectionCryptType)reader.ReadByte();
            reader.BaseStream.Position += 3;

            if (PartitionType == SectionPartitionType.Pfs0 && FsType == SectionFsType.Pfs0)
            {
                Type = SectionType.Pfs0;
                Pfs = new PfsSuperblock(reader);
            }
            else if (PartitionType == SectionPartitionType.Romfs && FsType == SectionFsType.Romfs)
            {
                if (CryptType == SectionCryptType.BKTR)
                {
                    Type = SectionType.Bktr;
                    Bktr = new BktrSuperblock(reader);
                }
                else
                {
                    Type = SectionType.Romfs;
                    Romfs = new RomfsSuperblock(reader);
                }
            }

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

    public class IvfcHeader
    {
        public string Magic;
        public uint Id;
        public uint MasterHashSize;
        public uint NumLevels;
        public IvfcLevelHeader[] LevelHeaders = new IvfcLevelHeader[6];
        public byte[] MasterHash;


        public IvfcHeader(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            Id = reader.ReadUInt32();
            MasterHashSize = reader.ReadUInt32();
            NumLevels = reader.ReadUInt32();

            for (int i = 0; i < LevelHeaders.Length; i++)
            {
                LevelHeaders[i] = new IvfcLevelHeader(reader);
            }

            reader.BaseStream.Position += 0x20;
            MasterHash = reader.ReadBytes(0x20);
        }
    }

    public class IvfcLevelHeader
    {
        public long LogicalOffset;
        public long HashDataSize;
        public int BlockSize;
        public uint Reserved;

        public IvfcLevelHeader(BinaryReader reader)
        {
            LogicalOffset = reader.ReadInt64();
            HashDataSize = reader.ReadInt64();
            BlockSize = reader.ReadInt32();
            Reserved = reader.ReadUInt32();
        }
    }

    public class BktrHeader
    {
        public long Offset;
        public long Size;
        public uint Magic;
        public uint Field14;
        public uint NumEntries;
        public uint Field1C;

        public BktrHeader(BinaryReader reader)
        {
            Offset = reader.ReadInt64();
            Size = reader.ReadInt64();
            Magic = reader.ReadUInt32();
            Field14 = reader.ReadUInt32();
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

    public enum SectionCryptType
    {
        None = 1,
        XTS,
        CTR,
        BKTR
    }

    public enum SectionFsType
    {
        Pfs0 = 2,
        Romfs
    }

    public enum SectionPartitionType
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

    public enum Validity
    {
        Unchecked,
        Invalid,
        Valid
    }
}
