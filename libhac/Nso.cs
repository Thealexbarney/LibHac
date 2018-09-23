using LibHac.Streams;
using Ryujinx.HLE.Loaders.Compression;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LibHac
{
    public class Nso
    {
        public NsoSection[] Sections;
        public RodataRelativeExtent[] RodataRelativeExtents;
        public uint BssSize;
        public byte[] BuildID = new byte[0x20];

        private SharedStreamSource StreamSource;

        public Nso(Stream stream)
        {
            StreamSource = new SharedStreamSource(stream);
            BinaryReader reader = new BinaryReader(StreamSource.CreateStream());
            if (reader.ReadAscii(4) != "NSO0")
                throw new InvalidDataException("NSO magic is incorrect!");
            reader.ReadUInt32(); // Version
            reader.ReadUInt32(); // Reserved/Unused
            BitArray flags = new BitArray(new int[] { (int) reader.ReadUInt32() });
            NsoSection textSection = new NsoSection(StreamSource);
            NsoSection rodataSection = new NsoSection(StreamSource);
            NsoSection dataSection = new NsoSection(StreamSource);
            textSection.IsCompressed = flags[0];
            textSection.CheckHash = flags[3];
            rodataSection.IsCompressed = flags[1];
            rodataSection.CheckHash = flags[4];
            dataSection.IsCompressed = flags[2];
            dataSection.CheckHash = flags[5];

            ReadSegmentHeader(textSection);
            reader.ReadUInt32(); // Module offset (TODO)
            ReadSegmentHeader(rodataSection);
            reader.ReadUInt32(); // Module file size
            ReadSegmentHeader(dataSection);
            BssSize = reader.ReadUInt32();
            reader.Read(BuildID, 0, 0x20);
            textSection.CompressedSize = reader.ReadUInt32();
            rodataSection.CompressedSize = reader.ReadUInt32();
            dataSection.CompressedSize = reader.ReadUInt32();
            reader.ReadBytes(0x1C); // Padding
            RodataRelativeExtents = new RodataRelativeExtent[]
            {
                ReadRodataRelativeExtent(), ReadRodataRelativeExtent(), ReadRodataRelativeExtent()
            };

            reader.Read(textSection.Hash, 0, 0x20);
            reader.Read(rodataSection.Hash, 0, 0x20);
            reader.Read(dataSection.Hash, 0, 0x20);

            Sections = new NsoSection[] {textSection, rodataSection, dataSection };
            reader.Close();
        }

        public void ReadSegmentHeader(NsoSection section)
        {
            BinaryReader reader = new BinaryReader(StreamSource.CreateStream());
            section.FileOffset = reader.ReadUInt32();
            section.MemoryOffset = reader.ReadUInt32();
            section.DecompressedSize = reader.ReadUInt32();
            reader.Close();
        }

        public RodataRelativeExtent ReadRodataRelativeExtent()
        {
            BinaryReader reader = new BinaryReader(StreamSource.CreateStream());
            RodataRelativeExtent extent = new RodataRelativeExtent();
            extent.RegionRodataOffset = reader.ReadUInt32();
            extent.RegionSize = reader.ReadUInt32();
            reader.Close();
            return extent;
        }

        public class NsoSection
        {
            private readonly SharedStreamSource StreamSource;

            public bool IsCompressed,
                 CheckHash;
            public uint FileOffset,
                MemoryOffset,
                DecompressedSize,
                CompressedSize;
            public byte[] Hash = new byte[0x20];

            public NsoSection(SharedStreamSource streamSource)
            {
                StreamSource = streamSource;
            }

            public Stream OpenCompressedStream()
            {
                return StreamSource.CreateStream(FileOffset, CompressedSize);
            }

            public Stream OpenDecompressedStream()
            {
                return new MemoryStream(Decompress());
            }

            public byte[] Decompress()
            {
                byte[] compressed = new byte[CompressedSize];
                OpenCompressedStream().Read(compressed, 0, (int) CompressedSize);
                return Lz4.Decompress(compressed, (int) DecompressedSize);
            }
        }

        public class RodataRelativeExtent
        {
            public uint 
                RegionRodataOffset,
                RegionSize;
        }


    }
}
