using System.Collections.Generic;

namespace LibHac.IO.Save
{
    public static class SaveExtensions
    {
        public static IEnumerable<(int block, int length)> DumpChain(this AllocationTable table, int startBlock)
        {
            var iterator = new AllocationTableIterator(table, startBlock);

            do
            {
                yield return (iterator.PhysicalBlock, iterator.CurrentSegmentSize);
            } while (iterator.MoveNext());
        }
    }
}
