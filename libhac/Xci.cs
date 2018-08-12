using System.IO;

namespace libhac
{
    public class Xci
    {
        public XciHeader Header { get; set; }
        public Xci(Keyset keyset, Stream stream)
        {
            Header = new XciHeader(keyset, stream);
        }
    }
}
