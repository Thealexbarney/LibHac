﻿using LibHac.Fs;

namespace LibHac
{
    public class Xci
    {
        public XciHeader Header { get; }

        private IStorage BaseStorage { get; }
        private object InitLocker { get; } = new object();
        private XciPartition RootPartition { get; set; }

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

        public XciPartition OpenPartition(XciPartitionType type)
        {
            XciPartition root = GetRootPartition();
            if (type == XciPartitionType.Root) return root;

            IStorage partitionStorage = root.OpenFile(type.GetFileName(), OpenMode.Read).AsStorage();
            return new XciPartition(partitionStorage);
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
