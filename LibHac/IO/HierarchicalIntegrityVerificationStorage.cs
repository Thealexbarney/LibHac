using System;

namespace LibHac.IO
{
    public class HierarchicalIntegrityVerificationStorage : Storage
    {
        public Storage[] Levels { get; }
        public Storage DataLevel { get; }
        public IntegrityCheckLevel IntegrityCheckLevel { get; }

        /// <summary>
        /// An array of the hash statuses of every block in each level.
        /// </summary>
        public Validity[][] LevelValidities { get; }
        public override long Length { get; }

        private IntegrityVerificationStorage[] IntegrityStorages { get; }

        public HierarchicalIntegrityVerificationStorage(IntegrityVerificationInfoStorage[] levelInfo, IntegrityCheckLevel integrityCheckLevel)
        {
            Levels = new Storage[levelInfo.Length];
            IntegrityCheckLevel = integrityCheckLevel;
            LevelValidities = new Validity[levelInfo.Length - 1][];
            IntegrityStorages = new IntegrityVerificationStorage[levelInfo.Length - 1];

            Levels[0] = levelInfo[0].Data;

            for (int i = 1; i < Levels.Length; i++)
            {
                var levelData = new IntegrityVerificationStorage(levelInfo[i], Levels[i - 1], integrityCheckLevel);

                Levels[i] = new CachedStorage(levelData, 4, true);
                LevelValidities[i - 1] = levelData.BlockValidities;
                IntegrityStorages[i - 1] = levelData;
            }

            DataLevel = Levels[Levels.Length - 1];
            Length = DataLevel.Length;
        }

        protected override int ReadSpan(Span<byte> destination, long offset)
        {
            return DataLevel.Read(destination, offset);
        }

        protected override void WriteSpan(ReadOnlySpan<byte> source, long offset)
        {
            DataLevel.Write(source, offset);
        }

        public override void Flush()
        {
            DataLevel.Flush();
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
            var storage = IntegrityStorages[IntegrityStorages.Length - 1];

            long blockSize = storage.SectorSize;
            int blockCount = (int)Util.DivideByRoundUp(Length, blockSize);

            var buffer = new byte[blockSize];
            var result = Validity.Valid;

            logger?.SetTotal(blockCount);

            for (int i = 0; i < blockCount; i++)
            {
                if (validities[i] == Validity.Unchecked)
                {
                    int toRead = (int) Math.Min(storage.Length - blockSize * i, buffer.Length);
                    storage.Read(buffer, blockSize * i, toRead, 0, IntegrityCheckLevel.IgnoreOnInvalid);
                }

                if (validities[i] == Validity.Invalid)
                {
                    result = Validity.Invalid;
                    if (returnOnError) break;
                }

                logger?.ReportAdd(1);
            }

            logger?.SetTotal(0);
            return result;
        }
    }

    public static class HierarchicalIntegrityVerificationStorageExtensions
    {
        internal static void SetLevelValidities(this HierarchicalIntegrityVerificationStorage stream, IvfcHeader header)
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
