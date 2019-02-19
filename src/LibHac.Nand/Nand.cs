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
        private GuidPartitionInfo[] Package2 { get; }
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
            Package2 = new[]
            {
                partitions.FirstOrDefault(x => x.Name == "BCPKG2-1-Normal-Main"),
                partitions.FirstOrDefault(x => x.Name == "BCPKG2-2-Normal-Sub"),
                partitions.FirstOrDefault(x => x.Name == "BCPKG2-3-SafeMode-Main"),
                partitions.FirstOrDefault(x => x.Name == "BCPKG2-4-SafeMode-Sub"),
                partitions.FirstOrDefault(x => x.Name == "BCPKG2-5-Repair-Main"),
                partitions.FirstOrDefault(x => x.Name == "BCPKG2-6-Repair-Sub")
            };
            Safe = partitions.FirstOrDefault(x => x.Name == "SAFE");
            System = partitions.FirstOrDefault(x => x.Name == "SYSTEM");
            User = partitions.FirstOrDefault(x => x.Name == "USER");
            Keyset = keyset;
        }

        public Stream OpenProdInfo()
        {
            IStorage encStorage = ProdInfo.Open().AsStorage();
            var decStorage = new CachedStorage(new Aes128XtsStorage(encStorage, Keyset.BisKeys[0], 0x4000, true), 0x4000, 4, true);
            return decStorage.AsStream(FileAccess.Read);
        }

        public FatFileSystemProvider OpenProdInfoF()
        {
            IStorage encStorage = ProdInfoF.Open().AsStorage();
            var decStorage = new CachedStorage(new Aes128XtsStorage(encStorage, Keyset.BisKeys[0], 0x4000, true), 0x4000, 4, true);
            var fat = new FatFileSystem(decStorage.AsStream(FileAccess.Read), Ownership.None);
            return new FatFileSystemProvider(fat);
        }

        public IStorage OpenPackage2(int index)
        {
            return Package2[index].Open().AsStorage().AsReadOnly();
        }

        public FatFileSystemProvider OpenSafePartition()
        {
            IStorage encStorage = Safe.Open().AsStorage();
            var decStorage = new CachedStorage(new Aes128XtsStorage(encStorage, Keyset.BisKeys[1], 0x4000, true), 0x4000, 4, true);
            var fat = new FatFileSystem(decStorage.AsStream(FileAccess.Read), Ownership.None);
            return new FatFileSystemProvider(fat);
        }

        public FatFileSystemProvider OpenSystemPartition()
        {
            IStorage encStorage = System.Open().AsStorage();
            var decStorage = new CachedStorage(new Aes128XtsStorage(encStorage, Keyset.BisKeys[2], 0x4000, true), 0x4000, 4, true);
            var fat = new FatFileSystem(decStorage.AsStream(FileAccess.Read), Ownership.None);
            return new FatFileSystemProvider(fat);
        }

        public FatFileSystemProvider OpenUserPartition()
        {
            IStorage encStorage = User.Open().AsStorage();
            var decStorage = new CachedStorage(new Aes128XtsStorage(encStorage, Keyset.BisKeys[3], 0x4000, true), 0x4000, 4, true);
            var fat = new FatFileSystem(decStorage.AsStream(FileAccess.Read), Ownership.None);
            return new FatFileSystemProvider(fat);
        }
    }
}