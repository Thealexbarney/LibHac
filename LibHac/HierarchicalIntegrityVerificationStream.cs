using System;
using System.IO;
using LibHac.Streams;

namespace LibHac
{
    public class HierarchicalIntegrityVerificationStream : Stream
    {
        public Stream[] Levels { get; }
        public Stream DataLevel { get; }
        public bool EnableIntegrityChecks { get; }

        public HierarchicalIntegrityVerificationStream(IntegrityVerificationInfo[] levelInfo, bool enableIntegrityChecks)
        {
            Levels = new Stream[levelInfo.Length];
            EnableIntegrityChecks = enableIntegrityChecks;

            Levels[0] = levelInfo[0].Data;

            for (int i = 1; i < Levels.Length; i++)
            {
                var levelData = new IntegrityVerificationStream(levelInfo[i].Data, Levels[i - 1],
                    levelInfo[i].BlockSizePower, enableIntegrityChecks);

                Levels[i] = new RandomAccessSectorStream(levelData);
            }

            DataLevel = Levels[Levels.Length - 1];
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
            return DataLevel.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => DataLevel.CanRead;
        public override bool CanSeek => DataLevel.CanSeek;
        public override bool CanWrite => false;
        public override long Length => DataLevel.Length;
        public override long Position
        {
            get => DataLevel.Position;
            set => DataLevel.Position = value;
        }
    }
}
