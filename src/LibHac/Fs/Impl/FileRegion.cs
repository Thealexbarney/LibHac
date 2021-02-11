using System;
using LibHac.Diag;
using LibHac.Util;

namespace LibHac.Fs.Impl
{
    public readonly struct FileRegion
    {
        public readonly long Offset;
        public readonly long Size;

        public FileRegion(long offset, long size)
        {
            Offset = offset;
            Size = size;

            Abort.DoAbortUnless(size >= 0);
        }

        public long GetEndOffset()
        {
            return Offset + Size;
        }

        public bool Includes(FileRegion other)
        {
            return Offset <= other.Offset && other.GetEndOffset() <= GetEndOffset();
        }

        public bool Intersects(FileRegion other)
        {
            return HasIntersection(this, other);
        }

        public FileRegion GetIntersection(FileRegion other)
        {
            return GetIntersection(this, other);
        }

        public FileRegion ExpandAndAlign(uint alignment)
        {
            long alignedStartOffset = Alignment.AlignDownPow2(Offset, alignment);
            long alignedEndOffset = Alignment.AlignUpPow2(GetEndOffset(), alignment);
            long alignedSize = alignedEndOffset - alignedStartOffset;

            return new FileRegion(alignedStartOffset, alignedSize);
        }

        public FileRegion ShrinkAndAlign(uint alignment)
        {
            long alignedStartOffset = Alignment.AlignUpPow2(Offset, alignment);
            long alignedEndOffset = Alignment.AlignDownPow2(GetEndOffset(), alignment);
            long alignedSize = alignedEndOffset - alignedStartOffset;

            return new FileRegion(alignedStartOffset, alignedSize);
        }

        public FileRegion GetEndRegionWithSizeLimit(long size)
        {
            if (size >= Size)
                return this;

            return new FileRegion(GetEndOffset() - size, size);
        }

        public static bool HasIntersection(FileRegion region1, FileRegion region2)
        {
            return region1.GetEndOffset() >= region2.Offset &&
                   region2.GetEndOffset() >= region1.Offset;
        }

        public static FileRegion GetIntersection(FileRegion region1, FileRegion region2)
        {
            if (!region1.Intersects(region2))
                return new FileRegion();

            long intersectionStartOffset = Math.Max(region1.Offset, region2.Offset);
            long intersectionEndOffset = Math.Min(region1.GetEndOffset(), region2.GetEndOffset());
            long intersectionSize = intersectionEndOffset - intersectionStartOffset;

            return new FileRegion(intersectionStartOffset, intersectionSize);
        }

        public static FileRegion GetInclusion(FileRegion region1, FileRegion region2)
        {
            long inclusionStartOffset = Math.Min(region1.Offset, region2.Offset);
            long inclusionEndOffset = Math.Max(region1.GetEndOffset(), region2.GetEndOffset());
            long inclusionSize = inclusionEndOffset - inclusionStartOffset;

            return new FileRegion(inclusionStartOffset, inclusionSize);
        }
    }
}
