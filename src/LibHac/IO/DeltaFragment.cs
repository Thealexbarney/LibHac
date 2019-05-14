using System;
using System.Collections.Generic;
using System.IO;

namespace LibHac.Fs
{
    public class DeltaFragment
    {
        private const string Ndv0Magic = "NDV0";
        private IStorage Original { get; set; }
        private IStorage Delta { get; }
        public DeltaFragmentHeader Header { get; }
        private List<DeltaFragmentSegment> Segments { get; } = new List<DeltaFragmentSegment>();

        public DeltaFragment(IStorage delta, IStorage originalData) : this(delta)
        {
            SetBaseStorage(originalData);
        }

        public DeltaFragment(IStorage delta)
        {
            Delta = delta;

            if (Delta.GetSize() < 0x40) throw new InvalidDataException("Delta file is too small.");

            Header = new DeltaFragmentHeader(delta.AsFile(OpenMode.Read));

            if (Header.Magic != Ndv0Magic) throw new InvalidDataException("NDV0 magic value is missing.");

            long fragmentSize = Header.FragmentHeaderSize + Header.FragmentBodySize;
            if (Delta.GetSize() < fragmentSize)
            {
                throw new InvalidDataException($"Delta file is smaller than the header indicates. (0x{fragmentSize} bytes)");
            }

            ParseDeltaStructure();
        }

        public void SetBaseStorage(IStorage baseStorage)
        {
            Original = baseStorage;

            if (Original.GetSize() != Header.OriginalSize)
            {
                throw new InvalidDataException($"Original file size does not match the size in the delta header. (0x{Header.OriginalSize} bytes)");
            }
        }

        public IStorage GetPatchedStorage()
        {
            if (Original == null) throw new InvalidOperationException("Cannot apply a delta patch without a base file.");

            var storages = new List<IStorage>();

            foreach (DeltaFragmentSegment segment in Segments)
            {
                IStorage source = segment.IsInOriginal ? Original : Delta;

                // todo Do this without tons of SubStorages
                IStorage sub = source.Slice(segment.SourceOffset, segment.Size);

                storages.Add(sub);
            }

            return new ConcatenationStorage(storages, true);
        }

        private void ParseDeltaStructure()
        {
            var reader = new FileReader(Delta.AsFile(OpenMode.Read));

            reader.Position = Header.FragmentHeaderSize;

            long offset = 0;

            while (offset < Header.NewSize)
            {
                ReadSegmentHeader(reader, out int size, out int seek);

                if (seek > 0)
                {
                    var segment = new DeltaFragmentSegment()
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
                    var segment = new DeltaFragmentSegment()
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

    internal class DeltaFragmentSegment
    {
        public long SourceOffset { get; set; }
        public int Size { get; set; }
        public bool IsInOriginal { get; set; }
    }

    public class DeltaFragmentHeader
    {
        public string Magic { get; }
        public long OriginalSize { get; }
        public long NewSize { get; }
        public long FragmentHeaderSize { get; }
        public long FragmentBodySize { get; }

        public DeltaFragmentHeader(IFile header)
        {
            var reader = new FileReader(header);

            Magic = reader.ReadAscii(4);
            OriginalSize = reader.ReadInt64(8);
            NewSize = reader.ReadInt64();
            FragmentHeaderSize = reader.ReadInt64();
            FragmentBodySize = reader.ReadInt64();
        }
    }
}
