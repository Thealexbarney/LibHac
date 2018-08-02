using System.IO;

namespace libhac
{
    public class Cnmt
    {
        public ulong TitleId { get; set; }
        public TitleVersion TitleVersion { get; set; }
        public TitleType Type { get; set; }
        public byte FieldD { get; set; }
        public int TableOffset { get; set; }
        public int ContentEntryCount { get; set; }
        public int MetaEntryCount { get; set; }

        public CnmtContentEntry[] ContentEntries { get; set; }
        public CnmtMetaEntry[] MetaEntries { get; set; }

        public ulong ApplicationTitleId { get; set; }
        public ulong PatchTitleId { get; set; }
        public TitleVersion MinimumSystemVersion { get; }
        public TitleVersion MinimumApplicationVersion { get; }

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
                MetaEntries = new CnmtMetaEntry[MetaEntryCount];

                for (int i = 0; i < ContentEntryCount; i++)
                {
                    ContentEntries[i] = new CnmtContentEntry(reader);
                }

                for (int i = 0; i < MetaEntryCount; i++)
                {
                    MetaEntries[i] = new CnmtMetaEntry(reader);
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

    public class CnmtMetaEntry
    {
        public ulong TitleId { get; }
        public TitleVersion Version { get; }
        public CnmtContentType Type { get; }

        public CnmtMetaEntry() { }

        public CnmtMetaEntry(BinaryReader reader)
        {
            TitleId = reader.ReadUInt64();
            Version = new TitleVersion(reader.ReadUInt32(), true);
            Type = (CnmtContentType)reader.ReadByte();
            reader.BaseStream.Position += 3;
        }
    }

    public enum CnmtContentType
    {
        Meta,
        Program,
        Data,
        Control,
        OfflineManualHtml,
        LegalHtml,
        UpdatePatch
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
        DeltaTitle
    }
}
