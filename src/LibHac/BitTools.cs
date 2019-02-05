namespace LibHac
{
    public static class BitTools
    {
        public static int SignExtend32(int value, int bits)
        {
            int shift = 8 * sizeof(int) - bits;
            return (value << shift) >> shift;
        }
    }
}
