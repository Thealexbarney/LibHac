using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.FsSystem.Detail;

namespace LibHac
{
    public class Xci
    {
        public XciHeader Header { get; }

        private IStorage BaseStorage { get; }
        private object InitLocker { get; } = new object();
        private XciPartition RootPartition { get; set; }
        private IFileSystem RootPartition2 { get; set; }

        public Xci(Keyset keyset, IStorage storage)
        {
            BaseStorage = storage;
            Header = new XciHeader(keyset, storage.AsStream());
        }

        public bool HasPartition(XciPartitionType type)
        {
            if (type == XciPartitionType.Root) return true;

            return GetRootPartition().FileExists(type.GetFileName());
        }

        public XciPartition OpenPartitionO(XciPartitionType type)
        {
            XciPartition root = GetRootPartition();
            if (type == XciPartitionType.Root) return root;

            root.OpenFile(out IFile partitionFile, type.GetFileName().ToU8Span(), OpenMode.Read).ThrowIfFailure();
            return new XciPartition(partitionFile.AsStorage());
        }

        public IFileSystem OpenPartition(XciPartitionType type)
        {
            IFileSystem root = GetRootPartition2();
            if (type == XciPartitionType.Root) return root;

            root.OpenFile(out IFile partitionFile, type.GetFileName().ToU8Span(), OpenMode.Read).ThrowIfFailure();

            var partitionFs = new PartitionFileSystemCore<HashedEntry>();
            partitionFs.Initialize(partitionFile.AsStorage()).ThrowIfFailure();

            return partitionFs;
        }

        private XciPartition GetRootPartition()
        {
            if (RootPartition != null) return RootPartition;

            InitializeRootPartition();

            return RootPartition;
        }

        private IFileSystem GetRootPartition2()
        {
            if (RootPartition2 != null) return RootPartition2;

            InitializeRootPartition2();

            return RootPartition2;
        }

        private void InitializeRootPartition()
        {
            lock (InitLocker)
            {
                if (RootPartition != null) return;

                IStorage rootStorage = BaseStorage.Slice(Header.RootPartitionOffset);

                RootPartition = new XciPartition(rootStorage)
                {
                    Offset = Header.RootPartitionOffset,
                    HashValidity = Header.PartitionFsHeaderValidity
                };
            }
        }

        private void InitializeRootPartition2()
        {
            lock (InitLocker)
            {
                if (RootPartition2 != null) return;

                IStorage rootStorage = BaseStorage.Slice(Header.RootPartitionOffset);

                var partitionFs = new PartitionFileSystemCore<HashedEntry>();
                partitionFs.Initialize(rootStorage).ThrowIfFailure();

                RootPartition2 = partitionFs;
            }
        }
    }

    public class XciPartition : PartitionFileSystem
    {
        public long Offset { get; internal set; }
        public Validity HashValidity { get; set; } = Validity.Unchecked;

        public XciPartition(IStorage storage) : base(storage) { }
    }
}
