using System.IO;
using System.Linq;
using LibHac.FsSystem.NcaUtils;
using LibHac.Ncm;
using ContentType = LibHac.Ncm.ContentType;

namespace LibHac
{
    public class Cnmt
    {
        public ulong TitleId { get; }
        public TitleVersion TitleVersion { get; }
        public ContentMetaType Type { get; }
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

        public byte[] Hash { get; }

        public Cnmt() { }

        public Cnmt(Stream file)
        {
            using (var reader = new BinaryReader(file))
            {
                TitleId = reader.ReadUInt64();
                uint version = reader.ReadUInt32();
                Type = (ContentMetaType)reader.ReadByte();
                TitleVersion = new TitleVersion(version, Type < ContentMetaType.Application);
                FieldD = reader.ReadByte();
                TableOffset = reader.ReadUInt16();
                ContentEntryCount = reader.ReadUInt16();
                MetaEntryCount = reader.ReadUInt16();

                // Old, pre-release cnmt files don't have the "required system version" field.
                // Try to detect this by reading the padding after that field.
                // The old format usually contains hashes there.
                file.Position += 8;
                int padding = reader.ReadInt32();
                bool isOldCnmtFormat = padding != 0;

                switch (Type)
                {
                    case ContentMetaType.Application:
                        ApplicationTitleId = TitleId;
                        PatchTitleId = reader.ReadUInt64();
                        MinimumSystemVersion = new TitleVersion(reader.ReadUInt32(), true);
                        break;
                    case ContentMetaType.Patch:
                        ApplicationTitleId = reader.ReadUInt64();
                        MinimumSystemVersion = new TitleVersion(reader.ReadUInt32(), true);
                        break;
                    case ContentMetaType.AddOnContent:
                        ApplicationTitleId = reader.ReadUInt64();
                        MinimumApplicationVersion = new TitleVersion(reader.ReadUInt32());
                        break;
                }

                int baseOffset = isOldCnmtFormat ? 0x18 : 0x20;
                file.Position = baseOffset + TableOffset;

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

                if (Type == ContentMetaType.Patch)
                {
                    ExtendedData = new CnmtExtended(reader);
                }

                Hash = reader.ReadBytes(0x20);
            }
        }
    }

    public class CnmtContentEntry
    {
        public byte[] Hash { get; set; }
        public byte[] NcaId { get; set; }
        public long Size { get; set; }
        public ContentType Type { get; set; }

        public CnmtContentEntry() { }

        public CnmtContentEntry(BinaryReader reader)
        {
            Hash = reader.ReadBytes(0x20);
            NcaId = reader.ReadBytes(0x10);
            Size = reader.ReadUInt32();
            Size |= (long)reader.ReadUInt16() << 32;
            Type = (ContentType)reader.ReadByte();
            reader.BaseStream.Position += 1;
        }
    }

    public class CnmtContentMetaEntry
    {
        public ulong TitleId { get; }
        public TitleVersion Version { get; }
        public ContentType Type { get; }

        public CnmtContentMetaEntry() { }

        public CnmtContentMetaEntry(BinaryReader reader)
        {
            TitleId = reader.ReadUInt64();
            Version = new TitleVersion(reader.ReadUInt32(), true);
            Type = (ContentType)reader.ReadByte();
            reader.BaseStream.Position += 3;
        }
    }

    public class CnmtExtended
    {
        public int PrevMetaCount { get; }
        public int PrevDeltaSetCount { get; }
        public int DeltaSetCount { get; }
        public int FragmentSetCount { get; }
        public int PrevContentCount { get; }
        public int DeltaContentCount { get; }

        public CnmtPrevMetaEntry[] PrevMetas { get; }
        public CnmtPrevDelta[] PrevDeltaSets { get; }
        public CnmtDeltaSetInfo[] DeltaSets { get; }
        public CnmtFragmentSetInfo[] FragmentSets { get; }
        public CnmtPrevContent[] PrevContents { get; }
        public CnmtContentEntry[] DeltaContents { get; }
        public FragmentMapEntry[] FragmentMap { get; }

        public CnmtExtended(BinaryReader reader)
        {
            PrevMetaCount = reader.ReadInt32(); // Lists all previous content meta files
            PrevDeltaSetCount = reader.ReadInt32(); // Lists all previous delta sets
            DeltaSetCount = reader.ReadInt32(); // Lists the delta set for the current title version
            FragmentSetCount = reader.ReadInt32(); // Groups fragments into full deltas
            PrevContentCount = reader.ReadInt32(); // Lists all previous NCAs for the title
            DeltaContentCount = reader.ReadInt32(); // Lists all NCAs containing delta fragments
            reader.BaseStream.Position += 4;

            PrevMetas = new CnmtPrevMetaEntry[PrevMetaCount];
            PrevDeltaSets = new CnmtPrevDelta[PrevDeltaSetCount];
            DeltaSets = new CnmtDeltaSetInfo[DeltaSetCount];
            FragmentSets = new CnmtFragmentSetInfo[FragmentSetCount];
            PrevContents = new CnmtPrevContent[PrevContentCount];
            DeltaContents = new CnmtContentEntry[DeltaContentCount];

            for (int i = 0; i < PrevMetaCount; i++)
            {
                PrevMetas[i] = new CnmtPrevMetaEntry(reader);
            }

            for (int i = 0; i < PrevDeltaSetCount; i++)
            {
                PrevDeltaSets[i] = new CnmtPrevDelta(reader);
            }

            for (int i = 0; i < DeltaSetCount; i++)
            {
                DeltaSets[i] = new CnmtDeltaSetInfo(reader);
            }

            for (int i = 0; i < FragmentSetCount; i++)
            {
                FragmentSets[i] = new CnmtFragmentSetInfo(reader);
            }

            for (int i = 0; i < PrevContentCount; i++)
            {
                PrevContents[i] = new CnmtPrevContent(reader);
            }

            for (int i = 0; i < DeltaContentCount; i++)
            {
                DeltaContents[i] = new CnmtContentEntry(reader);
            }

            int fragmentCount = FragmentSets.Sum(x => x.FragmentCount);
            FragmentMap = new FragmentMapEntry[fragmentCount];

            for (int i = 0; i < fragmentCount; i++)
            {
                FragmentMap[i] = new FragmentMapEntry(reader);
            }
        }
    }

