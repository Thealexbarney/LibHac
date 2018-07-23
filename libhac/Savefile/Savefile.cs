using System.IO;
using System.Text;

namespace libhac.Savefile
{
    public class Savefile
    {
        public Header Header { get; }
        public RemapStream FileRemap { get; }
        public RemapStream MetaRemap { get; }
        private Stream FileStream { get; }
        public JournalStream JournalStream { get; }

        public byte[] DuplexL1A { get; }
        public byte[] DuplexL1B { get; }
        public byte[] DuplexDataA { get; }
        public byte[] DuplexDataB { get; }

        public byte[] JournalTable { get; }
        public byte[] JournalBitmapUpdatedPhysical { get; }
        public byte[] JournalBitmapUpdatedVirtual { get; }
        public byte[] JournalBitmapUnassigned { get; }
        public byte[] JournalLayer1Hash { get; }
        public byte[] JournalLayer2Hash { get; }
        public byte[] JournalLayer3Hash { get; }
        public byte[] JournalStuff { get; }

        public Savefile(Stream file, IProgressReport logger = null)
        {
            FileStream = file;
            using (var reader = new BinaryReader(file, Encoding.Default, true))
            {
                Header = new Header(reader, logger);
                var layout = Header.Layout;
                FileRemap = new RemapStream(
                    new SubStream(file, layout.FileMapDataOffset, layout.FileMapDataSize),
                    Header.FileMapEntries, Header.FileRemap.MapSegmentCount);

                DuplexL1A = new byte[layout.DuplexL1Size];
                DuplexL1B = new byte[layout.DuplexL1Size];
                DuplexDataA = new byte[layout.DuplexDataSize];
                DuplexDataB = new byte[layout.DuplexDataSize];

                FileRemap.Position = layout.DuplexL1OffsetA;
                FileRemap.Read(DuplexL1A, 0, DuplexL1A.Length);
                FileRemap.Position = layout.DuplexL1OffsetB;
                FileRemap.Read(DuplexL1B, 0, DuplexL1B.Length);
                FileRemap.Position = layout.DuplexDataOffsetA;
                FileRemap.Read(DuplexDataA, 0, DuplexDataA.Length);
                FileRemap.Position = layout.DuplexDataOffsetB;
                FileRemap.Read(DuplexDataB, 0, DuplexDataB.Length);

                var duplexData = new SubStream(FileRemap, layout.DuplexDataOffsetB, layout.DuplexDataSize);
                MetaRemap = new RemapStream(duplexData, Header.MetaMapEntries, Header.MetaRemap.MapSegmentCount);

                JournalTable = new byte[layout.JournalTableSize];
                JournalBitmapUpdatedPhysical = new byte[layout.JournalBitmapUpdatedPhysicalSize];
                JournalBitmapUpdatedVirtual = new byte[layout.JournalBitmapUpdatedVirtualSize];
                JournalBitmapUnassigned = new byte[layout.JournalBitmapUnassignedSize];
                JournalLayer1Hash = new byte[layout.Layer1HashSize];
                JournalLayer2Hash = new byte[layout.Layer2HashSize];
                JournalLayer3Hash = new byte[layout.Layer3HashSize];
                JournalStuff = new byte[layout.Field150];

                MetaRemap.Position = layout.JournalTableOffset;
                MetaRemap.Read(JournalTable, 0, JournalTable.Length);
                MetaRemap.Position = layout.JournalBitmapUpdatedPhysicalOffset;
                MetaRemap.Read(JournalBitmapUpdatedPhysical, 0, JournalBitmapUpdatedPhysical.Length);
                MetaRemap.Position = layout.JournalBitmapUpdatedVirtualOffset;
                MetaRemap.Read(JournalBitmapUpdatedVirtual, 0, JournalBitmapUpdatedVirtual.Length);
                MetaRemap.Position = layout.JournalBitmapUnassignedOffset;
                MetaRemap.Read(JournalBitmapUnassigned, 0, JournalBitmapUnassigned.Length);
                MetaRemap.Position = layout.Layer1HashOffset;
                MetaRemap.Read(JournalLayer1Hash, 0, JournalLayer1Hash.Length);
                MetaRemap.Position = layout.Layer2HashOffset;
                MetaRemap.Read(JournalLayer2Hash, 0, JournalLayer2Hash.Length);
                MetaRemap.Position = layout.Layer3HashOffset;
                MetaRemap.Read(JournalLayer3Hash, 0, JournalLayer3Hash.Length);
                MetaRemap.Position = layout.Field148;
                MetaRemap.Read(JournalStuff, 0, JournalStuff.Length);

                var journalMap = JournalStream.ReadMappingEntries(JournalTable, JournalBitmapUpdatedPhysical,
                    JournalBitmapUpdatedVirtual, JournalBitmapUnassigned, Header.Journal.MappingEntryCount);

                var journalData = new SubStream(FileRemap, layout.JournalDataOffset,
                    layout.JournalDataSizeB + layout.SizeReservedArea);
                JournalStream = new JournalStream(journalData, journalMap, (int) Header.Journal.BlockSize)
                ;
            }
        }
    }
}
