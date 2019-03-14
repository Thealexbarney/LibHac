using System;
using System.IO;
using LibHac.IO;

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

            if (Header.Size < Storage.Length)
            {
                AssetStorage = Storage.Slice(Header.Size);
                var assetreader = new BinaryReader(AssetStorage.AsStream());

                AssetHeader = new NroAssetHeader(assetreader);
                if (AssetHeader.Magic != "ASET")
                    throw new InvalidDataException("ASET magic is incorrect!");
            }
        }

        public IStorage OpenNroSegment(NroSegmentType type, bool leaveOpen)
        {
            var index = Convert.ToInt32(type);
            if (Header.NroSegments[index].Size > 0)
            {
                return Storage.Slice(Header.NroSegments[index].FileOffset, Header.NroSegments[index].Size, leaveOpen);
            }
            return null;
        }

        public IStorage OpenNroAssetSection(NroAssetType type, bool leaveOpen)
        {
            var index = Convert.ToInt32(type);
            if (AssetHeader.NroAssetSections[index].Size > 0)
            {
                return AssetStorage.Slice(AssetHeader.NroAssetSections[index].FileOffset, AssetHeader.NroAssetSections[index].Size, leaveOpen);
            }
            return null;
        }

    }

    public class NroStart
    {
        private int Mod0offset { get; }

        public NroStart(BinaryReader reader) {
            reader.ReadBytes(0x4);
            Mod0offset = reader.ReadInt32();
            reader.ReadBytes(0x8);
        }
    }

    public class NroHeader
    {
        public string Magic { get; private set; }                   
        public uint Version { get; private set; }
        public uint Size { get; private set; }
        public uint BssSize { get; private set; }
        public byte[] BuildId { get; } = new byte[0x20];

        public NroSegment[] NroSegments { get; } = new NroSegment[0x3];

        public NroHeader(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            Version = reader.ReadUInt32();
            Size = reader.ReadUInt32();
            reader.ReadBytes(4);

            for (int i = 0; i < 3; i++)
            {
                NroSegments[i] = new NroSegment(reader,(NroSegmentType)i);
            }

            BssSize = reader.ReadUInt32();
            reader.ReadBytes(4);
            BuildId = reader.ReadBytes(0x20);
            reader.ReadBytes(0x20);
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
        public NroSegmentType Type { get; private set; }
        public uint FileOffset { get; private set; }
        public uint Size { get; private set; }

        public NroSegment(BinaryReader reader, NroSegmentType type)
        {
            Type = type;
            FileOffset = reader.ReadUInt32();
            Size = reader.ReadUInt32();
        }
    }
            
    public class NroAssetHeader
    {
        public string Magic { get; private set; }
        public uint Version { get; private set; }
        public NroAssetSection[] NroAssetSections { get; private set; } = new NroAssetSection[0x3];

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
        Romfs
    }

    public class NroAssetSection
    {
        public NroAssetType Type { get; private set; } 
        public uint FileOffset { get; private set; }
        public uint Size { get; private set; }

        public NroAssetSection(BinaryReader reader, NroAssetType type)
        {
            Type = type;
            FileOffset = (uint)reader.ReadUInt64();
            Size = (uint)reader.ReadUInt64();
        }
    }

}
