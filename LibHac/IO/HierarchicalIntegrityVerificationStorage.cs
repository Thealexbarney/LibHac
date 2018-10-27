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

        public override long Length { get; }
    }
}
