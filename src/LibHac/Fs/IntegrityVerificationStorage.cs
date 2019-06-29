﻿using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using LibHac.Fs.Save;

namespace LibHac.Fs
{
    public class IntegrityVerificationStorage : SectorStorage
    {
        private const int DigestSize = 0x20;

        private IStorage HashStorage { get; }
        public IntegrityCheckLevel IntegrityCheckLevel { get; }
        public Validity[] BlockValidities { get; }

        private byte[] Salt { get; }
        private IntegrityStorageType Type { get; }

        private readonly SHA256 _hash = SHA256.Create();
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

        private void ReadImpl(Span<byte> destination, long offset, IntegrityCheckLevel integrityCheckLevel)
        {
            int count = destination.Length;

            if (count < 0 || count > SectorSize)
                throw new ArgumentOutOfRangeException(nameof(destination), "Length is invalid.");

            long blockIndex = offset / SectorSize;

            if (BlockValidities[blockIndex] == Validity.Invalid && integrityCheckLevel == IntegrityCheckLevel.ErrorOnInvalid)
            {
                // Todo: Differentiate between the top and lower layers
                ThrowHelper.ThrowResult(ResultFs.InvalidHashInIvfc, "Hash error!");
            }

            bool needsHashCheck = integrityCheckLevel != IntegrityCheckLevel.None &&
                                  BlockValidities[blockIndex] == Validity.Unchecked;

            if (Type != IntegrityStorageType.Save && !needsHashCheck)
            {
                BaseStorage.Read(destination, offset);
                return;
            }

            Span<byte> hashBuffer = stackalloc byte[DigestSize];
            long hashPos = blockIndex * DigestSize;
            HashStorage.Read(hashBuffer, hashPos);

            if (Type == IntegrityStorageType.Save)
            {
                if (Util.IsEmpty(hashBuffer))
                {
                    destination.Clear();
                    BlockValidities[blockIndex] = Validity.Valid;
                    return;
                }

                if (!needsHashCheck)
                {
                    BaseStorage.Read(destination, offset);
                    return;
                }
            }

            byte[] dataBuffer = ArrayPool<byte>.Shared.Rent(SectorSize);
            try
            {
                BaseStorage.Read(destination, offset);
                destination.CopyTo(dataBuffer);

                if (BlockValidities[blockIndex] != Validity.Unchecked) return;

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
                    ThrowHelper.ThrowResult(ResultFs.InvalidHashInIvfc, "Hash error!");
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(dataBuffer);
            }
        }

        protected override void ReadImpl(Span<byte> destination, long offset)
        {
            ReadImpl(destination, offset, IntegrityCheckLevel);
        }

        public void Read(Span<byte> destination, long offset, IntegrityCheckLevel integrityCheckLevel)
        {
            ValidateParameters(destination, offset);
            ReadImpl(destination, offset, integrityCheckLevel);
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            long blockIndex = offset / SectorSize;
            long hashPos = blockIndex * DigestSize;

            int toWrite = (int)Math.Min(source.Length, GetSize() - offset);

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
                    hash[0x1F] |= 0b10000000;
                }

                return hash;
            }
        }

        public override void Flush()
        {
            HashStorage.Flush();
            base.Flush();
        }

        public void FsTrim()
        {
            if (Type != IntegrityStorageType.Save) return;

            Span<byte> digest = stackalloc byte[DigestSize];

            for (int i = 0; i < SectorCount; i++)
            {
                long hashPos = i * DigestSize;
                HashStorage.Read(digest, hashPos);

                if (!Util.IsEmpty(digest)) continue;

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
