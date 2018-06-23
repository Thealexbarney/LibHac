using System.IO;

namespace libhac
{
    public class Cnmt
    {
        public ulong TitleId { get; set; }
        public uint TitleVersion { get; set; }
        public TitleType Type { get; set; }
        public byte FieldD { get; set; }
        public int TableOffset { get; set; }
        public int ContentEntryCount { get; set; }
        public int MetaEntryCount { get; set; }

        public CnmtContentEntry[] ContentEntries { get; set; }

        public Cnmt(Stream file)
        {
            using (var reader = new BinaryReader(file))
            {
                TitleId = reader.ReadUInt64();
                TitleVersion = reader.ReadUInt32();
                Type = (TitleType)reader.ReadByte();
                FieldD = reader.ReadByte();
                TableOffset = reader.ReadUInt16();
                ContentEntryCount = reader.ReadUInt16();
                MetaEntryCount = reader.ReadUInt16();
                file.Position += 12;
                file.Position += TableOffset;

                ContentEntries = new CnmtContentEntry[ContentEntryCount];

                for (int i = 0; i < ContentEntryCount; i++)
                {
                    ContentEntries[i] = new CnmtContentEntry(reader);
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
        SystemDataArchive,
        SystemUpdate,
        FirmwarePackageA,
        FirmwarePackageB,
        RegularApplication = 0x80,
        UpdateTitle,
        AddOnContent,
        DeltaTitle
    }
}
