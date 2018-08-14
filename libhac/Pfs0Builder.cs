using System.Collections.Generic;
using System.IO;

namespace libhac
{
    public class Pfs0Builder
    {
        private List<Entry> Entries { get; } = new List<Entry>();
        private long DataLength { get; set; }

        public void AddFile(string filename, Stream stream)
        {
            var entry = new Entry
            {
                Name = filename,
                Stream = stream,
                Length = stream.Length,
                Offset = DataLength
            };

            DataLength += entry.Length;

            Entries.Add(entry);
        }

        public void Build(Stream output, IProgressReport logger = null)
        {
            var strings = new MemoryStream();
            var stringWriter = new BinaryWriter(strings);
            var writer = new BinaryWriter(output);

            foreach (var entry in Entries)
            {
                entry.StringOffset = (int)strings.Length;
                stringWriter.WriteUTF8Z(entry.Name);
            }

            strings.Position = Util.GetNextMultiple(strings.Length, 0x10);
            var stringTable = strings.ToArray();

            output.Position = 0;
            writer.WriteUTF8("PFS0");
            writer.Write(Entries.Count);
            writer.Write(stringTable.Length);
            writer.Write(0);

            foreach (var entry in Entries)
            {
                writer.Write(entry.Offset);
                writer.Write(entry.Length);
                writer.Write(entry.StringOffset);
                writer.Write(0);
            }

            writer.Write(stringTable);

            foreach (var entry in Entries)
            {
                logger?.LogMessage(entry.Name);
                entry.Stream.Position = 0;
                entry.Stream.CopyStream(output, entry.Length, logger);
            }
            logger?.SetTotal(0);
        }

        private class Entry
        {
            public string Name;
            public Stream Stream;
            public long Length;
            public long Offset;
            public int StringOffset;
        }
    }
}
