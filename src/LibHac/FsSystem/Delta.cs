using System;
using System.Collections.Generic;
using System.IO;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    public class Delta
    {
        private const string Ndv0Magic = "NDV0";
        private IStorage OriginalStorage { get; set; }
        private IStorage DeltaStorage { get; }
        public DeltaHeader Header { get; }
        private List<DeltaSegment> Segments { get; } = new List<DeltaSegment>();

        public Delta(IStorage deltaStorage, IStorage originalData) : this(deltaStorage)
        {
            SetBaseStorage(originalData);
        }

        public Delta(IStorage deltaStorage)
        {
            DeltaStorage = deltaStorage;
            deltaStorage.GetSize(out long deltaSize).ThrowIfFailure();

            if (deltaSize < 0x40) throw new InvalidDataException("Delta file is too small.");

            Header = new DeltaHeader(deltaStorage.AsFile(OpenMode.Read));

            if (Header.Magic != Ndv0Magic) throw new InvalidDataException("NDV0 magic value is missing.");

            long fragmentSize = Header.HeaderSize + Header.BodySize;
            if (deltaSize < fragmentSize)
            {
                throw new InvalidDataException($"Delta file is smaller than the header indicates. (0x{fragmentSize} bytes)");
            }

            ParseDeltaStructure();
        }

        public void SetBaseStorage(IStorage baseStorage)
        {
            OriginalStorage = baseStorage;
            baseStorage.GetSize(out long storageSize).ThrowIfFailure();

            if (storageSize != Header.OriginalSize)
            {
                throw new InvalidDataException($"Original file size does not match the size in the delta header. (0x{Header.OriginalSize} bytes)");
            }
        }

        public IStorage GetPatchedStorage()
        {
            if (OriginalStorage == null) throw new InvalidOperationException("Cannot apply a delta patch without a base file.");

            var storages = new List<IStorage>();

            foreach (DeltaSegment segment in Segments)
            {
                IStorage source = segment.IsInOriginal ? OriginalStorage : DeltaStorage;

                // todo Do this without tons of SubStorages
                IStorage sub = source.Slice(segment.SourceOffset, segment.Size);

                storages.Add(sub);
            }

            return new ConcatenationStorage(storages, true);
        }

        private void ParseDeltaStructure()
        {
            var reader = new FileReader(DeltaStorage.AsFile(OpenMode.Read));

            reader.Position = Header.HeaderSize;

            long offset = 0;

            while (offset < Header.NewSize)
            {
                ReadSegmentHeader(reader, out int size, out int seek);

                if (seek > 0)
                {
                    var segment = new DeltaSegment()
                    {
                        SourceOffset = offset,
                        Size = seek,
                        IsInOriginal = true
                    };

                    Segments.Add(segment);
                    offset += seek;
                }

                if (size > 0)
                {
                    var segment = new DeltaSegment()
                    {
                        SourceOffset = reader.Position,
                        Size = size,
                        IsInOriginal = false
                    };

                    Segments.Add(segment);
                    offset += size;
                }

                reader.Position += size;
            }
        }

        private static void ReadSegmentHeader(FileReader reader, out int size, out int seek)
        {
            byte type = reader.ReadUInt8();

            int seekBytes = (type & 3) + 1;
            int sizeBytes = ((type >> 3) & 3) + 1;

            size = ReadInt(reader, sizeBytes);
            seek = ReadInt(reader, seekBytes);
        }

        private static int ReadInt(FileReader reader, int bytes)
        {
            switch (bytes)
            {
                case 1: return reader.ReadUInt8();
                case 2: return reader.ReadUInt16();
                case 3: return reader.ReadUInt24();
                case 4: return reader.ReadInt32();
                default: return 0;
            }
        }
    }

    internal class DeltaSegment
    {
        public long SourceOffset { get; set; }
        public int Size { get; set; }
        public bool IsInOriginal { get; set; }
    }

    public class DeltaHeader
    {
        public string Magic { get; }
        public long OriginalSize { get; }
        public long NewSize { get; }
        public long HeaderSize { get; }
        public long BodySize { get; }

        public DeltaHeader(IFile header)
        {
            var reader = new FileReader(header);

            Magic = reader.ReadAscii(4);
            OriginalSize = reader.ReadInt64(8);
            NewSize = reader.ReadInt64();
            HeaderSize = reader.ReadInt64();
            BodySize = reader.ReadInt64();
        }
    }
}
