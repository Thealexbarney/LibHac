using System.IO;
using System.Text;

namespace libhac.Savefile
{
    public class Savefile
    {
        public Header Header { get; }
        public RemapStream FileRemap { get; }

        public Savefile(Stream file)
        {
            using (var reader = new BinaryReader(file, Encoding.Default, true))
            {
                Header = new Header(reader);
                FileRemap = new RemapStream(
                    new SubStream(file, Header.Layout.FileMapDataOffset, Header.Layout.FileMapDataSize),
                    Header.FileMapEntries, Header.FileRemap.MapSegmentCount);
            }
        }
    }
}
