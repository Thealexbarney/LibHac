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
        private IntegrityStorageType Type { get; }

        private readonly SHA256 _hash = SHA256.Create();
        private readonly object _locker = new object();

        public IntegrityVerificationStorage(IntegrityVerificationInfo info, Storage hashStorage,
            IntegrityCheckLevel integrityCheckLevel, bool leaveOpen)
            : base(info.Data, info.BlockSize, leaveOpen)
        {
            HashStorage = hashStorage;
            IntegrityCheckLevel = integrityCheckLevel;
            Salt = info.Salt;
            Type = info.Type;

            BlockValidities = new Validity[SectorCount];
        }

        private int ReadImpl(Span<byte> destination, long offset, IntegrityCheckLevel integrityCheckLevel)
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

            if (Type == IntegrityStorageType.Save && Util.IsEmpty(hashBuffer))
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
                    if (Type == IntegrityStorageType.PartitionFs)
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

        protected override int ReadImpl(Span<byte> destination, long offset)
        {
            return ReadImpl(destination, offset, IntegrityCheckLevel);
        }

        public void Read(Span<byte> destination, long offset, IntegrityCheckLevel integrityCheckLevel)
        {
            ValidateSpanParameters(destination, offset);
            ReadImpl(destination, offset, integrityCheckLevel);
        }

        public void Read(byte[] buffer, long offset, int count, int bufferOffset, IntegrityCheckLevel integrityCheckLevel)
        {
            ValidateArrayParameters(buffer, offset, count, bufferOffset);
            ReadImpl(buffer.AsSpan(bufferOffset, count), offset, integrityCheckLevel);
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            long blockIndex = offset / SectorSize;
            long hashPos = blockIndex * DigestSize;

            int toWrite = (int)Math.Min(source.Length, Length - offset);

            byte[] dataBuffer = ArrayPool<byte>.Shared.Rent(SectorSize);
            try
            {
                source.CopyTo(dataBuffer);
                byte[] hash = DoHash(dataBuffer, 0, toWrite);

                if (Type == IntegrityStorageType.Save && source.IsEmpty())
                {
                    Array.Clear(hash, 0, DigestSize);
                }

                BaseStorage.Write(source, offset);

                HashStorage.Write(hash, hashPos);
                BlockValidities[blockIndex] = Validity.Unchecked;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(dataBuffer);
            }
        }

        private byte[] DoHash(byte[] buffer, int offset, int count)
        {
            lock (_locker)
            {
                _hash.Initialize();

                if (Type == IntegrityStorageType.Save)
                {
                    _hash.TransformBlock(Salt, 0, Salt.Length, null, 0);
                }

                _hash.TransformBlock(buffer, offset, count, null, 0);
                _hash.TransformFinalBlock(buffer, 0, 0);

                byte[] hash = _hash.Hash;

                if (Type == IntegrityStorageType.Save)
                {
                    // This bit is set on all save hashes
                    hash[0x1F] |= 0x80;
                }

                return hash;
            }
        }

        public override void Flush()
        {
            HashStorage.Flush();
            base.Flush();
        }
    }

    /// <summary>
    /// Information for creating an <see cref="IntegrityVerificationStorage"/>
    /// </summary>
    public class IntegrityVerificationInfo
    {
        public Storage Data { get; set; }
        public int BlockSize { get; set; }
        public byte[] Salt { get; set; }
        public IntegrityStorageType Type { get; set; }
    }

    public enum IntegrityStorageType
    {
        Save,
        RomFs,
        PartitionFs
    }

    /// <summary>
    /// Represents the level of integrity checks to be performed.
    /// </summary>
    public enum IntegrityCheckLevel
    {
        /// <summary>
        /// No integrity checks will be performed.
        /// </summary>
        None,
        /// <summary>
        /// Invalid blocks will be marked as invalid when read, and will not cause an error.
        /// </summary>
        IgnoreOnInvalid,
        /// <summary>
        /// An <see cref="InvalidDataException"/> will be thrown if an integrity check fails.
        /// </summary>
        ErrorOnInvalid
    }
}
