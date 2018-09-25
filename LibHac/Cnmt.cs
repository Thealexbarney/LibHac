using System.IO;

namespace LibHac
{
    public class Cnmt
    {
        public ulong TitleId { get; }
        public TitleVersion TitleVersion { get; }
        public TitleType Type { get; }
        public byte FieldD { get; }
        public int TableOffset { get; }
        public int ContentEntryCount { get; }
        public int MetaEntryCount { get; }

        public CnmtContentEntry[] ContentEntries { get; }
        public CnmtContentMetaEntry[] MetaEntries { get; }

        public ulong ApplicationTitleId { get; }
        public ulong PatchTitleId { get; }
        public TitleVersion MinimumSystemVersion { get; }
        public TitleVersion MinimumApplicationVersion { get; }

        public CnmtExtended ExtendedData { get; }

        public Cnmt() { }

        public Cnmt(Stream file)
        {
            using (var reader = new BinaryReader(file))
            {
                TitleId = reader.ReadUInt64();
                var version = reader.ReadUInt32();
                Type = (TitleType)reader.ReadByte();
                TitleVersion = new TitleVersion(version, Type < TitleType.Application);
                FieldD = reader.ReadByte();
                TableOffset = reader.ReadUInt16();
                ContentEntryCount = reader.ReadUInt16();
                MetaEntryCount = reader.ReadUInt16();
                file.Position += 12;

                switch (Type)
                {
                    case TitleType.Application:
                        ApplicationTitleId = TitleId;
                        PatchTitleId = reader.ReadUInt64();
                        MinimumSystemVersion = new TitleVersion(reader.ReadUInt32(), true);
                        break;
                    case TitleType.Patch:
                        ApplicationTitleId = reader.ReadUInt64();
                        MinimumSystemVersion = new TitleVersion(reader.ReadUInt32(), true);
                        break;
                    case TitleType.AddOnContent:
                        ApplicationTitleId = reader.ReadUInt64();
                        MinimumApplicationVersion = new TitleVersion(reader.ReadUInt32());
                        break;
                }

                file.Position = 0x20 + TableOffset;

                ContentEntries = new CnmtContentEntry[ContentEntryCount];
                MetaEntries = new CnmtContentMetaEntry[MetaEntryCount];

                for (int i = 0; i < ContentEntryCount; i++)
                {
                    ContentEntries[i] = new CnmtContentEntry(reader);
                }

                for (int i = 0; i < MetaEntryCount; i++)
                {
                    MetaEntries[i] = new CnmtContentMetaEntry(reader);
                }

                if (Type == TitleType.Patch)
                {
                    ExtendedData = new CnmtExtended(reader);
                }
            }
        }
    }

    public class CnmtContentEntry
    {
        public byte[] Hash { get; set; }
        public byte[] NcaId { get; set; }
        public long Size { get; set; }
        public CnmtContentType Type { get; set; }

        public CnmtContentEntry() { }

        public CnmtContentEntry(BinaryReader reader)
        {
            Hash = reader.ReadBytes(0x20);
            NcaId = reader.ReadBytes(0x10);
            Size = reader.ReadUInt32();
            Size |= ((long)reader.ReadUInt16() << 32);
            Type = (CnmtContentType)reader.ReadByte();
            reader.BaseStream.Position += 1;
        }
    }

    public class CnmtContentMetaEntry
    {
        public ulong TitleId { get; }
        public TitleVersion Version { get; }
        public CnmtContentType Type { get; }

        public CnmtContentMetaEntry() { }

        public CnmtContentMetaEntry(BinaryReader reader)
        {
            TitleId = reader.ReadUInt64();
            Version = new TitleVersion(reader.ReadUInt32(), true);
            Type = (CnmtContentType)reader.ReadByte();
            reader.BaseStream.Position += 3;
        }
    }

    public class CnmtExtended
    {
        public int PrevMetaCount { get; }
        public int PrevDeltaCount { get; }
        public int DeltaInfoCount { get; }
        public int DeltaApplyCount { get; }
        public int PrevContentCount { get; }
        public int DeltaContentCount { get; }

        public CnmtPrevMetaEntry[] PrevMetas { get; }
        public CnmtPrevDelta[] PrevDeltas { get; }
        public CnmtDeltaInfo[] DeltaInfos { get; }
        public CnmtDeltaApplyInfo[] DeltaApplyInfos { get; }
        public CnmtPrevContent[] PrevContents { get; }
        public CnmtContentEntry[] DeltaContents { get; }

