using System;
using System.IO;
using System.Security.Cryptography;

namespace LibHac.IO
{
    class IntegrityVerificationStorage : SectorStorage
    {
        private const int DigestSize = 0x20;

        private Storage HashStorage { get; }
        public IntegrityCheckLevel IntegrityCheckLevel { get; }
        public Validity[] BlockValidities { get; }

        private byte[] Salt { get; }
        private IntegrityStreamType Type { get; }

        private readonly byte[] _hashBuffer = new byte[DigestSize];
        private readonly SHA256 _hash = SHA256.Create();

        public IntegrityVerificationStorage(IntegrityVerificationInfoStorage info, Storage hashStorage, IntegrityCheckLevel integrityCheckLevel)
            : base(info.Data, info.BlockSize, true)
        {
            HashStorage = hashStorage;
            IntegrityCheckLevel = integrityCheckLevel;
            Salt = info.Salt;
            Type = info.Type;

            BlockValidities = new Validity[SectorCount];
        }

        protected override int ReadSpan(Span<byte> destination, long offset)
        {
            IntegrityCheckLevel integrityCheckLevel = IntegrityCheckLevel;
            long blockIndex = offset / SectorSize;

            long hashPos = blockIndex * DigestSize;
            HashStorage.Read(_hashBuffer, hashPos);

            int bytesRead = BaseStorage.Read(destination, offset);
            int bytesToHash = SectorSize;

            if (bytesRead == 0) return 0;

            if (Type == IntegrityStreamType.Save && _hashBuffer.IsEmpty())
            {
                destination.Clear();
                BlockValidities[blockIndex] = Validity.Valid;
                return bytesRead;
            }

            if (bytesRead < SectorSize)
            {
                // Pad out unused portion of block
                destination.Slice(bytesRead).Clear();

                // Partition FS hashes don't pad out an incomplete block
                if (Type == IntegrityStreamType.PartitionFs)
                {
                    bytesToHash = bytesRead;
                }
            }

            if (BlockValidities[blockIndex] == Validity.Invalid && integrityCheckLevel == IntegrityCheckLevel.ErrorOnInvalid)
            {
                throw new InvalidDataException("Hash error!");
            }

            if (integrityCheckLevel == IntegrityCheckLevel.None) return bytesRead;

            if (BlockValidities[blockIndex] != Validity.Unchecked) return bytesRead;

            byte[] hash = DoHash(destination.ToArray(), 0, bytesToHash);

            Validity validity = Util.ArraysEqual(_hashBuffer, hash) ? Validity.Valid : Validity.Invalid;
            BlockValidities[blockIndex] = validity;

            if (validity == Validity.Invalid && integrityCheckLevel == IntegrityCheckLevel.ErrorOnInvalid)
            {
                throw new InvalidDataException("Hash error!");
            }

            return bytesRead;
        }

        private byte[] DoHash(byte[] buffer, int offset, int count)
        {
            _hash.Initialize();

            if (Type == IntegrityStreamType.Save)
            {
                _hash.TransformBlock(Salt, 0, Salt.Length, null, 0);
            }

            _hash.TransformBlock(buffer, offset, buffer.Length, null, 0);

            if (count - buffer.Length > 0)
            {
                var padding = new byte[count - buffer.Length];
                _hash.TransformBlock(padding, 0, padding.Length, null, 0);
            }

            _hash.TransformFinalBlock(buffer, 0, 0);

            byte[] hash = _hash.Hash;

            if (Type == IntegrityStreamType.Save)
            {
                // This bit is set on all save hashes
                hash[0x1F] |= 0x80;
            }

            return hash;
        }
    }

    public class IntegrityVerificationInfoStorage
    {
        public Storage Data { get; set; }
        public int BlockSize { get; set; }
        public byte[] Salt { get; set; }
        public IntegrityStreamType Type { get; set; }
    }
}
