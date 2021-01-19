using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using LibHac.Fs;
using LibHac.Fs.Fsa;

using Aes = LibHac.Crypto.Aes;

namespace LibHac.FsSystem
{
    public class AesXtsFileHeader
    {
        internal const uint AesXtsFileMagic = 0x3058414E;
        public byte[] Signature { get; set; } = new byte[0x20];
        public uint Magic { get; }
        public byte[] EncryptedKey1 { get; } = new byte[0x10];
        public byte[] EncryptedKey2 { get; } = new byte[0x10];
        public long Size { get; private set; }

        public byte[] DecryptedKey1 { get; } = new byte[0x10];
        public byte[] DecryptedKey2 { get; } = new byte[0x10];
        public byte[] Kek1 { get; } = new byte[0x10];
        public byte[] Kek2 { get; } = new byte[0x10];

        public AesXtsFileHeader(IFile aesXtsFile)
        {
            aesXtsFile.GetSize(out long fileSize).ThrowIfFailure();

            if (fileSize < 0x80)
            {
                ThrowHelper.ThrowResult(ResultFs.AesXtsFileHeaderTooShort.Value);
            }

            var reader = new FileReader(aesXtsFile);

            reader.ReadBytes(Signature);
            Magic = reader.ReadUInt32();
            reader.Position += 4;
            reader.ReadBytes(EncryptedKey1);
            reader.ReadBytes(EncryptedKey2);
            Size = reader.ReadInt64();

            if (Magic != AesXtsFileMagic)
            {
                ThrowHelper.ThrowResult(ResultFs.AesXtsFileHeaderInvalidMagic.Value, "Invalid NAX0 magic value");
            }
        }

        public AesXtsFileHeader(byte[] key, long fileSize, string path, byte[] kekSeed, byte[] verificationKey)
        {
            Array.Copy(key, 0, DecryptedKey1, 0, 0x10);
            Array.Copy(key, 0x10, DecryptedKey2, 0, 0x10);
            Magic = AesXtsFileMagic;
            Size = fileSize;

            EncryptHeader(path, kekSeed, verificationKey);
        }

        public void EncryptHeader(string path, byte[] kekSeed, byte[] verificationKey)
        {
            GenerateKek(kekSeed, path);
            EncryptKeys();
            Signature = CalculateHmac(verificationKey);
        }

        public bool TryDecryptHeader(string path, byte[] kekSeed, byte[] verificationKey)
        {
            GenerateKek(kekSeed, path);
            DecryptKeys();

            byte[] hmac = CalculateHmac(verificationKey);
            return Utilities.ArraysEqual(hmac, Signature);
        }

        public void SetSize(long size, byte[] verificationKey)
        {
            Size = size;
            Signature = CalculateHmac(verificationKey);
        }

        private void DecryptKeys()
        {
            Aes.DecryptEcb128(EncryptedKey1, DecryptedKey1, Kek1);
            Aes.DecryptEcb128(EncryptedKey2, DecryptedKey2, Kek2);
        }

        private void EncryptKeys()
        {
            Aes.EncryptEcb128(DecryptedKey1, EncryptedKey1, Kek1);
            Aes.EncryptEcb128(DecryptedKey2, EncryptedKey2, Kek2);
        }

        private void GenerateKek(byte[] kekSeed, string path)
        {
            var hash = new HMACSHA256(kekSeed);
            byte[] pathBytes = Encoding.UTF8.GetBytes(path);

            byte[] checksum = hash.ComputeHash(pathBytes, 0, pathBytes.Length);
            Array.Copy(checksum, 0, Kek1, 0, 0x10);
            Array.Copy(checksum, 0x10, Kek2, 0, 0x10);
        }

        private byte[] CalculateHmac(byte[] key)
        {
            byte[] message = ToBytes(true).AsSpan(0x20).ToArray();
            var hash = new HMACSHA256(message);

            return hash.ComputeHash(key);
        }

        public byte[] ToBytes(bool writeDecryptedKey)
        {
            uint magic = Magic;
            long size = Size;
            byte[] key1 = writeDecryptedKey ? DecryptedKey1 : EncryptedKey1;
            byte[] key2 = writeDecryptedKey ? DecryptedKey2 : EncryptedKey2;

            byte[] data = new byte[0x80];

            Array.Copy(Signature, data, 0x20);
            MemoryMarshal.Write(data.AsSpan(0x20), ref magic);

            Array.Copy(key1, 0, data, 0x28, 0x10);
            Array.Copy(key2, 0, data, 0x38, 0x10);

            MemoryMarshal.Write(data.AsSpan(0x48), ref size);

            return data;
        }
    }
}
