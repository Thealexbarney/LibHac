using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LibHac.IO
{
    public class HierarchicalIntegrityVerificationStorage : Storage
    {
        public IStorage[] Levels { get; }
        public IStorage DataLevel { get; }
        public IntegrityCheckLevel IntegrityCheckLevel { get; }

        /// <summary>
        /// An array of the hash statuses of every block in each level.
        /// </summary>
        public Validity[][] LevelValidities { get; }
        public override long Length { get; }

        private IntegrityVerificationStorage[] IntegrityStorages { get; }

        public HierarchicalIntegrityVerificationStorage(IntegrityVerificationInfo[] levelInfo, IntegrityCheckLevel integrityCheckLevel, bool leaveOpen)
        {
            Levels = new IStorage[levelInfo.Length];
            IntegrityCheckLevel = integrityCheckLevel;
            LevelValidities = new Validity[levelInfo.Length - 1][];
            IntegrityStorages = new IntegrityVerificationStorage[levelInfo.Length - 1];

            Levels[0] = levelInfo[0].Data;

            for (int i = 1; i < Levels.Length; i++)
            {
                var levelData = new IntegrityVerificationStorage(levelInfo[i], Levels[i - 1], integrityCheckLevel, leaveOpen);

                Levels[i] = new CachedStorage(levelData, 4, leaveOpen);
                LevelValidities[i - 1] = levelData.BlockValidities;
                IntegrityStorages[i - 1] = levelData;
            }

            DataLevel = Levels[Levels.Length - 1];
            Length = DataLevel.Length;

            if (!leaveOpen) ToDispose.Add(DataLevel);
        }

        public HierarchicalIntegrityVerificationStorage(IvfcHeader header, IStorage masterHash, IStorage data,
            IntegrityStorageType type, IntegrityCheckLevel integrityCheckLevel, bool leaveOpen)
            : this(header, ToStorageList(header, masterHash, data, leaveOpen), type, integrityCheckLevel, leaveOpen) { }

        public HierarchicalIntegrityVerificationStorage(IvfcHeader header, IList<IStorage> levels,
            IntegrityStorageType type, IntegrityCheckLevel integrityCheckLevel, bool leaveOpen)
            : this(GetIvfcInfo(header, levels, type), integrityCheckLevel, leaveOpen) { }

        private static List<IStorage> ToStorageList(IvfcHeader header, IStorage masterHash, IStorage data, bool leaveOpen)
        {
            var levels = new List<IStorage> { masterHash };

            for (int i = 0; i < header.NumLevels - 1; i++)
            {
                IvfcLevelHeader level = header.LevelHeaders[i];
                levels.Add(data.Slice(level.Offset, level.Size, leaveOpen));
            }

            return levels;
        }

        private static IntegrityVerificationInfo[] GetIvfcInfo(IvfcHeader ivfc, IList<IStorage> levels, IntegrityStorageType type)
        {
            var initInfo = new IntegrityVerificationInfo[ivfc.NumLevels];

            initInfo[0] = new IntegrityVerificationInfo
            {
                Data = levels[0],
                BlockSize = 0
            };

            for (int i = 1; i < ivfc.NumLevels; i++)
            {
                initInfo[i] = new IntegrityVerificationInfo
                {
                    Data = levels[i],
                    BlockSize = 1 << ivfc.LevelHeaders[i - 1].BlockSizePower,
                    Salt = new HMACSHA256(Encoding.ASCII.GetBytes(SaltSources[i - 1])).ComputeHash(ivfc.SaltSource),
                    Type = type
                };
            }

            return initInfo;
        }

        protected override void ReadImpl(Span<byte> destination, long offset)
        {
            DataLevel.Read(destination, offset);
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
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
            IntegrityVerificationStorage storage = IntegrityStorages[IntegrityStorages.Length - 1];

            long blockSize = storage.SectorSize;
            int blockCount = (int)Util.DivideByRoundUp(Length, blockSize);

            var buffer = new byte[blockSize];
            var result = Validity.Valid;

            logger?.SetTotal(blockCount);

            for (int i = 0; i < blockCount; i++)
            {
                if (validities[i] == Validity.Unchecked)
                {
                    int toRead = (int)Math.Min(storage.Length - blockSize * i, buffer.Length);
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

        private static readonly string[] SaltSources =
        {
            "HierarchicalIntegrityVerificationStorage::Master",
            "HierarchicalIntegrityVerificationStorage::L1",
            "HierarchicalIntegrityVerificationStorage::L2",
            "HierarchicalIntegrityVerificationStorage::L3",
            "HierarchicalIntegrityVerificationStorage::L4",
            "HierarchicalIntegrityVerificationStorage::L5"
        };
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

    public class IvfcHeader
    {
        public string Magic;
        public int Version;
        public int MasterHashSize;
        public int NumLevels;
        public IvfcLevelHeader[] LevelHeaders = new IvfcLevelHeader[6];
        public byte[] SaltSource;
        public byte[] MasterHash;

        public IvfcHeader() { }

        public IvfcHeader(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            reader.BaseStream.Position += 2;
            Version = reader.ReadInt16();
            MasterHashSize = reader.ReadInt32();
            NumLevels = reader.ReadInt32();

            for (int i = 0; i < LevelHeaders.Length; i++)
            {
                LevelHeaders[i] = new IvfcLevelHeader(reader);
            }

            SaltSource = reader.ReadBytes(0x20);
            MasterHash = reader.ReadBytes(0x20);
        }

        public IvfcHeader(IStorage storage) : this(new BinaryReader(storage.AsStream())) { }
    }

    public class IvfcLevelHeader
    {
        public long Offset;
        public long Size;
        public int BlockSizePower;
        public uint Reserved;

        public Validity HashValidity = Validity.Unchecked;

        public IvfcLevelHeader() { }

        public IvfcLevelHeader(BinaryReader reader)
        {
            Offset = reader.ReadInt64();
            Size = reader.ReadInt64();
            BlockSizePower = reader.ReadInt32();
            Reserved = reader.ReadUInt32();
        }
    }
}
