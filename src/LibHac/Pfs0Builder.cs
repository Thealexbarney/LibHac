using LibHac.IO;
using System.Collections.Generic;
using System.IO;

namespace LibHac
{
    public class Pfs0Builder
    {
        private List<Entry> Entries { get; } = new List<Entry>();
        private long DataLength { get; set; }

        public void AddFile(string filename, IStorage storage)
        {
            var entry = new Entry
            {
                Name = filename,
                Storage = storage,
                Length = storage.Length,
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

            foreach (Entry entry in Entries)
            {
                entry.StringOffset = (int)strings.Length;
                stringWriter.WriteUTF8Z(entry.Name);
            }

            strings.Position = Util.GetNextMultiple(strings.Length, 0x10);
            byte[] stringTable = strings.ToArray();

            output.Position = 0;
            writer.WriteUTF8("PFS0");
            writer.Write(Entries.Count);
            writer.Write(stringTable.Length);
            writer.Write(0);

            foreach (Entry entry in Entries)
            {
                writer.Write(entry.Offset);
                writer.Write(entry.Length);
                writer.Write(entry.StringOffset);
                writer.Write(0);
            }

            writer.Write(stringTable);

            foreach (Entry entry in Entries)
            {
                logger?.LogMessage(entry.Name);
                entry.Storage.CopyTo(output.AsStorage(), logger);
            }
            logger?.SetTotal(0);
        }

        private class Entry
        {
            public string Name;
            public IStorage Storage;
            public long Length;
            public long Offset;
            public int StringOffset;
        }
    }
}
