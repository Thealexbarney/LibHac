using System;
using System.IO;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.FsSystem;

namespace LibHac
{
    [Obsolete("This class has been deprecated. LibHac.Boot.Package2StorageReader should be used instead.")]
    public class Package2
    {
        private const uint Pk21Magic = 0x31324B50; // PK21

        public Package2Header Header { get; }
        public int KeyRevision { get; }
        public byte[] Key { get; }
        public int PackageSize { get; }
        public int HeaderVersion { get; }

        private IStorage Storage { get; }

        public Package2(KeySet keySet, IStorage storage)
        {
            Storage = storage;
            IStorage headerStorage = Storage.Slice(0, 0x200);

            KeyRevision = FindKeyGeneration(keySet, headerStorage);
            Key = keySet.Package2Keys[KeyRevision].DataRo.ToArray();

            Header = new Package2Header(headerStorage, keySet, KeyRevision);

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
            byte[] counter = new byte[0x10];
            Array.Copy(Header.Counter, counter, 0x10);
            Utilities.IncrementByteArray(counter);

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
            // Handle 8.0.0+ INI1 embedded within Kernel
            // Todo: Figure out how to better deal with this once newer versions are released
            if (Header.SectionSizes[1] == 0)
            {
                IStorage kernelStorage = OpenKernel();

                var reader = new BinaryReader(kernelStorage.AsStream());
                reader.BaseStream.Position = 0x168;

                int embeddedIniOffset = (int)reader.ReadInt64();

                reader.BaseStream.Position = embeddedIniOffset + 4;
                int size = reader.ReadInt32();

                return kernelStorage.Slice(embeddedIniOffset, size);
            }

            int offset = 0x200 + Header.SectionSizes[0];
            IStorage encStorage = Storage.Slice(offset, Header.SectionSizes[1]);

            return new CachedStorage(new Aes128CtrStorage(encStorage, Key, Header.SectionCounters[1], true), 0x4000, 4, true);
        }

        private int FindKeyGeneration(KeySet keySet, IStorage storage)
        {
            byte[] counter = new byte[0x10];
            byte[] decBuffer = new byte[0x10];

            storage.Read(0x100, counter).ThrowIfFailure();

            for (int i = 0; i < 0x20; i++)
            {
                var dec = new Aes128CtrStorage(storage.Slice(0x100), keySet.Package2Keys[i].DataRo.ToArray(), counter,
                    false);
                dec.Read(0x50, decBuffer).ThrowIfFailure();

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

        public Package2Header(IStorage storage, KeySet keySet, int keyGeneration)
        {
            var reader = new BinaryReader(storage.AsStream());
            byte[] key = keySet.Package2Keys[keyGeneration].DataRo.ToArray();

            Signature = reader.ReadBytes(0x100);
            byte[] sigData = reader.ReadBytes(0x100);
            SignatureValidity = CryptoOld.Rsa2048PssVerify(sigData, Signature, keySet.Package2SigningKeyParams.Modulus);

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
