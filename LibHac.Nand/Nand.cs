using System.IO;
using System.Linq;
using DiscUtils;
using DiscUtils.Fat;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using LibHac.Streams;
using LibHac.XTSSharp;

namespace LibHac.Nand
{
    public class Nand
    {
        private GuidPartitionInfo ProdInfo { get; }
        private GuidPartitionInfo ProdInfoF { get; }
        private GuidPartitionInfo Safe { get; }
        private GuidPartitionInfo System { get; }
        private GuidPartitionInfo User { get; }
        public Keyset Keyset { get; }

        public Nand(Stream stream, Keyset keyset)
        {
            var disc = new GuidPartitionTable(stream, Geometry.Null);
            GuidPartitionInfo[] partitions = disc.Partitions.Select(x => (GuidPartitionInfo)x).ToArray();
            ProdInfo = partitions.FirstOrDefault(x => x.Name == "PRODINFO");
            ProdInfoF = partitions.FirstOrDefault(x => x.Name == "PRODINFOF");
            Safe = partitions.FirstOrDefault(x => x.Name == "SAFE");
            System = partitions.FirstOrDefault(x => x.Name == "SYSTEM");
            User = partitions.FirstOrDefault(x => x.Name == "USER");
            Keyset = keyset;
        }

        public Stream OpenProdInfo()
        {
            SparseStream encStream = ProdInfo.Open();
            Xts xts = XtsAes128.Create(Keyset.BisKeys[0]);
            var decStream = new RandomAccessSectorStream(new XtsSectorStream(encStream, xts, 0x4000, 0), true);
            return decStream;
        }

        public NandPartition OpenProdInfoF()
        {
            SparseStream encStream = ProdInfoF.Open();
            Xts xts = XtsAes128.Create(Keyset.BisKeys[0]);
            var decStream = new RandomAccessSectorStream(new XtsSectorStream(encStream, xts, 0x4000, 0), true);
            var fat = new FatFileSystem(decStream, Ownership.None);
            return new NandPartition(fat);
        }

        public NandPartition OpenSafePartition()
        {
            SparseStream encStream = Safe.Open();
            Xts xts = XtsAes128.Create(Keyset.BisKeys[1]);
            var decStream = new RandomAccessSectorStream(new XtsSectorStream(encStream, xts, 0x4000, 0), true);
            var fat = new FatFileSystem(decStream, Ownership.None);
            return new NandPartition(fat);
        }

        public NandPartition OpenSystemPartition()
        {
            SparseStream encStream = System.Open();
            Xts xts = XtsAes128.Create(Keyset.BisKeys[2]);
            var decStream = new RandomAccessSectorStream(new XtsSectorStream(encStream, xts, 0x4000, 0), true);
            var fat = new FatFileSystem(decStream, Ownership.None);
            return new NandPartition(fat);
        }

        public NandPartition OpenUserPartition()
        {
            SparseStream encStream = User.Open();
            Xts xts = XtsAes128.Create(Keyset.BisKeys[3]);
            var decStream = new RandomAccessSectorStream(new XtsSectorStream(encStream, xts, 0x4000, 0), true);
            var fat = new FatFileSystem(decStream, Ownership.None);
            return new NandPartition(fat);
        }
    }
}
