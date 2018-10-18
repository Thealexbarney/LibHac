using System;
using System.IO;
using System.Security.Cryptography;
using LibHac.Streams;

namespace LibHac
{
    public class IntegrityVerificationStream : SectorStream
    {
        private const int DigestSize = 0x20;

        private Stream HashStream { get; }
        public IntegrityCheckLevel IntegrityCheckLevel { get; }
        public Validity[] BlockValidities { get; }

        private byte[] Salt { get; }
        private IntegrityStreamType Type { get; }

        private readonly byte[] _hashBuffer = new byte[DigestSize];
        private readonly SHA256 _hash = SHA256.Create();

        public IntegrityVerificationStream(IntegrityVerificationInfo info, Stream hashStream, IntegrityCheckLevel integrityCheckLevel)
            : base(info.Data, info.BlockSize)
        {
            HashStream = hashStream;
            IntegrityCheckLevel = integrityCheckLevel;
            Salt = info.Salt;
            Type = info.Type;

            BlockValidities = new Validity[SectorCount];
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                case SeekOrigin.End:
                    Position = Length - offset;
                    break;
            }

            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer, offset, count, IntegrityCheckLevel);

        public int Read(byte[] buffer, int offset, int count, IntegrityCheckLevel integrityCheckLevel)
        {
            long blockNum = CurrentSector;
            HashStream.Position = blockNum * DigestSize;
            HashStream.Read(_hashBuffer, 0, DigestSize);

            int bytesRead = base.Read(buffer, offset, count);
            int bytesToHash = SectorSize;

            if (bytesRead == 0) return 0;

            // If a hash is zero the data for the entire block is zero
            if (Type == IntegrityStreamType.Save && _hashBuffer.IsEmpty())
            {
                Array.Clear(buffer, offset, SectorSize);
                BlockValidities[blockNum] = Validity.Valid;
                return bytesRead;
            }

            if (bytesRead < SectorSize)
            {
                // Pad out unused portion of block
                Array.Clear(buffer, offset + bytesRead, SectorSize - bytesRead);

                // Partition FS hashes don't pad out an incomplete block
                if (Type == IntegrityStreamType.PartitionFs)
                {
                    bytesToHash = bytesRead;
                }
            }

            if (BlockValidities[blockNum] == Validity.Invalid && integrityCheckLevel == IntegrityCheckLevel.ErrorOnInvalid)
            {
                throw new InvalidDataException("Hash error!");
            }

            if (integrityCheckLevel == IntegrityCheckLevel.None) return bytesRead;

            if (BlockValidities[blockNum] != Validity.Unchecked) return bytesRead;

            byte[] hash = DoHash(buffer, offset, bytesToHash);

            Validity validity = Util.ArraysEqual(_hashBuffer, hash) ? Validity.Valid : Validity.Invalid;
            BlockValidities[blockNum] = validity;

            if (validity == Validity.Invalid && integrityCheckLevel == IntegrityCheckLevel.ErrorOnInvalid)
            {
                throw new InvalidDataException("Hash error!");
            }

            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            long blockNum = CurrentSector;
            int toWrite = (int)Math.Min(count, Length - Position);
            byte[] hash = DoHash(buffer, offset, toWrite);

            if (Type == IntegrityStreamType.Save && buffer.IsEmpty())
            {
                Array.Clear(hash, 0, DigestSize);
            }

            base.Write(buffer, offset, count);

            HashStream.Position = blockNum * DigestSize;
            HashStream.Write(hash, 0, DigestSize);
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

        public override void Flush()
        {
            HashStream.Flush();
            base.Flush();
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
    }

    /// <summary>
    /// Information for creating an <see cref="IntegrityVerificationStream"/>
    /// </summary>
    public class IntegrityVerificationInfo
    {
        public Stream Data { get; set; }
        public int BlockSize { get; set; }
        public byte[] Salt { get; set; }
        public IntegrityStreamType Type { get; set; }
    }

    public enum IntegrityStreamType
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
