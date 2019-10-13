using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class ConcatenationStorageBuilder
    {
        private List<ConcatenationStorageSegment> Segments { get; }

        public ConcatenationStorageBuilder()
        {
            Segments = new List<ConcatenationStorageSegment>();
        }

        public ConcatenationStorageBuilder(IEnumerable<ConcatenationStorageSegment> segments)
        {
            Segments = segments.ToList();
        }

        public void Add(IStorage storage, long offset)
        {
            Segments.Add(new ConcatenationStorageSegment(storage, offset));
        }

        public ConcatenationStorage Build()
        {
            List<ConcatenationStorageSegment> segments = Segments.OrderBy(x => x.Offset).ToList();
            var sources = new List<IStorage>();

            long offset = 0;

            foreach (ConcatenationStorageSegment segment in segments)
            {
                long paddingNeeded = segment.Offset - offset;

                if (paddingNeeded < 0) throw new InvalidDataException("Builder has segments that overlap.");

                if (paddingNeeded > 0)
                {
                    sources.Add(new NullStorage(paddingNeeded));
                }

                segment.Storage.GetSize(out long segmentSize).ThrowIfFailure();

                sources.Add(segment.Storage);
                offset = segment.Offset + segmentSize;
            }

            return new ConcatenationStorage(sources, true);
        }
    }

    public class ConcatenationStorageSegment
    {
        public IStorage Storage { get; }
        public long Offset { get; }

        public ConcatenationStorageSegment(IStorage storage, long offset)
        {
            Storage = storage;
            Offset = offset;
        }
    }
}
