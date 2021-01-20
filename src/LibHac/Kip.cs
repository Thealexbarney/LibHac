using System;
using System.IO;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Kernel;

namespace LibHac
{
    [Obsolete("This class has been deprecated. LibHac.Loader.KipReader should be used instead.")]
    public class Kip
    {
        private const int HeaderSize = 0x100;

        public KipHeader Header { get; }

        public int[] SectionOffsets { get; } = new int[6];
        public int Size { get; }

        private IStorage Storage { get; }

        public Kip(IStorage storage)
        {
            Header = new KipHeader(storage);

            Size = HeaderSize;

            for (int index = 0; index < Header.Sections.Length; index++)
            {
                int sectionSize = Header.Sections[index].CompressedSize;
                SectionOffsets[index] = Size;
                Size += sectionSize;
            }

            Storage = storage.Slice(0, Size);
        }

        public IStorage OpenSection(int index)
        {
            if (index < 0 || index > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Section index must be between 0-5");
            }

            return Storage.Slice(SectionOffsets[index], Header.Sections[index].CompressedSize);
        }

        public byte[] DecompressSection(int index)
        {
            IStorage compStorage = OpenSection(index);
            compStorage.GetSize(out long compressedSize).ThrowIfFailure();

            byte[] compressed = new byte[compressedSize];
            compStorage.Read(0, compressed).ThrowIfFailure();

            return DecompressBlz(compressed);
        }

        public IStorage OpenRawFile() => Storage;

        private static byte[] DecompressBlz(byte[] compressed)
        {
            int additionalSize = BitConverter.ToInt32(compressed, compressed.Length - 4);
            int headerSize = BitConverter.ToInt32(compressed, compressed.Length - 8);
            int totalCompSize = BitConverter.ToInt32(compressed, compressed.Length - 12);

            byte[] decompressed = new byte[totalCompSize + additionalSize];

            int inOffset = totalCompSize - headerSize;
            int outOffset = totalCompSize + additionalSize;

            while (outOffset > 0)
            {
                byte control = compressed[--inOffset];

                for (int i = 0; i < 8; i++)
                {
                    if ((control & 0x80) != 0)
                    {
                        if (inOffset < 2) throw new InvalidDataException("KIP1 decompression out of bounds!");

                        inOffset -= 2;

                        ushort segmentValue = BitConverter.ToUInt16(compressed, inOffset);
                        int segmentSize = ((segmentValue >> 12) & 0xF) + 3;
                        int segmentOffset = (segmentValue & 0x0FFF) + 3;

                        if (outOffset < segmentSize)
                        {
                            // Kernel restricts segment copy to stay in bounds.
                            segmentSize = outOffset;
                        }
                        outOffset -= segmentSize;

                        for (int j = 0; j < segmentSize; j++)
                        {
                            decompressed[outOffset + j] = decompressed[outOffset + j + segmentOffset];
                        }
                    }
                    else
                    {
                        // Copy directly.
                        if (inOffset < 1) throw new InvalidDataException("KIP1 decompression out of bounds!");

                        decompressed[--outOffset] = compressed[--inOffset];
                    }
                    control <<= 1;
                    if (outOffset == 0) return decompressed;
                }
            }

            return decompressed;
        }
    }

    public class KipHeader
    {
        public string Magic { get; }
        public string Name { get; }
        public ulong TitleId { get; }
        public int ProcessCategory { get; }
        public byte MainThreadPriority { get; }
        public byte DefaultCore { get; }
        public byte Field1E { get; }
        public byte Flags { get; }
        public KipSectionHeader[] Sections { get; } = new KipSectionHeader[6];
        public byte[] Capabilities { get; }

        public KipHeader(IStorage storage)
        {
            var reader = new BinaryReader(storage.AsStream());

            Magic = reader.ReadAscii(4);
            if (Magic != "KIP1")
            {
                throw new InvalidDataException("Invalid KIP file!");
            }

            Name = reader.ReadAsciiZ(0xC);

            reader.BaseStream.Position = 0x10;
            TitleId = reader.ReadUInt64();
            ProcessCategory = reader.ReadInt32();
            MainThreadPriority = reader.ReadByte();
            DefaultCore = reader.ReadByte();
            Field1E = reader.ReadByte();
            Flags = reader.ReadByte();

            for (int i = 0; i < Sections.Length; i++)
            {
                Sections[i] = new KipSectionHeader(reader);
            }

            Capabilities = reader.ReadBytes(0x20);
        }
    }

    public class KipSectionHeader
    {
        public int OutOffset { get; }
        public int DecompressedSize { get; }
        public int CompressedSize { get; }
        public int Attribute { get; }

        public KipSectionHeader(BinaryReader reader)
        {
            OutOffset = reader.ReadInt32();
            DecompressedSize = reader.ReadInt32();
            CompressedSize = reader.ReadInt32();
            Attribute = reader.ReadInt32();
        }
    }

    public class Ini1
    {
        public KipReader[] Kips { get; }

        public string Magic { get; }
        public int Size { get; }
        public int KipCount { get; }

        private IStorage Storage { get; }

        public Ini1(IStorage storage)
        {
            Storage = storage;

            var reader = new BinaryReader(Storage.AsStream());

            Magic = reader.ReadAscii(4);
            if (Magic != "INI1")
            {
                throw new InvalidDataException("Invalid INI1 file!");
            }

            Size = reader.ReadInt32();
            KipCount = reader.ReadInt32();

            Kips = new KipReader[KipCount];
            int offset = 0x10;

            for (int i = 0; i < KipCount; i++)
            {
                Kips[i] = new KipReader();
                Kips[i].Initialize(Storage.Slice(offset)).ThrowIfFailure();

                offset += Kips[i].GetFileSize();
            }
        }
    }
}
