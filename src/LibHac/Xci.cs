using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac
{
    public class Xci
    {
        public XciHeader Header { get; }

        private IStorage BaseStorage { get; }
        private object InitLocker { get; } = new object();
        private XciPartition RootPartition { get; set; }

        public Xci(KeySet keySet, IStorage storage)
        {
            BaseStorage = storage;
            Header = new XciHeader(keySet, storage.AsStream());
        }

        public bool HasPartition(XciPartitionType type)
        {
            if (type == XciPartitionType.Root) return true;

            return GetRootPartition().FileExists(type.GetFileName());
        }

        public XciPartition OpenPartition(XciPartitionType type)
        {
            XciPartition root = GetRootPartition();
            if (type == XciPartitionType.Root) return root;

            root.OpenFile(out IFile partitionFile, type.GetFileName().ToU8Span(), OpenMode.Read).ThrowIfFailure();
            return new XciPartition(partitionFile.AsStorage());
        }

        private XciPartition GetRootPartition()
        {
            if (RootPartition != null) return RootPartition;

            InitializeRootPartition();

            return RootPartition;
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
    }

    public class XciPartition : PartitionFileSystem
    {
        public long Offset { get; internal set; }
        public Validity HashValidity { get; set; } = Validity.Unchecked;

        public XciPartition(IStorage storage) : base(storage) { }
    }
}
