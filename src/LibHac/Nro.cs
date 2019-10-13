using System.IO;
using LibHac.Fs;
using LibHac.FsSystem;

namespace LibHac
{
    public class Nro
    {
        public NroStart Start { get; }
        public NroHeader Header { get; }
        public NroAssetHeader AssetHeader { get; }
        private IStorage Storage { get; }
        private IStorage AssetStorage { get; }

        public Nro(IStorage storage)
        {
            Storage = storage;
            var reader = new BinaryReader(Storage.AsStream());
            Start = new NroStart(reader);
            Header = new NroHeader(reader);

            if (Header.Magic != "NRO0")
                throw new InvalidDataException("NRO0 magic is incorrect!");

            Storage.GetSize(out long storageSize).ThrowIfFailure();

            if (Header.Size < storageSize)
            {
                AssetStorage = Storage.Slice(Header.Size);
                var assetReader = new BinaryReader(AssetStorage.AsStream());

                AssetHeader = new NroAssetHeader(assetReader);
                if (AssetHeader.Magic != "ASET")
                    throw new InvalidDataException("ASET magic is incorrect!");
            }
        }

        public IStorage OpenNroSegment(NroSegmentType type, bool leaveOpen)
        {
            NroSegment segment = Header.NroSegments[(int)type];

            if (segment.Size <= 0) return new NullStorage(0);

            return Storage.Slice(segment.FileOffset, segment.Size, leaveOpen);
        }

        public IStorage OpenNroAssetSection(NroAssetType type, bool leaveOpen)
        {
            NroAssetSection header = AssetHeader.NroAssetSections[(int)type];

            if (header.Size <= 0) return new NullStorage(0);

            return AssetStorage.Slice(header.FileOffset, header.Size, leaveOpen);
        }

    }

    public class NroStart
    {
        public int Mod0Offset { get; }

        public NroStart(BinaryReader reader)
        {
            reader.BaseStream.Position += 4;
            Mod0Offset = reader.ReadInt32();
            reader.BaseStream.Position += 8;
        }
    }

    public class NroHeader
    {
        public string Magic { get; }
        public uint Version { get; }
        public uint Size { get; }
        public uint BssSize { get; }
        public byte[] BuildId { get; }

        public NroSegment[] NroSegments { get; } = new NroSegment[0x3];

        public NroHeader(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            Version = reader.ReadUInt32();
            Size = reader.ReadUInt32();
            reader.BaseStream.Position += 4;

            for (int i = 0; i < 3; i++)
            {
                NroSegments[i] = new NroSegment(reader, (NroSegmentType)i);
            }

            BssSize = reader.ReadUInt32();
            reader.BaseStream.Position += 4;
            BuildId = reader.ReadBytes(0x20);
            reader.BaseStream.Position += 0x20;
        }
    }

    public enum NroSegmentType
    {
        Text = 0,
        Ro,
        Data
    }

    public class NroSegment
    {
        public NroSegmentType Type { get; }
        public uint FileOffset { get; }
        public uint Size { get; }

        public NroSegment(BinaryReader reader, NroSegmentType type)
        {
            Type = type;
            FileOffset = reader.ReadUInt32();
            Size = reader.ReadUInt32();
        }
    }

    public class NroAssetHeader
    {
        public string Magic { get; }
        public uint Version { get; }
        public NroAssetSection[] NroAssetSections { get; } = new NroAssetSection[0x3];

        public NroAssetHeader(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            Version = reader.ReadUInt32();
            for (int i = 0; i < 3; i++)
            {
                NroAssetSections[i] = new NroAssetSection(reader, (NroAssetType)i);
            }
        }
    }

    public enum NroAssetType
    {
        Icon = 0,
        Nacp,
        RomFs
    }

    public class NroAssetSection
    {
        public NroAssetType Type { get; }
        public uint FileOffset { get; }
        public uint Size { get; }

        public NroAssetSection(BinaryReader reader, NroAssetType type)
        {
            Type = type;
            FileOffset = (uint)reader.ReadUInt64();
            Size = (uint)reader.ReadUInt64();
        }
    }
}