    public class CnmtPrevMetaEntry
    {
        public ulong TitleId { get; }
        public TitleVersion Version { get; }
        public ContentMetaType Type { get; }
        public byte[] Hash { get; }
        public short ContentCount { get; }
        public short CnmtPrevMetaEntryField32 { get; }
        public int CnmtPrevMetaEntryField34 { get; }

        public CnmtPrevMetaEntry(BinaryReader reader)
        {
            TitleId = reader.ReadUInt64();
            Version = new TitleVersion(reader.ReadUInt32());
            Type = (ContentMetaType)reader.ReadByte();
            reader.BaseStream.Position += 3;
            Hash = reader.ReadBytes(0x20);
            ContentCount = reader.ReadInt16();
            CnmtPrevMetaEntryField32 = reader.ReadInt16();
            CnmtPrevMetaEntryField34 = reader.ReadInt32();
        }
    }

    public class CnmtPrevDelta
    {
        public ulong TitleIdOld { get; }
        public ulong TitleIdNew { get; }
        public TitleVersion VersionOld { get; }
        public TitleVersion VersionNew { get; }
        public long Size { get; }
        public long CnmtPrevDeltaField20 { get; }

        public CnmtPrevDelta(BinaryReader reader)
        {
            TitleIdOld = reader.ReadUInt64();
            TitleIdNew = reader.ReadUInt64();
            VersionOld = new TitleVersion(reader.ReadUInt32());
            VersionNew = new TitleVersion(reader.ReadUInt32());
            Size = reader.ReadInt64();
            CnmtPrevDeltaField20 = reader.ReadInt64();
        }
    }

    public class CnmtDeltaSetInfo
    {
        public ulong TitleIdOld { get; }
        public ulong TitleIdNew { get; }
        public TitleVersion VersionOld { get; }
        public TitleVersion VersionNew { get; }
        public long FragmentSetCount { get; }
        public long DeltaContentCount { get; }

        public CnmtDeltaSetInfo(BinaryReader reader)
        {
            TitleIdOld = reader.ReadUInt64();
            TitleIdNew = reader.ReadUInt64();
            VersionOld = new TitleVersion(reader.ReadUInt32());
            VersionNew = new TitleVersion(reader.ReadUInt32());
            FragmentSetCount = reader.ReadInt64();
            DeltaContentCount = reader.ReadInt64();
        }
    }

    public class CnmtFragmentSetInfo
    {
        public byte[] NcaIdOld { get; }
        public byte[] NcaIdNew { get; }
        public long SizeOld { get; }
        public long SizeNew { get; }
        public short FragmentCount { get; }
        public ContentType Type { get; }
        public UpdateType DeltaType { get; }
        public int FragmentSetInfoField30 { get; }


        public CnmtFragmentSetInfo(BinaryReader reader)
        {
            NcaIdOld = reader.ReadBytes(0x10);
            NcaIdNew = reader.ReadBytes(0x10);

            SizeOld = reader.ReadUInt32();
            SizeOld |= (long)reader.ReadUInt16() << 32;
            SizeNew |= (long)reader.ReadUInt16() << 32;
            SizeNew = reader.ReadUInt32();

            FragmentCount = reader.ReadInt16();
            Type = (ContentType)reader.ReadByte();
            DeltaType = (UpdateType)reader.ReadByte();
            FragmentSetInfoField30 = reader.ReadInt32();
        }
    }

    public class CnmtPrevContent
    {
        public byte[] NcaId { get; }
        public long Size { get; }
        public ContentType Type { get; }

        public CnmtPrevContent(BinaryReader reader)
        {
            NcaId = reader.ReadBytes(0x10);
            Size = reader.ReadUInt32();
            Size |= (long)reader.ReadUInt16() << 32;
            Type = (ContentType)reader.ReadByte();
            reader.BaseStream.Position += 1;
        }
    }

    public class FragmentMapEntry
    {
        public short ContentIndex { get; }
        public short FragmentIndex { get; }

        public FragmentMapEntry(BinaryReader reader)
        {
            ContentIndex = reader.ReadInt16();
            FragmentIndex = reader.ReadInt16();
        }
    }
}
