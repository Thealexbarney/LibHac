using System;
using System.IO;
using LibHac.IO;
using LibHac.Streams;

namespace LibHac
{
    public class Package2
    {
        private const uint Pk21Magic = 0x31324B50; // PK21

        public Package2Header Header { get; }
        public int KeyRevision { get; }
        public byte[] Key { get; }
        public int PackageSize { get; }
        public int HeaderVersion { get; }

        private SharedStreamSource StreamSource { get; }

        public Package2(Keyset keyset, Stream stream)
        {
            StreamSource = new SharedStreamSource(stream);
            SharedStream headerStream = StreamSource.CreateStream(0, 0x200);

            KeyRevision = FindKeyGeneration(keyset, headerStream);
            Key = keyset.Package2Keys[KeyRevision];

            Header = new Package2Header(headerStream, Key);

            PackageSize = BitConverter.ToInt32(Header.Counter, 0) ^ BitConverter.ToInt32(Header.Counter, 8) ^
                          BitConverter.ToInt32(Header.Counter, 12);

            HeaderVersion = Header.Counter[4] ^ Header.Counter[6] ^ Header.Counter[7];

            if (PackageSize != 0x200 + Header.SectionSizes[0] + Header.SectionSizes[1] + Header.SectionSizes[2])
            {
                throw new InvalidDataException("Package2 Header is corrupt!");
            }
        }

        public Stream OpenHeaderPart1()
        {
            return StreamSource.CreateStream(0, 0x110);
        }

        public Stream OpenHeaderPart2()
        {
            SharedStream encStream = StreamSource.CreateStream(0x110, 0xF0);

            // The counter starts counting at 0x100, but the block at 0x100 isn't encrypted.
            // Increase the counter by one and start decrypting at 0x110.
            var counter = new byte[0x10];
            Array.Copy(Header.Counter, counter, 0x10);
            Util.IncrementByteArray(counter);

            return new CachedStorage(new Aes128CtrStorage(encStream.AsStorage(), Key, counter, true), 4, true).AsStream();
        }

        public Stream OpenKernel()
        {
            int offset = 0x200;
            SharedStream encStream = StreamSource.CreateStream(offset, Header.SectionSizes[0]);

            return new CachedStorage(new Aes128CtrStorage(encStream.AsStorage(), Key, Header.SectionCounters[0], true), 4, true).AsStream();
        }

        public Stream OpenIni1()
        {
            int offset = 0x200 + Header.SectionSizes[0];
            SharedStream encStream = StreamSource.CreateStream(offset, Header.SectionSizes[1]);

            return new CachedStorage(new Aes128CtrStorage(encStream.AsStorage(), Key, Header.SectionCounters[1], true), 4, true).AsStream();
        }

        private int FindKeyGeneration(Keyset keyset, Stream stream)
        {
            var counter = new byte[0x10];
            var decBuffer = new byte[0x10];

            stream.Position = 0x100;
            stream.Read(counter, 0, 0x10);

            for (int i = 0; i < 0x20; i++)
            {
                var dec = new Aes128CtrStorage(stream.AsStorage(0x100), keyset.Package2Keys[i], counter, false);
                dec.Read(decBuffer, 0x50);

                if (BitConverter.ToUInt32(decBuffer, 0) == Pk21Magic)
                {
                    stream.Position = 0;
                    return i;
                }
            }

            throw new InvalidDataException("Failed to decrypt package2! Is the correct key present?");
        }
    }

    public class Package2Header
    {
        public byte[] Signature { get; }
        public byte[] Counter { get; }

        public byte[][] SectionCounters { get; } = new byte[4][];
        public int[] SectionSizes { get; } = new int[4];
        public int[] SectionOffsets { get; } = new int[4];
        public byte[][] SectionHashes { get; } = new byte[4][];

        public string Magic { get; }
        public int BaseOffset { get; }
        public int VersionMax { get; }
        public int VersionMin { get; }

        public Package2Header(Stream stream, byte[] key)
        {
            var reader = new BinaryReader(stream);

            Signature = reader.ReadBytes(0x100);
            Counter = reader.ReadBytes(0x10);

            var headerStream = new CachedStorage(new Aes128CtrStorage(stream.AsStorage(0x100), key, Counter, true), 4, true).AsStream();

            headerStream.Position = 0x10;
            reader = new BinaryReader(headerStream);

            for (int i = 0; i < 4; i++)
            {
                SectionCounters[i] = reader.ReadBytes(0x10);
            }

            Magic = reader.ReadAscii(4);
            BaseOffset = reader.ReadInt32();

            reader.BaseStream.Position += 4;
            VersionMax = reader.ReadByte();
            VersionMin = reader.ReadByte();

            reader.BaseStream.Position += 2;

            for (int i = 0; i < 4; i++)
            {
                SectionSizes[i] = reader.ReadInt32();
            }

            for (int i = 0; i < 4; i++)
            {
                SectionOffsets[i] = reader.ReadInt32();
            }

            for (int i = 0; i < 4; i++)
            {
                SectionHashes[i] = reader.ReadBytes(0x20);
            }
        }
    }
}
