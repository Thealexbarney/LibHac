using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LibHac.Streams;

namespace LibHac
{
    public class IntegrityVerificationStream : SectorStream
    {
        private const int DigestSize = 0x20;

        private Stream HashStream { get; }
        public bool EnableIntegrityChecks { get; }

        private byte[] Salt { get; }
        private IntegrityStreamType Type { get; }

        private readonly byte[] _hashBuffer = new byte[DigestSize];
        private readonly SHA256 _hash = SHA256.Create();

        public IntegrityVerificationStream(IntegrityVerificationInfo info, Stream hashStream, bool enableIntegrityChecks)
            : base(info.Data, 1 << info.BlockSizePower)
        {
            HashStream = hashStream;
            EnableIntegrityChecks = enableIntegrityChecks;
            Salt = info.Salt;
            Type = info.Type;
        }

        public override void Flush()
        {
            throw new NotImplementedException();
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

        public override int Read(byte[] buffer, int offset, int count)
        {
            HashStream.Position = CurrentSector * DigestSize;
            HashStream.Read(_hashBuffer, 0, DigestSize);

            int bytesRead = base.Read(buffer, 0, count);

            // If a hash is zero the data for the entire block is zero
            if (Type == IntegrityStreamType.Save && _hashBuffer.IsEmpty())
            {
                Array.Clear(buffer, 0, SectorSize);
                return bytesRead;
            }

            if (bytesRead < SectorSize)
            {
                // Pad out unused portion of block
                Array.Clear(buffer, bytesRead, SectorSize - bytesRead);
            }

            if (!EnableIntegrityChecks) return bytesRead;

            _hash.Initialize();

            if (Type == IntegrityStreamType.Save)
            {
                _hash.TransformBlock(Salt, 0, Salt.Length, null, 0);
            }

            _hash.TransformBlock(buffer, 0, SectorSize, null, 0);
            _hash.TransformFinalBlock(buffer, 0, 0);

            byte[] hash = _hash.Hash;

            if (Type == IntegrityStreamType.Save)
            {
                // This bit is set on all save hashes
                hash[0x1F] |= 0x80;
            }

            if (!Util.ArraysEqual(_hashBuffer, hash))
            {
                throw new InvalidDataException("Hash error!");
            }

            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
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
        public int BlockSizePower { get; set; }
        public byte[] Salt { get; set; }
        public IntegrityStreamType Type { get; set; }
    }

    public enum IntegrityStreamType
    {
        Save,
        RomFs
    }
}
