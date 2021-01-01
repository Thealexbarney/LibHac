using System;
using System.Buffers;
using System.IO;
using LibHac.Crypto;
using LibHac.Fs;
using LibHac.FsSystem.Save;

namespace LibHac.FsSystem
{
    public class IntegrityVerificationStorage : SectorStorage
    {
        private const int DigestSize = 0x20;

        private IStorage HashStorage { get; }
        public IntegrityCheckLevel IntegrityCheckLevel { get; }
        public Validity[] BlockValidities { get; }

        private byte[] Salt { get; }
        private IntegrityStorageType Type { get; }

        private readonly IHash _hash = Sha256.CreateSha256Generator();
        private readonly object _locker = new object();

        public IntegrityVerificationStorage(IntegrityVerificationInfo info, IStorage hashStorage,
            IntegrityCheckLevel integrityCheckLevel, bool leaveOpen)
            : base(info.Data, info.BlockSize, leaveOpen)
        {
            HashStorage = hashStorage;
            IntegrityCheckLevel = integrityCheckLevel;
            Salt = info.Salt;
            Type = info.Type;

            BlockValidities = new Validity[SectorCount];
        }

        private Result ReadImpl(long offset, Span<byte> destination, IntegrityCheckLevel integrityCheckLevel)
        {
            int count = destination.Length;

            if (count < 0 || count > SectorSize)
                throw new ArgumentOutOfRangeException(nameof(destination), "Length is invalid.");

            long blockIndex = offset / SectorSize;

            if (BlockValidities[blockIndex] == Validity.Invalid && integrityCheckLevel == IntegrityCheckLevel.ErrorOnInvalid)
            {
                // Todo: Differentiate between the top and lower layers
                ThrowHelper.ThrowResult(ResultFs.NonRealDataVerificationFailed.Value, "Hash error!");
            }

            bool needsHashCheck = integrityCheckLevel != IntegrityCheckLevel.None &&
                                  BlockValidities[blockIndex] == Validity.Unchecked;

            if (Type != IntegrityStorageType.Save && !needsHashCheck)
            {
                BaseStorage.Read(offset, destination);
                return Result.Success;
            }

            Span<byte> hashBuffer = stackalloc byte[DigestSize];
            long hashPos = blockIndex * DigestSize;
            HashStorage.Read(hashPos, hashBuffer);

            if (Type == IntegrityStorageType.Save)
            {
                if (Utilities.IsZeros(hashBuffer))
                {
                    destination.Clear();
                    BlockValidities[blockIndex] = Validity.Valid;
                    return Result.Success;
                }

                if (!needsHashCheck)
                {
                    BaseStorage.Read(offset, destination);
                    return Result.Success;
                }
            }

            byte[] dataBuffer = ArrayPool<byte>.Shared.Rent(SectorSize);
            try
            {
                BaseStorage.Read(offset, destination);
                destination.CopyTo(dataBuffer);

                if (BlockValidities[blockIndex] != Validity.Unchecked) return Result.Success;

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

                Validity validity = Utilities.SpansEqual(hashBuffer, hash) ? Validity.Valid : Validity.Invalid;
                BlockValidities[blockIndex] = validity;

                if (validity == Validity.Invalid && integrityCheckLevel == IntegrityCheckLevel.ErrorOnInvalid)
                {
                    ThrowHelper.ThrowResult(ResultFs.NonRealDataVerificationFailed.Value, "Hash error!");
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(dataBuffer);
            }

            return Result.Success;
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            return ReadImpl(offset, destination, IntegrityCheckLevel);
        }

        public Result Read(long offset, Span<byte> destination, IntegrityCheckLevel integrityCheckLevel)
        {
            // ValidateParameters(destination, offset);
            return ReadImpl(offset, destination, integrityCheckLevel);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            long blockIndex = offset / SectorSize;
            long hashPos = blockIndex * DigestSize;

            Result rc = GetSize(out long storageSize);
            if (rc.IsFailure()) return rc;

            int toWrite = (int)Math.Min(source.Length, storageSize - offset);

            byte[] dataBuffer = ArrayPool<byte>.Shared.Rent(SectorSize);
            try
            {
                source.CopyTo(dataBuffer);
                byte[] hash = DoHash(dataBuffer, 0, toWrite);

                if (Type == IntegrityStorageType.Save && source.IsZeros())
                {
                    Array.Clear(hash, 0, DigestSize);
                }

                BaseStorage.Write(offset, source);

                HashStorage.Write(hashPos, hash);
                BlockValidities[blockIndex] = Validity.Unchecked;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(dataBuffer);
            }

            return Result.Success;
        }

        private byte[] DoHash(byte[] buffer, int offset, int count)
        {
            lock (_locker)
            {
                _hash.Initialize();

                if (Type == IntegrityStorageType.Save)
                {
                    _hash.Update(Salt);
                }

                _hash.Update(buffer.AsSpan(offset, count));

                byte[] hash = new byte[Sha256.DigestSize];
                _hash.GetHash(hash);

                if (Type == IntegrityStorageType.Save)
                {
                    // This bit is set on all save hashes
                    hash[0x1F] |= 0b10000000;
                }

                return hash;
            }
        }

        protected override Result DoFlush()
        {
            Result rc = HashStorage.Flush();
            if (rc.IsFailure()) return rc;

            return base.DoFlush();
        }

        public void FsTrim()
        {
            if (Type != IntegrityStorageType.Save) return;

            Span<byte> digest = stackalloc byte[DigestSize];

            for (int i = 0; i < SectorCount; i++)
            {
                long hashPos = i * DigestSize;
                HashStorage.Read(hashPos, digest).ThrowIfFailure();

                if (!Utilities.IsZeros(digest)) continue;

                int dataOffset = i * SectorSize;
                BaseStorage.Fill(SaveDataFileSystem.TrimFillValue, dataOffset, SectorSize);
            }
        }
    }

    /// <summary>
    /// Information for creating an <see cref="IntegrityVerificationStorage"/>
    /// </summary>
    public class IntegrityVerificationInfo
    {
        public IStorage Data { get; set; }
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
