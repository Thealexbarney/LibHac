using System.IO;
using System.Linq;
using DiscUtils;
using DiscUtils.Fat;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using LibHac.IO;

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
            IStorage encStorage = ProdInfo.Open().AsStorage();
            var decStorage = new CachedStorage(new Aes128XtsStorage(encStorage, Keyset.BisKeys[0], 0x4000, true), 0x4000, 4, true);
            decStorage.SetReadOnly();
            return decStorage.AsStream();
        }

        public NandPartition OpenProdInfoF()
        {
            IStorage encStorage = ProdInfoF.Open().AsStorage();
            var decStorage = new CachedStorage(new Aes128XtsStorage(encStorage, Keyset.BisKeys[0], 0x4000, true), 0x4000, 4, true);
            decStorage.SetReadOnly();
            var fat = new FatFileSystem(decStorage.AsStream(), Ownership.None);
            return new NandPartition(fat);
        }

        public NandPartition OpenSafePartition()
        {
            IStorage encStorage = Safe.Open().AsStorage();
            var decStorage = new CachedStorage(new Aes128XtsStorage(encStorage, Keyset.BisKeys[1], 0x4000, true), 0x4000, 4, true);
            decStorage.SetReadOnly();
            var fat = new FatFileSystem(decStorage.AsStream(), Ownership.None);
            return new NandPartition(fat);
        }

        public NandPartition OpenSystemPartition()
        {
            IStorage encStorage = System.Open().AsStorage();
            var decStorage = new CachedStorage(new Aes128XtsStorage(encStorage, Keyset.BisKeys[2], 0x4000, true), 0x4000, 4, true);
            decStorage.SetReadOnly();
            var fat = new FatFileSystem(decStorage.AsStream(), Ownership.None);
            return new NandPartition(fat);
        }

        public NandPartition OpenUserPartition()
        {
            IStorage encStorage = User.Open().AsStorage();
            var decStorage = new CachedStorage(new Aes128XtsStorage(encStorage, Keyset.BisKeys[3], 0x4000, true), 0x4000, 4, true);
            decStorage.SetReadOnly();
            var fat = new FatFileSystem(decStorage.AsStream(), Ownership.None);
            return new NandPartition(fat);
        }
    }
}
