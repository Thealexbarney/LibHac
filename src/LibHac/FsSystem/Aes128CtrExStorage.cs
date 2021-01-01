using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class Aes128CtrExStorage : Aes128CtrStorage
    {
        public static readonly int NodeSize = 1024 * 16;

        private BucketTree Table { get; } = new BucketTree();

        private readonly object _locker = new object();

        [StructLayout(LayoutKind.Sequential, Size = 0x10)]
        public struct Entry
        {
            public long Offset;
            public int Reserved;
            public int Generation;
        }

        public Aes128CtrExStorage(IStorage baseStorage, SubStorage nodeStorage, SubStorage entryStorage,
            int entryCount, byte[] key, byte[] counter, bool leaveOpen)
            : base(baseStorage, key, counter, leaveOpen)
        {
            Result rc = Table.Initialize(nodeStorage, entryStorage, NodeSize, Unsafe.SizeOf<Entry>(), entryCount);
            rc.ThrowIfFailure();
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            if (destination.Length == 0)
                return Result.Success;

            var visitor = new BucketTree.Visitor();

            try
            {
                Result rc = Table.Find(ref visitor, offset);
                if (rc.IsFailure()) return rc;

                long inPos = offset;
                int outPos = 0;
                int remaining = destination.Length;

                while (remaining > 0)
                {
                    var currentEntry = visitor.Get<Entry>();

                    // Get and validate the next entry offset
                    long nextEntryOffset;
                    if (visitor.CanMoveNext())
                    {
                        rc = visitor.MoveNext();
                        if (rc.IsFailure()) return rc;

                        nextEntryOffset = visitor.Get<Entry>().Offset;
                        if (!Table.Includes(nextEntryOffset))
                            return ResultFs.InvalidIndirectEntryOffset.Log();
                    }
                    else
                    {
                        nextEntryOffset = Table.GetEnd();
                    }

                    int bytesToRead = (int)Math.Min(nextEntryOffset - inPos, remaining);

                    lock (_locker)
                    {
                        UpdateCounterSubsection((uint)currentEntry.Generation);

                        rc = base.DoRead(inPos, destination.Slice(outPos, bytesToRead));
                        if (rc.IsFailure()) return rc;
                    }

                    outPos += bytesToRead;
                    inPos += bytesToRead;
                    remaining -= bytesToRead;
                }
            }
            finally { visitor.Dispose(); }

            return Result.Success;
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            return ResultFs.UnsupportedWriteForAesCtrCounterExtendedStorage.Log();
        }

        protected override Result DoFlush()
        {
            return Result.Success;
        }

        private void UpdateCounterSubsection(uint value)
        {
            Counter[7] = (byte)value;
            Counter[6] = (byte)(value >> 8);
            Counter[5] = (byte)(value >> 16);
            Counter[4] = (byte)(value >> 24);
        }
    }
}
