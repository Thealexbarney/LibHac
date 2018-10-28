using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;

namespace LibHac.IO
{
    public class IntegrityVerificationStorage : SectorStorage
    {
        private const int DigestSize = 0x20;

        private Storage HashStorage { get; }
        public IntegrityCheckLevel IntegrityCheckLevel { get; }
        public Validity[] BlockValidities { get; }

        private byte[] Salt { get; }
        private IntegrityStreamType Type { get; }

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

        private int ReadSpan(Span<byte> destination, long offset, IntegrityCheckLevel integrityCheckLevel)
        {
            int count = destination.Length;

            if (count < 0 || count > SectorSize)
                throw new ArgumentOutOfRangeException(nameof(destination), "Length is invalid.");

            Span<byte> hashBuffer = stackalloc byte[DigestSize];
            long blockIndex = offset / SectorSize;
            long hashPos = blockIndex * DigestSize;

            if (BlockValidities[blockIndex] == Validity.Invalid && integrityCheckLevel == IntegrityCheckLevel.ErrorOnInvalid)
            {
                throw new InvalidDataException("Hash error!");
            }

            HashStorage.Read(hashBuffer, hashPos);

            if (Type == IntegrityStreamType.Save && hashBuffer.IsEmpty())
            {
                destination.Clear();
                BlockValidities[blockIndex] = Validity.Valid;
                return 0;
            }

            byte[] dataBuffer = ArrayPool<byte>.Shared.Rent(SectorSize);
            try
            {
                BaseStorage.Read(dataBuffer, offset, count, 0);
                dataBuffer.AsSpan(0, count).CopyTo(destination);

                if (integrityCheckLevel == IntegrityCheckLevel.None) return 0;
                if (BlockValidities[blockIndex] != Validity.Unchecked) return 0;

                int bytesToHash = SectorSize;

                if (count < SectorSize)
                {
                    // Pad out unused portion of block
                    Array.Clear(dataBuffer, count, SectorSize - count);

                    // Partition FS hashes don't pad out an incomplete block
                    if (Type == IntegrityStreamType.PartitionFs)
                    {
                        bytesToHash = count;
                    }
                }

                byte[] hash = DoHash(dataBuffer, 0, bytesToHash);

                Validity validity = Util.SpansEqual(hashBuffer, hash) ? Validity.Valid : Validity.Invalid;
                BlockValidities[blockIndex] = validity;

                if (validity == Validity.Invalid && integrityCheckLevel == IntegrityCheckLevel.ErrorOnInvalid)
                {
                    throw new InvalidDataException("Hash error!");
                }

                return 0;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(dataBuffer);
            }
        }

        protected override int ReadSpan(Span<byte> destination, long offset)
        {
            return ReadSpan(destination, offset, IntegrityCheckLevel);
        }

        public void Read(Span<byte> destination, long offset, IntegrityCheckLevel integrityCheckLevel)
        {
            ValidateSpanParameters(destination, offset);
            ReadSpan(destination, offset, integrityCheckLevel);
        }

        public void Read(byte[] buffer, long offset, int count, int bufferOffset, IntegrityCheckLevel integrityCheckLevel)
        {
            ValidateArrayParameters(buffer, offset, count, bufferOffset);
            ReadSpan(buffer.AsSpan(bufferOffset, count), offset, integrityCheckLevel);
        }

        protected override void WriteSpan(ReadOnlySpan<byte> source, long offset)
        {
            //long blockNum = CurrentSector;
            //int toWrite = (int)Math.Min(count, Length - Position);
            //byte[] hash = DoHash(buffer, offset, toWrite);

            //if (Type == IntegrityStreamType.Save && buffer.IsEmpty())
            //{
            //    Array.Clear(hash, 0, DigestSize);
            //}

            //base.Write(buffer, offset, count);

            //HashStream.Position = blockNum * DigestSize;
            //HashStream.Write(hash, 0, DigestSize);
        }

        private byte[] DoHash(byte[] buffer, int offset, int count)
        {
            _hash.Initialize();

            if (Type == IntegrityStreamType.Save)
            {
                _hash.TransformBlock(Salt, 0, Salt.Length, null, 0);
            }

            _hash.TransformBlock(buffer, offset, count, null, 0);
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

    /// <summary>
    /// Information for creating an <see cref="IntegrityVerificationStorage"/>
    /// </summary>
    public class IntegrityVerificationInfoStorage
    {
        public Storage Data { get; set; }
        public int BlockSize { get; set; }
        public byte[] Salt { get; set; }
        public IntegrityStreamType Type { get; set; }
    }
}
