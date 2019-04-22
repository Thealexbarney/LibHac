using System;
using System.IO;
using LibHac.IO;

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

        private IStorage Storage { get; }

        public Package2(Keyset keyset, IStorage storage)
        {
            Storage = storage;
            IStorage headerStorage = Storage.Slice(0, 0x200);

            KeyRevision = FindKeyGeneration(keyset, headerStorage);
            Key = keyset.Package2Keys[KeyRevision];

            Header = new Package2Header(headerStorage, keyset, KeyRevision);

            PackageSize = BitConverter.ToInt32(Header.Counter, 0) ^ BitConverter.ToInt32(Header.Counter, 8) ^
                          BitConverter.ToInt32(Header.Counter, 12);

            HeaderVersion = Header.Counter[4] ^ Header.Counter[6] ^ Header.Counter[7];

            if (PackageSize != 0x200 + Header.SectionSizes[0] + Header.SectionSizes[1] + Header.SectionSizes[2])
            {
                throw new InvalidDataException("Package2 Header is corrupt!");
            }
        }

        public IStorage OpenDecryptedPackage()
        {
            if (Header.SectionSizes[1] == 0)
            {
                IStorage[] storages = { OpenHeaderPart1(), OpenHeaderPart2(), OpenKernel() };

                return new ConcatenationStorage(storages, true);
            }
            else
            {
                IStorage[] storages = { OpenHeaderPart1(), OpenHeaderPart2(), OpenKernel(), OpenIni1() };

                return new ConcatenationStorage(storages, true);
            }
        }

        private IStorage OpenHeaderPart1()
        {
            return Storage.Slice(0, 0x110);
        }

        private IStorage OpenHeaderPart2()
        {
            IStorage encStorage = Storage.Slice(0x110, 0xF0);

            // The counter starts counting at 0x100, but the block at 0x100 isn't encrypted.
            // Increase the counter by one and start decrypting at 0x110.
            var counter = new byte[0x10];
            Array.Copy(Header.Counter, counter, 0x10);
            Util.IncrementByteArray(counter);

            return new CachedStorage(new Aes128CtrStorage(encStorage, Key, counter, true), 0x4000, 4, true);
        }

        public IStorage OpenKernel()
        {
            int offset = 0x200;
            IStorage encStorage = Storage.Slice(offset, Header.SectionSizes[0]);

            return new CachedStorage(new Aes128CtrStorage(encStorage, Key, Header.SectionCounters[0], true), 0x4000, 4, true);
        }

        public IStorage OpenIni1()
        {
            int offset;

            // Handle 8.0.0+ INI1 embedded within Kernel
            if (Header.SectionSizes[1] == 0)
            {
                IStorage kernelStorage = OpenKernel();

                var reader = new BinaryReader(kernelStorage.AsStream());

                reader.BaseStream.Position = 0x168;
                offset = reader.ReadInt32();

                reader.BaseStream.Position = offset + 4;
                int size = reader.ReadInt32();

                IStorage ini1Storage = kernelStorage.Slice(offset, size);

                return new CachedStorage(ini1Storage, 0x4000, 4, true);
            }

            offset = 0x200 + Header.SectionSizes[0];
            IStorage encStorage = Storage.Slice(offset, Header.SectionSizes[1]);

            return new CachedStorage(new Aes128CtrStorage(encStorage, Key, Header.SectionCounters[1], true), 0x4000, 4, true);
        }

        private int FindKeyGeneration(Keyset keyset, IStorage storage)
        {
            var counter = new byte[0x10];
            var decBuffer = new byte[0x10];

            storage.Read(counter, 0x100);

            for (int i = 0; i < 0x20; i++)
            {
                var dec = new Aes128CtrStorage(storage.Slice(0x100), keyset.Package2Keys[i], counter, false);
                dec.Read(decBuffer, 0x50);

                if (BitConverter.ToUInt32(decBuffer, 0) == Pk21Magic)
                {
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

        public Validity SignatureValidity { get; }

        public Package2Header(IStorage storage, Keyset keyset, int keyGeneration)
        {
            var reader = new BinaryReader(storage.AsStream());
            byte[] key = keyset.Package2Keys[keyGeneration];

            Signature = reader.ReadBytes(0x100);
            byte[] sigData = reader.ReadBytes(0x100);
            SignatureValidity = Crypto.Rsa2048PssVerify(sigData, Signature, keyset.Package2FixedKeyModulus);

            reader.BaseStream.Position -= 0x100;
            Counter = reader.ReadBytes(0x10);

            Stream headerStream = new CachedStorage(new Aes128CtrStorage(storage.Slice(0x100), key, Counter, true), 0x4000, 4, true).AsStream();

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
