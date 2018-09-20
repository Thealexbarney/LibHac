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
        public bool EnableIntegrityChecks { get; }

        private readonly byte[] _hashBuffer = new byte[DigestSize];
        private readonly SHA256 _hash = SHA256.Create();

        public IntegrityVerificationStream(Stream dataStream, Stream hashStream, int blockSizePower, bool enableIntegrityChecks)
            : base(dataStream, 1 << blockSizePower)
        {
            HashStream = hashStream;
            EnableIntegrityChecks = enableIntegrityChecks;
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

            // If a hash is zero the data for the entire block is zero
            if (_hashBuffer.IsEmpty())
            {
                Array.Clear(buffer, 0, SectorSize);
            }

            int bytesRead = base.Read(buffer, 0, count);

            if (bytesRead < SectorSize)
            {
                // Pad out unused portion of block
                Array.Clear(buffer, bytesRead, SectorSize - bytesRead);
            }

            if (EnableIntegrityChecks && !Util.ArraysEqual(_hashBuffer, _hash.ComputeHash(buffer)))
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
    }
}
