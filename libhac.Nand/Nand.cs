using System.IO;
using System.Linq;
using DiscUtils;
using DiscUtils.Fat;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using libhac.XTSSharp;

namespace libhac.Nand
{
    public class Nand
    {
        private GuidPartitionInfo ProdInfo { get; }
        private GuidPartitionInfo ProdInfoF { get; }
        private GuidPartitionInfo Safe { get; }
        private GuidPartitionInfo System { get; }
        private GuidPartitionInfo User { get; }
        private Keyset Keyset { get; }

        public Nand(Stream stream, Keyset keyset)
        {
            var disc = new GuidPartitionTable(stream, Geometry.Null);
            var partitions = disc.Partitions.Select(x => (GuidPartitionInfo)x).ToArray();
            ProdInfo = partitions.FirstOrDefault(x => x.Name == "PRODINFO");
            ProdInfoF = partitions.FirstOrDefault(x => x.Name == "PRODINFOF");
            Safe = partitions.FirstOrDefault(x => x.Name == "SAFE");
            System = partitions.FirstOrDefault(x => x.Name == "SYSTEM");
            User = partitions.FirstOrDefault(x => x.Name == "USER");
            Keyset = keyset;
        }

        public Stream OpenProdInfo()
        {
            var encStream = ProdInfo.Open();
            var xts = XtsAes128.Create(Keyset.bis_keys[0]);
            var decStream = new RandomAccessSectorStream(new XtsSectorStream(encStream, xts, 0x4000, 0), true);
            return decStream;
        }

        public FatFileSystem OpenProdInfoF()
        {
            var encStream = ProdInfoF.Open();
            var xts = XtsAes128.Create(Keyset.bis_keys[0]);
            var decStream = new RandomAccessSectorStream(new XtsSectorStream(encStream, xts, 0x4000, 0), true);
            FatFileSystem fat = new FatFileSystem(decStream, Ownership.None);
            return fat;
        }

        public FatFileSystem OpenSafePartition()
        {
            var encStream = Safe.Open();
            var xts = XtsAes128.Create(Keyset.bis_keys[1]);
            var decStream = new RandomAccessSectorStream(new XtsSectorStream(encStream, xts, 0x4000, 0), true);
            FatFileSystem fat = new FatFileSystem(decStream, Ownership.None);
            return fat;
        }

        public FatFileSystem OpenSystemPartition()
        {
            var encStream = System.Open();
            var xts = XtsAes128.Create(Keyset.bis_keys[2]);
            var decStream = new RandomAccessSectorStream(new XtsSectorStream(encStream, xts, 0x4000, 0), true);
            FatFileSystem fat = new FatFileSystem(decStream, Ownership.None);
            return fat;
        }

        public FatFileSystem OpenUserPartition()
        {
            var encStream = User.Open();
            var xts = XtsAes128.Create(Keyset.bis_keys[3]);
            var decStream = new RandomAccessSectorStream(new XtsSectorStream(encStream, xts, 0x4000, 0), true);
            FatFileSystem fat = new FatFileSystem(decStream, Ownership.None);
            return fat;
        }
    }
}
