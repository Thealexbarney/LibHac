using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LibHac.IO
{
    public class AesXtsFile : FileBase
    {
        private IFile BaseFile { get; }
        private string Path { get; }
        private byte[] KekSeed { get; }
        private byte[] VerificationKey { get; }
        private int BlockSize { get; }

        public byte[] Hmac { get; private set; }
        public byte[][] EncKeys { get; } = Util.CreateJaggedArray<byte[][]>(2, 0x10);
        public byte[][] Keys { get; } = Util.CreateJaggedArray<byte[][]>(2, 0x10);
        public byte[] Key { get; } = new byte[0x20];
        public long Length { get; private set; }
        private IStorage BaseStorage { get; }

        private const int HeaderLength = 0x4000;

        public AesXtsFile(OpenMode mode, IFile baseFile, string path, ReadOnlySpan<byte> kekSeed, ReadOnlySpan<byte> verificationKey, int blockSize)
        {
            Mode = mode;
            BaseFile = baseFile;
            Path = path;
            KekSeed = kekSeed.ToArray();
            VerificationKey = verificationKey.ToArray();
            BlockSize = blockSize;

            ReadHeader(BaseFile);

            DeriveKeys();
            Storage encStorage = new FileStorage(BaseFile).Slice(HeaderLength, Length);
            BaseStorage = new CachedStorage(new Aes128XtsStorage(encStorage, Key, BlockSize, true), 4, true);
        }

        private void ReadHeader(IFile file)
        {
            var reader = new BinaryReader(file.AsStream());

            Hmac = reader.ReadBytes(0x20);
            string magic = reader.ReadAscii(4);
            reader.BaseStream.Position += 4;
            if (magic != "NAX0") throw new InvalidDataException("Not an NAX0 file");
            EncKeys[0] = reader.ReadBytes(0x10);
            EncKeys[1] = reader.ReadBytes(0x10);
            Length = reader.ReadInt64();
        }

        private void DeriveKeys()
        {
            var validationHashKey = new byte[0x60];
            BaseFile.Read(validationHashKey, 0x20);

            var naxSpecificKeys = Util.CreateJaggedArray<byte[][]>(2, 0x10);

            // Use the sd path to generate the kek for this NAX0
            var hash = new HMACSHA256(KekSeed);
            byte[] sdPathBytes = Encoding.ASCII.GetBytes(Path);
            byte[] checksum = hash.ComputeHash(sdPathBytes, 0, sdPathBytes.Length);
            Array.Copy(checksum, 0, naxSpecificKeys[0], 0, 0x10);
            Array.Copy(checksum, 0x10, naxSpecificKeys[1], 0, 0x10);

            // Decrypt this NAX0's keys
            Crypto.DecryptEcb(naxSpecificKeys[0], EncKeys[0], Keys[0], 0x10);
            Crypto.DecryptEcb(naxSpecificKeys[1], EncKeys[1], Keys[1], 0x10);
            Array.Copy(Keys[0], 0, Key, 0, 0x10);
            Array.Copy(Keys[1], 0, Key, 0x10, 0x10);

            // Copy the decrypted keys into the NAX0 header and use that for the HMAC key
            // for validating that the keys are correct
            Array.Copy(Keys[0], 0, validationHashKey, 8, 0x10);
            Array.Copy(Keys[1], 0, validationHashKey, 0x18, 0x10);

            var validationHash = new HMACSHA256(validationHashKey);
            byte[] validationMac = validationHash.ComputeHash(VerificationKey);

            if (Util.ArraysEqual(Hmac, validationMac))
            {
                return;
            }

            throw new ArgumentException("NAX0 key derivation failed.");
        }

        public override int Read(Span<byte> destination, long offset)
        {
            int toRead = ValidateReadParamsAndGetSize(destination, offset);

            BaseStorage.Read(destination.Slice(0, toRead), offset);

            return toRead;
        }

        public override void Write(ReadOnlySpan<byte> source, long offset)
        {
            ValidateWriteParams(source, offset);

            BaseStorage.Write(source, offset);
        }

        public override void Flush()
        {
            BaseStorage.Flush();
        }

        public override long GetSize()
        {
            return Length;
        }

        public override void SetSize(long size)
        {
            throw new NotImplementedException();
        }
    }
}
