using System.IO;

namespace libhac
{
    public class Xci
    {
        private const string UpdatePartitionName = "update";
        private const string NormalPartitionName = "normal";
        private const string SecurePartitionName = "secure";
        private const string LogoPartitionName = "logo";

        public XciHeader Header { get; }
        public Pfs RootPartition { get; }
        public Pfs UpdatePartition { get; }
        public Pfs NormalPartition { get; }
        public Pfs SecurePartition { get; }
        public Pfs LogoPartition { get; }

        public Xci(Keyset keyset, Stream stream)
        {
            Header = new XciHeader(keyset, stream);
            var hfs0Stream = new SubStream(stream, Header.PartitionFsHeaderAddress);
            RootPartition = new Pfs(hfs0Stream);

            if (RootPartition.TryOpenFile(UpdatePartitionName, out var updateStream))
            {
                UpdatePartition = new Pfs(updateStream);
            }

            if (RootPartition.TryOpenFile(NormalPartitionName, out var normalStream))
            {
                NormalPartition = new Pfs(normalStream);
            }

            if (RootPartition.TryOpenFile(SecurePartitionName, out var secureStream))
            {
                SecurePartition = new Pfs(secureStream);
            }

            if (RootPartition.TryOpenFile(LogoPartitionName, out var logoStream))
            {
                LogoPartition = new Pfs(logoStream);
            }
        }
    }
}
