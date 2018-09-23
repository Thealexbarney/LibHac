using LibHac.Streams;
using LZ4;
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

        private BinaryReader reader;

        public Nso(Stream stream)
        {
            reader = new BinaryReader(stream);
            if (reader.ReadAscii(4) != "NSO0")
                throw new InvalidDataException("NSO magic is incorrect!");
            reader.ReadUInt32(); // Version
            reader.ReadUInt32(); // Reserved/Unused
            BitArray flags = new BitArray(new int[] { (int) reader.ReadUInt32() });
            NsoSection textSection = new NsoSection(stream);
            NsoSection rodataSection = new NsoSection(stream);
            NsoSection dataSection = new NsoSection(stream);
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
        }

        public void ReadSegmentHeader(NsoSection section)
        {
            section.FileOffset = reader.ReadUInt32();
            section.MemoryOffset = reader.ReadUInt32();
            section.DecompressedSize = reader.ReadUInt32();   
        }

        public RodataRelativeExtent ReadRodataRelativeExtent()
        {
            RodataRelativeExtent extent = new RodataRelativeExtent();
            extent.RegionRodataOffset = reader.ReadUInt32();
            extent.RegionSize = reader.ReadUInt32();
            return extent;
        }

        public class NsoSection
        {
            private Stream Stream;

            public bool IsCompressed,
                 CheckHash;
            public uint FileOffset,
                MemoryOffset,
                DecompressedSize,
                CompressedSize;
            public byte[] Hash = new byte[0x20];

            public NsoSection(Stream stream)
            {
                Stream = stream;
            }

            public Stream OpenCompressedStream()
            {
                return new SubStream(Stream, FileOffset, CompressedSize);
            }

            public Stream OpenDecompressedStream()
            {
                return new LZ4Stream(OpenCompressedStream(), LZ4StreamMode.Decompress);
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