        public CnmtExtended(BinaryReader reader)
        {
            PrevMetaCount = reader.ReadInt32();
            PrevDeltaCount = reader.ReadInt32();
            DeltaInfoCount = reader.ReadInt32();
            DeltaApplyCount = reader.ReadInt32();
            PrevContentCount = reader.ReadInt32();
            DeltaContentCount = reader.ReadInt32();
            reader.BaseStream.Position += 4;

            PrevMetas = new CnmtPrevMetaEntry[PrevMetaCount];
            PrevDeltas = new CnmtPrevDelta[PrevDeltaCount];
            DeltaInfos = new CnmtDeltaInfo[DeltaInfoCount];
            DeltaApplyInfos = new CnmtDeltaApplyInfo[DeltaApplyCount];
            PrevContents = new CnmtPrevContent[PrevContentCount];
            DeltaContents = new CnmtContentEntry[DeltaContentCount];

            for (int i = 0; i < PrevMetaCount; i++)
            {
                PrevMetas[i] = new CnmtPrevMetaEntry(reader);
            }

            for (int i = 0; i < PrevDeltaCount; i++)
            {
                PrevDeltas[i] = new CnmtPrevDelta(reader);
            }

            for (int i = 0; i < DeltaInfoCount; i++)
            {
                DeltaInfos[i] = new CnmtDeltaInfo(reader);
            }

            for (int i = 0; i < DeltaApplyCount; i++)
            {
                DeltaApplyInfos[i] = new CnmtDeltaApplyInfo(reader);
            }

            for (int i = 0; i < PrevContentCount; i++)
            {
                PrevContents[i] = new CnmtPrevContent(reader);
            }

            for (int i = 0; i < DeltaContentCount; i++)
            {
                DeltaContents[i] = new CnmtContentEntry(reader);
            }
        }
    }

    public class CnmtPrevMetaEntry
    {
        public ulong TitleId { get; }
        public TitleVersion Version { get; }
        public TitleType Type { get; }
        public byte[] Hash { get; }
        public short Field30 { get; }
        public short Field32 { get; }
        public int Field34 { get; }

        public CnmtPrevMetaEntry(BinaryReader reader)
        {
            TitleId = reader.ReadUInt64();
            Version = new TitleVersion(reader.ReadUInt32());
            Type = (TitleType)reader.ReadByte();
            reader.BaseStream.Position += 3;
            Hash = reader.ReadBytes(0x20);
            Field30 = reader.ReadInt16();
            Field32 = reader.ReadInt16();
            Field34 = reader.ReadInt32();
        }
    }

    public class CnmtPrevDelta
    {
        public ulong TitleIdOld { get; }
        public ulong TitleIdNew { get; }
        public TitleVersion VersionOld { get; }
        public TitleVersion VersionNew { get; }
        public long Size { get; }
        public long Field20 { get; }

        public CnmtPrevDelta(BinaryReader reader)
        {
            TitleIdOld = reader.ReadUInt64();
            TitleIdNew = reader.ReadUInt64();
            VersionOld = new TitleVersion(reader.ReadUInt32());
            VersionNew = new TitleVersion(reader.ReadUInt32());
            Size = reader.ReadInt64();
            Field20 = reader.ReadInt64();
        }
    }

    public class CnmtDeltaInfo
    {
        public ulong TitleIdOld { get; }
        public ulong TitleIdNew { get; }
        public TitleVersion VersionOld { get; }
        public TitleVersion VersionNew { get; }
        public long Field18 { get; }
        public long Field20 { get; }

        public CnmtDeltaInfo(BinaryReader reader)
        {
            TitleIdOld = reader.ReadUInt64();
            TitleIdNew = reader.ReadUInt64();
            VersionOld = new TitleVersion(reader.ReadUInt32());
            VersionNew = new TitleVersion(reader.ReadUInt32());
            Field18 = reader.ReadInt64();
            Field20 = reader.ReadInt64();
        }
    }

    public class CnmtDeltaApplyInfo
    {
        public byte[] NcaIdOld { get; }
        public byte[] NcaIdNew { get; }
        public long SizeOld { get; }
        public long SizeNew { get; }
        public short Field2C { get; }
        public CnmtContentType Type { get; }
        public short Field2F { get; }
        public int Field30 { get; }


        public CnmtDeltaApplyInfo(BinaryReader reader)
        {
            NcaIdOld = reader.ReadBytes(0x10);
            NcaIdNew = reader.ReadBytes(0x10);

            SizeOld = reader.ReadUInt32();
            SizeOld |= ((long)reader.ReadUInt16() << 32);
            SizeNew |= ((long)reader.ReadUInt16() << 32);
            SizeNew = reader.ReadUInt32();

            Field2C = reader.ReadInt16();
            Type = (CnmtContentType)reader.ReadByte();
            Field2F = reader.ReadByte();
            Field30 = reader.ReadInt32();
        }
    }

    public class CnmtPrevContent
    {
        public byte[] NcaId { get; }
        public long Size { get; }
        public CnmtContentType Type { get; }

        public CnmtPrevContent(BinaryReader reader)
        {
            NcaId = reader.ReadBytes(0x10);
            Size = reader.ReadUInt32();
            Size |= ((long)reader.ReadUInt16() << 32);
            Type = (CnmtContentType)reader.ReadByte();
            reader.BaseStream.Position += 1;
        }
    }

    public enum CnmtContentType
    {
        Meta,
        Program,
        Data,
        Control,
        HtmlDocument,
        LegalInformation,
        DeltaFragment
    }

    public enum TitleType
    {
        SystemProgram = 1,
        SystemData,
        SystemUpdate,
        BootImagePackage,
        BootImagePackageSafe,
        Application = 0x80,
        Patch,
        AddOnContent,
        Delta
    }
}
