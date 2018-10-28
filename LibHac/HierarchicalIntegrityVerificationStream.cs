using System;
using System.IO;
using LibHac.Streams;

namespace LibHac
{
    public class HierarchicalIntegrityVerificationStream : Stream
    {
        public Stream[] Levels { get; }
        public Stream DataLevel { get; }
        public IntegrityCheckLevel IntegrityCheckLevel { get; }

        /// <summary>
        /// An array of the hash statuses of every block in each level.
        /// </summary>
        public Validity[][] LevelValidities { get; }

        private IntegrityVerificationStream[] IntegrityStreams { get; }

        public HierarchicalIntegrityVerificationStream(IntegrityVerificationInfo[] levelInfo, IntegrityCheckLevel integrityCheckLevel)
        {
            Levels = new Stream[levelInfo.Length];
            IntegrityCheckLevel = integrityCheckLevel;
            LevelValidities = new Validity[levelInfo.Length - 1][];
            IntegrityStreams = new IntegrityVerificationStream[levelInfo.Length - 1];

            Levels[0] = levelInfo[0].Data;

            for (int i = 1; i < Levels.Length; i++)
            {
                var levelData = new IntegrityVerificationStream(levelInfo[i], Levels[i - 1], integrityCheckLevel);

                Levels[i] = new RandomAccessSectorStream(levelData);
                LevelValidities[i - 1] = levelData.BlockValidities;
                IntegrityStreams[i - 1] = levelData;
            }

            DataLevel = Levels[Levels.Length - 1];
        }

        /// <summary>
        /// Checks the hashes of any unchecked blocks and returns the <see cref="Validity"/> of the data.
        /// </summary>
        /// <param name="returnOnError">If <see langword="true"/>, return as soon as an invalid block is found.</param>
        /// <param name="logger">An optional <see cref="IProgressReport"/> for reporting progress.</param>
        /// <returns>The <see cref="Validity"/> of the data of the specified hash level.</returns>
        public Validity Validate(bool returnOnError, IProgressReport logger = null)
        {
            Validity[] validities = LevelValidities[LevelValidities.Length - 1];
            IntegrityVerificationStream stream = IntegrityStreams[IntegrityStreams.Length - 1];

            // Restore the original position of the stream when we're done validating
            long initialPosition = stream.Position;

            long blockSize = stream.SectorSize;
            int blockCount = (int)Util.DivideByRoundUp(Length, blockSize);

            var buffer = new byte[blockSize];
            var result = Validity.Valid;

            logger?.SetTotal(blockCount);

            for (int i = 0; i < blockCount; i++)
            {
                if (validities[i] == Validity.Unchecked)
                {
                    stream.Position = blockSize * i;
                    stream.Read(buffer, 0, buffer.Length, IntegrityCheckLevel.IgnoreOnInvalid);
                }

                if (validities[i] == Validity.Invalid)
                {
                    result = Validity.Invalid;
                    if (returnOnError) break;
                }

                logger?.ReportAdd(1);
            }

            logger?.SetTotal(0);
            stream.Position = initialPosition;
            return result;
        }

        public override void Flush()
        {
            DataLevel.Flush();
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
            DataLevel.Write(buffer, offset, count);
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

    public static class HierarchicalIntegrityVerificationStreamExtensions
    {
        internal static void SetLevelValidities(this HierarchicalIntegrityVerificationStream stream, IvfcHeader header)
        {
            for (int i = 0; i < stream.Levels.Length - 1; i++)
            {
                Validity[] level = stream.LevelValidities[i];
                var levelValidity = Validity.Valid;

                foreach (Validity block in level)
                {
                    if (block == Validity.Invalid)
                    {
                        levelValidity = Validity.Invalid;
                        break;
                    }

                    if (block == Validity.Unchecked && levelValidity != Validity.Invalid)
                    {
                        levelValidity = Validity.Unchecked;
                    }
                }

                header.LevelHeaders[i].HashValidity = levelValidity;
            }
        }
    }
}
