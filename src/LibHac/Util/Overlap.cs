namespace LibHac.Util
{
    public static class Overlap
    {
        public static bool HasOverlap(ulong start0, ulong size0, ulong start1, ulong size1)
        {
            if (start0 <= start1)
            {
                return start1 < start0 + size0;
            }

            return start0 < start1 + size1;
        }

        public static bool Contains(ulong start, ulong size, ulong value)
        {
            return start <= value && value < start + size;
        }
    }
}
